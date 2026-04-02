namespace Purfle.IdentityHub.Core.Models;

/// <summary>
/// Records the revocation of a signing key. Once revoked, manifests signed
/// with this key should be treated as untrusted.
/// </summary>
public sealed class RevocationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string KeyId { get; set; }
    public required string Reason { get; set; }
    public string? RevokedBy { get; set; }
    public DateTimeOffset RevokedAt { get; set; } = DateTimeOffset.UtcNow;
}
