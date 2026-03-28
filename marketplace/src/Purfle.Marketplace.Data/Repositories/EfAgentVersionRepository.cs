using Microsoft.EntityFrameworkCore;
using Purfle.Marketplace.Core.Repositories;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Data.Repositories;

public sealed class EfAgentVersionRepository(MarketplaceDbContext db) : IAgentVersionRepository
{
    public async Task<CoreEntities.AgentVersion?> FindByAgentIdAndVersionAsync(string agentId, string version, CancellationToken ct)
    {
        var v = await db.AgentVersions.AsNoTracking()
            .Include(av => av.AgentListing)
            .Where(av => av.AgentListing.AgentId == agentId && av.Version == version && av.AgentListing.IsListed)
            .FirstOrDefaultAsync(ct);
        return v is null ? null : ToCore(v);
    }

    public async Task<CoreEntities.AgentVersion?> FindLatestByAgentIdAsync(string agentId, CancellationToken ct)
    {
        var v = await db.AgentVersions.AsNoTracking()
            .Include(av => av.AgentListing)
            .Where(av => av.AgentListing.AgentId == agentId && av.AgentListing.IsListed)
            .OrderByDescending(av => av.PublishedAt)
            .FirstOrDefaultAsync(ct);
        return v is null ? null : ToCore(v);
    }

    public async Task<IReadOnlyList<CoreEntities.AgentVersion>> FindByListingIdAsync(Guid listingId, CancellationToken ct)
    {
        var versions = await db.AgentVersions.AsNoTracking()
            .Where(v => v.AgentListingId == listingId)
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(ct);
        return versions.Select(ToCore).ToList();
    }

    public async Task<bool> ExistsAsync(Guid listingId, string version, CancellationToken ct)
    {
        return await db.AgentVersions.AnyAsync(v => v.AgentListingId == listingId && v.Version == version, ct);
    }

    public async Task CreateAsync(CoreEntities.AgentVersion version, CancellationToken ct)
    {
        db.AgentVersions.Add(ToEf(version));
        await db.SaveChangesAsync(ct);
    }

    public async Task IncrementDownloadsAsync(Guid versionId, CancellationToken ct)
    {
        var v = await db.AgentVersions.FirstOrDefaultAsync(av => av.Id == versionId, ct);
        if (v is not null)
        {
            v.Downloads++;
            await db.SaveChangesAsync(ct);
        }
    }

    private static CoreEntities.AgentVersion ToCore(Entities.AgentVersion e) => new()
    {
        Id = e.Id,
        AgentListingId = e.AgentListingId,
        Version = e.Version,
        ManifestBlobRef = e.ManifestJson, // EF still stores inline; blob ref = the JSON itself
        SigningKeyId = e.SigningKeyId,
        PublishedAt = e.PublishedAt,
        Downloads = e.Downloads,
    };

    private static Entities.AgentVersion ToEf(CoreEntities.AgentVersion c) => new()
    {
        Id = c.Id,
        AgentListingId = c.AgentListingId,
        Version = c.Version,
        ManifestJson = c.ManifestBlobRef, // reverse mapping
        SigningKeyId = c.SigningKeyId,
        PublishedAt = c.PublishedAt,
        Downloads = c.Downloads,
    };
}
