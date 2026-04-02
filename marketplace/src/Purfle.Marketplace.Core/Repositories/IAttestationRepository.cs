using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Core.Repositories;

public interface IAttestationRepository
{
    Task<IReadOnlyList<Attestation>> FindByAgentIdAsync(string agentId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string agentId, string type, CancellationToken ct = default);
    Task CreateAsync(Attestation attestation, CancellationToken ct = default);
}
