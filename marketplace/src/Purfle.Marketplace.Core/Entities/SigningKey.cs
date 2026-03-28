namespace Purfle.Marketplace.Core.Entities;

public sealed class SigningKey
{
    public Guid Id { get; set; }
    public required string KeyId { get; set; }
    public string PublisherId { get; set; } = null!;
    public required string Algorithm { get; set; }
    public required byte[] PublicKeyX { get; set; }
    public required byte[] PublicKeyY { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
