using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Core.Repositories;

public interface IAgentListingRepository
{
    Task<AgentListing?> FindByAgentIdAsync(string agentId, CancellationToken ct = default);
    Task<AgentSearchPage> SearchAsync(string? term, int page, int pageSize, CancellationToken ct = default);
    Task CreateAsync(AgentListing listing, CancellationToken ct = default);
    Task UpdateAsync(AgentListing listing, CancellationToken ct = default);
}
