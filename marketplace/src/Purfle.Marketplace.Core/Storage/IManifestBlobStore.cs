namespace Purfle.Marketplace.Core.Storage;

public interface IManifestBlobStore
{
    Task<string> StoreAsync(string agentId, string version, string manifestJson, CancellationToken ct = default);
    Task<string> RetrieveAsync(string blobRef, CancellationToken ct = default);
    Task DeleteAsync(string blobRef, CancellationToken ct = default);
}
