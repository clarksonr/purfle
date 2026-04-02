using System.Diagnostics;
using System.Text;

namespace Purfle.Runtime.Platform;

/// <summary>
/// Credential store backed by the macOS Keychain via the <c>security</c> CLI tool.
/// Credentials are stored as generic passwords with service "Purfle" and account set to the key.
/// </summary>
public sealed class MacOSCredentialStore : ICredentialStore
{
    private const string ServiceName = "Purfle";

    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // -w outputs only the password value to stdout
        var (exitCode, stdout, _) = await RunSecurityAsync(
            "find-generic-password",
            "-s", ServiceName,
            "-a", key,
            "-w");

        if (exitCode != 0)
            return null;

        var value = stdout.Trim();
        return value.Length == 0 ? null : value;
    }

    public async Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        // -U updates the item if it already exists
        var (exitCode, _, stderr) = await RunSecurityAsync(
            "add-generic-password",
            "-s", ServiceName,
            "-a", key,
            "-w", value,
            "-U");

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to write credential '{key}' to macOS Keychain. " +
                $"security exited with code {exitCode}: {stderr.Trim()}");
        }
    }

    public async Task DeleteAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var (exitCode, _, stderr) = await RunSecurityAsync(
            "delete-generic-password",
            "-s", ServiceName,
            "-a", key);

        // Exit code 44 means "item not found" — treat as success.
        if (exitCode != 0 && exitCode != 44)
        {
            throw new InvalidOperationException(
                $"Failed to delete credential '{key}' from macOS Keychain. " +
                $"security exited with code {exitCode}: {stderr.Trim()}");
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunSecurityAsync(
        params string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "security",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}
