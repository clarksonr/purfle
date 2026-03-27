using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentCapability
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; } = false;
}
