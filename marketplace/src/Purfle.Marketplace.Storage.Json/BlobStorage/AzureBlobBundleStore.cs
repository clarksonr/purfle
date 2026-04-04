using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Storage.Json.BlobStorage;

/// <summary>
/// Stores .purfle ZIP bundles in Azure Blob Storage.
/// Container: configurable (default: purfle-bundles) — blob path: bundles/{agentId}/{version}.purfle
/// Computes SHA-256 on upload and stores it as blob metadata. Returns it on download.
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

        // Read bundle into memory to compute SHA-256 before upload
        using var ms = new MemoryStream();
        await bundle.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        ms.Position = 0;
        await blobClient.UploadAsync(ms, new BlobUploadOptions
        {
            Metadata = new Dictionary<string, string> { ["sha256"] = sha256 },
        }, ct);

        return blobRef;
    }

    public async Task<Stream> RetrieveAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    /// <summary>
    /// Retrieves the bundle and its SHA-256 hash (from blob metadata).
    /// </summary>
    public async Task<(Stream Content, string? Sha256)> RetrieveWithHashAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var props = await blobClient.GetPropertiesAsync(cancellationToken: ct);
        props.Value.Metadata.TryGetValue("sha256", out var sha256);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return (response.Value.Content, sha256);
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

    /// <summary>
    /// Retrieves the SHA-256 hash from blob metadata without downloading the content.
    /// </summary>
    public async Task<string?> GetHashAsync(string blobRef, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobRef);
        var props = await blobClient.GetPropertiesAsync(cancellationToken: ct);
        props.Value.Metadata.TryGetValue("sha256", out var sha256);
        return sha256;
    }
}
