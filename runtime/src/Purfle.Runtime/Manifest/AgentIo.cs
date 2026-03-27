using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentIo
{
    [JsonPropertyName("input")]
    public required JsonElement Input { get; init; }

    [JsonPropertyName("output")]
    public required JsonElement Output { get; init; }
}
