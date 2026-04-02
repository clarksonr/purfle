using System.Text.Json.Nodes;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tests.Integration.Helpers;
using Purfle.Sdk;

namespace Purfle.Runtime.Tests.Integration;

/// <summary>
/// End-to-end tests for the AIVM load sequence (spec §4).
/// Every test drives a complete load through <see cref="AgentLoader"/> and asserts
/// on the step at which the load succeeds or fails.
///
/// Base manifest: <c>spec/examples/hello-world.agent.json</c> (via <see cref="ManifestTestFactory"/>).
/// Signing uses a real ECDSA P-256 test key registered in a <see cref="StaticKeyRegistry"/>;
/// no crypto is mocked or bypassed.
/// </summary>
public sealed class LoadSequenceTests
{
    private readonly ManifestTestFactory _factory = new();

    private AgentLoader CreateLoader(
        IReadOnlySet<string>? runtimeCaps   = null,
        IAdapterFactory?      adapterFactory = null)
    {
        var caps = runtimeCaps ?? new HashSet<string> { CapabilityNegotiator.WellKnown.Inference };
        return new AgentLoader(
            new IdentityVerifier(_factory.CreateRegistry()),
            caps,
            adapterFactory);
    }

    // ── 1. Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Load_ValidSignedManifest_Succeeds()
    {
        var result = await CreateLoader().LoadAsync(_factory.BuildSignedJson());

        Assert.True(result.Success);
        Assert.NotNull(result.Manifest);
        Assert.NotNull(result.Sandbox);
        Assert.Equal("Hello World", result.Manifest.Name);
        Assert.Empty(result.Warnings);
    }

    // ── 2. Step 1 — JSON parse ────────────────────────────────────────────────

    [Fact]
    public async Task Load_MalformedJson_FailsAtParseStep()
    {
        var result = await CreateLoader().LoadAsync("{ this is not json {{");

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.MalformedJson, result.FailureReason);
    }

    // ── 3. Step 2 — schema validation ─────────────────────────────────────────

    [Fact]
    public async Task Load_ValidJsonMissingRequiredField_FailsAtSchemaStep()
    {
        // JSON is syntactically valid but is missing the required "name" field.
        const string json = """
            {
              "purfle": "0.1",
              "id": "11111111-1111-4111-a111-111111111111",
              "version": "1.0.0",
              "description": "Schema test — name field omitted.",
              "identity": {
                "author": "Test",
                "email": "test@example.com",
                "key_id": "test-key",
                "algorithm": "ES256",
                "issued_at": "2026-01-01T00:00:00Z",
                "expires_at": "2027-01-01T00:00:00Z",
                "signature": "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA"
              },
              "capabilities": [],
              "runtime": { "requires": "purfle/0.1", "engine": "anthropic" }
            }
            """;

        var result = await CreateLoader().LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    // ── 4. Step 3 — identity verification ────────────────────────────────────

    [Fact]
    public async Task Load_TamperedManifest_FailsSignatureVerification()
    {
        var signed   = _factory.BuildSignedJson();
        var tampered = JsonNode.Parse(signed)!.AsObject();
        tampered["name"] = "Tampered!";

        var result = await CreateLoader().LoadAsync(tampered.ToJsonString());

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SignatureInvalid, result.FailureReason);
    }

    [Fact]
    public async Task Load_ExpiredManifest_FailsIdentityVerification()
    {
        var json = _factory.BuildSignedJson(node =>
            node["identity"]!.AsObject()["expires_at"] = "2025-01-01T00:00:00Z");

        var result = await CreateLoader().LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.IdentityExpired, result.FailureReason);
    }

    // ── 5. Step 4 — capability negotiation ───────────────────────────────────

    [Fact]
    public async Task Load_MissingRequiredCapability_FailsCapabilityNegotiation()
    {
        // Declare "mcp.tool" which the test runtime does not support.
        var json = _factory.BuildSignedJson(node =>
        {
            node["capabilities"] = new JsonArray { "mcp.tool" };
        });

        var result = await CreateLoader(
            runtimeCaps: new HashSet<string> { CapabilityNegotiator.WellKnown.Inference }
        ).LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.CapabilityMissing, result.FailureReason);
        Assert.Contains("mcp.tool", result.FailureMessage, StringComparison.Ordinal);
    }

    // ── 6. Step 7 — adapter resolution ───────────────────────────────────────

    [Fact]
    public async Task Load_EngineNotSupportedByAdapterFactory_FailsWithEngineNotSupported()
    {
        var json   = _factory.BuildSignedJson();
        var result = await CreateLoader(adapterFactory: new AlwaysThrowAdapterFactory()).LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.EngineNotSupported, result.FailureReason);
    }

    // ── Private test stubs ────────────────────────────────────────────────────

    private sealed class AlwaysThrowAdapterFactory : IAdapterFactory
    {
        public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
            => throw new NotSupportedException(
                $"No adapter registered for engine '{manifest.Runtime.Engine}'.");
    }
}
