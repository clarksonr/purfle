using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Manifest;

/// <summary>
/// Tests for <see cref="ManifestLoader.Load(string)"/>.
/// All paths use spec/examples/ manifests located by navigating up from the test
/// output directory to the repo root.
/// </summary>
public sealed class ManifestLoaderTests
{
    private readonly ManifestLoader _loader = new();

    // ── Repo root helper ──────────────────────────────────────────────────────

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (CLAUDE.md not found).");
    }

    private static string ExamplePath(string fileName)
        => Path.Combine(RepoRoot(), "spec", "examples", fileName);

    // ── Test 1: hello-world.agent.json ────────────────────────────────────────

    [Fact]
    public void Load_HelloWorld_ReturnsPopulatedManifest()
    {
        var manifest = _loader.Load(ExamplePath("hello-world.agent.json"));

        Assert.Equal("0.1",          manifest.Purfle);
        Assert.Equal("Hello World",  manifest.Name);
        Assert.Equal("0.1.0",        manifest.Version);
        Assert.Equal(Guid.Parse("11111111-1111-4111-a111-111111111111"), manifest.Id);
        Assert.NotNull(manifest.Identity);
        Assert.Equal("clarksonr",    manifest.Identity.Author);
        Assert.Equal("ES256",        manifest.Identity.Algorithm);
        Assert.Single(manifest.Capabilities);
        Assert.Equal("llm.chat",     manifest.Capabilities[0]);
        Assert.Equal("anthropic",    manifest.Runtime.Engine);
        Assert.Null(manifest.Lifecycle);
        Assert.Null(manifest.Permissions);
    }

    // ── Test 2: assistant.agent.json ─────────────────────────────────────────

    [Fact]
    public void Load_Assistant_ReturnsPopulatedManifest()
    {
        var manifest = _loader.Load(ExamplePath("assistant.agent.json"));

        Assert.Equal("0.1",       manifest.Purfle);
        Assert.Equal("Assistant", manifest.Name);
        Assert.Equal(Guid.Parse("55555555-5555-4555-a555-555555555555"), manifest.Id);
        Assert.Contains("llm.chat",         manifest.Capabilities);
        Assert.Contains("network.outbound", manifest.Capabilities);
        Assert.Contains("env.read",         manifest.Capabilities);
        Assert.NotNull(manifest.Permissions);
        Assert.True(manifest.Permissions!.ContainsKey("network.outbound"));
        Assert.NotNull(manifest.Lifecycle);
        Assert.Equal("terminate", manifest.Lifecycle!.OnError);
    }

    // ── Test 3: nonexistent path → ManifestNotFoundException ─────────────────

    [Fact]
    public void Load_NonexistentPath_ThrowsManifestNotFoundException()
    {
        var ex = Assert.Throws<ManifestNotFoundException>(
            () => _loader.Load("/nonexistent/path/agent.json"));

        Assert.Contains("/nonexistent/path/agent.json", ex.Message);
        Assert.Equal("/nonexistent/path/agent.json", ex.ManifestPath);
    }

    // ── Test 4: malformed JSON → ManifestParseException ───────────────────────

    [Fact]
    public void Load_MalformedJson_ThrowsManifestParseException()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "{ this is not valid json {{{{");
            Assert.Throws<ManifestParseException>(() => _loader.Load(tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── Test 5: missing required field "id" → ManifestParseException ──────────

    [Fact]
    public void Load_MissingRequiredField_Id_ThrowsManifestParseException()
    {
        const string json = """
            {
              "purfle": "0.1",
              "name": "Test Agent",
              "version": "1.0.0",
              "identity": {
                "author": "Test",
                "email": "test@example.com",
                "key_id": "k1",
                "algorithm": "ES256",
                "issued_at": "2026-01-01T00:00:00Z",
                "expires_at": "2027-01-01T00:00:00Z"
              },
              "capabilities": [],
              "runtime": { "requires": "purfle/0.1", "engine": "anthropic" }
            }
            """;

        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, json);
            Assert.Throws<ManifestParseException>(() => _loader.Load(tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
