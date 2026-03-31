using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Adapters;

/// <summary>
/// Lightweight <see cref="ILlmAdapter"/> that calls the Anthropic Messages API
/// for single-turn completions. Intended for use with the <c>Purfle.Runtime.Scheduling</c>
/// infrastructure. Does not perform tool-call loops or sandbox enforcement.
///
/// <para>
/// Reads <c>ANTHROPIC_API_KEY</c> from the environment. Model and max_tokens are
/// taken from <see cref="AgentManifest.Runtime"/>; both fall back to safe defaults
/// when omitted.
/// </para>
/// </summary>
public sealed class AnthropicAdapter : ILlmAdapter
{
    private const string ApiEndpoint    = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion     = "2023-06-01";
    private const string EnvApiKey      = "ANTHROPIC_API_KEY";
    private const string DefaultModel   = "claude-sonnet-4-6";
    private const int    DefaultMaxTokens = 1024;

    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly int        _maxTokens;
    // Key is read lazily at call time so the adapter can be constructed before the
    // environment variable is available (e.g. before the app injects it from SecureStorage).

    /// <summary>Creates an adapter using default model and max_tokens.</summary>
    public AnthropicAdapter(HttpClient? http = null)
    {
        _model     = DefaultModel;
        _maxTokens = DefaultMaxTokens;
        _http      = http ?? new HttpClient();
    }

    /// <summary>Creates an adapter whose model/max_tokens come from the manifest.</summary>
    public AnthropicAdapter(AgentManifest manifest, HttpClient? http = null)
    {
        _model     = manifest.Runtime.Model     ?? DefaultModel;
        _maxTokens = manifest.Runtime.MaxTokens ?? DefaultMaxTokens;
        _http      = http ?? new HttpClient();
    }

    /// <inheritdoc/>
    /// <exception cref="LlmAdapterException">
    /// Thrown when <c>ANTHROPIC_API_KEY</c> is not set or when the API returns an error.
    /// </exception>
    public async Task<string> CompleteAsync(
        string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new LlmAdapterException($"Environment variable '{EnvApiKey}' is not set.");

        var body = new MessagesRequest(
            Model:     _model,
            MaxTokens: _maxTokens,
            System:    systemPrompt,
            Messages:  [new TextMessage("user", userMessage)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = JsonContent.Create(body, options: s_jsonOptions);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new LlmAdapterException(
                $"Anthropic API returned {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content
                         .ReadFromJsonAsync<MessagesResponse>(s_jsonOptions, ct)
                     ?? throw new LlmAdapterException(
                         "Anthropic API returned an empty response.");

        return result.Content.First(b => b.Type == "text").Text
               ?? throw new LlmAdapterException(
                   "Anthropic API response contained no text block.");
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record MessagesRequest(
        string Model, int MaxTokens, string System, IEnumerable<object> Messages);

    private sealed record TextMessage(string Role, string Content);

    private sealed record MessagesResponse(ContentBlock[] Content, string StopReason);

    private sealed record ContentBlock(string Type, string? Text);
}
