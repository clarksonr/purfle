using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Purfle.Runtime.Identity;

/// <summary>
/// Implements <see cref="IKeyRegistry"/> by calling the Purfle Marketplace API.
/// Target endpoint: GET /api/keys/{keyId}
/// </summary>
public sealed class HttpKeyRegistryClient : IKeyRegistry
{
    private readonly HttpClient _httpClient;

    public HttpKeyRegistryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public HttpKeyRegistryClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public async Task<PublicKey?> GetKeyAsync(string keyId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/keys/{Uri.EscapeDataString(keyId)}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<KeyResponse>(ct);
        if (dto is null)
            return null;

        return new PublicKey
        {
            KeyId = dto.KeyId,
            Algorithm = dto.Algorithm,
            X = Convert.FromBase64String(dto.X),
            Y = Convert.FromBase64String(dto.Y),
        };
    }

    public async Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/keys/{Uri.EscapeDataString(keyId)}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<KeyResponse>(ct);
        return dto?.IsRevoked ?? false;
    }

    private sealed record KeyResponse
    {
        [JsonPropertyName("keyId")]
        public required string KeyId { get; init; }

        [JsonPropertyName("algorithm")]
        public required string Algorithm { get; init; }

        [JsonPropertyName("x")]
        public required string X { get; init; }

        [JsonPropertyName("y")]
        public required string Y { get; init; }

        [JsonPropertyName("isRevoked")]
        public bool IsRevoked { get; init; }
    }
}
