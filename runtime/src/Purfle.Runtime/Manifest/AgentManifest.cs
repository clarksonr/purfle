using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentManifest
{
    [JsonPropertyName("purfle")]
    public required string Purfle { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("identity")]
    public required AgentIdentity Identity { get; init; }

    [JsonPropertyName("capabilities")]
    public required IReadOnlyList<AgentCapability> Capabilities { get; init; }

    [JsonPropertyName("permissions")]
    public required AgentPermissions Permissions { get; init; }

    [JsonPropertyName("lifecycle")]
    public required AgentLifecycle Lifecycle { get; init; }

    [JsonPropertyName("runtime")]
    public required AgentRuntime Runtime { get; init; }

    [JsonPropertyName("io")]
    public required AgentIo Io { get; init; }
}
