using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Auth;

namespace Purfle.Runtime.Ollama;

/// <summary>
/// Inference adapter for Ollama (local LLM inference at http://localhost:11434).
///
/// <para>
/// Ollama runs locally and requires no API key. The adapter calls the
/// <c>/api/chat</c> endpoint with <c>stream: false</c> to get a single
/// complete response.
/// </para>
///
/// <para>
/// The model defaults to <c>llama3</c> but can be overridden via
/// <c>runtime.model</c> in the agent manifest.
/// </para>
/// </summary>
public sealed class OllamaAdapter : IInferenceAdapter, ILlmAdapter
{
    private const string DefaultBaseUrl = "http://localhost:11434";
    private const string ChatEndpoint   = "/api/chat";
    private const string DefaultModel   = "llama3";

    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly string     _baseUrl;

    /// <inheritdoc/>
    public string EngineId => "ollama";

    /// <summary>
    /// Creates an Ollama adapter.
    /// </summary>
    /// <param name="model">
    /// Model name to use (e.g. "llama3", "mistral", "codellama").
    /// Defaults to "llama3" when null or empty.
    /// </param>
    /// <param name="baseUrl">
    /// Ollama server base URL. Defaults to "http://localhost:11434".
    /// </param>
    /// <param name="http">Optional <see cref="HttpClient"/> for testing.</param>
    public OllamaAdapter(string? model = null, string? baseUrl = null, HttpClient? http = null)
    {
        _model   = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _http    = http ?? new HttpClient();
    }

    /// <summary>Creates an Ollama adapter from a resolved credential.</summary>
    public OllamaAdapter(ResolvedCredential credential, HttpClient? http = null)
    {
        _model   = string.IsNullOrWhiteSpace(credential.Model) ? DefaultModel : credential.Model;
        _baseUrl = (credential.Profile.Credential is LocalServiceCredential ls ? ls.BaseUrl : DefaultBaseUrl).TrimEnd('/');
        _http    = http ?? new HttpClient();
    }

    /// <summary>Token usage from the most recent call.</summary>
    private int _lastInputTokens;
    private int _lastOutputTokens;

    // ── ILlmAdapter ──────────────────────────────────────────────────────────────

    async Task<LlmResult> ILlmAdapter.CompleteAsync(string systemPrompt, string userMessage,
                                            CancellationToken ct)
    {
        _lastInputTokens = 0;
        _lastOutputTokens = 0;
        var text = await InvokeAsync(systemPrompt, userMessage, ct);
        return new LlmResult(text, _lastInputTokens, _lastOutputTokens);
    }

    // ── Single-turn ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = new List<OllamaMessage>
        {
            new("system", systemPrompt),
            new("user", userMessage),
        };

        var reply = await PostChatAsync(messages, ct);
        return reply;
    }

    // ── Multi-turn ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(string Reply, List<object> UpdatedHistory)> InvokeMultiTurnAsync(
        string systemPrompt,
        List<object> conversationHistory,
        string userMessage,
        CancellationToken ct = default)
    {
        // Build the messages list: system prompt + prior history + new user message.
        var messages = new List<OllamaMessage> { new("system", systemPrompt) };

        foreach (var entry in conversationHistory)
        {
            if (entry is OllamaMessage msg)
            {
                messages.Add(msg);
            }
            else if (entry is JsonElement je)
            {
                // Deserialize from JSON (e.g. when history was round-tripped through serialization).
                var deserialized = je.Deserialize<OllamaMessage>(s_serializerOptions);
                if (deserialized is not null)
                    messages.Add(deserialized);
            }
        }

        messages.Add(new OllamaMessage("user", userMessage));

        var replyText = await PostChatAsync(messages, ct);

        // Build updated history (excludes the system prompt — caller provides it each time).
        var updatedHistory = new List<object>();
        foreach (var entry in conversationHistory)
            updatedHistory.Add(entry);

        updatedHistory.Add(new OllamaMessage("user", userMessage));
        updatedHistory.Add(new OllamaMessage("assistant", replyText));

        return (replyText, updatedHistory);
    }

    // ── HTTP helper ──────────────────────────────────────────────────────────

    private async Task<string> PostChatAsync(
        List<OllamaMessage> messages,
        CancellationToken ct)
    {
        var url = _baseUrl + ChatEndpoint;

        var requestBody = new OllamaChatRequest(
            Model:    _model,
            Messages: messages,
            Stream:   false);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(url, requestBody, s_serializerOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmAdapterException(
                $"Failed to connect to Ollama at '{_baseUrl}'. Is Ollama running? ({ex.Message})", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new LlmAdapterException(
                $"Request to Ollama at '{_baseUrl}' timed out.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new LlmAdapterException(
                $"Ollama API returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        OllamaChatResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(s_serializerOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new LlmAdapterException(
                "Failed to deserialize Ollama response.", ex);
        }

        if (result is null)
            throw new LlmAdapterException("Ollama API returned an empty response.");

        var content = result.Message?.Content;
        if (string.IsNullOrEmpty(content))
            throw new LlmAdapterException("Ollama API returned a response with no content.");

        _lastInputTokens  += result.PromptEvalCount ?? 0;
        _lastOutputTokens += result.EvalCount ?? 0;

        return content;
    }

    // ── Serializer options ───────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Request / response shapes ────────────────────────────────────────────

    /// <summary>A single message in the Ollama chat format.</summary>
    public sealed record OllamaMessage(
        [property: JsonPropertyName("role")]    string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")]    string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")]   bool Stream);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")]           OllamaMessageResponse? Message,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")]        int? EvalCount);

    private sealed record OllamaMessageResponse(
        [property: JsonPropertyName("role")]    string? Role,
        [property: JsonPropertyName("content")] string? Content);
}
