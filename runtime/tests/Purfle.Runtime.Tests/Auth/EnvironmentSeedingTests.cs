namespace Purfle.Runtime.Tests.Auth;

using Purfle.Runtime.Auth;
using Purfle.Runtime.Platform;
using Microsoft.Extensions.Logging.Abstractions;

public class EnvironmentSeedingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InMemoryCredentialStore _credStore;
    private readonly AuthProfileStore _store;

    public EnvironmentSeedingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _credStore = new InMemoryCredentialStore();
        _store = new AuthProfileStore(
            _credStore,
            NullLogger<AuthProfileStore>.Instance,
            Path.Combine(_tempDir, "auth-profiles.json"));
    }

    [Fact]
    public async Task SeedFromEnvironment_CreatesProfileFromEnvVar()
    {
        // Arrange — set a test env var
        var originalKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-key-from-env-12345");
        try
        {
            // Act
            await _store.SeedFromEnvironmentAsync();

            // Assert
            var profile = await _store.GetActiveProfileAsync("gemini");
            Assert.NotNull(profile);
            Assert.Equal("gemini:env", profile.ProfileId);
            Assert.IsType<ApiKeyCredential>(profile.Credential);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalKey);
        }
    }

    [Fact]
    public async Task SeedFromEnvironment_DoesNotOverwriteExisting()
    {
        // Arrange
        await _store.AddProfileAsync("gemini", "manual", new ApiKeyCredential("manual-key-1234567"));

        var originalKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-key-1234567890");
        try
        {
            // Act
            await _store.SeedFromEnvironmentAsync();

            // Assert — manual profile should still be active
            var profile = await _store.GetActiveProfileAsync("gemini");
            Assert.NotNull(profile);
            Assert.Equal("gemini:manual", profile.ProfileId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalKey);
        }
    }

    [Fact]
    public async Task SeedFromEnvironment_CreatesOllamaDefault()
    {
        // Arrange — ensure no OLLAMA_HOST set
        var originalHost = Environment.GetEnvironmentVariable("OLLAMA_HOST");
        Environment.SetEnvironmentVariable("OLLAMA_HOST", null);
        try
        {
            // Act
            await _store.SeedFromEnvironmentAsync();

            // Assert
            var profile = await _store.GetActiveProfileAsync("ollama");
            Assert.NotNull(profile);
            Assert.Equal("ollama:local", profile.ProfileId);
            var cred = Assert.IsType<LocalServiceCredential>(profile.Credential);
            Assert.Equal("http://localhost:11434", cred.BaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OLLAMA_HOST", originalHost);
        }
    }

    [Fact]
    public async Task SeedFromEnvironment_EnvProfilesNamedEnv()
    {
        // Arrange
        var originalKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-env-test1234");
        try
        {
            // Act
            await _store.SeedFromEnvironmentAsync();

            // Assert
            var profiles = await _store.GetProfilesAsync("anthropic");
            Assert.Single(profiles);
            Assert.Equal("anthropic:env", profiles[0].ProfileId);
            Assert.Equal("env", profiles[0].Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalKey);
        }
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
