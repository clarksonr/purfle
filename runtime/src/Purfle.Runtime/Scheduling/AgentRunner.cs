using System.Diagnostics;
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

    /// <summary>
    /// Optional directory that may contain <c>system.md</c>.
    /// When set, the system prompt is loaded from file instead of the default.
    /// </summary>
    public string? PromptsDirectory { get; }

    /// <summary>Error message from the most recent failed run, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>Token usage from the most recent run (input + output tokens).</summary>
    public (int Input, int Output) LastTokenUsage { get; private set; }

    /// <summary>Duration of the most recent run.</summary>
    public TimeSpan? LastRunDuration { get; private set; }

    public AgentRunner(AgentManifest manifest, ILlmAdapter llmAdapter, string? promptsDirectory = null)
    {
        Manifest    = manifest;
        _llmAdapter = llmAdapter;
        PromptsDirectory = promptsDirectory;
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
        Status    = AgentStatus.Running;
        LastError = null;
        var now   = DateTime.UtcNow;
        var sw    = Stopwatch.StartNew();
        try
        {
            var systemPrompt = await LoadSystemPromptAsync(ct);
            var userMessage  = $"You have been triggered at {now:O}. Perform your task.";
            var result       = await _llmAdapter.CompleteAsync(systemPrompt, userMessage, ct);

            sw.Stop();
            LastRunDuration  = sw.Elapsed;
            LastTokenUsage   = (result.InputTokens, result.OutputTokens);

            await WriteLogAsync(now, result.Text, ct);
            await WriteStructuredLogAsync(now, sw.Elapsed, "success", null, ct);
            Status  = AgentStatus.Idle;
            LastRun = now;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastRunDuration = sw.Elapsed;
            Status    = AgentStatus.Error;
            LastError = ex.Message;
            try
            {
                await WriteLogAsync(now, $"ERROR: {ex.Message}\n{ex.StackTrace}", ct);
                await WriteStructuredLogAsync(now, sw.Elapsed, "error", ex.Message, ct);
            }
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

    private async Task<string> LoadSystemPromptAsync(CancellationToken ct)
    {
        if (PromptsDirectory is not null)
        {
            var path = Path.Combine(PromptsDirectory, "system.md");
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path, ct);
        }
        return $"You are {Manifest.Name}. {Manifest.Description ?? string.Empty}".TrimEnd();
    }

    private async Task WriteLogAsync(DateTime timestamp, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(OutputPath);
        var logPath = Path.Combine(OutputPath, "run.log");
        var entry   = $"=== {timestamp:O} ==={Environment.NewLine}{content}{Environment.NewLine}{Environment.NewLine}";
        await File.AppendAllTextAsync(logPath, entry, ct);
    }

    private async Task WriteStructuredLogAsync(
        DateTime triggerTime, TimeSpan duration, string status, string? error, CancellationToken ct)
    {
        Directory.CreateDirectory(OutputPath);
        var logPath = Path.Combine(OutputPath, "run.jsonl");
        var entry = new RunLogEntry
        {
            AgentId     = Manifest.Id.ToString(),
            AgentName   = Manifest.Name,
            TriggerTime = triggerTime.ToString("O"),
            DurationMs  = (long)duration.TotalMilliseconds,
            Status      = status,
            InputTokens = LastTokenUsage.Input,
            OutputTokens = LastTokenUsage.Output,
            OutputPath  = OutputPath,
            Error       = error,
        };
        await File.AppendAllTextAsync(logPath, entry.ToJson() + Environment.NewLine, ct);
    }
}
