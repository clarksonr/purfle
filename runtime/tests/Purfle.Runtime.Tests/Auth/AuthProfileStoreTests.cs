namespace Purfle.Runtime.Tests.Auth;

using Purfle.Runtime.Auth;
using Purfle.Runtime.Platform;
using Microsoft.Extensions.Logging.Abstractions;

public class AuthProfileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InMemoryCredentialStore _credStore;
    private readonly AuthProfileStore _store;

    public AuthProfileStoreTests()
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
    public async Task AddProfile_CreatesNewProfile()
    {
        var credential = new ApiKeyCredential("sk-test-1234567890abcdef");

        var profile = await _store.AddProfileAsync("gemini", "default", credential);

        Assert.Equal("gemini:default", profile.ProfileId);
        Assert.Equal("gemini", profile.Provider);
        Assert.Equal(ProfileStatus.Unknown, profile.Status);
        Assert.True(profile.Credential.IsWellFormed);
    }

    [Fact]
    public async Task AddProfile_DuplicateId_Throws()
    {
        await _store.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-test-key1234567"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-test-key2345678")));
    }

    [Fact]
    public async Task GetActiveProfile_ReturnsFirstIfNoneSet()
    {
        await _store.AddProfileAsync("anthropic", "work", new ApiKeyCredential("sk-ant-work1234"));
        await _store.AddProfileAsync("anthropic", "personal", new ApiKeyCredential("sk-ant-personal1"));

        var active = await _store.GetActiveProfileAsync("anthropic");

        Assert.NotNull(active);
        Assert.Equal("anthropic:work", active.ProfileId);
    }

    [Fact]
    public async Task SetActiveProfile_ChangesActiveProfile()
    {
        await _store.AddProfileAsync("anthropic", "work", new ApiKeyCredential("sk-ant-work1234"));
        await _store.AddProfileAsync("anthropic", "personal", new ApiKeyCredential("sk-ant-personal1"));

        await _store.SetActiveProfileAsync("anthropic", "anthropic:personal");
        var active = await _store.GetActiveProfileAsync("anthropic");

        Assert.Equal("anthropic:personal", active?.ProfileId);
    }

    [Fact]
    public async Task MarkCooldown_SetsStatusAndTime()
    {
        await _store.AddProfileAsync("openai", "default", new ApiKeyCredential("sk-test-openai1234"));

        await _store.MarkCooldownAsync("openai:default", TimeSpan.FromMinutes(5));
        var profile = await _store.GetActiveProfileAsync("openai");

        Assert.Equal(ProfileStatus.Cooldown, profile?.Status);
        Assert.NotNull(profile?.CooldownUntilUtc);
        Assert.True(profile.CooldownUntilUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task RemoveProfile_RemovesFromStoreAndKeychain()
    {
        await _store.AddProfileAsync("gemini", "temp", new ApiKeyCredential("sk-temp-1234567890"));

        var removed = await _store.RemoveProfileAsync("gemini:temp");
        var profile = await _store.GetActiveProfileAsync("gemini");

        Assert.True(removed);
        Assert.Null(profile);
    }

    [Fact]
    public async Task Persistence_SurvivesReload()
    {
        await _store.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-persist-12345"));

        // Create new store instance pointing to same file
        var store2 = new AuthProfileStore(
            _credStore,
            NullLogger<AuthProfileStore>.Instance,
            Path.Combine(_tempDir, "auth-profiles.json"));
        await store2.InitializeAsync();
        var profile = await store2.GetActiveProfileAsync("gemini");

        Assert.NotNull(profile);
        Assert.Equal("gemini:default", profile.ProfileId);
        store2.Dispose();
    }

    [Fact]
    public async Task OAuthCredential_TracksExpiration()
    {
        var credential = new OAuthCredential(
            AccessToken: "ya29.access-token",
            RefreshToken: "1//refresh-token",
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(30));

        var profile = await _store.AddProfileAsync("gemini", "oauth", credential);

        var oauth = profile.Credential as OAuthCredential;
        Assert.NotNull(oauth);
        Assert.False(oauth.IsExpired);
        Assert.True(oauth.ExpiresWithin(TimeSpan.FromHours(1)));
        Assert.False(oauth.ExpiresWithin(TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task ProfileChanged_FiresOnAdd()
    {
        AuthProfileChangedEventArgs? eventArgs = null;
        _store.ProfileChanged += (_, args) => eventArgs = args;

        await _store.AddProfileAsync("ollama", "local", new LocalServiceCredential("http://localhost:11434"));

        Assert.NotNull(eventArgs);
        Assert.Equal("ollama:local", eventArgs.ProfileId);
        Assert.Equal(AuthProfileChangeType.Added, eventArgs.ChangeType);
    }

    [Fact]
    public async Task UpdateStatus_ChangesProfileStatus()
    {
        await _store.AddProfileAsync("gemini", "default", new ApiKeyCredential("sk-test-status1234"));

        await _store.UpdateStatusAsync("gemini:default", ProfileStatus.Active);
        var profile = await _store.GetActiveProfileAsync("gemini");

        Assert.Equal(ProfileStatus.Active, profile?.Status);
        Assert.NotNull(profile?.LastVerifiedUtc);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
