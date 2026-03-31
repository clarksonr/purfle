using System.Text.Json.Nodes;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tests.Integration.Helpers;

namespace Purfle.Runtime.Tests.Integration;

/// <summary>
/// Proves that an agent's runtime sandbox enforces exactly the permissions
/// declared in its manifest — nothing more, nothing less.
///
/// Uses the canonical permission format: "network.outbound", "env.read",
/// "fs.read", "fs.write", "mcp.tool" as permission keys.
/// </summary>
public sealed class SandboxEnforcementTests
{
    private readonly ManifestTestFactory _factory = new();

    // All well-known capabilities — negotiation must pass so we can test sandbox enforcement.
    private static readonly IReadOnlySet<string> s_allCaps = new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.NetworkOutbound,
        CapabilityNegotiator.WellKnown.EnvRead,
        CapabilityNegotiator.WellKnown.FsRead,
        CapabilityNegotiator.WellKnown.FsWrite,
        CapabilityNegotiator.WellKnown.McpTool,
    };

    private async Task<AgentSandbox> LoadSandboxAsync(string manifestJson)
    {
        var loader = new AgentLoader(
            new IdentityVerifier(_factory.CreateRegistry()),
            s_allCaps);

        var result = await loader.LoadAsync(manifestJson);
        Assert.True(result.Success, $"Load failed unexpectedly: {result.FailureMessage}");
        return result.Sandbox!;
    }

    // ── Deny by default ───────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyPermissions_DeniesAllResourceClasses()
    {
        var sandbox = await LoadSandboxAsync(_factory.BuildSignedJson());

        Assert.False(sandbox.CanAccessUrl("https://any.example.com/api"));
        Assert.False(sandbox.CanReadPath("/any/path/file.txt"));
        Assert.False(sandbox.CanWritePath("/any/path/file.txt"));
        Assert.False(sandbox.CanReadEnv("ANY_VAR"));
        Assert.False(sandbox.CanUseMcpTool("any-tool"));
    }

    // ── Network ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Network_CallToAllowedHost_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithNetworkOutbound(["api.example.com"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanAccessUrl("https://api.example.com/v1/search"));
    }

    [Fact]
    public async Task Network_CallToHostNotInAllowList_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithNetworkOutbound(["api.example.com"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanAccessUrl("https://evil.example.com/exfil"));
    }

    // ── Filesystem read ───────────────────────────────────────────────────────

    [Fact]
    public async Task Filesystem_ReadWithinDeclaredPath_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithFsRead(["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadPath("/data/inputs/records.json"));
    }

    [Fact]
    public async Task Filesystem_ReadOutsideDeclaredPath_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithFsRead(["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanReadPath("/etc/passwd"));
    }

    // ── Filesystem write ──────────────────────────────────────────────────────

    [Fact]
    public async Task Filesystem_WriteWithinDeclaredPath_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithFsWrite(["/tmp/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanWritePath("/tmp/agent-output.json"));
    }

    [Fact]
    public async Task Filesystem_WriteOutsideDeclaredPath_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithFsWrite(["/tmp/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanWritePath("/home/user/.ssh/authorized_keys"));
    }

    [Fact]
    public async Task Filesystem_ReadGrantDoesNotConferWriteAccess()
    {
        var json = _factory.BuildSignedJson(WithFsRead(["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadPath("/data/file.json"));
        Assert.False(sandbox.CanWritePath("/data/file.json"));
    }

    // ── Environment variables ─────────────────────────────────────────────────

    [Fact]
    public async Task Environment_DeclaredVariable_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithEnvRead(["MODEL_API_ENDPOINT"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadEnv("MODEL_API_ENDPOINT"));
    }

    [Fact]
    public async Task Environment_UndeclaredVariable_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithEnvRead(["MODEL_API_ENDPOINT"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanReadEnv("SECRET_API_KEY"));
    }

    // ── MCP tools ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task McpTools_WhenMcpToolPermissionGranted_AllToolsArePermitted()
    {
        // Canonical mcp.tool permission has no tool list — all MCP tools are allowed
        var json = _factory.BuildSignedJson(WithMcpTool());

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanUseMcpTool("filesystem"));
        Assert.True(sandbox.CanUseMcpTool("web-search"));
    }

    [Fact]
    public async Task McpTools_WhenNoMcpPermission_AllToolsAreBlocked()
    {
        var sandbox = await LoadSandboxAsync(_factory.BuildSignedJson());

        Assert.False(sandbox.CanUseMcpTool("filesystem"));
    }

    // ── Mutation helpers ──────────────────────────────────────────────────────

    private static Action<JsonObject> WithNetworkOutbound(string[] hosts)
        => node =>
        {
            var arr = new JsonArray();
            foreach (var h in hosts) arr.Add(h);
            node["permissions"] = new JsonObject
            {
                ["network.outbound"] = new JsonObject { ["hosts"] = arr },
            };
            AddCapabilityIfMissing(node, "network.outbound");
        };

    private static Action<JsonObject> WithFsRead(string[] paths)
        => node =>
        {
            var arr = new JsonArray();
            foreach (var p in paths) arr.Add(p);
            node["permissions"] = new JsonObject
            {
                ["fs.read"] = new JsonObject { ["paths"] = arr },
            };
            AddCapabilityIfMissing(node, "fs.read");
        };

    private static Action<JsonObject> WithFsWrite(string[] paths)
        => node =>
        {
            var arr = new JsonArray();
            foreach (var p in paths) arr.Add(p);
            node["permissions"] = new JsonObject
            {
                ["fs.write"] = new JsonObject { ["paths"] = arr },
            };
            AddCapabilityIfMissing(node, "fs.write");
        };

    private static Action<JsonObject> WithEnvRead(string[] vars)
        => node =>
        {
            var arr = new JsonArray();
            foreach (var v in vars) arr.Add(v);
            node["permissions"] = new JsonObject
            {
                ["env.read"] = new JsonObject { ["vars"] = arr },
            };
            AddCapabilityIfMissing(node, "env.read");
        };

    private static Action<JsonObject> WithMcpTool()
        => node =>
        {
            node["permissions"] = new JsonObject
            {
                ["mcp.tool"] = new JsonObject(),
            };
            AddCapabilityIfMissing(node, "mcp.tool");
        };

    /// <summary>
    /// Ensures the manifest's capabilities array contains <paramref name="cap"/>.
    /// Required because the canonical schema enforces that every permissions key
    /// also appears in capabilities[].
    /// </summary>
    private static void AddCapabilityIfMissing(JsonObject node, string cap)
    {
        var caps = node["capabilities"]?.AsArray() ?? new JsonArray();
        if (!caps.Any(c => c?.GetValue<string>() == cap))
            caps.Add(cap);
        node["capabilities"] = caps;
    }
}
