using System.Security.Cryptography;
using System.Text;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Identity;

/// <summary>
/// Implements load sequence step 3: identity verification.
/// <list type="number">
///   <item>Retrieve public key for key_id.</item>
///   <item>Check revocation status.</item>
///   <item>Verify JWS signature over the canonical manifest body.</item>
///   <item>Check expires_at.</item>
/// </list>
/// </summary>
public sealed class IdentityVerifier(IKeyRegistry keyRegistry)
{
    public async Task<VerificationResult> VerifyAsync(
        AgentManifest manifest,
        string rawManifestJson,
        CancellationToken ct = default)
    {
        var identity = manifest.Identity;

        // 3a — retrieve key
        var key = await keyRegistry.GetKeyAsync(identity.KeyId, ct);
        if (key is null)
            return VerificationResult.Fail(LoadFailureReason.KeyNotFound,
                $"Key '{identity.KeyId}' not found in registry.");

        // 3b — revocation check
        if (await keyRegistry.IsRevokedAsync(identity.KeyId, ct))
            return VerificationResult.Fail(LoadFailureReason.KeyRevoked,
                $"Key '{identity.KeyId}' has been revoked.");

        // 3c — verify signature
        if (!identity.Algorithm.Equals("ES256", StringComparison.Ordinal))
            return VerificationResult.Fail(LoadFailureReason.SignatureInvalid,
                $"Unsupported algorithm '{identity.Algorithm}'. Only ES256 is supported in v0.1.");

        if (identity.Signature is null)
            return VerificationResult.Fail(LoadFailureReason.SignatureInvalid,
                "Manifest has no signature. Sign the manifest with the SDK before deploying.");

        var signatureValid = VerifyEs256(key, rawManifestJson, identity.Signature);
        if (!signatureValid)
            return VerificationResult.Fail(LoadFailureReason.SignatureInvalid,
                "Signature verification failed.");

        // 3d — expiry
        if (identity.ExpiresAt <= DateTimeOffset.UtcNow)
            return VerificationResult.Fail(LoadFailureReason.ManifestExpired,
                $"Manifest expired at {identity.ExpiresAt:O}.");

        return VerificationResult.Ok();
    }

    /// <summary>
    /// Verifies an ES256 JWS Compact Serialization against the canonical manifest body.
    /// The JWS signing input is: ASCII(BASE64URL(header) + "." + BASE64URL(payload))
    /// where payload is the canonical manifest JSON with identity.signature removed.
    /// </summary>
    private static bool VerifyEs256(PublicKey key, string rawManifestJson, string jws)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3)
            return false;

        var headerB64 = parts[0];
        var payloadB64 = parts[1];
        var signatureBytes = Base64UrlDecode(parts[2]);

        var canonicalBytes = CanonicalJson.ForSigning(rawManifestJson);
        var expectedPayloadB64 = Base64UrlEncode(canonicalBytes);

        if (!string.Equals(payloadB64, expectedPayloadB64, StringComparison.Ordinal))
            return false;

        var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");

        var ecParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = key.X, Y = key.Y },
        };

        using var ecdsa = ECDsa.Create(ecParams);

        return ecdsa.VerifyData(
            signingInput,
            signatureBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input
            .Replace('-', '+')
            .Replace('_', '/')
            .PadRight(input.Length + (4 - input.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
