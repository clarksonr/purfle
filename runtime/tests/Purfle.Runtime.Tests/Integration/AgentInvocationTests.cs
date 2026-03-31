using System.Text.Json.Nodes;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Sessions;
using Purfle.Runtime.Tests.Integration.Helpers;

namespace Purfle.Runtime.Tests.Integration;

/// <summary>
/// Live AI integration tests. Each test loads a real agent manifest, re-signs it with an
/// ephemeral key (the same local-dev trust model used by the MAUI app), invokes the
/// actual LLM endpoint, and asserts the response is sensible.
///
/// Tests skip automatically when the required API key is not set, so they are safe
/// to include in normal CI — they report as Skipped rather than failing.
///
/// To run all live tests:
///   $env:GEMINI_API_KEY    = "..."
///   $env:ANTHROPIC_API_KEY = "..."
///   dotnet test --filter "Category=LiveAI"
/// </summary>
[Trait("Category", "LiveAI")]
public sealed class AgentInvocationTests
{
    private static readonly IReadOnlySet<string> RuntimeCaps = new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.FsRead,
        CapabilityNegotiator.WellKnown.FsWrite,
    };

    private static AgentLoader CreateLoader(StaticKeyRegistry registry) =>
        new(new IdentityVerifier(registry), RuntimeCaps, new TestAdapterFactory());

    // ── Gemini: chat agent ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ChatAgent_SimpleQuestion_ReturnsAnswer()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY")),
            "GEMINI_API_KEY not set");

        var manifestJson = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "chat.agent.json"));

        var (signedJson, registry) = AgentResigner.Resign(manifestJson);
        var result = await CreateLoader(registry).LoadAsync(signedJson);

        Assert.True(result.Success, $"Load failed: {result.FailureReason} — {result.FailureMessage}");

        var reply = await result.Adapter!.InvokeAsync(
            "You are a helpful assistant. Be concise.",
            "What is the capital of France? Reply with just the city name.",
            CancellationToken.None);

        Assert.NotEmpty(reply);
        Assert.Contains("Paris", reply, StringComparison.OrdinalIgnoreCase);
    }

    // ── Gemini: file-search agent reading a file ───────────────────────────────

    [SkippableFact]
    public async Task FileSearchAgent_ReadFile_ReturnsContent()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY")),
            "GEMINI_API_KEY not set");

        var tempFile = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, "The secret code is PURFLE-7734.");

            // Override filesystem.read to allow the temp directory so the built-in
            // read_file tool can reach the fixture file without touching Downloads.
            var tempDir = Path.GetTempPath().Replace('\\', '/');
            var manifestJson = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "file-search.agent.json"));

            var (signedJson, registry) = AgentResigner.Resign(manifestJson, m =>
            {
                m["permissions"]!.AsObject()["fs.read"] =
                    JsonNode.Parse($$$"""{"paths":["{{{tempDir}}}**/*"]}""")!;
            });

            var result = await CreateLoader(registry).LoadAsync(signedJson);
            Assert.True(result.Success, $"Load failed: {result.FailureReason} — {result.FailureMessage}");

            var normalizedPath = tempFile.Replace('\\', '/');
            var reply = await result.Adapter!.InvokeAsync(
                "You are a file reading assistant. Use the read_file tool to answer questions about file contents.",
                $"Use read_file to read '{normalizedPath}' and tell me the secret code.",
                CancellationToken.None);

            Assert.NotEmpty(reply);
            Assert.Contains("PURFLE-7734", reply, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── Anthropic: hello-world agent ───────────────────────────────────────────

    [SkippableFact]
    public async Task HelloWorldAgent_SimpleQuestion_ReturnsAnswer()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            "ANTHROPIC_API_KEY not set");

        var manifestJson = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "hello-world.agent.json"));

        var (signedJson, registry) = AgentResigner.Resign(manifestJson);
        var result = await CreateLoader(registry).LoadAsync(signedJson);

        Assert.True(result.Success, $"Load failed: {result.FailureReason} — {result.FailureMessage}");

        var reply = await result.Adapter!.InvokeAsync(
            "You are a helpful assistant. Be concise.",
            "What is 6 times 7? Reply with just the number.",
            CancellationToken.None);

        Assert.NotEmpty(reply);
        Assert.Contains("42", reply);
    }

    // ── Multi-turn: conversation context persists across turns ─────────────────

    [SkippableFact]
    public async Task ChatAgent_MultiTurn_MaintainsContext()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY")),
            "GEMINI_API_KEY not set");

        var manifestJson = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "chat.agent.json"));

        var (signedJson, registry) = AgentResigner.Resign(manifestJson);
        var result = await CreateLoader(registry).LoadAsync(signedJson);

        Assert.True(result.Success, $"Load failed: {result.FailureReason} — {result.FailureMessage}");

        var session = new ConversationSession(result.Adapter!, "You are a concise assistant.");

        await session.SendAsync("My favourite number is 9371. Remember it.");
        var second = await session.SendAsync("What is my favourite number?");

        Assert.Contains("9371", second);
    }
}
