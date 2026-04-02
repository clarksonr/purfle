using System.Diagnostics;
using System.Text.Json;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tools;

namespace Purfle.Runtime.Ipc;

/// <summary>
/// Runs an agent as an external process, communicating via stdin/stdout JSON (IPC protocol).
/// Supports .dll (dotnet), .js (node), and native executables.
///
/// Tool calls from the agent are dispatched to:
/// 1. MCP servers (via <see cref="IMcpClient"/> instances keyed by tool name)
/// 2. Built-in tools (via <see cref="BuiltInToolExecutor"/>)
/// All dispatches are gated by the <see cref="AgentSandbox"/>.
/// </summary>
public sealed class ProcessAgentRunner
{
    private readonly AgentSandbox _sandbox;
    private readonly Dictionary<string, IMcpClient> _mcpToolRoutes;
    private readonly BuiltInToolExecutor? _builtInExecutor;

    /// <summary>
    /// Creates a new <see cref="ProcessAgentRunner"/> with full tool dispatch support.
    /// </summary>
    /// <param name="sandbox">Sandbox for permission enforcement.</param>
    /// <param name="mcpToolRoutes">
    /// Map of tool name → MCP client. Built by the runtime at load time from the
    /// agent's manifest <c>tools</c> array and the connected MCP servers.
    /// </param>
    /// <param name="builtInExecutor">
    /// Optional executor for built-in tools (read_file, write_file, etc.).
    /// When null, only MCP tools are available.
    /// </param>
    public ProcessAgentRunner(
        AgentSandbox sandbox,
        Dictionary<string, IMcpClient>? mcpToolRoutes = null,
        BuiltInToolExecutor? builtInExecutor = null)
    {
        _sandbox = sandbox;
        _mcpToolRoutes = mcpToolRoutes ?? [];
        _builtInExecutor = builtInExecutor;
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

                    var result = await DispatchToolCallAsync(toolCall, ct);

                    var toolResult = new IpcToolResult
                    {
                        Type = "toolResult",
                        Id = toolCall.Id,
                        Result = result
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

    /// <summary>
    /// Dispatches a tool call to the appropriate handler: MCP client first,
    /// then built-in executor. Returns an error string for unknown tools.
    /// </summary>
    private async Task<object> DispatchToolCallAsync(IpcToolCall toolCall, CancellationToken ct)
    {
        var argsJson = toolCall.Arguments is JsonElement je
            ? je.GetRawText()
            : JsonSerializer.Serialize(toolCall.Arguments ?? new { });

        // Route 1: MCP tool
        if (_mcpToolRoutes.TryGetValue(toolCall.Tool, out var mcpClient))
        {
            try
            {
                return await mcpClient.CallToolAsync(toolCall.Tool, argsJson, ct);
            }
            catch (Exception ex)
            {
                return $"Error: MCP tool '{toolCall.Tool}' failed — {ex.Message}";
            }
        }

        // Route 2: built-in tool
        if (_builtInExecutor is not null)
        {
            try
            {
                var argsElement = JsonDocument.Parse(argsJson).RootElement;
                return await _builtInExecutor.ExecuteAsync(toolCall.Tool, argsElement, ct);
            }
            catch (Exception ex)
            {
                return $"Error: built-in tool '{toolCall.Tool}' failed — {ex.Message}";
            }
        }

        return $"Error: unknown tool '{toolCall.Tool}'. No MCP server or built-in handler registered.";
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
