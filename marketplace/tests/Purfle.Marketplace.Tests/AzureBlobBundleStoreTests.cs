using Purfle.Marketplace.Core.Storage;
using Purfle.Marketplace.Storage.Json.BlobStorage;

namespace Purfle.Marketplace.Tests;

/// <summary>
/// Tests for <see cref="AzureBlobBundleStore"/> using the Azurite emulator.
/// Set AZURE_STORAGE_CONNECTION_STRING to "UseDevelopmentStorage=true" and start
/// Azurite to run these tests locally. Skipped when Azurite is not available.
/// </summary>
public sealed class AzureBlobBundleStoreTests : IAsyncLifetime
{
    private const string TestContainer = "purfle-bundle-test";
    private IBundleBlobStore? _store;
    private bool _available;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            _available = false;
            return;
        }

        try
        {
            _store = new AzureBlobBundleStore(connectionString, TestContainer);
            using var probe = new MemoryStream(new byte[] { 0x00 });
            await _store.StoreAsync("__probe__", "0.0.0", probe);
            await _store.DeleteAsync("bundles/__probe__/0.0.0.purfle");
            _available = true;
        }
        catch
        {
            _available = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void SkipIfUnavailable()
    {
        if (!_available)
            Assert.Fail("SKIPPED: Azurite / Azure Storage not available. Set AZURE_STORAGE_CONNECTION_STRING to run.");
    }

    [Fact]
    public async Task StoreAndRetrieveBundle()
    {
        if (!_available) return;

        var content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD };
        using var stream = new MemoryStream(content);

        var blobRef = await _store!.StoreAsync("azure-test-agent", "1.0.0", stream);

        Assert.StartsWith("bundles/", blobRef);
        Assert.EndsWith(".purfle", blobRef);

        using var retrieved = await _store.RetrieveAsync(blobRef);
        using var ms = new MemoryStream();
        await retrieved.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());

        await _store.DeleteAsync(blobRef);
    }

    [Fact]
    public async Task ExistsReturnsTrueAfterStore()
    {
        if (!_available) return;

        var content = new byte[] { 0x01, 0x02 };
        using var stream = new MemoryStream(content);

        var blobRef = await _store!.StoreAsync("azure-exists-agent", "2.0.0", stream);

        Assert.True(await _store.ExistsAsync(blobRef));

        await _store.DeleteAsync(blobRef);
    }

    [Fact]
    public async Task ExistsReturnsFalseForMissing()
    {
        if (!_available) return;

        Assert.False(await _store!.ExistsAsync("bundles/missing/1.0.0.purfle"));
    }

    [Fact]
    public async Task DeleteRemovesBundle()
    {
        if (!_available) return;

        var content = new byte[] { 0xDE, 0xAD };
        using var stream = new MemoryStream(content);

        var blobRef = await _store!.StoreAsync("azure-delete-agent", "1.0.0", stream);
        Assert.True(await _store.ExistsAsync(blobRef));

        await _store.DeleteAsync(blobRef);
        Assert.False(await _store.ExistsAsync(blobRef));
    }

    [Fact]
    public async Task StoreOverwritesExistingBundle()
    {
        if (!_available) return;

        var content1 = new byte[] { 0x01 };
        var content2 = new byte[] { 0x02, 0x03 };

        using (var s1 = new MemoryStream(content1))
            await _store!.StoreAsync("azure-overwrite-agent", "1.0.0", s1);

        using (var s2 = new MemoryStream(content2))
            await _store!.StoreAsync("azure-overwrite-agent", "1.0.0", s2);

        using var retrieved = await _store!.RetrieveAsync("bundles/azure-overwrite-agent/1.0.0.purfle");
        using var ms = new MemoryStream();
        await retrieved.CopyToAsync(ms);
        Assert.Equal(content2, ms.ToArray());

        await _store.DeleteAsync("bundles/azure-overwrite-agent/1.0.0.purfle");
    }
}
