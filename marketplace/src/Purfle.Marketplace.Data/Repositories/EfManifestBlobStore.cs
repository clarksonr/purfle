using Purfle.Marketplace.Core.Storage;

namespace Purfle.Marketplace.Data.Repositories;

/// <summary>
/// Transitional blob store that stores manifest JSON inline in the AgentVersion.ManifestJson column.
/// The blob ref IS the manifest JSON itself during the EF Core transition.
/// </summary>
public sealed class EfManifestBlobStore : IManifestBlobStore
{
    public Task<string> StoreAsync(string agentId, string version, string manifestJson, CancellationToken ct)
    {
        // In the EF transitional layer, the blob ref is the manifest JSON itself.
        return Task.FromResult(manifestJson);
    }

    public Task<string> RetrieveAsync(string blobRef, CancellationToken ct)
    {
        return Task.FromResult(blobRef);
    }

    public Task DeleteAsync(string blobRef, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
