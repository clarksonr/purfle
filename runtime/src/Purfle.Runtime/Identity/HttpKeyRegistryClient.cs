using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Purfle.Runtime.Identity;

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

    private static string EncodeKeyId(string keyId) => keyId.Replace("/", "__");

    public async Task<PublicKey?> GetKeyAsync(string keyId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/keys/{EncodeKeyId(keyId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Gone) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JwkResponse>(ct);
        if (dto is null) return null;
        return new PublicKey
        {
            KeyId    = keyId,
            Algorithm = "ES256",
            X = Base64UrlDecode(dto.X),
            Y = Base64UrlDecode(dto.Y),
        };
    }

    public async Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/keys/{EncodeKeyId(keyId)}", ct);
        return response.StatusCode == System.Net.HttpStatusCode.Gone;
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "=";  break;
        }
        return Convert.FromBase64String(s);
    }

    private sealed record JwkResponse
    {
        [JsonPropertyName("kty")] public required string Kty { get; init; }
        [JsonPropertyName("crv")] public required string Crv { get; init; }
        [JsonPropertyName("x")]   public required string X   { get; init; }
        [JsonPropertyName("y")]   public required string Y   { get; init; }
    }
}