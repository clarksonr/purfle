using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentRuntime
{
    [JsonPropertyName("requires")]
    public required string Requires { get; init; }

    [JsonPropertyName("engine")]
    public required EngineType Engine { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("adapter")]
    public string? Adapter { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<EngineType>))]
public enum EngineType
{
    [JsonStringEnumMemberName("openai-compatible")]
    OpenAiCompatible,

    [JsonStringEnumMemberName("anthropic")]
    Anthropic,

    [JsonStringEnumMemberName("ollama")]
    Ollama,
}
