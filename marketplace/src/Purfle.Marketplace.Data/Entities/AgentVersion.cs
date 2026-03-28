namespace Purfle.Marketplace.Data.Entities;

public sealed class AgentVersion
{
    public Guid Id { get; set; }

    public Guid AgentListingId { get; set; }
    public AgentListing AgentListing { get; set; } = null!;

    public required string Version { get; set; }

    /// <summary>
    /// The complete signed manifest JSON.
    /// </summary>
    public required string ManifestJson { get; set; }

    public Guid SigningKeyId { get; set; }
    public SigningKey SigningKey { get; set; } = null!;

    public DateTimeOffset PublishedAt { get; set; }
    public long Downloads { get; set; }
}
