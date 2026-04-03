using System.Text.Json.Serialization;

namespace Purfle.Runtime.TokenUsage;

/// <summary>
/// Data class for a single token usage record, serialized as one JSON line
/// in <c>usage.jsonl</c>.
/// </summary>
public sealed class TokenUsageRecord
{
    [JsonPropertyName("ts")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("agent_id")]
    public string AgentId { get; init; } = "";

    [JsonPropertyName("engine")]
    public string Engine { get; init; } = "";

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
