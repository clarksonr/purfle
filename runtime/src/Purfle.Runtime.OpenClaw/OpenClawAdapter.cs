using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Auth;

namespace Purfle.Runtime.OpenClaw;

/// <summary>
/// Inference adapter for the OpenAI Chat Completions API (https://api.openai.com/v1/chat/completions).
///
/// <para>
/// This adapter targets the standard OpenAI API. Because the project was originally named
/// "OpenClaw" for OpenAI-compatible engines, the namespace retains that name while the
/// adapter itself speaks native OpenAI protocol.
/// </para>
///
/// Requirements:
///   - <c>OPENAI_API_KEY</c> must be set in the host environment. The runtime
///     reads it directly — agents do not need to declare <c>env.read</c> for it.
///   - <c>runtime.model</c> in the manifest selects the OpenAI model.
///     Defaults to <c>gpt-4o</c> when not specified.
/// </summary>
public sealed class OpenClawAdapter : IInferenceAdapter, ILlmAdapter
{
    private const string ApiEndpoint  = "https://api.openai.com/v1/chat/completions";
    private const string EnvApiKey    = "OPENAI_API_KEY";
    private const string DefaultModel = "gpt-4o";
    private const int    DefaultMaxTokens = 4096;

    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly string     _apiKey;
    private readonly int        _maxTokens;

    /// <inheritdoc/>
    public string EngineId => "openai";

    /// <summary>
    /// Creates an OpenAI adapter.
    /// </summary>
    /// <param name="model">
    /// The OpenAI model to use (e.g. "gpt-4o", "gpt-4o-mini"). When null, defaults to <c>gpt-4o</c>.
    /// </param>
    /// <param name="maxTokens">Maximum tokens for responses. Defaults to 4096.</param>
    /// <param name="http">Optional <see cref="HttpClient"/> for testing.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>OPENAI_API_KEY</c> is not set in the environment.
    /// </exception>
    public OpenClawAdapter(string? model = null, int maxTokens = DefaultMaxTokens, HttpClient? http = null)
    {
        var key = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Environment variable '{EnvApiKey}' is not set.");

        _apiKey    = key;
        _model     = model ?? DefaultModel;
        _maxTokens = maxTokens;
        _http      = http ?? new HttpClient();
    }

    /// <summary>Creates an OpenAI adapter from a resolved credential.</summary>
    public OpenClawAdapter(ResolvedCredential credential, int maxTokens = DefaultMaxTokens, HttpClient? http = null)
    {
        var key = credential.Profile.Credential switch
        {
            ApiKeyCredential ak => ak.ApiKey,
            OAuthCredential oa => oa.AccessToken,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("No API key available for OpenAI.");

        _apiKey    = key;
        _model     = credential.Model ?? DefaultModel;
        _maxTokens = maxTokens;
        _http      = http ?? new HttpClient();
    }

    /// <summary>Token usage from the most recent API call.</summary>
    private int _lastInputTokens;
    private int _lastOutputTokens;

    // ── ILlmAdapter ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    async Task<LlmResult> ILlmAdapter.CompleteAsync(string systemPrompt, string userMessage,
                                            CancellationToken ct)
    {
        _lastInputTokens = 0;
        _lastOutputTokens = 0;
        var text = await InvokeAsync(systemPrompt, userMessage, ct);
        return new LlmResult(text, _lastInputTokens, _lastOutputTokens);
    }

    // ── Single-turn interface ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
            new("user", userMessage),
        };

        var response = await PostAsync(messages, ct);
        return ExtractContent(response);
    }

    // ── Multi-turn interface ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(string Reply, List<object> UpdatedHistory)> InvokeMultiTurnAsync(
        string systemPrompt,
        List<object> conversationHistory,
        string userMessage,
        CancellationToken ct = default)
    {
        // Build the full messages array: system + history + new user message.
        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
        };

        // Replay conversation history. Each history entry is either a ChatMessage
        // or a generic object with Role/Content from a prior turn.
        foreach (var entry in conversationHistory)
        {
            if (entry is ChatMessage cm)
            {
                messages.Add(cm);
            }
            else
            {
                // Fallback: serialize and re-parse to extract role/content.
                var json = JsonSerializer.Serialize(entry, s_serializerOptions);
                var parsed = JsonSerializer.Deserialize<ChatMessage>(json, s_serializerOptions);
                if (parsed is not null)
                    messages.Add(parsed);
            }
        }

        messages.Add(new ChatMessage("user", userMessage));

        var response = await PostAsync(messages, ct);
        var reply = ExtractContent(response);

        // Build updated history (excludes system message — caller manages that separately).
        var updatedHistory = new List<object>(conversationHistory)
        {
            new ChatMessage("user", userMessage),
            new ChatMessage("assistant", reply),
        };

        return (reply, updatedHistory);
    }

    // ── HTTP helper ──────────────────────────────────────────────────────────────

    private async Task<ChatCompletionResponse> PostAsync(
        List<ChatMessage> messages, CancellationToken ct)
    {
        var requestBody = new ChatCompletionRequest(
            Model:     _model,
            Messages:  messages,
            MaxTokens: _maxTokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(requestBody, options: s_serializerOptions);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"OpenAI API {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(s_serializerOptions, ct)
               ?? throw new InvalidOperationException("OpenAI API returned an empty response.");

        if (result.Usage is { } usage)
        {
            _lastInputTokens  += usage.PromptTokens;
            _lastOutputTokens += usage.CompletionTokens;
        }

        return result;
    }

    // ── Response extraction ──────────────────────────────────────────────────────

    private static string ExtractContent(ChatCompletionResponse response)
    {
        if (response.Choices is not { Length: > 0 })
            throw new InvalidOperationException("OpenAI API returned no choices.");

        var content = response.Choices[0].Message.Content;
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("OpenAI API returned an empty message content.");

        return content;
    }

    // ── Serializer options ───────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Request / response shapes ────────────────────────────────────────────────

    private sealed record ChatCompletionRequest(
        string Model,
        List<ChatMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    internal sealed record ChatMessage(
        string Role,
        string Content);

    private sealed record ChatCompletionResponse(
        Choice[] Choices,
        Usage? Usage);

    private sealed record Choice(
        ChatMessage Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
