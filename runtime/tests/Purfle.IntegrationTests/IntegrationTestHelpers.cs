using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;

namespace Purfle.IntegrationTests;

/// <summary>
/// Shared helpers for building signed manifests and mock adapters in integration tests.
/// </summary>
internal sealed class ManifestTestFactory
{
    public const string TestKeyId = "integration-test-key";

    private readonly ECDsa _ecKey;

    public PublicKey PublicKey { get; }

    public ManifestTestFactory()
    {
        _ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = _ecKey.ExportParameters(includePrivateParameters: false);
        PublicKey = new PublicKey
        {
            KeyId = TestKeyId,
            Algorithm = "ES256",
            X = p.Q.X!,
            Y = p.Q.Y!,
        };
    }

    public StaticKeyRegistry CreateRegistry() => new([PublicKey]);

    /// <summary>
    /// Builds a validly-signed manifest JSON string, optionally mutated.
    /// </summary>
    public string BuildSignedJson(Action<JsonObject>? mutate = null)
    {
        var node = JsonNode.Parse(BaseManifestJson())!.AsObject();
        mutate?.Invoke(node);

        var identity = node["identity"]!.AsObject();
        identity["key_id"] = TestKeyId;
        identity["signature"] = "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA"; // placeholder

        var jsonForSigning = node.ToJsonString();
        identity["signature"] = Sign(jsonForSigning);

        return node.ToJsonString();
    }

    /// <summary>
    /// Builds a signed manifest JSON and then tampers with the name field.
    /// The signature will not match.
    /// </summary>
    public string BuildTamperedJson()
    {
        var validJson = BuildSignedJson();
        var node = JsonNode.Parse(validJson)!.AsObject();
        node["name"] = "TAMPERED AGENT NAME";
        return node.ToJsonString();
    }

    private string Sign(string manifestJson)
    {
        var canonical = CanonicalJson.ForSigning(manifestJson);
        var headerJson = $$$"""{"alg":"ES256","kid":"{{{TestKeyId}}}"}""";
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(canonical);
        var input = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sigBytes = _ecKey.SignData(
            input,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(sigBytes)}";
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string BaseManifestJson() => """
    {
      "purfle": "0.1",
      "id": "11111111-1111-4111-a111-111111111111",
      "name": "Hello World",
      "version": "0.1.0",
      "description": "Integration test agent.",
      "identity": {
        "author": "test",
        "email": "test@example.com",
        "key_id": "test-key",
        "algorithm": "ES256",
        "issued_at": "2026-01-01T00:00:00Z",
        "expires_at": "2027-12-31T00:00:00Z"
      },
      "capabilities": ["llm.chat"],
      "runtime": {
        "requires": "purfle/0.1",
        "engine": "gemini",
        "model": "gemini-2.0-flash"
      }
    }
    """;
}

/// <summary>
/// Mock LLM adapter that returns predetermined text and token counts.
/// </summary>
internal sealed class MockLlmAdapter(string responseText = "Hello from mock!", int inputTokens = 100, int outputTokens = 50) : ILlmAdapter
{
    public int CallCount { get; private set; }

    public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(new LlmResult(responseText, inputTokens, outputTokens));
    }
}
