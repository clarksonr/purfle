using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Storage.Json.BlobStorage;

/// <summary>
/// Stores .purfle ZIP bundles on the local filesystem.
/// Blob path: {dataDirectory}/blobs/bundles/{agentId}/{version}.purfle
/// </summary>
public sealed class LocalFileBundleStore(string dataDirectory) : IBundleBlobStore
{
    private readonly string _blobRoot = Path.Combine(dataDirectory, "blobs");

    public async Task<string> StoreAsync(string agentId, string version, Stream bundle, CancellationToken ct)
    {
        var blobRef = $"bundles/{agentId}/{version}.purfle";
        var fullPath = Path.Combine(_blobRoot, blobRef);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await bundle.CopyToAsync(fs, ct);
        return blobRef;
    }

    public Task<Stream> RetrieveAsync(string blobRef, CancellationToken ct)
    {
        var fullPath = Path.Combine(_blobRoot, blobRef);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Bundle not found: {blobRef}");
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string blobRef, CancellationToken ct)
    {
        var fullPath = Path.Combine(_blobRoot, blobRef);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string blobRef, CancellationToken ct)
    {
        var fullPath = Path.Combine(_blobRoot, blobRef);
        return Task.FromResult(File.Exists(fullPath));
    }
}
