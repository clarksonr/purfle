using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Anthropic;

/// <summary>
/// Inference adapter for the Anthropic Messages API (https://api.anthropic.com/v1/messages).
///
/// Requirements:
///   - The manifest must declare <c>ANTHROPIC_API_KEY</c> in
///     <c>permissions.environment.allow</c>; the sandbox enforces this.
///   - <c>runtime.model</c> in the manifest selects the Anthropic model.
///     Defaults to <c>claude-sonnet-4-6</c> when not specified.
/// </summary>
public sealed class AnthropicAdapter : IInferenceAdapter
{
    private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private const string EnvApiKey = "ANTHROPIC_API_KEY";
    private const string DefaultModel = "claude-sonnet-4-6";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _apiKey;

    public string EngineId => "anthropic";

    /// <summary>
    /// Creates an Anthropic adapter.
    /// </summary>
    /// <param name="manifest">The loaded agent manifest.</param>
    /// <param name="sandbox">The agent's permission sandbox.</param>
    /// <param name="http">
    /// Optional <see cref="HttpClient"/>. If null, a new instance is created.
    /// In production, inject a shared client from <c>IHttpClientFactory</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sandbox does not permit reading <c>ANTHROPIC_API_KEY</c>
    /// or when the environment variable is not set.
    /// </exception>
    public AnthropicAdapter(AgentManifest manifest, AgentSandbox sandbox, HttpClient? http = null)
    {
        if (!sandbox.CanReadEnv(EnvApiKey))
            throw new InvalidOperationException(
                $"Anthropic adapter requires '{EnvApiKey}' in permissions.environment.allow.");

        var key = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Environment variable '{EnvApiKey}' is not set.");

        _apiKey = key;
        _model = manifest.Runtime.Model ?? DefaultModel;
        _http = http ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var requestBody = new MessagesRequest(
            Model: _model,
            MaxTokens: 4096,
            System: systemPrompt,
            Messages: [new Message(Role: "user", Content: userMessage)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = JsonContent.Create(requestBody, options: s_serializerOptions);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Anthropic API {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<MessagesResponse>(
            s_serializerOptions, ct)
            ?? throw new InvalidOperationException("Anthropic API returned an empty response.");

        if (result.Content is not { Length: > 0 })
            throw new InvalidOperationException("Anthropic API returned no content blocks.");

        // Return the text of the first content block.
        return result.Content[0].Text;
    }

    // ─── Request / response shapes ────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] Message[] Messages);

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record MessagesResponse(
        [property: JsonPropertyName("content")] ContentBlock[] Content);

    private sealed record ContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);
}
