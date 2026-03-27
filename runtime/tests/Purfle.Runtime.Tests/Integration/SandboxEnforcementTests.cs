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
/// Each test loads a signed manifest through the complete <see cref="AgentLoader"/>
/// pipeline, extracts the resulting <see cref="AgentSandbox"/>, and verifies that
/// attempts to exceed the declared scope are blocked.  The deny-by-default
/// principle (spec §3.3) is also covered explicitly.
/// </summary>
public sealed class SandboxEnforcementTests
{
    private readonly ManifestTestFactory _factory = new();

    /// <summary>
    /// Runs the full load sequence and returns the sandbox on success.
    /// Fails the test immediately if loading fails for any reason.
    /// </summary>
    private async Task<AgentSandbox> LoadSandboxAsync(string manifestJson)
    {
        var loader = new AgentLoader(
            new ManifestLoader(),
            new IdentityVerifier(_factory.CreateRegistry()),
            new HashSet<string> { CapabilityNegotiator.WellKnown.Inference });

        var result = await loader.LoadAsync(manifestJson);
        Assert.True(result.Success, $"Load failed unexpectedly: {result.FailureMessage}");
        return result.Sandbox!;
    }

    // ── Deny by default ───────────────────────────────────────────────────────

    /// <summary>
    /// hello-world declares <c>permissions: {}</c>.  Every resource class must
    /// be denied because no allowlist is present (spec §3.3 — deny by default).
    /// </summary>
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
    public async Task Network_CallToAllowedUrl_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithNetwork(
            allow: ["https://api.example.com/*"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanAccessUrl("https://api.example.com/v1/search"));
    }

    [Fact]
    public async Task Network_CallToUrlNotInAllowList_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithNetwork(
            allow: ["https://api.example.com/*"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanAccessUrl("https://evil.example.com/exfil"));
    }

    [Fact]
    public async Task Network_CallToExplicitlyDeniedUrl_IsBlockedEvenIfAlsoAllowed()
    {
        var json = _factory.BuildSignedJson(WithNetwork(
            allow: ["https://api.example.com/*"],
            deny:  ["https://api.example.com/admin/*"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanAccessUrl("https://api.example.com/v1/search"));
        Assert.False(sandbox.CanAccessUrl("https://api.example.com/admin/users"));
    }

    // ── Filesystem read ───────────────────────────────────────────────────────

    [Fact]
    public async Task Filesystem_ReadWithinDeclaredPath_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithFilesystem(
            read: ["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadPath("/data/inputs/records.json"));
    }

    [Fact]
    public async Task Filesystem_ReadOutsideDeclaredPath_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithFilesystem(
            read: ["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanReadPath("/etc/passwd"));
    }

    // ── Filesystem write ──────────────────────────────────────────────────────

    [Fact]
    public async Task Filesystem_WriteWithinDeclaredPath_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithFilesystem(
            write: ["/tmp/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanWritePath("/tmp/agent-output.json"));
    }

    [Fact]
    public async Task Filesystem_WriteOutsideDeclaredPath_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithFilesystem(
            write: ["/tmp/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanWritePath("/home/user/.ssh/authorized_keys"));
    }

    [Fact]
    public async Task Filesystem_ReadGrantDoesNotConferWriteAccess()
    {
        var json = _factory.BuildSignedJson(WithFilesystem(
            read: ["/data/**"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadPath("/data/file.json"));
        Assert.False(sandbox.CanWritePath("/data/file.json"));
    }

    // ── Environment variables ─────────────────────────────────────────────────

    [Fact]
    public async Task Environment_DeclaredVariable_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithEnvironment(["MODEL_API_ENDPOINT"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanReadEnv("MODEL_API_ENDPOINT"));
    }

    [Fact]
    public async Task Environment_UndeclaredVariable_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithEnvironment(["MODEL_API_ENDPOINT"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanReadEnv("SECRET_API_KEY"));
    }

    // ── MCP tools ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task McpTools_DeclaredTool_IsPermitted()
    {
        var json = _factory.BuildSignedJson(WithMcpTools(["filesystem"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.True(sandbox.CanUseMcpTool("filesystem"));
    }

    [Fact]
    public async Task McpTools_UndeclaredTool_IsBlocked()
    {
        var json = _factory.BuildSignedJson(WithMcpTools(["filesystem"]));

        var sandbox = await LoadSandboxAsync(json);

        Assert.False(sandbox.CanUseMcpTool("web-search"));
    }

    // ── Mutation helpers ──────────────────────────────────────────────────────

    private static Action<JsonObject> WithNetwork(
        string[]? allow = null,
        string[]? deny  = null)
        => node =>
        {
            var network = new JsonObject();
            if (allow is not null)
                network["allow"] = ToJsonArray(allow);
            if (deny is not null)
                network["deny"] = ToJsonArray(deny);

            node["permissions"] = new JsonObject { ["network"] = network };
        };

    private static Action<JsonObject> WithFilesystem(
        string[]? read  = null,
        string[]? write = null)
        => node =>
        {
            var fs = new JsonObject();
            if (read is not null)
                fs["read"] = ToJsonArray(read);
            if (write is not null)
                fs["write"] = ToJsonArray(write);

            node["permissions"] = new JsonObject { ["filesystem"] = fs };
        };

    private static Action<JsonObject> WithEnvironment(string[] allow)
        => node =>
        {
            node["permissions"] = new JsonObject
            {
                ["environment"] = new JsonObject { ["allow"] = ToJsonArray(allow) },
            };
        };

    private static Action<JsonObject> WithMcpTools(string[] mcp)
        => node =>
        {
            node["permissions"] = new JsonObject
            {
                ["tools"] = new JsonObject { ["mcp"] = ToJsonArray(mcp) },
            };
        };

    private static JsonArray ToJsonArray(string[] values)
    {
        var arr = new JsonArray();
        foreach (var v in values)
            arr.Add(v);
        return arr;
    }
}
