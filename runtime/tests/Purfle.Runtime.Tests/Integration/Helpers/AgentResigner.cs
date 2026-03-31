using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Integration.Helpers;

/// <summary>
/// Re-signs an arbitrary agent manifest JSON with a freshly-generated ephemeral key,
/// returning the signed JSON and a <see cref="StaticKeyRegistry"/> pre-loaded with
/// that key. This is the same "local dev trust" model used by <c>AgentExecutorService</c>
/// in the MAUI app: we don't need the original publisher key, just a self-consistent
/// signed manifest that the full 7-step load sequence can verify.
/// </summary>
internal static class AgentResigner
{
    private const string EphemeralKeyId = "live-test-key";

    /// <summary>
    /// Parses <paramref name="manifestJson"/>, optionally applies <paramref name="mutate"/>,
    /// then re-signs with a fresh ephemeral ECDSA P-256 key.
    /// </summary>
    public static (string SignedJson, StaticKeyRegistry Registry) Resign(
        string manifestJson,
        Action<JsonObject>? mutate = null)
    {
        var node = JsonNode.Parse(manifestJson)!.AsObject();
        mutate?.Invoke(node);

        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecKey.ExportParameters(includePrivateParameters: false);

        var pubKey = new PublicKey
        {
            KeyId     = EphemeralKeyId,
            Algorithm = "ES256",
            X         = p.Q.X!,
            Y         = p.Q.Y!,
        };

        // Redirect identity to the ephemeral key and plant a placeholder before signing.
        var identity = node["identity"]!.AsObject();
        identity["key_id"]    = EphemeralKeyId;
        identity["signature"] = "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA";

        identity["signature"] = Sign(ecKey, node.ToJsonString());

        return (node.ToJsonString(), new StaticKeyRegistry([pubKey]));
    }

    private static string Sign(ECDsa key, string manifestJson)
    {
        var canonical   = CanonicalJson.ForSigning(manifestJson);
        var headerJson  = $$$"""{"alg":"ES256","kid":"{{{EphemeralKeyId}}}"}""";
        var headerB64   = B64(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64  = B64(canonical);
        var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sigBytes = key.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{B64(sigBytes)}";
    }

    private static string B64(byte[] b) =>
        Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
