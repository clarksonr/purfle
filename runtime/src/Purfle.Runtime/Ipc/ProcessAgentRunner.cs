using System.Diagnostics;
using System.Text.Json;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Ipc;

/// <summary>
/// Runs an agent as an external process, communicating via stdin/stdout JSON (IPC protocol).
/// Supports .dll (dotnet), .js (node), and native executables.
/// </summary>
public sealed class ProcessAgentRunner
{
    private readonly AgentSandbox _sandbox;

    public ProcessAgentRunner(AgentSandbox sandbox)
    {
        _sandbox = sandbox;
    }

    public async Task<string> ExecuteAsync(
        string entrypoint,
        string input,
        CancellationToken ct = default)
    {
        var workingDir = Path.GetDirectoryName(Path.GetFullPath(entrypoint)) ?? ".";
        var (executable, args) = ResolveEntrypoint(entrypoint);

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executable} {args}");

        var request = new IpcRequest
        {
            Type = "execute",
            Id = Guid.NewGuid().ToString(),
            Input = input
        };

        await process.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(request));
        await process.StandardInput.FlushAsync();

        var finalOutput = "";

        while (!ct.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(ct);
            if (line == null) break;

            var response = JsonSerializer.Deserialize<IpcResponse>(line);
            if (response == null) continue;

            finalOutput = response.Output;

            if (response.ToolCalls is { Count: > 0 })
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    if (!_sandbox.CanUseMcpTool(toolCall.Tool))
                        throw new UnauthorizedAccessException(
                            $"Agent not permitted to call tool: {toolCall.Tool}");

                    var toolResult = new IpcToolResult
                    {
                        Type = "toolResult",
                        Id = toolCall.Id,
                        Result = new { error = "Tool dispatch not yet wired to MCP" }
                    };

                    await process.StandardInput.WriteLineAsync(
                        JsonSerializer.Serialize(toolResult));
                    await process.StandardInput.FlushAsync();
                }
            }

            if (response.Done) break;
        }

        if (!process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync(ct);
        }

        return finalOutput;
    }

    private static (string executable, string args) ResolveEntrypoint(string entrypoint)
    {
        return entrypoint switch
        {
            var e when e.EndsWith(".dll") => ("dotnet", e),
            var e when e.EndsWith(".js") => ("node", e),
            var e when e.EndsWith(".py") => ("python", e),
            var e when e.EndsWith(".exe") => (e, ""),
            _ => throw new NotSupportedException($"Unknown entrypoint type: {entrypoint}")
        };
    }
}
