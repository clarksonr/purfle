using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentLifecycle
{
    [JsonPropertyName("init_timeout_ms")]
    public int InitTimeoutMs { get; init; } = 5000;

    [JsonPropertyName("max_runtime_ms")]
    public int MaxRuntimeMs { get; init; } = 300_000;

    [JsonPropertyName("on_error")]
    public required OnErrorPolicy OnError { get; init; }

    [JsonPropertyName("restartable")]
    public bool Restartable { get; init; } = false;
}

[JsonConverter(typeof(JsonStringEnumConverter<OnErrorPolicy>))]
public enum OnErrorPolicy
{
    [JsonStringEnumMemberName("terminate")]
    Terminate,

    [JsonStringEnumMemberName("suspend")]
    Suspend,

    [JsonStringEnumMemberName("retry")]
    Retry,
}
