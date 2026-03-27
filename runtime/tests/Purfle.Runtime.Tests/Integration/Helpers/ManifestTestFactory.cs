using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Integration.Helpers;

/// <summary>
/// Builds and signs agent manifest JSON for integration tests.
///
/// Uses <c>spec/examples/hello-world.agent.json</c> as the base template (copied
/// to the test output directory by the project file).  Callers supply a mutation
/// delegate to adjust fields for each test scenario.  Signing uses a real ECDSA
/// P-256 key pair generated per-factory-instance; the matching public key is
/// registered in a <see cref="StaticKeyRegistry"/> so the full
/// <see cref="IdentityVerifier"/> pipeline runs without any bypasses.
/// </summary>
internal sealed class ManifestTestFactory
{
    /// <summary>Key identifier embedded in every manifest produced by this factory.</summary>
    public const string TestKeyId = "integration-test-key";

    private readonly ECDsa _ecKey;

    /// <summary>The public key that corresponds to every manifest this factory signs.</summary>
    public PublicKey PublicKey { get; }

    public ManifestTestFactory()
    {
        _ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = _ecKey.ExportParameters(includePrivateParameters: false);
        PublicKey = new PublicKey
        {
            KeyId     = TestKeyId,
            Algorithm = "ES256",
            X         = p.Q.X!,
            Y         = p.Q.Y!,
        };
    }

    /// <summary>
    /// Returns a <see cref="StaticKeyRegistry"/> pre-loaded with this factory's public key.
    /// Pass this to <see cref="IdentityVerifier"/> when building an <see cref="AgentLoader"/>.
    /// </summary>
    public StaticKeyRegistry CreateRegistry() => new([PublicKey]);

    /// <summary>
    /// Loads <c>hello-world.agent.json</c>, applies <paramref name="mutate"/> (if any),
    /// sets <c>identity.key_id</c> to <see cref="TestKeyId"/>, and produces a validly-signed
    /// manifest JSON string ready to pass to <see cref="AgentLoader.LoadAsync"/>.
    /// </summary>
    /// <remarks>
    /// Signing happens over the canonical form of the JSON after mutations are applied,
    /// so tamper tests must mutate the <em>returned</em> string — not call this method with
    /// a mutation that introduces the tamper.
    /// </remarks>
    public string BuildSignedJson(Action<JsonObject>? mutate = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "hello-world.agent.json");
        var node = JsonNode.Parse(File.ReadAllText(basePath))!.AsObject();

        mutate?.Invoke(node);

        // Redirect identity to the locally-trusted test key.
        var identity = node["identity"]!.AsObject();
        identity["key_id"] = TestKeyId;

        // ForSigning strips the signature field before canonicalizing, so the
        // placeholder value only needs to satisfy the schema pattern ^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+$
        // in case the caller's mutation leaves it present.
        identity["signature"] = "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA";

        var jsonForSigning = node.ToJsonString();
        identity["signature"] = Sign(jsonForSigning);

        return node.ToJsonString();
    }

    // ── private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Produces a JWS Compact Serialization (header.payload.signature) where
    /// payload is the base64url-encoded canonical manifest bytes.
    /// Mirrors <see cref="IdentityVerifier.VerifyEs256"/> exactly.
    /// </summary>
    private string Sign(string manifestJson)
    {
        var canonical   = CanonicalJson.ForSigning(manifestJson);
        var headerJson  = $$$"""{"alg":"ES256","kid":"{{{TestKeyId}}}"}""";
        var headerB64   = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64  = Base64UrlEncode(canonical);
        var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sigBytes = _ecKey.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(sigBytes)}";
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
