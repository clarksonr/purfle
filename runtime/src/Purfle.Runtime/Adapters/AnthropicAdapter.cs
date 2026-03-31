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
    private readonly string     _apiKey;
    private readonly string     _model;
    private readonly int        _maxTokens;

    /// <param name="manifest">The agent manifest supplying runtime parameters.</param>
    /// <param name="http">Optional <see cref="HttpClient"/> override (e.g. for tests).</param>
    /// <exception cref="LlmAdapterException">
    /// Thrown when <c>ANTHROPIC_API_KEY</c> is not set in the environment.
    /// </exception>
    public AnthropicAdapter(AgentManifest manifest, HttpClient? http = null)
    {
        var key = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new LlmAdapterException(
                $"Environment variable '{EnvApiKey}' is not set.");

        _apiKey    = key;
        _model     = manifest.Runtime.Model     ?? DefaultModel;
        _maxTokens = manifest.Runtime.MaxTokens ?? DefaultMaxTokens;
        _http      = http ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(
        string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var body = new MessagesRequest(
            Model:     _model,
            MaxTokens: _maxTokens,
            System:    systemPrompt,
            Messages:  [new TextMessage("user", userMessage)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", _apiKey);
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
