using System.Text.Json.Serialization;

namespace Purfle.Runtime.Ipc;

/// <summary>
/// Message sent from an agent process to the AIVM runtime via stdout.
/// </summary>
public sealed class IpcResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "response";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("output")]
    public string Output { get; set; } = "";

    [JsonPropertyName("toolCalls")]
    public List<IpcToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public sealed class IpcToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("arguments")]
    public object? Arguments { get; set; }
}

public sealed class IpcToolResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "toolResult";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("result")]
    public object? Result { get; set; }
}
