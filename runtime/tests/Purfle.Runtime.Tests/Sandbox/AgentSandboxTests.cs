using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Tests.Sandbox;

public sealed class AgentSandboxTests
{
    private static AgentSandbox Build(
        (string[] allow, string[] deny)? network = null,
        (string[] read, string[] write)? filesystem = null,
        string[]? envAllow = null,
        string[]? mcpTools = null)
    {
        var permissions = new AgentPermissions
        {
            Network = network.HasValue ? new NetworkPermissions
            {
                Allow = network.Value.allow,
                Deny = network.Value.deny,
            } : null,
            Filesystem = filesystem.HasValue ? new FilesystemPermissions
            {
                Read = filesystem.Value.read,
                Write = filesystem.Value.write,
            } : null,
            Environment = envAllow is not null ? new EnvironmentPermissions { Allow = envAllow } : null,
            Tools = mcpTools is not null ? new ToolPermissions { Mcp = mcpTools } : null,
        };
        return new AgentSandbox(permissions);
    }

    // ── network ───────────────────────────────────────────────────────────────

    [Fact]
    public void Network_NoPermissions_DeniesAll()
    {
        var sandbox = Build();
        Assert.False(sandbox.CanAccessUrl("https://example.com/api"));
    }

    [Fact]
    public void Network_AllowedPattern_Permits()
    {
        var sandbox = Build(network: (["https://api.example.com/*"], []));
        Assert.True(sandbox.CanAccessUrl("https://api.example.com/search"));
    }

    [Fact]
    public void Network_DenyOverridesAllow()
    {
        var sandbox = Build(network: (["https://api.example.com/*"], ["*"]));
        Assert.False(sandbox.CanAccessUrl("https://api.example.com/search"));
    }

    [Fact]
    public void Network_PatternNotMatched_Denies()
    {
        var sandbox = Build(network: (["https://api.example.com/*"], []));
        Assert.False(sandbox.CanAccessUrl("https://other.com/api"));
    }

    // ── filesystem ────────────────────────────────────────────────────────────

    [Fact]
    public void Filesystem_NoPermissions_DeniesAll()
    {
        var sandbox = Build();
        Assert.False(sandbox.CanReadPath("/workspace/src/file.cs"));
        Assert.False(sandbox.CanWritePath("/workspace/output/result.json"));
    }

    [Fact]
    public void Filesystem_GlobReadPattern_Permits()
    {
        var sandbox = Build(filesystem: (["/workspace/src/**"], []));
        Assert.True(sandbox.CanReadPath("/workspace/src/file.cs"));
        Assert.False(sandbox.CanReadPath("/workspace/tests/file.cs"));
    }

    [Fact]
    public void Filesystem_ReadGrantDoesNotImplyWrite()
    {
        var sandbox = Build(filesystem: (["/workspace/**"], []));
        Assert.True(sandbox.CanReadPath("/workspace/file.cs"));
        Assert.False(sandbox.CanWritePath("/workspace/file.cs"));
    }

    // ── environment ───────────────────────────────────────────────────────────

    [Fact]
    public void Environment_AllowedVar_Permits()
    {
        var sandbox = Build(envAllow: ["API_KEY"]);
        Assert.True(sandbox.CanReadEnv("API_KEY"));
        Assert.False(sandbox.CanReadEnv("SECRET_TOKEN"));
    }

    // ── mcp tools ─────────────────────────────────────────────────────────────

    [Fact]
    public void McpTools_AllowedTool_Permits()
    {
        var sandbox = Build(mcpTools: ["file", "search"]);
        Assert.True(sandbox.CanUseMcpTool("file"));
        Assert.False(sandbox.CanUseMcpTool("shell"));
    }
}
