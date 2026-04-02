namespace Purfle.Marketplace.Core.Entities;

public sealed class Publisher
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsVerified { get; set; }
    public string? Domain { get; set; }
    public string? VerificationChallenge { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? SecurityStamp { get; set; }
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
