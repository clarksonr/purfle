using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Repositories;

public sealed class JsonAttestationRepository : IAttestationRepository
{
    private readonly JsonDocumentStore<Attestation> _store;

    public JsonAttestationRepository(string dataDirectory)
    {
        _store = new JsonDocumentStore<Attestation>(Path.Combine(dataDirectory, "attestations.json"));
    }

    public async Task<IReadOnlyList<Attestation>> FindByAgentIdAsync(string agentId, CancellationToken ct)
        => await _store.WhereAsync(a => a.AgentId == agentId, ct);

    public async Task<bool> ExistsAsync(string agentId, string type, CancellationToken ct)
        => await _store.AnyAsync(a => a.AgentId == agentId && a.Type == type, ct);

    public async Task CreateAsync(Attestation attestation, CancellationToken ct)
        => await _store.AddAsync(attestation, ct);
}
