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
    public const string TestKeyId = "integration-test-key";

    private readonly ECDsa _ecKey;

    public PublicKey PublicKey { get; }

    public ManifestTestFactory()
    {
        _ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p  = _ecKey.ExportParameters(includePrivateParameters: false);
        PublicKey = new PublicKey
        {
            KeyId     = TestKeyId,
            Algorithm = "ES256",
            X         = p.Q.X!,
            Y         = p.Q.Y!,
        };
    }

    public StaticKeyRegistry CreateRegistry() => new([PublicKey]);

    /// <summary>
    /// Loads <c>hello-world.agent.json</c>, applies <paramref name="mutate"/> (if any),
    /// sets <c>identity.key_id</c> to <see cref="TestKeyId"/>, and produces a
    /// validly-signed manifest JSON string.
    /// </summary>
    public string BuildSignedJson(Action<JsonObject>? mutate = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "hello-world.agent.json");
        var node     = JsonNode.Parse(File.ReadAllText(basePath))!.AsObject();

        mutate?.Invoke(node);

        var identity    = node["identity"]!.AsObject();
        identity["key_id"]    = TestKeyId;
        identity["signature"] = "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA";

        var jsonForSigning    = node.ToJsonString();
        identity["signature"] = Sign(jsonForSigning);

        return node.ToJsonString();
    }

    private string Sign(string manifestJson)
    {
        var canonical  = CanonicalJson.ForSigning(manifestJson);
        var headerJson = $$$"""{"alg":"ES256","kid":"{{{TestKeyId}}}"}""";
        var headerB64  = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(canonical);
        var input      = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sigBytes   = _ecKey.SignData(
            input,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(sigBytes)}";
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
