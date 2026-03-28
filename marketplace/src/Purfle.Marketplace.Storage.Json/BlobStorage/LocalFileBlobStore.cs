using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Storage.Json.BlobStorage;

/// <summary>
/// Stores agent manifest JSON files on the local filesystem.
/// Blob path: {dataDirectory}/blobs/{agentId}/{version}.json
/// </summary>
public sealed class LocalFileBlobStore(string dataDirectory) : IManifestBlobStore
{
    private readonly string _blobRoot = Path.Combine(dataDirectory, "blobs");

    public async Task<string> StoreAsync(string agentId, string version, string manifestJson, CancellationToken ct)
    {
        var blobRef = $"manifests/{agentId}/{version}.json";
        var fullPath = Path.Combine(_blobRoot, blobRef);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, manifestJson, ct);
        return blobRef;
    }

    public async Task<string> RetrieveAsync(string blobRef, CancellationToken ct)
    {
        var fullPath = Path.Combine(_blobRoot, blobRef);
        return await File.ReadAllTextAsync(fullPath, ct);
    }

    public Task DeleteAsync(string blobRef, CancellationToken ct)
    {
        var fullPath = Path.Combine(_blobRoot, blobRef);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
