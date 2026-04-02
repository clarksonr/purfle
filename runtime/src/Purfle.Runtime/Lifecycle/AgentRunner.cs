using Purfle.Runtime.Adapters;

namespace Purfle.Runtime.Lifecycle;

/// <summary>
/// Executes a single scheduled agent run: loads the system prompt, calls the LLM,
/// and appends the response to <c>OutputPath/run.log</c> with a timestamp header.
/// </summary>
public sealed class AgentRunner
{
    private const string SystemPromptFileName = "system.md";

    private const string DefaultSystemPrompt =
        "You are a helpful agent. Describe what you would do if you had access to the declared tools.";

    private readonly ILlmAdapter _adapter;
    private readonly string? _promptsDirectory;
    private readonly string _outputPath;

    /// <param name="adapter">LLM adapter used to complete the trigger message.</param>
    /// <param name="promptsDirectory">
    /// Directory that may contain <c>system.md</c>. Pass <c>null</c> to use the default prompt.
    /// </param>
    /// <param name="outputPath">Directory where <c>run.log</c> is written.</param>
    public AgentRunner(ILlmAdapter adapter, string? promptsDirectory, string outputPath)
    {
        _adapter = adapter;
        _promptsDirectory = promptsDirectory;
        _outputPath = outputPath;
    }

    /// <summary>
    /// Runs one trigger cycle: resolve system prompt → call LLM → write log entry.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var systemPrompt = await LoadSystemPromptAsync(ct);
        var timestamp    = DateTimeOffset.UtcNow;
        var userMessage  = $"You have been triggered at {timestamp:O}. Perform your task.";
        var result       = await _adapter.CompleteAsync(systemPrompt, userMessage, ct);
        await WriteLogAsync(timestamp, result.Text, ct);
    }

    private async Task<string> LoadSystemPromptAsync(CancellationToken ct)
    {
        if (_promptsDirectory is not null)
        {
            var path = Path.Combine(_promptsDirectory, SystemPromptFileName);
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path, ct);
        }

        return DefaultSystemPrompt;
    }

    private async Task WriteLogAsync(DateTimeOffset timestamp, string response, CancellationToken ct)
    {
        Directory.CreateDirectory(_outputPath);
        var logPath = Path.Combine(_outputPath, "run.log");
        var entry = $"=== {timestamp:O} ==={Environment.NewLine}{response}{Environment.NewLine}{Environment.NewLine}";
        await File.AppendAllTextAsync(logPath, entry, ct);
    }
}
