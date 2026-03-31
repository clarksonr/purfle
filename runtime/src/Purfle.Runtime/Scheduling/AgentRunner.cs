using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Scheduling;

public enum AgentStatus { Idle, Running, Error, Stopped }

/// <summary>
/// Owns the execution lifecycle for a single scheduled agent.
/// Each call to <see cref="RunOnceAsync"/> is one trigger cycle:
/// resolve system prompt → call LLM → append to <c>OutputPath/run.log</c>.
///
/// <para>
/// Errors are swallowed: <see cref="Status"/> is set to <see cref="AgentStatus.Error"/>
/// and the exception message is appended to the log, but the exception is never
/// rethrown. Agent failures must not crash the host.
/// </para>
/// </summary>
public sealed class AgentRunner
{
    private readonly ILlmAdapter _llmAdapter;

    public AgentManifest Manifest    { get; }
    public AgentStatus   Status      { get; private set; } = AgentStatus.Idle;
    public DateTime?     LastRun     { get; private set; }
    public DateTime?     NextRun     { get; internal set; }

    /// <summary>
    /// Directory where <c>run.log</c> is written.
    /// Resolves to <c>%LOCALAPPDATA%/aivm/output/{manifest.Id}</c>.
    /// </summary>
    public string OutputPath { get; }

    public AgentRunner(AgentManifest manifest, ILlmAdapter llmAdapter)
    {
        Manifest    = manifest;
        _llmAdapter = llmAdapter;
        OutputPath  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aivm", "output", manifest.Id.ToString());
    }

    /// <summary>
    /// Executes one trigger cycle. Sets <see cref="Status"/> and <see cref="LastRun"/>
    /// on completion. Never rethrows — errors are logged and reflected in
    /// <see cref="Status"/>.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        Status = AgentStatus.Running;
        var now = DateTime.UtcNow;
        try
        {
            var systemPrompt = BuildDefaultSystemPrompt();
            var userMessage  = $"You have been triggered at {now:O}. Perform your task.";
            var response     = await _llmAdapter.CompleteAsync(systemPrompt, userMessage, ct);

            await WriteLogAsync(now, response, ct);
            Status  = AgentStatus.Idle;
            LastRun = now;
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            try { await WriteLogAsync(now, $"ERROR: {ex.Message}", ct); }
            catch { /* best-effort: don't cascade a write failure */ }
        }
    }

    /// <summary>Marks the runner stopped; no further runs will be scheduled.</summary>
    public Task StopAsync()
    {
        Status = AgentStatus.Stopped;
        return Task.CompletedTask;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private string BuildDefaultSystemPrompt()
        => $"You are {Manifest.Name}. {Manifest.Description ?? string.Empty}".TrimEnd();

    private async Task WriteLogAsync(DateTime timestamp, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(OutputPath);
        var logPath = Path.Combine(OutputPath, "run.log");
        var entry   = $"=== {timestamp:O} ==={Environment.NewLine}{content}{Environment.NewLine}{Environment.NewLine}";
        await File.AppendAllTextAsync(logPath, entry, ct);
    }
}
