namespace Purfle.Marketplace.Data.Entities;

public sealed class SigningKey
{
    public Guid Id { get; set; }

    /// <summary>
    /// Matches identity.key_id in agent manifests.
    /// </summary>
    public required string KeyId { get; set; }

    public string PublisherId { get; set; } = null!;
    public Publisher Publisher { get; set; } = null!;

    public required string Algorithm { get; set; }

    /// <summary>Raw X coordinate of the P-256 public key point (32 bytes).</summary>
    public required byte[] PublicKeyX { get; set; }

    /// <summary>Raw Y coordinate of the P-256 public key point (32 bytes).</summary>
    public required byte[] PublicKeyY { get; set; }

    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
