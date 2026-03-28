namespace Purfle.Marketplace.Data.Entities;

public sealed class AgentListing
{
    public Guid Id { get; set; }

    /// <summary>
    /// The manifest "id" field (UUID v4).
    /// </summary>
    public required string AgentId { get; set; }

    public string PublisherId { get; set; } = null!;
    public Publisher Publisher { get; set; } = null!;

    public required string Name { get; set; }
    public required string Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsListed { get; set; } = true;

    public ICollection<AgentVersion> Versions { get; set; } = [];
}
