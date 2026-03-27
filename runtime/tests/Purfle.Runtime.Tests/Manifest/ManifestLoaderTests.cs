using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Manifest;

public sealed class ManifestLoaderTests
{
    private readonly ManifestLoader _loader = new();

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string ValidManifestJson(Action<Dictionary<string, object>>? mutate = null)
    {
        var doc = new Dictionary<string, object>
        {
            ["purfle"] = "0.1",
            ["id"] = "11111111-1111-4111-a111-111111111111",
            ["name"] = "Test Agent",
            ["version"] = "1.0.0",
            ["description"] = "A test agent.",
            ["identity"] = new Dictionary<string, object>
            {
                ["author"] = "Test Author",
                ["email"] = "test@example.com",
                ["key_id"] = "test-key-001",
                ["algorithm"] = "ES256",
                ["issued_at"] = "2026-01-01T00:00:00Z",
                ["expires_at"] = "2027-01-01T00:00:00Z",
                ["signature"] = "eyJhbGciOiJFUzI1NiJ9.dGVzdA.dGVzdA",
            },
            ["capabilities"] = new object[] { },
            ["permissions"] = new Dictionary<string, object> { },
            ["lifecycle"] = new Dictionary<string, object>
            {
                ["on_error"] = "terminate",
            },
            ["runtime"] = new Dictionary<string, object>
            {
                ["requires"] = "purfle/0.1",
                ["engine"] = "openai-compatible",
            },
            ["io"] = new Dictionary<string, object>
            {
                ["input"] = new Dictionary<string, object> { ["type"] = "object" },
                ["output"] = new Dictionary<string, object> { ["type"] = "object" },
            },
        };

        mutate?.Invoke(doc);
        return System.Text.Json.JsonSerializer.Serialize(doc);
    }

    // ── step 1: parse ─────────────────────────────────────────────────────────

    [Fact]
    public void Load_InvalidJson_ReturnsParseFailure()
    {
        var result = _loader.Load("not json {{{");

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.MalformedJson, result.FailureReason);
    }

    [Fact]
    public void Load_NullJson_ReturnsParseFailure()
    {
        var result = _loader.Load("null");

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.MalformedJson, result.FailureReason);
    }

    // ── step 2: schema validation ─────────────────────────────────────────────

    [Fact]
    public void Load_ValidManifest_Succeeds()
    {
        var result = _loader.Load(ValidManifestJson());

        Assert.True(result.Success);
        Assert.NotNull(result.Manifest);
        Assert.Equal("0.1", result.Manifest.Purfle);
        Assert.Equal("Test Agent", result.Manifest.Name);
    }

    [Fact]
    public void Load_MissingRequiredField_ReturnsSchemaFailure()
    {
        var json = ValidManifestJson(doc => doc.Remove("name"));
        var result = _loader.Load(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    [Fact]
    public void Load_InvalidPurfleVersion_ReturnsSchemaFailure()
    {
        var json = ValidManifestJson(doc => doc["purfle"] = "not-a-version");
        var result = _loader.Load(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    [Fact]
    public void Load_InvalidSemver_ReturnsSchemaFailure()
    {
        var json = ValidManifestJson(doc => doc["version"] = "1.0");
        var result = _loader.Load(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    [Fact]
    public void Load_InvalidEngine_ReturnsSchemaFailure()
    {
        var json = ValidManifestJson(doc =>
            doc["runtime"] = new Dictionary<string, object>
            {
                ["requires"] = "purfle/0.1",
                ["engine"] = "unknown-engine",
            });
        var result = _loader.Load(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    [Fact]
    public void Load_UnknownTopLevelField_ReturnsSchemaFailure()
    {
        var json = ValidManifestJson(doc => doc["unexpected_field"] = "value");
        var result = _loader.Load(json);

        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SchemaValidationFailed, result.FailureReason);
    }

    [Fact]
    public void Load_ValidCapabilityWithRequired_DeserializesCorrectly()
    {
        var json = ValidManifestJson(doc =>
            doc["capabilities"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "web-search",
                    ["description"] = "Search the web.",
                    ["required"] = true,
                },
            });

        var result = _loader.Load(json);

        Assert.True(result.Success);
        var cap = Assert.Single(result.Manifest!.Capabilities);
        Assert.Equal("web-search", cap.Id);
        Assert.True(cap.Required);
    }

    [Fact]
    public void Load_EmptyCapabilities_Succeeds()
    {
        var json = ValidManifestJson(doc => doc["capabilities"] = new object[] { });
        var result = _loader.Load(json);

        Assert.True(result.Success);
        Assert.Empty(result.Manifest!.Capabilities);
    }
}
