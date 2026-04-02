using Purfle.Runtime.Platform;

namespace Purfle.Runtime.Tests.Platform;

public sealed class InMemoryCredentialStoreTests
{
    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsNull()
    {
        var store = new InMemoryCredentialStore();

        var result = await store.GetAsync("nonexistent-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        var store = new InMemoryCredentialStore();

        await store.SetAsync("api-key", "sk-secret-123");
        var result = await store.GetAsync("api-key");

        Assert.Equal("sk-secret-123", result);
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingValue()
    {
        var store = new InMemoryCredentialStore();

        await store.SetAsync("token", "old-value");
        await store.SetAsync("token", "new-value");
        var result = await store.GetAsync("token");

        Assert.Equal("new-value", result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesKey()
    {
        var store = new InMemoryCredentialStore();

        await store.SetAsync("temp-key", "temp-value");
        await store.DeleteAsync("temp-key");
        var result = await store.GetAsync("temp-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_UnknownKey_DoesNotThrow()
    {
        var store = new InMemoryCredentialStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("does-not-exist"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task MultipleKeys_StoredIndependently()
    {
        var store = new InMemoryCredentialStore();

        await store.SetAsync("key-a", "value-a");
        await store.SetAsync("key-b", "value-b");
        await store.SetAsync("key-c", "value-c");

        Assert.Equal("value-a", await store.GetAsync("key-a"));
        Assert.Equal("value-b", await store.GetAsync("key-b"));
        Assert.Equal("value-c", await store.GetAsync("key-c"));

        // Deleting one doesn't affect others
        await store.DeleteAsync("key-b");

        Assert.Equal("value-a", await store.GetAsync("key-a"));
        Assert.Null(await store.GetAsync("key-b"));
        Assert.Equal("value-c", await store.GetAsync("key-c"));
    }
}
