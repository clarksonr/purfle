using Purfle.Marketplace.Core.Repositories;
using Purfle.Runtime.Identity;

namespace Purfle.Marketplace.Api.Services;

/// <summary>
/// Bridges the marketplace signing key repository to the runtime's <see cref="IKeyRegistry"/>
/// so that <see cref="IdentityVerifier"/> can verify signatures on publish.
/// </summary>
public sealed class DbKeyRegistry(ISigningKeyRepository signingKeys) : IKeyRegistry
{
    public async Task<PublicKey?> GetKeyAsync(string keyId, CancellationToken ct = default)
    {
        var key = await signingKeys.FindByKeyIdAsync(keyId, ct);

        if (key is null)
            return null;

        return new PublicKey
        {
            KeyId = key.KeyId,
            Algorithm = key.Algorithm,
            X = key.PublicKeyX,
            Y = key.PublicKeyY,
        };
    }

    public async Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default)
    {
        var key = await signingKeys.FindByKeyIdAsync(keyId, ct);
        return key?.IsRevoked ?? false;
    }
}
