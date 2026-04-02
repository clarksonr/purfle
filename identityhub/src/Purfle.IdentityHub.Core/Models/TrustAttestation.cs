namespace Purfle.IdentityHub.Core.Models;

/// <summary>
/// A trust attestation issued by the IdentityHub for an agent. Types include
/// "marketplace-listed", "publisher-verified", "community-reviewed", etc.
/// </summary>
public sealed class TrustAttestation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AgentId { get; set; }
    public required string Type { get; set; }
    public required string IssuedBy { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Signature { get; set; }
}
