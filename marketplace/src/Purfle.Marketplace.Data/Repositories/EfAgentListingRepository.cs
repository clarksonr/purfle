using Microsoft.EntityFrameworkCore;
using Purfle.Marketplace.Core.Repositories;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Data.Repositories;

public sealed class EfAgentListingRepository(MarketplaceDbContext db) : IAgentListingRepository
{
    public async Task<CoreEntities.AgentListing?> FindByAgentIdAsync(string agentId, CancellationToken ct)
    {
        var listing = await db.AgentListings.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgentId == agentId, ct);
        return listing is null ? null : ToCore(listing);
    }

    public async Task<AgentSearchPage> SearchAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var query = db.AgentListings.AsNoTracking().Where(a => a.IsListed);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lower = term.ToLowerInvariant();
            query = query.Where(a =>
                a.Name.ToLower().Contains(lower) ||
                a.Description.ToLower().Contains(lower));
        }

        var totalCount = await query.CountAsync(ct);

        var listings = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AgentId,
                a.Name,
                a.Description,
                a.Publisher.DisplayName,
                LatestVersion = a.Versions
                    .OrderByDescending(v => v.PublishedAt)
                    .Select(v => new { v.Version, v.PublishedAt })
                    .FirstOrDefault(),
                TotalDownloads = a.Versions.Sum(v => v.Downloads),
            })
            .ToListAsync(ct);

        var items = listings.Select(a => new AgentSearchItem(
            a.AgentId,
            a.Name,
            a.Description,
            a.DisplayName,
            a.LatestVersion?.Version,
            a.LatestVersion?.PublishedAt,
            a.TotalDownloads
        )).ToList();

        return new AgentSearchPage(items, totalCount);
    }

    public async Task CreateAsync(CoreEntities.AgentListing listing, CancellationToken ct)
    {
        db.AgentListings.Add(ToEf(listing));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CoreEntities.AgentListing listing, CancellationToken ct)
    {
        var existing = await db.AgentListings.FirstOrDefaultAsync(a => a.Id == listing.Id, ct)
            ?? throw new InvalidOperationException($"AgentListing {listing.Id} not found.");
        existing.Name = listing.Name;
        existing.Description = listing.Description;
        existing.UpdatedAt = listing.UpdatedAt;
        existing.IsListed = listing.IsListed;
        await db.SaveChangesAsync(ct);
    }

    private static CoreEntities.AgentListing ToCore(Entities.AgentListing e) => new()
    {
        Id = e.Id,
        AgentId = e.AgentId,
        PublisherId = e.PublisherId,
        Name = e.Name,
        Description = e.Description,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        IsListed = e.IsListed,
    };

    private static Entities.AgentListing ToEf(CoreEntities.AgentListing c) => new()
    {
        Id = c.Id,
        AgentId = c.AgentId,
        PublisherId = c.PublisherId,
        Name = c.Name,
        Description = c.Description,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        IsListed = c.IsListed,
    };
}
