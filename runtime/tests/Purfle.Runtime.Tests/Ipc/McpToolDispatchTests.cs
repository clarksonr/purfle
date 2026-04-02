using System.Text.Json;
using Purfle.Runtime.Ipc;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tools;

namespace Purfle.Runtime.Tests.Ipc;

/// <summary>
/// Tests that <see cref="ProcessAgentRunner"/> correctly dispatches tool calls
/// to MCP clients and built-in executors.
///
/// These tests use a mock agent process (a small Node.js script) that emits
/// IPC tool calls on stdout and reads results from stdin, exercising the full
/// dispatch pipeline without needing a real LLM.
/// </summary>
public sealed class McpToolDispatchTests : IDisposable
{
    private readonly string _tempDir;

    public McpToolDispatchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"purfle-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task DispatchesToMcpClient_WhenToolRouteExists()
    {
        // Arrange: create a mock agent script that calls a tool and returns the result
        var scriptPath = CreateMockAgentScript("mcp_tool_test", """
            process.stdin.once('data', (chunk) => {
                const req = JSON.parse(chunk.toString().trim());
                // Request a tool call
                const response = {
                    type: "response",
                    id: req.id,
                    output: "",
                    done: false,
                    toolCalls: [{ id: "tc-1", tool: "files/read", arguments: { path: "test.txt" } }]
                };
                process.stdout.write(JSON.stringify(response) + "\n");

                // Wait for tool result, then emit final response
                process.stdin.once('data', (resultChunk) => {
                    const toolResult = JSON.parse(resultChunk.toString().trim());
                    const finalResponse = {
                        type: "response",
                        id: req.id,
                        output: "Got: " + JSON.stringify(toolResult.result),
                        done: true,
                        toolCalls: null
                    };
                    process.stdout.write(JSON.stringify(finalResponse) + "\n");
                });
            });
            """);

        var mockMcp = new MockMcpClient("files/read", "file content here");
        var sandbox = CreatePermissiveSandbox();
        var runner = new ProcessAgentRunner(
            sandbox,
            mcpToolRoutes: new Dictionary<string, IMcpClient> { ["files/read"] = mockMcp });

        // Act
        var output = await runner.ExecuteAsync(scriptPath, "read test.txt");

        // Assert
        Assert.Contains("file content here", output);
        Assert.Equal(1, mockMcp.CallCount);
        Assert.Equal("files/read", mockMcp.LastToolName);
    }

    [Fact]
    public async Task DispatchesToBuiltInExecutor_WhenNoMcpRoute()
    {
        // Arrange: agent calls read_file (a built-in tool)
        var testFile = Path.Combine(_tempDir, "readable.txt");
        File.WriteAllText(testFile, "built-in content");

        var scriptPath = CreateMockAgentScript("builtin_tool_test", $$"""
            process.stdin.once('data', (chunk) => {
                const req = JSON.parse(chunk.toString().trim());
                const response = {
                    type: "response",
                    id: req.id,
                    output: "",
                    done: false,
                    toolCalls: [{ id: "tc-1", tool: "read_file", arguments: { path: "{{testFile.Replace("\\", "\\\\")}}" } }]
                };
                process.stdout.write(JSON.stringify(response) + "\n");

                process.stdin.once('data', (resultChunk) => {
                    const toolResult = JSON.parse(resultChunk.toString().trim());
                    const finalResponse = {
                        type: "response",
                        id: req.id,
                        output: "Got: " + JSON.stringify(toolResult.result),
                        done: true,
                        toolCalls: null
                    };
                    process.stdout.write(JSON.stringify(finalResponse) + "\n");
                });
            });
            """);

        // Sandbox allows reading _tempDir
        var sandbox = CreateSandboxWithReadPath(_tempDir);
        var executor = new BuiltInToolExecutor(sandbox);
        var runner = new ProcessAgentRunner(sandbox, builtInExecutor: executor);

        // Act
        var output = await runner.ExecuteAsync(scriptPath, "read the file");

        // Assert
        Assert.Contains("built-in content", output);
    }

