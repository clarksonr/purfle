namespace Purfle.Runtime.Identity;

/// <summary>
/// Source of public keys and revocation status for manifest signature verification.
/// </summary>
public interface IKeyRegistry
{
    /// <summary>
    /// Returns the public key for <paramref name="keyId"/>, or <c>null</c> if not found.
    /// </summary>
    Task<PublicKey?> GetKeyAsync(string keyId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the key has been revoked.
    /// MUST be checked on every load — runtimes MUST NOT cache revocation status.
    /// </summary>
    Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default);
}
