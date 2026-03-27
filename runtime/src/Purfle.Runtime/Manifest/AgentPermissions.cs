using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentPermissions
{
    [JsonPropertyName("network")]
    public NetworkPermissions? Network { get; init; }

    [JsonPropertyName("filesystem")]
    public FilesystemPermissions? Filesystem { get; init; }

    [JsonPropertyName("environment")]
    public EnvironmentPermissions? Environment { get; init; }

    [JsonPropertyName("tools")]
    public ToolPermissions? Tools { get; init; }
}

public sealed record NetworkPermissions
{
    [JsonPropertyName("allow")]
    public IReadOnlyList<string> Allow { get; init; } = [];

    [JsonPropertyName("deny")]
    public IReadOnlyList<string> Deny { get; init; } = [];
}

public sealed record FilesystemPermissions
{
    [JsonPropertyName("read")]
    public IReadOnlyList<string> Read { get; init; } = [];

    [JsonPropertyName("write")]
    public IReadOnlyList<string> Write { get; init; } = [];
}

public sealed record EnvironmentPermissions
{
    [JsonPropertyName("allow")]
    public IReadOnlyList<string> Allow { get; init; } = [];
}

public sealed record ToolPermissions
{
    [JsonPropertyName("mcp")]
    public IReadOnlyList<string> Mcp { get; init; } = [];
}
