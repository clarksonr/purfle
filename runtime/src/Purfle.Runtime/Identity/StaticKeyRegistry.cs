namespace Purfle.Runtime.Identity;

/// <summary>
/// An in-memory key registry for testing and local development.
/// Production use should implement <see cref="IKeyRegistry"/> against the Purfle key registry API.
/// </summary>
public sealed class StaticKeyRegistry : IKeyRegistry
{
    private readonly Dictionary<string, PublicKey> _keys;
    private readonly HashSet<string> _revoked;

    public StaticKeyRegistry(IEnumerable<PublicKey> keys, IEnumerable<string>? revokedKeyIds = null)
    {
        _keys = keys.ToDictionary(k => k.KeyId, StringComparer.Ordinal);
        _revoked = revokedKeyIds is not null
            ? new HashSet<string>(revokedKeyIds, StringComparer.Ordinal)
            : [];
    }

    public Task<PublicKey?> GetKeyAsync(string keyId, CancellationToken ct = default)
        => Task.FromResult(_keys.GetValueOrDefault(keyId));

    public Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default)
        => Task.FromResult(_revoked.Contains(keyId));
}
