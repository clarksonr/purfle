using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Identity;

/// <summary>
/// Tests for load sequence step 3 — identity verification.
/// Uses real ECDSA P-256 key pairs; no mocked crypto.
/// </summary>
public sealed class IdentityVerifierTests
{
    // Fixed timestamps so the canonical JSON is stable across two BuildManifest calls.
    private static readonly DateTimeOffset s_issuedAt  = new(2026, 3, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_validExpiry = new(2027, 3, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_pastExpiry  = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (ECDsa ecKey, PublicKey pubKey) GenerateKey(string keyId = "test-key")
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: false);
        var pubKey = new PublicKey
        {
            KeyId = keyId,
            Algorithm = "ES256",
            X = p.Q.X!,
            Y = p.Q.Y!,
        };
        return (ecdsa, pubKey);
    }

    /// <summary>
    /// Signs the canonical form of <paramref name="manifestJson"/> (with signature removed)
    /// and returns a JWS compact serialization.
    /// </summary>
    private static string Sign(string manifestJson, ECDsa key, string keyId)
    {
        var canonical = Purfle.Runtime.Manifest.CanonicalJson.ForSigning(manifestJson);
        var header = $$$"""{"alg":"ES256","kid":"{{{keyId}}}"}""";
        var headerB64  = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadB64 = Base64UrlEncode(canonical);
        var input = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sig = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(sig)}";
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>
    /// Builds a manifest and signs it. Returns both the manifest object and its
    /// serialized JSON so the verifier sees the exact same bytes that were signed.
    /// </summary>
    private static (AgentManifest manifest, string rawJson) BuildSigned(
        ECDsa key,
        string keyId,
        DateTimeOffset? expiresAt = null)
    {
        // 1. Build with placeholder signature
        var placeholder = BuildManifest("placeholder", keyId, expiresAt ?? s_validExpiry);
        var placeholderJson = JsonSerializer.Serialize(placeholder);

        // 2. Sign the placeholder JSON (ForSigning strips the signature field)
        var sig = Sign(placeholderJson, key, keyId);

        // 3. Inject real signature and re-serialize — all other fields are identical
        var signed = placeholder with { Identity = placeholder.Identity with { Signature = sig } };
        var rawJson = JsonSerializer.Serialize(signed);

        return (signed, rawJson);
    }

    private static AgentManifest BuildManifest(string signature, string keyId, DateTimeOffset expiresAt) =>
        new()
        {
            Purfle = "0.1",
            Id = "11111111-1111-4111-a111-111111111111",
            Name = "Test Agent",
            Version = "1.0.0",
            Description = "A test agent.",
            Identity = new AgentIdentity
            {
                Author = "Tester",
                Email = "test@example.com",
                KeyId = keyId,
                Algorithm = "ES256",
                IssuedAt = s_issuedAt,
                ExpiresAt = expiresAt,
                Signature = signature,
            },
            Capabilities = [],
            Permissions = new AgentPermissions(),
            Lifecycle = new AgentLifecycle { OnError = OnErrorPolicy.Terminate },
            Runtime = new AgentRuntime { Requires = "purfle/0.1", Engine = EngineType.OpenAiCompatible },
            Io = new AgentIo
            {
                Input  = JsonDocument.Parse("""{"type":"object"}""").RootElement,
                Output = JsonDocument.Parse("""{"type":"object"}""").RootElement,
            },
        };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidSignature_Succeeds()
    {
        var (ecKey, pubKey) = GenerateKey();
        var (manifest, rawJson) = BuildSigned(ecKey, pubKey.KeyId);

        var registry = new StaticKeyRegistry([pubKey]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(manifest, rawJson);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Verify_UnknownKey_ReturnsKeyNotFound()
    {
        var (_, pubKey) = GenerateKey("known-key");
        var manifest = BuildManifest("eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA", "unknown-key", s_validExpiry);
        var rawJson = JsonSerializer.Serialize(manifest);

        var registry = new StaticKeyRegistry([pubKey]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(manifest, rawJson);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.KeyNotFound, result.FailureReason);
    }

    [Fact]
    public async Task Verify_RevokedKey_ReturnsKeyRevoked()
    {
        var (ecKey, pubKey) = GenerateKey("revoked-key");
        var (manifest, rawJson) = BuildSigned(ecKey, "revoked-key");

        var registry = new StaticKeyRegistry([pubKey], revokedKeyIds: ["revoked-key"]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(manifest, rawJson);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.KeyRevoked, result.FailureReason);
    }

    [Fact]
    public async Task Verify_TamperedManifest_ReturnsSignatureInvalid()
    {
        var (ecKey, pubKey) = GenerateKey();
        var (manifest, _) = BuildSigned(ecKey, pubKey.KeyId);

        // Tamper: change the name after signing, then re-serialize
        var tampered = manifest with { Name = "Tampered!" };
        var tamperedJson = JsonSerializer.Serialize(tampered);

        var registry = new StaticKeyRegistry([pubKey]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(tampered, tamperedJson);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SignatureInvalid, result.FailureReason);
    }

    [Fact]
    public async Task Verify_ExpiredManifest_ReturnsManifestExpired()
    {
        var (ecKey, pubKey) = GenerateKey();
        var (manifest, rawJson) = BuildSigned(ecKey, pubKey.KeyId, expiresAt: s_pastExpiry);

        var registry = new StaticKeyRegistry([pubKey]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(manifest, rawJson);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.ManifestExpired, result.FailureReason);
    }
}
