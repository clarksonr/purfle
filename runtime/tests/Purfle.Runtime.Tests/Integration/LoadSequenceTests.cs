using System.Text.Json.Nodes;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tests.Integration.Helpers;

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
            new ManifestLoader(),
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

    /// <summary>
    /// Valid JSON that is missing a required top-level field ("name") must be
    /// rejected at schema validation before reaching identity verification.
    /// </summary>
    [Fact]
    public async Task Load_ValidJsonMissingRequiredField_FailsAtSchemaStep()
    {
        // Build raw JSON without "name" — no signing required because the loader
        // fails at step 2, before it ever reaches step 3 (signature verification).
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
              "permissions": {},
              "lifecycle": { "on_error": "terminate" },
              "runtime": { "requires": "purfle/0.1", "engine": "openai-compatible" },
              "io": { "input": { "type": "object" }, "output": { "type": "object" } }
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
        // Sign a valid manifest, then mutate the serialized JSON before loading.
        // The payload in the JWS no longer matches the tampered content.
        var signed = _factory.BuildSignedJson();
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
        Assert.Equal(LoadFailureReason.ManifestExpired, result.FailureReason);
    }

    // ── 5. Step 4 — capability negotiation ───────────────────────────────────

    [Fact]
    public async Task Load_MissingRequiredCapability_FailsCapabilityNegotiation()
    {
        var json = _factory.BuildSignedJson(node =>
        {
            node["capabilities"] = new JsonArray
            {
                new JsonObject { ["id"] = "code-execution", ["required"] = true },
            };
        });

        // Runtime advertises only implicit inference — code-execution is absent.
        var result = await CreateLoader(
            runtimeCaps: new HashSet<string> { CapabilityNegotiator.WellKnown.Inference }
        ).LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.CapabilityMissing, result.FailureReason);
        Assert.Contains("code-execution", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_MissingOptionalCapability_SucceedsWithWarning()
    {
        var json = _factory.BuildSignedJson(node =>
        {
            node["capabilities"] = new JsonArray
            {
                // required omitted → defaults to false in the schema
                new JsonObject { ["id"] = "text-to-speech" },
            };
        });

        var result = await CreateLoader(
            runtimeCaps: new HashSet<string> { CapabilityNegotiator.WellKnown.Inference }
        ).LoadAsync(json);

        Assert.True(result.Success);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("text-to-speech", warning, StringComparison.Ordinal);
    }

    // ── 6. Step 7 — adapter resolution ───────────────────────────────────────

    /// <summary>
    /// The JSON schema constrains <c>runtime.engine</c> to a fixed enum, so a
    /// genuinely unknown engine string is rejected at step 2 before it reaches
    /// the adapter layer.  The equivalent step-7 failure is an adapter factory
    /// that does not support the manifest's (valid) declared engine.  This test
    /// proves that path returns <see cref="LoadFailureReason.EngineNotSupported"/>.
    /// </summary>
    [Fact]
    public async Task Load_EngineNotSupportedByAdapterFactory_FailsWithEngineNotSupported()
    {
        // hello-world declares engine: openai-compatible.
        var json   = _factory.BuildSignedJson();
        var result = await CreateLoader(adapterFactory: new AlwaysThrowAdapterFactory()).LoadAsync(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.EngineNotSupported, result.FailureReason);
    }

    // ── Private test stubs ────────────────────────────────────────────────────

    /// <summary>
    /// Simulates a runtime that has no adapter registered for any engine.
    /// </summary>
    private sealed class AlwaysThrowAdapterFactory : IAdapterFactory
    {
        public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox)
            => throw new NotSupportedException(
                $"No adapter registered for engine '{manifest.Runtime.Engine}'.");
    }
}
