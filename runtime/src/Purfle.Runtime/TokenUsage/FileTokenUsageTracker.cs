using System.Text.Json;

namespace Purfle.Runtime.TokenUsage;

/// <summary>
/// Appends token usage records as JSON lines to
/// <c>&lt;outputBasePath&gt;/&lt;agentId&gt;/usage.jsonl</c>.
/// Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class FileTokenUsageTracker : ITokenUsageTracker
{
    private readonly string _outputBasePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions s_options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Creates a new tracker.
    /// </summary>
    /// <param name="outputBasePath">
    /// Base directory for agent output. Defaults to
    /// <c>%LOCALAPPDATA%/aivm/output</c> when null.
    /// </param>
    public FileTokenUsageTracker(string? outputBasePath = null)
    {
        _outputBasePath = outputBasePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "aivm", "output");
    }

    public async Task RecordAsync(string agentId, string engine, string model,
                                   int promptTokens, int completionTokens,
                                   DateTimeOffset timestamp)
    {
        var record = new TokenUsageRecord
        {
            Timestamp        = timestamp.ToString("O"),
            AgentId          = agentId,
            Engine           = engine,
            Model            = model,
            PromptTokens     = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens      = promptTokens + completionTokens,
        };

        var json = JsonSerializer.Serialize(record, s_options);
        var dir  = Path.Combine(_outputBasePath, agentId);

        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, "usage.jsonl");
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
        }
        finally
        {
            _lock.Release();
        }
    }
}
