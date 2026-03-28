using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Repositories;

public sealed class JsonAgentListingRepository : IAgentListingRepository
{
    private readonly JsonDocumentStore<AgentListing> _listingStore;
    private readonly JsonDocumentStore<AgentVersion> _versionStore;
    private readonly JsonDocumentStore<Publisher> _publisherStore;

    public JsonAgentListingRepository(string dataDirectory)
    {
        _listingStore = new JsonDocumentStore<AgentListing>(Path.Combine(dataDirectory, "agent-listings.json"));
        _versionStore = new JsonDocumentStore<AgentVersion>(Path.Combine(dataDirectory, "agent-versions.json"));
        _publisherStore = new JsonDocumentStore<Publisher>(Path.Combine(dataDirectory, "publishers.json"));
    }

    public async Task<AgentListing?> FindByAgentIdAsync(string agentId, CancellationToken ct)
        => await _listingStore.FindAsync(a => a.AgentId == agentId, ct);

    public async Task<AgentSearchPage> SearchAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var listings = await _listingStore.GetAllAsync(ct);
        var versions = await _versionStore.GetAllAsync(ct);
        var publishers = await _publisherStore.GetAllAsync(ct);

        var query = listings.Where(a => a.IsListed).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lower = term.ToLowerInvariant();
            query = query.Where(a =>
                a.Name.ToLower().Contains(lower) ||
                a.Description.ToLower().Contains(lower));
        }

        var filtered = query.ToList();
        var totalCount = filtered.Count;

        var items = filtered
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a =>
            {
                var publisher = publishers.FirstOrDefault(p => p.Id == a.PublisherId);
                var agentVersions = versions.Where(v => v.AgentListingId == a.Id).ToList();
                var latest = agentVersions.OrderByDescending(v => v.PublishedAt).FirstOrDefault();
                var totalDownloads = agentVersions.Sum(v => v.Downloads);

                return new AgentSearchItem(
                    a.AgentId,
                    a.Name,
                    a.Description,
                    publisher?.DisplayName ?? a.PublisherId,
                    latest?.Version,
                    latest?.PublishedAt,
                    totalDownloads
                );
            })
            .ToList();

        return new AgentSearchPage(items, totalCount);
    }

    public async Task CreateAsync(AgentListing listing, CancellationToken ct)
        => await _listingStore.AddAsync(listing, ct);

    public async Task UpdateAsync(AgentListing listing, CancellationToken ct)
        => await _listingStore.UpdateAsync(a => a.Id == listing.Id, listing, ct);
}
