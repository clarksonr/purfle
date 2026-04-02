using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;

namespace Purfle.Marketplace.Api.Services;

public sealed class AttestationService(IAttestationRepository attestations)
{
    public const string PublisherVerified = "publisher-verified";
    public const string MarketplaceListed = "marketplace-listed";

    /// <summary>
    /// Issue attestations for a newly published agent from a verified publisher.
    /// </summary>
    public async Task IssuePublishAttestationsAsync(string agentId, bool publisherIsVerified, CancellationToken ct)
    {
        // Always issue marketplace-listed when published
        if (!await attestations.ExistsAsync(agentId, MarketplaceListed, ct))
        {
            await attestations.CreateAsync(new Attestation
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                Type = MarketplaceListed,
                IssuedBy = "purfle-marketplace",
                IssuedAt = DateTimeOffset.UtcNow,
            }, ct);
        }

        // Issue publisher-verified only if publisher has completed domain verification
        if (publisherIsVerified && !await attestations.ExistsAsync(agentId, PublisherVerified, ct))
        {
            await attestations.CreateAsync(new Attestation
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                Type = PublisherVerified,
                IssuedBy = "purfle-marketplace",
                IssuedAt = DateTimeOffset.UtcNow,
            }, ct);
        }
    }

    /// <summary>
    /// Get all attestations for an agent.
    /// </summary>
    public async Task<IReadOnlyList<Attestation>> GetAttestationsAsync(string agentId, CancellationToken ct)
        => await attestations.FindByAgentIdAsync(agentId, ct);
}
