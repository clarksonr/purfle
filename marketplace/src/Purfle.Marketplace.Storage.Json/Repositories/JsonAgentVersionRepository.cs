using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Repositories;

public sealed class JsonAgentVersionRepository : IAgentVersionRepository
{
    private readonly JsonDocumentStore<AgentVersion> _store;
    private readonly JsonDocumentStore<AgentListing> _listingStore;

    public JsonAgentVersionRepository(string dataDirectory)
    {
        _store = new JsonDocumentStore<AgentVersion>(Path.Combine(dataDirectory, "agent-versions.json"));
        _listingStore = new JsonDocumentStore<AgentListing>(Path.Combine(dataDirectory, "agent-listings.json"));
    }

    public async Task<AgentVersion?> FindByAgentIdAndVersionAsync(string agentId, string version, CancellationToken ct)
    {
        var listing = await _listingStore.FindAsync(a => a.AgentId == agentId && a.IsListed, ct);
        if (listing is null) return null;

        return await _store.FindAsync(v => v.AgentListingId == listing.Id && v.Version == version, ct);
    }

    public async Task<AgentVersion?> FindLatestByAgentIdAsync(string agentId, CancellationToken ct)
    {
        var listing = await _listingStore.FindAsync(a => a.AgentId == agentId && a.IsListed, ct);
        if (listing is null) return null;

        var versions = await _store.WhereAsync(v => v.AgentListingId == listing.Id, ct);
        return versions.OrderByDescending(v => v.PublishedAt).FirstOrDefault();
    }

    public async Task<IReadOnlyList<AgentVersion>> FindByListingIdAsync(Guid listingId, CancellationToken ct)
    {
        var versions = await _store.WhereAsync(v => v.AgentListingId == listingId, ct);
        return versions.OrderByDescending(v => v.PublishedAt).ToList();
    }

    public async Task<bool> ExistsAsync(Guid listingId, string version, CancellationToken ct)
        => await _store.AnyAsync(v => v.AgentListingId == listingId && v.Version == version, ct);

    public async Task CreateAsync(AgentVersion version, CancellationToken ct)
        => await _store.AddAsync(version, ct);

    public async Task IncrementDownloadsAsync(Guid versionId, CancellationToken ct)
    {
        var existing = await _store.FindAsync(v => v.Id == versionId, ct);
        if (existing is not null)
        {
            existing.Downloads++;
            await _store.UpdateAsync(v => v.Id == versionId, existing, ct);
        }
    }
}
