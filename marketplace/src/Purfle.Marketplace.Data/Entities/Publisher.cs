using Microsoft.AspNetCore.Identity;

namespace Purfle.Marketplace.Data.Entities;

public sealed class Publisher : IdentityUser
{
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsVerified { get; set; }

    public ICollection<SigningKey> SigningKeys { get; set; } = [];
    public ICollection<AgentListing> AgentListings { get; set; } = [];
}
