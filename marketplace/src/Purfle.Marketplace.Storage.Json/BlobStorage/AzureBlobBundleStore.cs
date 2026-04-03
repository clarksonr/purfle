using Azure.Storage.Blobs;
using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Storage.Json.BlobStorage;

/// <summary>
/// Stores .purfle ZIP bundles in Azure Blob Storage.
/// Container: configurable (default: purfle-bundles) — blob path: bundles/{agentId}/{version}.purfle
/// </summary>
public sealed class AzureBlobBundleStore : IBundleBlobStore
{
    private readonly BlobContainerClient _container;

    public AzureBlobBundleStore(string connectionString, string containerName)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> StoreAsync(string agentId, string version, Stream bundle, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobRef = $"bundles/{agentId}/{version}.purfle";
        var blobClient = _container.GetBlobClient(blobRef);

        await blobClient.UploadAsync(bundle, overwrite: true, cancellationToken: ct);

        return blobRef;
    }

    public async Task<Stream> RetrieveAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var response = await blobClient.ExistsAsync(ct);
        return response.Value;
    }
}
