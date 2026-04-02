using Purfle.IdentityHub.Core.Models;

namespace Purfle.IdentityHub.Core.Services;

/// <summary>
/// Key registry with revocation — manages ES256 signing keys and tracks revocations.
/// </summary>
public interface IKeyRevocationService
{
    Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default);
    Task<RevocationRecord> RevokeAsync(string keyId, string reason, string? revokedBy = null, CancellationToken ct = default);
    Task<IReadOnlyList<RevocationRecord>> GetRevocationsAsync(CancellationToken ct = default);
}
