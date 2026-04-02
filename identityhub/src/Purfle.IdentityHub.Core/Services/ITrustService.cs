using Purfle.IdentityHub.Core.Models;

namespace Purfle.IdentityHub.Core.Services;

/// <summary>
/// Trust attestation service — issues and queries trust attestations for agents.
/// </summary>
public interface ITrustService
{
    Task<IReadOnlyList<TrustAttestation>> GetAttestationsAsync(string agentId, CancellationToken ct = default);
    Task<TrustAttestation> IssueAsync(TrustAttestation attestation, CancellationToken ct = default);
}
