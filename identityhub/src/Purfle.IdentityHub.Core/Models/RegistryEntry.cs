namespace Purfle.IdentityHub.Core.Models;

/// <summary>
/// An entry in the agent registry. Wraps the marketplace AgentListing with
/// additional metadata for the IdentityHub trust layer.
/// </summary>
public sealed class RegistryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AgentId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Version { get; set; }
    public string? PublisherId { get; set; }
    public string? KeyId { get; set; }
    public bool IsListed { get; set; } = true;
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
