using System.Text.Json.Serialization;

namespace Purfle.Runtime.Ipc;

/// <summary>
/// Message sent from the AIVM runtime to an agent process via stdin.
/// </summary>
public sealed class IpcRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "execute";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("input")]
    public string? Input { get; set; }

    [JsonPropertyName("context")]
    public IpcContext? Context { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }
}

public sealed class IpcContext
{
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("previousMessages")]
    public List<object>? PreviousMessages { get; set; }
}
