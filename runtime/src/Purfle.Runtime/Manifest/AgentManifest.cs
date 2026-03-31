using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentManifest
{
    [JsonPropertyName("purfle")]
    public required string Purfle { get; init; }

    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("identity")]
    public required IdentityBlock Identity { get; init; }

    [JsonPropertyName("capabilities")]
    public required List<string> Capabilities { get; init; }

    [JsonPropertyName("permissions")]
    public Dictionary<string, JsonElement>? Permissions { get; init; }

    [JsonPropertyName("schedule")]
    public ScheduleBlock? Schedule { get; init; }

    [JsonPropertyName("runtime")]
    public required RuntimeBlock Runtime { get; init; }

    [JsonPropertyName("lifecycle")]
    public LifecycleBlock? Lifecycle { get; init; }

    [JsonPropertyName("tools")]
    public List<ToolBinding>? Tools { get; init; }

    [JsonPropertyName("io")]
    public JsonElement? Io { get; init; }
}

public sealed record IdentityBlock
{
    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("key_id")]
    public required string KeyId { get; init; }

    [JsonPropertyName("algorithm")]
    public required string Algorithm { get; init; }

    [JsonPropertyName("issued_at")]
    public required DateTimeOffset IssuedAt { get; init; }

    [JsonPropertyName("expires_at")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// JWS Compact Serialization of the signed canonical manifest body.
    /// Omitted at authoring time; injected by the SDK on publish.
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; init; }
}

public sealed record RuntimeBlock
{
    [JsonPropertyName("requires")]
    public required string Requires { get; init; }

    /// <summary>Inference engine identifier (e.g. "anthropic", "ollama").</summary>
    [JsonPropertyName("engine")]
    public required string Engine { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }
}

public sealed record LifecycleBlock
{
    [JsonPropertyName("on_load")]
    public string? OnLoad { get; init; }

    [JsonPropertyName("on_unload")]
    public string? OnUnload { get; init; }

    /// <summary>AIVM behaviour on unhandled error: "terminate" | "log" | "ignore".</summary>
    [JsonPropertyName("on_error")]
    public required string OnError { get; init; }
}

public sealed record ScheduleBlock
{
    [JsonPropertyName("trigger")]
    public required string Trigger { get; init; }

    [JsonPropertyName("interval_minutes")]
    public int? IntervalMinutes { get; init; }

    [JsonPropertyName("cron")]
    public string? Cron { get; init; }
}

public sealed record ToolBinding
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("server")]
    public required string Server { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
