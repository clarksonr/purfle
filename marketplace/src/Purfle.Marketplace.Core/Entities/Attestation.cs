namespace Purfle.Marketplace.Core.Entities;

public sealed class Attestation
{
    public Guid Id { get; set; }
    public required string AgentId { get; set; }
    public required string Type { get; set; }  // "publisher-verified", "marketplace-listed"
    public required string IssuedBy { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public string? Signature { get; set; }
}
