using System.Text.Json;
using Purfle.Runtime.Ipc;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Tests.Ipc;

/// <summary>
/// Integration tests that use the real <c>mcp-file-server</c> (tools/mcp-file-server)
/// to verify end-to-end MCP tool dispatch through <see cref="ProcessAgentRunner"/>.
///
/// These tests require Node.js on PATH and the mcp-file-server to be built
/// (dist/index.js must exist). They are skipped if either condition isn't met.
/// </summary>
public sealed class McpFileServerIntegrationTests : IAsyncDisposable
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string McpServerDir = Path.Combine(RepoRoot, "tools", "mcp-file-server");
    private static readonly string McpServerScript = Path.Combine(McpServerDir, "dist", "index.js");
    private static readonly string WorkspaceDir = Path.Combine(RepoRoot, "agents", "file-assistant", "workspace");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    private McpClient? _mcpClient;

    private static bool CanRun()
        => File.Exists(McpServerScript) && Directory.Exists(WorkspaceDir);

    [SkippableFact]
    public async Task McpClient_ListTools_ReturnsFileServerTools()
    {
        Skip.IfNot(CanRun(), "mcp-file-server not built or workspace missing");

        var client = await GetClientAsync();
        var tools = await client.ListToolsAsync();

        Assert.True(tools.Count >= 3, $"Expected at least 3 tools, got {tools.Count}");
        Assert.Contains(tools, t => t.Name == "files/read");
        Assert.Contains(tools, t => t.Name == "files/list");
        Assert.Contains(tools, t => t.Name == "files/search");
    }

    [SkippableFact]
    public async Task McpClient_ReadFile_ReturnsContent()
    {
        Skip.IfNot(CanRun(), "mcp-file-server not built or workspace missing");

        var client = await GetClientAsync();
        var result = await client.CallToolAsync("files/read", """{"path":"readme.txt"}""");

        Assert.NotEmpty(result);
        Assert.DoesNotContain("Error:", result);
    }

    [SkippableFact]
    public async Task McpClient_ListFiles_ReturnsWorkspaceContents()
    {
        Skip.IfNot(CanRun(), "mcp-file-server not built or workspace missing");

        var client = await GetClientAsync();
        var result = await client.CallToolAsync("files/list", """{"path":"."}""");

        Assert.Contains("readme.txt", result);
    }

    [SkippableFact]
    public async Task ProcessAgentRunner_DispatchesToRealMcpServer()
    {
        Skip.IfNot(CanRun(), "mcp-file-server not built or workspace missing");

        var client = await GetClientAsync();

        // Create a mock agent script that calls files/list via tool call
        var tempDir = Path.Combine(Path.GetTempPath(), $"purfle-mcp-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var scriptPath = Path.Combine(tempDir, "agent.js");
            File.WriteAllText(scriptPath, """
                const readline = require('readline');
                const rl = readline.createInterface({ input: process.stdin });
                let reqId = null;

                rl.on('line', (line) => {
                    const msg = JSON.parse(line);
                    if (msg.type === 'execute') {
                        reqId = msg.id;
                        process.stdout.write(JSON.stringify({
                            type: "response",
                            id: reqId,
                            output: "",
                            done: false,
                            toolCalls: [{ id: "tc-1", tool: "files/list", arguments: { path: "." } }]
                        }) + "\n");
                    } else if (msg.type === 'toolResult') {
                        process.stdout.write(JSON.stringify({
                            type: "response",
                            id: reqId,
                            output: "Files: " + JSON.stringify(msg.result),
                            done: true,
                            toolCalls: null
                        }) + "\n");
                    }
                });
                """);

            var sandbox = new AgentSandbox(new Dictionary<string, JsonElement>
            {
                ["mcp.tool"] = JsonDocument.Parse("{}").RootElement,
            });

            var runner = new ProcessAgentRunner(
                sandbox,
                mcpToolRoutes: new Dictionary<string, IMcpClient> { ["files/list"] = client });

            var output = await runner.ExecuteAsync(scriptPath, "list files");

            Assert.Contains("readme.txt", output);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<McpClient> GetClientAsync()
    {
        if (_mcpClient is not null)
            return _mcpClient;

        _mcpClient = new McpClient(
            "node",
            McpServerScript,
            workingDirectory: McpServerDir);

        // Set MCP_FILE_ROOT via the process environment isn't possible after creation,
        // but the server defaults to agents/file-assistant/workspace which is what we want.

        // Warm up: list tools triggers the initialize handshake.
        await _mcpClient.ListToolsAsync();

        return _mcpClient;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
            await _mcpClient.DisposeAsync();
    }
}
