using System.Runtime.Loader;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Tests.Integration.Helpers;
using Purfle.Sdk;

namespace Purfle.Runtime.Tests.Assembly;

/// <summary>
/// End-to-end tests that load a real agent DLL (HelloAgent) through <see cref="AgentLoader"/>
/// and verify the full pipeline: parse → schema → identity → capabilities → sandbox → assembly.
/// </summary>
public sealed class AssemblyLoadTests : IDisposable
{
    private static readonly string TestAgentDir =
        Path.Combine(AppContext.BaseDirectory, "test-agents", "hello");

    private readonly ManifestTestFactory _factory = new();
    private readonly List<AssemblyLoadContext> _contexts = [];

    [Fact]
    public async Task LoadAsync_WithRealAgentDll_ReturnsAgentInstance()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        Assert.NotNull(result.AgentInstance);
        Assert.NotNull(result.LoadContext);
        _contexts.Add(result.LoadContext);
    }

    [Fact]
    public async Task LoadAsync_AgentInstance_HasCorrectSystemPrompt()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        Assert.Equal(
            "You are HelloAgent, a test agent for the Purfle AIVM.",
            result.AgentInstance!.SystemPrompt);
        _contexts.Add(result.LoadContext!);
    }

    [Fact]
    public async Task LoadAsync_AgentInstance_ExposesGreetTool()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        var tools = result.AgentInstance!.Tools;
        Assert.Single(tools);
        Assert.Equal("greet", tools[0].Name);
        Assert.Equal("Returns a greeting for the given name.", tools[0].Description);
        _contexts.Add(result.LoadContext!);
    }

    [Fact]
    public async Task LoadAsync_GreetTool_ExecutesCorrectly()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        var tool = result.AgentInstance!.Tools[0];
        var output = await tool.ExecuteAsync("""{"name":"Purfle"}""");
        Assert.Equal("Hello, Purfle!", output);
        _contexts.Add(result.LoadContext!);
    }

    [Fact]
    public async Task LoadAsync_AgentInstance_ImplementsIAgent()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        // The agent was loaded in an isolated ALC, but IAgent type identity
        // is shared via the default ALC — so this cast must work.
        Assert.IsAssignableFrom<IAgent>(result.AgentInstance);
        _contexts.Add(result.LoadContext!);
    }

    [Fact]
    public async Task LoadAsync_AssemblyLoadContext_IsCollectible()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        Assert.True(result.LoadContext!.IsCollectible);
        _contexts.Add(result.LoadContext);
    }

    [Fact]
    public async Task LoadAsync_WithoutAssembliesDirectory_ReturnsNullAgentInstance()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result = await loader.LoadAsync(json, assembliesDirectory: null);

        Assert.True(result.Success, $"Load failed: {result.FailureMessage}");
        Assert.Null(result.AgentInstance);
        Assert.Null(result.LoadContext);
    }

    [Fact]
    public async Task LoadAsync_MissingAgentDll_ReturnsAssemblyLoadFailed()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var result = await loader.LoadAsync(json, emptyDir);

            Assert.False(result.Success);
            Assert.Equal(LoadFailureReason.AssemblyLoadFailed, result.FailureReason);
            Assert.Contains("agent.dll", result.FailureMessage);
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_TwoAgentsLoadInIsolation()
    {
        var (loader, _) = CreateLoader();
        var json = _factory.BuildSignedJson(WithMinimalCapabilities);

        var result1 = await loader.LoadAsync(json, TestAgentDir);
        var result2 = await loader.LoadAsync(json, TestAgentDir);

        Assert.True(result1.Success, $"Load 1 failed: {result1.FailureMessage}");
        Assert.True(result2.Success, $"Load 2 failed: {result2.FailureMessage}");

        // Each load gets its own AssemblyLoadContext.
        Assert.NotSame(result1.LoadContext, result2.LoadContext);

        // But both agent instances implement the same IAgent interface.
        Assert.IsAssignableFrom<IAgent>(result1.AgentInstance);
        Assert.IsAssignableFrom<IAgent>(result2.AgentInstance);

        // They're distinct instances.
        Assert.NotSame(result1.AgentInstance, result2.AgentInstance);

        _contexts.Add(result1.LoadContext!);
        _contexts.Add(result2.LoadContext!);
    }

    private (AgentLoader loader, StaticKeyRegistry registry) CreateLoader()
    {
        var registry = _factory.CreateRegistry();
        var verifier = new IdentityVerifier(registry);
        var capabilities = new HashSet<string> { "llm.chat" };
        return (new AgentLoader(verifier, capabilities), registry);
    }

    private static void WithMinimalCapabilities(System.Text.Json.Nodes.JsonObject manifest)
    {
        // Ensure capabilities only requires llm.chat (which our runtime provides).
        manifest["capabilities"] = new System.Text.Json.Nodes.JsonArray("llm.chat");
    }

    public void Dispose()
    {
        foreach (var ctx in _contexts)
        {
            try { ctx.Unload(); } catch { /* best-effort */ }
        }
    }
}
