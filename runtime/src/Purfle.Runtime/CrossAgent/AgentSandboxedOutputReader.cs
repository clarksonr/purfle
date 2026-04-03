using System.Text.Json;

namespace Purfle.Runtime.CrossAgent;

/// <summary>
/// Concrete implementation of <see cref="IAgentOutputReader"/> that enforces the
/// io.reads allowlist. The AIVM creates one instance per agent, scoped to that
/// agent's declared reads only.
/// </summary>
public sealed class AgentSandboxedOutputReader : IAgentOutputReader
{
    private readonly string _requestingAgentId;
    private readonly IReadOnlySet<string> _allowedAgentIds;
    private readonly string _outputBasePath;

    /// <summary>
    /// Creates a sandboxed reader for one agent.
    /// </summary>
    /// <param name="requestingAgentId">The agent that owns this reader.</param>
    /// <param name="allowedAgentIds">Agent IDs from io.reads — the only agents this reader can access.</param>
    /// <param name="outputBasePath">
    /// Base path for agent outputs. Defaults to &lt;LocalAppData&gt;/aivm/output.
    /// </param>
    public AgentSandboxedOutputReader(
        string requestingAgentId,
        IReadOnlySet<string> allowedAgentIds,
        string? outputBasePath = null)
    {
        _requestingAgentId = requestingAgentId;
        _allowedAgentIds = allowedAgentIds;
        _outputBasePath = outputBasePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "aivm", "output");
    }

    public async Task<string?> ReadLatestAsync(string agentId, CancellationToken ct = default)
    {
        EnforceAllowlist(agentId);

        var logPath = Path.Combine(_outputBasePath, agentId, "run.log");
        if (!File.Exists(logPath))
            return null;

        var content = await File.ReadAllTextAsync(logPath, ct);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // run.log uses "=== <timestamp> ===" delimiters. Return the last entry.
        var entries = content.Split("=== ", StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0)
            return null;

        var lastEntry = entries[^1];
        // Strip the timestamp header line
        var newlineIdx = lastEntry.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        if (newlineIdx >= 0 && newlineIdx + Environment.NewLine.Length < lastEntry.Length)
            return lastEntry[(newlineIdx + Environment.NewLine.Length)..].TrimEnd();

        return lastEntry.TrimEnd();
    }

    public async Task<IReadOnlyList<AgentOutputRecord>> ReadHistoryAsync(
        string agentId, int maxRuns, CancellationToken ct = default)
    {
        EnforceAllowlist(agentId);

        var jsonlPath = Path.Combine(_outputBasePath, agentId, "run.jsonl");
        if (!File.Exists(jsonlPath))
            return [];

        var lines = await File.ReadAllLinesAsync(jsonlPath, ct);
        var records = new List<AgentOutputRecord>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var runId = root.TryGetProperty("trigger_time", out var tt) ? tt.GetString() ?? "" : "";
                var timestamp = root.TryGetProperty("trigger_time", out var ts)
                    ? DateTimeOffset.TryParse(ts.GetString(), out var dto) ? dto : DateTimeOffset.MinValue
                    : DateTimeOffset.MinValue;
                var status = root.TryGetProperty("status", out var st) && st.GetString() == "error"
                    ? AgentOutputStatus.Error
                    : AgentOutputStatus.Success;

                // Read the output content from run.log corresponding to this entry
                var content = root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String
                    ? err.GetString() ?? ""
                    : "";

                // Try to read the actual output from the output path
                if (status == AgentOutputStatus.Success)
                {
                    var outputDir = Path.Combine(_outputBasePath, agentId);
                    var logPath = Path.Combine(outputDir, "run.log");
                    if (File.Exists(logPath))
                    {
                        // For history, we include a summary — the full log content
                        // is available via ReadLatestAsync for the most recent run
                        content = $"Run at {timestamp:O} — {status}";
                    }
                }

                records.Add(new AgentOutputRecord(runId, timestamp, content, status));
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        // Return newest first, limited to maxRuns
        records.Reverse();
        return records.Take(maxRuns).ToList();
    }

    private void EnforceAllowlist(string agentId)
    {
        if (!_allowedAgentIds.Contains(agentId))
            throw new AgentSandboxViolationException(_requestingAgentId, agentId);
    }
}
