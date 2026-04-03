namespace Purfle.Runtime.Tests.Auth;

using Purfle.Runtime.Auth;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Platform;
using Microsoft.Extensions.Logging.Abstractions;

public class CredentialResolutionEngineTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly InMemoryCredentialStore _credStore;
    private readonly AuthProfileStore _profileStore;
    private readonly UserProviderPreferences _prefs;
    private readonly CredentialResolutionEngine _resolver;

    public CredentialResolutionEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _credStore = new InMemoryCredentialStore();
        _profileStore = new AuthProfileStore(
            _credStore,
            NullLogger<AuthProfileStore>.Instance,
            Path.Combine(_tempDir, "auth-profiles.json"));
        _prefs = new UserProviderPreferences(Path.Combine(_tempDir, "prefs.json"));
        _resolver = new CredentialResolutionEngine(
            _profileStore,
            _prefs,
            NullLogger<CredentialResolutionEngine>.Instance);
    }

    public async Task InitializeAsync()
    {
        await _profileStore.InitializeAsync();
        await _prefs.LoadAsync();
    }

    public Task DisposeAsync()
    {
        _profileStore.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Resolve_UsesAgentPreferredEngine()
    {
        await _profileStore.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-gemini-key1234"));
        await _profileStore.AddProfileAsync("anthropic", "default", new ApiKeyCredential("sk-anthro-key1234"));

        var manifest = CreateManifest("gemini");

        var result = await _resolver.ResolveAsync(manifest);

        Assert.NotNull(result);
        Assert.Equal("gemini", result.Provider);
        Assert.Equal(ResolutionSource.AgentPreferred, result.Source);
    }

    [Fact]
    public async Task Resolve_FallsBackToEngineFallbackList()
    {
        // Only anthropic available
        await _profileStore.AddProfileAsync("anthropic", "default", new ApiKeyCredential("sk-anthro-key1234"));

        var manifest = CreateManifest("gemini", fallbacks: ["openai", "anthropic"]);

        var result = await _resolver.ResolveAsync(manifest);

        Assert.NotNull(result);
        Assert.Equal("anthropic", result.Provider);
        Assert.Equal(ResolutionSource.AgentFallback, result.Source);
    }

    [Fact]
    public async Task Resolve_FallsBackToUserPreference()
    {
        await _profileStore.AddProfileAsync("ollama", "local", new LocalServiceCredential("http://localhost:11434"));
        await _prefs.SetOrderAsync(["ollama", "gemini", "anthropic", "openai"]);

        var manifest = CreateManifest("gemini"); // Gemini not available

        var result = await _resolver.ResolveAsync(manifest);

        Assert.NotNull(result);
        Assert.Equal("ollama", result.Provider);
        Assert.Equal(ResolutionSource.UserPreference, result.Source);
    }

    [Fact]
    public async Task Resolve_SkipsCooldownProfiles()
    {
        await _profileStore.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-gemini-key1234"));
        await _profileStore.AddProfileAsync("anthropic", "default", new ApiKeyCredential("sk-anthro-key1234"));
        await _profileStore.MarkCooldownAsync("gemini:default", TimeSpan.FromMinutes(10));

        var manifest = CreateManifest("gemini", fallbacks: ["anthropic"]);

        var result = await _resolver.ResolveAsync(manifest);

        Assert.NotNull(result);
        Assert.Equal("anthropic", result.Provider);
    }

    [Fact]
    public async Task Resolve_ReturnsNullWhenNoCredentials()
    {
        var manifest = CreateManifest("gemini");

        var result = await _resolver.ResolveAsync(manifest);

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolve_UsesManifestModelForPreferredEngine()
    {
        await _profileStore.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-gemini-key1234"));

        var manifest = CreateManifest("gemini", model: "gemini-2.0-pro");

        var result = await _resolver.ResolveAsync(manifest);

        Assert.Equal("gemini-2.0-pro", result?.Model);
    }

    [Fact]
    public async Task Resolve_UsesDefaultModelForFallbackEngine()
    {
        await _profileStore.AddProfileAsync("anthropic", "default", new ApiKeyCredential("sk-anthro-key1234"));

        var manifest = CreateManifest("gemini", model: "gemini-2.0-pro", fallbacks: ["anthropic"]);

        var result = await _resolver.ResolveAsync(manifest);

        Assert.Equal("claude-sonnet-4-20250514", result?.Model); // Default, not manifest model
    }

    [Fact]
    public async Task GetResolutionAttempts_ReturnsOrderedDiagnostics()
    {
        await _profileStore.AddProfileAsync("anthropic", "default", new ApiKeyCredential("sk-anthro-key1234"));

        var manifest = CreateManifest("gemini", fallbacks: ["openai"]);

        var attempts = await _resolver.GetResolutionAttemptsAsync(manifest);

        Assert.Equal(4, attempts.Count);
        Assert.Equal("gemini", attempts[0].Provider);
        Assert.False(attempts[0].HasProfile);
        Assert.Equal("No credential configured", attempts[0].FailureReason);

        Assert.Equal("openai", attempts[1].Provider); // From fallback
        Assert.False(attempts[1].HasProfile);

        Assert.Equal("anthropic", attempts[2].Provider); // From user prefs
        Assert.True(attempts[2].HasProfile);
        Assert.Null(attempts[2].FailureReason);
    }

    private static AgentManifest CreateManifest(
        string? engine = null,
        string? model = null,
        IReadOnlyList<string>? fallbacks = null)
    {
        return new AgentManifest
        {
            Purfle = "0.1",
            Id = Guid.NewGuid(),
            Name = "test-agent",
            Version = "1.0.0",
            Identity = new IdentityBlock
            {
                Author = "test",
                Email = "test@test.com",
                KeyId = "test-key",
                Algorithm = "ES256",
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
            },
            Capabilities = ["llm.chat"],
            Runtime = new RuntimeBlock
            {
                Requires = "purfle/0.1",
                Engine = engine ?? "gemini",
                Model = model,
                EngineFallback = fallbacks
            }
        };
    }
}
