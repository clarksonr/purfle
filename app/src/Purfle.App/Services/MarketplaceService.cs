using System.Net.Http.Json;
using Purfle.Marketplace.Shared;

namespace Purfle.App.Services;

/// <summary>
/// HTTP client for the Purfle Marketplace API.
/// </summary>
public sealed class MarketplaceService
{
    private readonly HttpClient _http;

    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string? AccessToken { get; set; }

    public MarketplaceService()
    {
        _http = new HttpClient();
    }

    private void SetAuth()
    {
        if (!string.IsNullOrEmpty(AccessToken))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<AgentSearchResponse> SearchAsync(string? query, int page = 1)
    {
        SetAuth();
        var q = string.IsNullOrWhiteSpace(query) ? "" : $"q={Uri.EscapeDataString(query)}&";
        var url = $"{BaseUrl}/api/agents?{q}page={page}&pageSize=20";
        return await _http.GetFromJsonAsync<AgentSearchResponse>(url)
            ?? new AgentSearchResponse([], page, 20, 0);
    }

    public async Task<AgentDetailResponse?> GetAgentAsync(string agentId)
    {
        SetAuth();
        var resp = await _http.GetAsync($"{BaseUrl}/api/agents/{Uri.EscapeDataString(agentId)}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AgentDetailResponse>();
    }

    public async Task<string?> DownloadManifestAsync(string agentId, string? version = null)
    {
        SetAuth();
        var path = version is not null
            ? $"api/agents/{Uri.EscapeDataString(agentId)}/versions/{Uri.EscapeDataString(version)}"
            : $"api/agents/{Uri.EscapeDataString(agentId)}/latest";

        var resp = await _http.GetAsync($"{BaseUrl}/{path}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync();
    }
}
