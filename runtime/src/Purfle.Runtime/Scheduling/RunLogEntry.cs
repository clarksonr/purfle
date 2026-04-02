using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Runtime.Scheduling;

/// <summary>
/// Structured log entry for a single agent run. Written as JSON to
/// <c>run.jsonl</c> (one entry per line, append-only).
/// </summary>
public sealed class RunLogEntry
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; init; } = "";

    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = "";

    [JsonPropertyName("trigger_time")]
    public string TriggerTime { get; init; } = "";

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = ""; // "success" | "error"

    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("tool_calls")]
    public List<string> ToolCalls { get; init; } = [];

    [JsonPropertyName("output_path")]
    public string OutputPath { get; init; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    private static readonly JsonSerializerOptions s_options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, s_options);
}
