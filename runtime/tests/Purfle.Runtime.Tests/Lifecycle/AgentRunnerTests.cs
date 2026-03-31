using Purfle.Runtime.Adapters;
using Purfle.Runtime.Lifecycle;

namespace Purfle.Runtime.Tests.Lifecycle;

public sealed class AgentRunnerTests
{
    // ── CompleteAsync call verification ───────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoPromptsDirectory_CallsCompleteAsyncWithDefaultSystemPrompt()
    {
        var adapter   = new FakeLlmAdapter("ok");
        var outputDir = TempDir();
        try
        {
            await new AgentRunner(adapter, null, outputDir).RunAsync();

            Assert.Single(adapter.Calls);
            Assert.Contains("helpful agent", adapter.Calls[0].SystemPrompt,
                StringComparison.OrdinalIgnoreCase);
        }
        finally { Cleanup(outputDir); }
    }

    [Fact]
    public async Task RunAsync_WithSystemPromptFile_UsesFileContent()
    {
        var root       = TempDir();
        var promptsDir = Path.Combine(root, "prompts");
        var outputDir  = Path.Combine(root, "output");
        try
        {
            Directory.CreateDirectory(promptsDir);
            await File.WriteAllTextAsync(Path.Combine(promptsDir, "system.md"),
                "Watch the inbox carefully.");

            var adapter = new FakeLlmAdapter("done");
            await new AgentRunner(adapter, promptsDir, outputDir).RunAsync();

            Assert.Single(adapter.Calls);
            Assert.Equal("Watch the inbox carefully.", adapter.Calls[0].SystemPrompt);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public async Task RunAsync_PromptsDirectoryExistsButNoFile_UsesDefaultPrompt()
    {
        var root       = TempDir();
        var promptsDir = Path.Combine(root, "prompts");
        var outputDir  = Path.Combine(root, "output");
        try
        {
            Directory.CreateDirectory(promptsDir); // exists but system.md is absent

            var adapter = new FakeLlmAdapter("done");
            await new AgentRunner(adapter, promptsDir, outputDir).RunAsync();

            Assert.Single(adapter.Calls);
            Assert.Contains("helpful agent", adapter.Calls[0].SystemPrompt,
                StringComparison.OrdinalIgnoreCase);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public async Task RunAsync_UserMessage_ContainsTriggerTimestampAndInstruction()
    {
        var adapter   = new FakeLlmAdapter("ok");
        var outputDir = TempDir();
        try
        {
            var before = DateTimeOffset.UtcNow;
            await new AgentRunner(adapter, null, outputDir).RunAsync();
            var after = DateTimeOffset.UtcNow;

            var msg = adapter.Calls[0].UserMessage;
            Assert.Contains("You have been triggered at", msg);
            Assert.Contains("Perform your task", msg);

            // Timestamp embedded in the message should be parseable and in range.
            var tsStart = msg.IndexOf("triggered at ", StringComparison.Ordinal) + "triggered at ".Length;
            var tsEnd   = msg.IndexOf('.', tsStart + 1); // end of fractional seconds
            // Find the full ISO offset — ends at the first space or end-of-word after the 'Z'/'+'/'-' offset
            var tsStr   = msg[tsStart..].Split(' ')[0].TrimEnd('.');
            var ts      = DateTimeOffset.Parse(tsStr);
            Assert.InRange(ts, before.AddSeconds(-1), after.AddSeconds(1));
        }
        finally { Cleanup(outputDir); }
    }

    // ── run.log output ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WritesResponseToRunLog()
    {
        var adapter   = new FakeLlmAdapter("Email summary: 3 new messages.");
        var outputDir = TempDir();
        try
        {
            await new AgentRunner(adapter, null, outputDir).RunAsync();

            var logPath = Path.Combine(outputDir, "run.log");
            Assert.True(File.Exists(logPath));
            var content = await File.ReadAllTextAsync(logPath);
            Assert.Contains("Email summary: 3 new messages.", content);
        }
        finally { Cleanup(outputDir); }
    }

    [Fact]
    public async Task RunAsync_LogEntryHasTimestampHeader()
    {
        var adapter   = new FakeLlmAdapter("result");
        var outputDir = TempDir();
        try
        {
            await new AgentRunner(adapter, null, outputDir).RunAsync();

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "run.log"));
            Assert.Contains("===", content);
        }
        finally { Cleanup(outputDir); }
    }

    [Fact]
    public async Task RunAsync_CreatesOutputDirectoryIfAbsent()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"purfle-new-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(outputDir));
        try
        {
            await new AgentRunner(new FakeLlmAdapter("x"), null, outputDir).RunAsync();
            Assert.True(File.Exists(Path.Combine(outputDir, "run.log")));
        }
        finally { Cleanup(outputDir); }
    }

    [Fact]
    public async Task RunAsync_MultipleRuns_AppendsToRunLog()
    {
        var adapter   = new FakeLlmAdapter("run response");
        var outputDir = TempDir();
        try
        {
            var runner = new AgentRunner(adapter, null, outputDir);
            await runner.RunAsync();
            await runner.RunAsync();

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "run.log"));
            // Each run writes one "=== <timestamp> ===" header — count them.
            var headerCount = 0;
            var idx = 0;
            while ((idx = content.IndexOf("===", idx, StringComparison.Ordinal)) >= 0)
            {
                headerCount++;
                idx += 3;
            }
            Assert.Equal(4, headerCount); // "=== ts ===" → 2 occurrences of "===" per run × 2 runs
            Assert.Equal(2, adapter.Calls.Count);
        }
        finally { Cleanup(outputDir); }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string TempDir()
        => Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private sealed record CallRecord(string SystemPrompt, string UserMessage);

    private sealed class FakeLlmAdapter(string response) : ILlmAdapter
    {
        public List<CallRecord> Calls { get; } = [];

        public Task<string> CompleteAsync(string systemPrompt, string userMessage)
        {
            Calls.Add(new CallRecord(systemPrompt, userMessage));
            return Task.FromResult(response);
        }
    }
}
