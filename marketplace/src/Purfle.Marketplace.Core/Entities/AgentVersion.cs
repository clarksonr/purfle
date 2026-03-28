namespace Purfle.Marketplace.Core.Entities;

public sealed class AgentVersion
{
    public Guid Id { get; set; }
    public Guid AgentListingId { get; set; }
    public required string Version { get; set; }
    public required string ManifestBlobRef { get; set; }
    public Guid SigningKeyId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public long Downloads { get; set; }
}
