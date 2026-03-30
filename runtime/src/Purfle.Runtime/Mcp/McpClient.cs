using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Purfle.Runtime.Mcp;

/// <summary>
/// MCP client that communicates with an MCP server over stdio using JSON-RPC 2.0.
/// The server is launched as a child process; requests are written to stdin,
/// responses are read from stdout.
/// </summary>
public sealed class McpClient : IMcpClient
{
    private readonly Process _process;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _nextId = 1;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Creates and starts an MCP server process.
    /// </summary>
    /// <param name="command">The command to run (e.g. "npx", "python").</param>
    /// <param name="arguments">Arguments to pass to the command (e.g. "-y @modelcontextprotocol/server-filesystem").</param>
    /// <param name="workingDirectory">Optional working directory for the server process.</param>
    public McpClient(string command, string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
        };

        if (workingDirectory is not null)
            psi.WorkingDirectory = workingDirectory;

        _process = new Process { StartInfo = psi };
        _process.Start();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var response = await SendRequestAsync("tools/list", JsonDocument.Parse("{}").RootElement, ct);

        var tools = new List<McpToolInfo>();
        if (response.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString()!;
                var description = tool.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? ""
                    : "";
                var inputSchema = tool.TryGetProperty("inputSchema", out var schema)
                    ? schema.GetRawText()
                    : """{"type":"object","properties":{}}""";

                tools.Add(new McpToolInfo(name, description, inputSchema));
            }
        }

        return tools;
    }

    /// <inheritdoc/>
    public async Task<string> CallToolAsync(string toolName, string arguments, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var args = JsonDocument.Parse(arguments).RootElement;
        var callParams = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            name = toolName,
            arguments = args,
        })).RootElement;

        var response = await SendRequestAsync("tools/call", callParams, ct);

        // MCP tool results have a "content" array; extract text entries.
        if (response.TryGetProperty("content", out var content))
        {
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var text))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(text.GetString());
                }
            }
            return sb.ToString();
        }

        return response.GetRawText();
    }

    /// <summary>
    /// Sends the MCP initialize handshake if not already done.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var initParams = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "purfle-runtime", version = "0.1.0" },
            })).RootElement;

            await SendRequestAsync("initialize", initParams, ct);

            // Send initialized notification (no response expected).
            await SendNotificationAsync("notifications/initialized", ct);

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sends a JSON-RPC request and reads the response.
    /// </summary>
    private async Task<JsonElement> SendRequestAsync(string method, JsonElement @params, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var id = _nextId++;
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method,
                @params,
            });

            await _process.StandardInput.WriteLineAsync(request.AsMemory(), ct);
            await _process.StandardInput.FlushAsync();

            // Read lines until we get a JSON-RPC response with our id.
            while (true)
            {
                var line = await ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // Skip notifications (no id field).
                    if (!root.TryGetProperty("id", out var responseId)) continue;

                    if (responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
                    {
                        if (root.TryGetProperty("error", out var error))
                        {
                            var message = error.TryGetProperty("message", out var msg)
                                ? msg.GetString() : "Unknown MCP error";
                            throw new InvalidOperationException($"MCP error: {message}");
                        }

                        // Clone the result so it survives doc disposal.
                        return root.TryGetProperty("result", out var result)
                            ? result.Clone()
                            : JsonDocument.Parse("{}").RootElement;
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON — skip (could be server stderr leaking).
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no response expected).
    /// </summary>
    private async Task SendNotificationAsync(string method, CancellationToken ct)
    {
        var notification = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method,
            @params = new { },
        });

        await _process.StandardInput.WriteLineAsync(notification.AsMemory(), ct);
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>
    /// Reads a line from the MCP server's stdout with timeout protection.
    /// </summary>
    private async Task<string> ReadLineAsync(CancellationToken ct)
    {
        var readTask = _process.StandardOutput.ReadLineAsync(ct);
        var completed = await Task.WhenAny(readTask.AsTask(), Task.Delay(TimeSpan.FromSeconds(30), ct));

        if (completed != readTask.AsTask())
            throw new TimeoutException("MCP server did not respond within 30 seconds.");

        return await readTask ?? throw new InvalidOperationException("MCP server closed stdout.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(5000))
                    _process.Kill();
            }
        }
        catch { /* best-effort cleanup */ }

        _process.Dispose();
        _lock.Dispose();
        await ValueTask.CompletedTask;
    }
}