    [Fact]
    public async Task ReturnsErrorString_WhenToolNotFound()
    {
        var scriptPath = CreateMockAgentScript("unknown_tool_test", """
            process.stdin.once('data', (chunk) => {
                const req = JSON.parse(chunk.toString().trim());
                const response = {
                    type: "response",
                    id: req.id,
                    output: "",
                    done: false,
                    toolCalls: [{ id: "tc-1", tool: "nonexistent_tool", arguments: {} }]
                };
                process.stdout.write(JSON.stringify(response) + "\n");

                process.stdin.once('data', (resultChunk) => {
                    const toolResult = JSON.parse(resultChunk.toString().trim());
                    const finalResponse = {
                        type: "response",
                        id: req.id,
                        output: "Result: " + JSON.stringify(toolResult.result),
                        done: true,
                        toolCalls: null
                    };
                    process.stdout.write(JSON.stringify(finalResponse) + "\n");
                });
            });
            """);

        var sandbox = CreatePermissiveSandbox();
        var runner = new ProcessAgentRunner(sandbox);

        // Act
        var output = await runner.ExecuteAsync(scriptPath, "try unknown tool");

        // Assert
        Assert.Contains("unknown tool", output);
        Assert.Contains("nonexistent_tool", output);
    }

    [Fact]
    public async Task ThrowsUnauthorizedAccessException_WhenSandboxDenies()
    {
        var scriptPath = CreateMockAgentScript("denied_tool_test", """
            process.stdin.once('data', (chunk) => {
                const req = JSON.parse(chunk.toString().trim());
                const response = {
                    type: "response",
                    id: req.id,
                    output: "",
                    done: false,
                    toolCalls: [{ id: "tc-1", tool: "files/read", arguments: { path: "secret.txt" } }]
                };
                process.stdout.write(JSON.stringify(response) + "\n");
            });
            """);

        // Sandbox with NO mcp.tool permission → CanUseMcpTool returns false
        var sandbox = new AgentSandbox(new Dictionary<string, JsonElement>());
        var runner = new ProcessAgentRunner(sandbox);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => runner.ExecuteAsync(scriptPath, "try denied tool"));
    }

    [Fact]
    public async Task HandlesMcpClientError_ReturnsErrorString()
    {
        var scriptPath = CreateMockAgentScript("mcp_error_test", """
            process.stdin.once('data', (chunk) => {
                const req = JSON.parse(chunk.toString().trim());
                const response = {
                    type: "response",
                    id: req.id,
                    output: "",
                    done: false,
                    toolCalls: [{ id: "tc-1", tool: "files/read", arguments: { path: "test.txt" } }]
                };
                process.stdout.write(JSON.stringify(response) + "\n");

                process.stdin.once('data', (resultChunk) => {
                    const toolResult = JSON.parse(resultChunk.toString().trim());
                    const finalResponse = {
                        type: "response",
                        id: req.id,
                        output: "Error result: " + JSON.stringify(toolResult.result),
                        done: true,
                        toolCalls: null
                    };
                    process.stdout.write(JSON.stringify(finalResponse) + "\n");
                });
            });
            """);

        var failingMcp = new FailingMcpClient("Connection refused");
        var sandbox = CreatePermissiveSandbox();
        var runner = new ProcessAgentRunner(
            sandbox,
            mcpToolRoutes: new Dictionary<string, IMcpClient> { ["files/read"] = failingMcp });

        // Act
        var output = await runner.ExecuteAsync(scriptPath, "try failing tool");

        // Assert — error is returned as a string, not thrown
        Assert.Contains("Connection refused", output);
    }

    [Fact]
    public async Task DispatchesMultipleToolCalls_InSequence()
    {
        // Uses line-based parsing to handle stdin chunks that may arrive merged or split.
        var scriptPath = CreateMockAgentScript("multi_tool_test", """
            const readline = require('readline');
            const rl = readline.createInterface({ input: process.stdin });
            let reqId = null;
            let results = [];
            let gotInitial = false;

            rl.on('line', (line) => {
                const msg = JSON.parse(line);
                if (!gotInitial && msg.type === 'execute') {
                    gotInitial = true;
                    reqId = msg.id;
                    const response = {
                        type: "response",
                        id: reqId,
                        output: "",
                        done: false,
                        toolCalls: [
                            { id: "tc-1", tool: "tool_a", arguments: { q: "first" } },
                            { id: "tc-2", tool: "tool_b", arguments: { q: "second" } }
                        ]
                    };
                    process.stdout.write(JSON.stringify(response) + "\n");
                } else if (msg.type === 'toolResult') {
                    results.push(msg);
                    if (results.length === 2) {
                        const finalResponse = {
                            type: "response",
                            id: reqId,
                            output: results.map(r => JSON.stringify(r.result)).join("|"),
                            done: true,
                            toolCalls: null
                        };
                        process.stdout.write(JSON.stringify(finalResponse) + "\n");
                    }
                }
            });
            """);

        var mockA = new MockMcpClient("tool_a", "result_a");
        var mockB = new MockMcpClient("tool_b", "result_b");
        var sandbox = CreatePermissiveSandbox();
        var runner = new ProcessAgentRunner(
            sandbox,
            mcpToolRoutes: new Dictionary<string, IMcpClient>
            {
                ["tool_a"] = mockA,
                ["tool_b"] = mockB,
            });

        // Act
        var output = await runner.ExecuteAsync(scriptPath, "multi tool call");

        // Assert
        Assert.Equal(1, mockA.CallCount);
        Assert.Equal(1, mockB.CallCount);
        Assert.Contains("result_a", output);
        Assert.Contains("result_b", output);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string CreateMockAgentScript(string name, string jsBody)
    {
        var path = Path.Combine(_tempDir, $"{name}.js");
        File.WriteAllText(path, jsBody);
        return path;
    }

    private static AgentSandbox CreatePermissiveSandbox()
    {
        // mcp.tool present → CanUseMcpTool returns true for all tools
        var perms = new Dictionary<string, JsonElement>
        {
            ["mcp.tool"] = JsonDocument.Parse("{}").RootElement,
        };
        return new AgentSandbox(perms);
    }

    private static AgentSandbox CreateSandboxWithReadPath(string path)
    {
        var perms = new Dictionary<string, JsonElement>
        {
            ["mcp.tool"] = JsonDocument.Parse("{}").RootElement,
            ["fs.read"] = JsonDocument.Parse($$"""{"paths":["{{path.Replace("\\", "/")}}/**"]}""").RootElement,
        };
        return new AgentSandbox(perms);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Mock MCP client ─────────────────────────────────────────────────────

    private sealed class MockMcpClient : IMcpClient
    {
        private readonly string _toolName;
        private readonly string _result;

        public int CallCount { get; private set; }
        public string? LastToolName { get; private set; }
        public string? LastArguments { get; private set; }

        public MockMcpClient(string toolName, string result)
        {
            _toolName = toolName;
            _result = result;
        }

        public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<McpToolInfo>>([new McpToolInfo(_toolName, "mock tool", "{}")]);

        public Task<string> CallToolAsync(string toolName, string arguments, CancellationToken ct = default)
        {
            CallCount++;
            LastToolName = toolName;
            LastArguments = arguments;
            return Task.FromResult(_result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingMcpClient : IMcpClient
    {
        private readonly string _errorMessage;

        public FailingMcpClient(string errorMessage) => _errorMessage = errorMessage;

        public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<McpToolInfo>>([]);

        public Task<string> CallToolAsync(string toolName, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException(_errorMessage);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
