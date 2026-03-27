namespace Purfle.Runtime.Identity;

/// <summary>
/// An EC public key retrieved from the key registry.
/// </summary>
public sealed record PublicKey
{
    public required string KeyId { get; init; }

    /// <summary>
    /// JWA algorithm identifier. Only "ES256" is supported in v0.1.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Raw X coordinate of the P-256 public key point (32 bytes).
    /// </summary>
    public required byte[] X { get; init; }

    /// <summary>
    /// Raw Y coordinate of the P-256 public key point (32 bytes).
    /// </summary>
    public required byte[] Y { get; init; }
}
