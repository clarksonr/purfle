using Purfle.Marketplace.Core.Storage;
using Purfle.Marketplace.Storage.Json.BlobStorage;

namespace Purfle.Marketplace.Tests;

public sealed class BundleBlobStoreTests : IDisposable
{
    private readonly string _dataDir;
    private readonly IBundleBlobStore _store;

    public BundleBlobStoreTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"purfle-bundle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);
        _store = new LocalFileBundleStore(_dataDir);
    }

    [Fact]
    public async Task StoreAndRetrieveBundle()
    {
        var content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD };
        using var stream = new MemoryStream(content);

        var blobRef = await _store.StoreAsync("test-agent", "1.0.0", stream);

        Assert.StartsWith("bundles/", blobRef);
        Assert.EndsWith(".purfle", blobRef);

        using var retrieved = await _store.RetrieveAsync(blobRef);
        using var ms = new MemoryStream();
        await retrieved.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public async Task ExistsReturnsTrueAfterStore()
    {
        var content = new byte[] { 0x01, 0x02 };
        using var stream = new MemoryStream(content);

        var blobRef = await _store.StoreAsync("exists-agent", "2.0.0", stream);
        Assert.True(await _store.ExistsAsync(blobRef));
    }

    [Fact]
    public async Task ExistsReturnsFalseForMissing()
    {
        Assert.False(await _store.ExistsAsync("bundles/missing/1.0.0.purfle"));
    }

    [Fact]
    public async Task DeleteRemovesBundle()
    {
        var content = new byte[] { 0xDE, 0xAD };
        using var stream = new MemoryStream(content);

        var blobRef = await _store.StoreAsync("delete-agent", "1.0.0", stream);
        Assert.True(await _store.ExistsAsync(blobRef));

        await _store.DeleteAsync(blobRef);
        Assert.False(await _store.ExistsAsync(blobRef));
    }

    [Fact]
    public async Task RetrieveThrowsForMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _store.RetrieveAsync("bundles/no-such/1.0.0.purfle"));
    }

    [Fact]
    public async Task StoreOverwritesExistingBundle()
    {
        var content1 = new byte[] { 0x01 };
        var content2 = new byte[] { 0x02, 0x03 };

        using (var s1 = new MemoryStream(content1))
            await _store.StoreAsync("overwrite-agent", "1.0.0", s1);

        using (var s2 = new MemoryStream(content2))
            await _store.StoreAsync("overwrite-agent", "1.0.0", s2);

        using var retrieved = await _store.RetrieveAsync("bundles/overwrite-agent/1.0.0.purfle");
        using var ms = new MemoryStream();
        await retrieved.CopyToAsync(ms);
        Assert.Equal(content2, ms.ToArray());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }
}
