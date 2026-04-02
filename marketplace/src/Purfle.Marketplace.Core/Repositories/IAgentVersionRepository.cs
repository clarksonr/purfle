using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Core.Repositories;

public interface IAgentVersionRepository
{
    Task<AgentVersion?> FindByAgentIdAndVersionAsync(string agentId, string version, CancellationToken ct = default);
    Task<AgentVersion?> FindLatestByAgentIdAsync(string agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentVersion>> FindByListingIdAsync(Guid listingId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid listingId, string version, CancellationToken ct = default);
    Task CreateAsync(AgentVersion version, CancellationToken ct = default);
    Task UpdateAsync(AgentVersion version, CancellationToken ct = default);
    Task IncrementDownloadsAsync(Guid versionId, CancellationToken ct = default);
}
