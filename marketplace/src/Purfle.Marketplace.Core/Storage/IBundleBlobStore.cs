namespace Purfle.Marketplace.Core.Storage;

/// <summary>
/// Stores and retrieves binary .purfle ZIP bundles.
/// </summary>
public interface IBundleBlobStore
{
    Task<string> StoreAsync(string agentId, string version, Stream bundle, CancellationToken ct = default);
    Task<Stream> RetrieveAsync(string blobRef, CancellationToken ct = default);
    Task DeleteAsync(string blobRef, CancellationToken ct = default);
    Task<bool> ExistsAsync(string blobRef, CancellationToken ct = default);
}
