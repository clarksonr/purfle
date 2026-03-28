using System.Text;
using Azure.Storage.Blobs;
using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Storage.Json.BlobStorage;

/// <summary>
/// Stores agent manifest JSON in Azure Blob Storage.
/// Container: configurable (default: purfle-manifests) — blob path: manifests/{agentId}/{version}.json
/// </summary>
public sealed class AzureBlobStore : IManifestBlobStore
{
    private readonly BlobContainerClient _container;

    public AzureBlobStore(string connectionString, string containerName)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> StoreAsync(string agentId, string version, string manifestJson, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobRef = $"manifests/{agentId}/{version}.json";
        var blobClient = _container.GetBlobClient(blobRef);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(manifestJson));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        return blobRef;
    }

    public async Task<string> RetrieveAsync(string blobRef, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
        return response.Value.Content.ToString();
    }

    public async Task DeleteAsync(string blobRef, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
