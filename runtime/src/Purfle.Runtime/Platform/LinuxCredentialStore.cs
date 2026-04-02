using System.Diagnostics;

namespace Purfle.Runtime.Platform;

/// <summary>
/// Credential store backed by libsecret via the <c>secret-tool</c> CLI.
/// Credentials are stored with attribute application=purfle and key={key}.
/// Requires the <c>libsecret-tools</c> package to be installed.
/// </summary>
public sealed class LinuxCredentialStore : ICredentialStore
{
    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var (exitCode, stdout, _) = await RunSecretToolAsync(
            "lookup",
            "application", "purfle",
            "key", key);

        if (exitCode != 0)
            return null;

        var value = stdout.Trim();
        return value.Length == 0 ? null : value;
    }

    public async Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        // secret-tool store reads the secret from stdin
        var (exitCode, _, stderr) = await RunSecretToolAsync(
            stdinData: value,
            "store",
            "--label", $"Purfle: {key}",
            "application", "purfle",
            "key", key);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to write credential '{key}' to Linux Secret Service. " +
                $"secret-tool exited with code {exitCode}: {stderr.Trim()}");
        }
    }

    public async Task DeleteAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var (exitCode, _, stderr) = await RunSecretToolAsync(
            "clear",
            "application", "purfle",
            "key", key);

        // secret-tool clear exits 0 even if the item doesn't exist, but handle non-zero defensively.
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to delete credential '{key}' from Linux Secret Service. " +
                $"secret-tool exited with code {exitCode}: {stderr.Trim()}");
        }
    }

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunSecretToolAsync(
        params string[] args)
    {
        return RunSecretToolAsync(stdinData: null, args);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunSecretToolAsync(
        string? stdinData,
        params string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "secret-tool",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdinData is not null,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();

        if (stdinData is not null)
        {
            await process.StandardInput.WriteAsync(stdinData);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}
