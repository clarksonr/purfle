using System.Text.Json.Serialization;

namespace Purfle.Runtime.Manifest;

public sealed record AgentIdentity
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

    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}
