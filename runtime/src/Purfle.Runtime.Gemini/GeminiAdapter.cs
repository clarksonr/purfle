using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tools;
using Purfle.Sdk;

namespace Purfle.Runtime.Gemini;

/// <summary>
/// Inference adapter for the Google Gemini generateContent API.
///
/// <para>
/// Tool support mirrors the Anthropic adapter: read_file, write_file, http_get
/// are offered based on manifest permissions; MCP and agent custom tools are
/// also supported.
/// </para>
///
/// Requirements:
///   - <c>GEMINI_API_KEY</c> must be set in the host environment. The runtime
///     reads it directly — agents do not need to declare <c>env.read</c> for it.
///   - <c>runtime.model</c> in the manifest selects the Gemini model.
///     Defaults to <c>gemini-2.5-flash</c> when not specified.
/// </summary>
public sealed class GeminiAdapter : IInferenceAdapter
{
    private const string ApiEndpointBase = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string EnvApiKey       = "GEMINI_API_KEY";
    private const string DefaultModel    = "gemini-2.5-flash";
    private const int    MaxIterations   = 10;

    private readonly HttpClient          _http;
    private readonly string              _model;
    private readonly string              _apiKey;
    private readonly AgentSandbox        _sandbox;
    private readonly BuiltInToolExecutor _executor;
    private readonly IReadOnlyList<FunctionDeclaration>       _tools;
    private readonly IReadOnlyDictionary<string, IMcpClient>  _mcpToolRoutes;
    private readonly IReadOnlyDictionary<string, IAgentTool>  _agentToolRoutes;

    /// <inheritdoc/>
    public string EngineId => "gemini";

    private static readonly HashSet<string> BuiltInToolNames =
        new(["read_file", "write_file", "http_get", "search_files", "find_files"], StringComparer.Ordinal);

    public GeminiAdapter(
        AgentManifest manifest,
        AgentSandbox sandbox,
        HttpClient? http = null,
        IReadOnlyList<IMcpClient>? mcpClients = null,
        IAgent? agent = null)
    {
        var key = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Environment variable '{EnvApiKey}' is not set.");

        _apiKey   = key;
        _model    = manifest.Runtime.Model ?? DefaultModel;
        _http     = http ?? new HttpClient();
        _sandbox  = sandbox;
        _executor = new BuiltInToolExecutor(sandbox, _http);

        // ── MCP tools ────────────────────────────────────────────────────────
        var mcpRoutes   = new Dictionary<string, IMcpClient>();
        var mcpToolDefs = new List<FunctionDeclaration>();

        if (mcpClients is not null)
        {
            foreach (var client in mcpClients)
            {
                try
                {
                    var tools = client.ListToolsAsync().GetAwaiter().GetResult();
                    foreach (var tool in tools)
                    {
                        if (!sandbox.CanUseMcpTool(tool.Name)) continue;
                        mcpRoutes[tool.Name] = client;
                        mcpToolDefs.Add(BuildMcpFunctionDeclaration(tool));
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[GeminiAdapter] MCP server unreachable at load: {ex.Message}. " +
                        "Skipping tool registration for this server.");
                }
            }
        }

        _mcpToolRoutes = mcpRoutes;

        // ── Agent custom tools ───────────────────────────────────────────────
        var agentRoutes   = new Dictionary<string, IAgentTool>(StringComparer.Ordinal);
        var agentToolDefs = new List<FunctionDeclaration>();

        if (agent?.Tools is { Count: > 0 } customTools)
        {
            foreach (var tool in customTools)
            {
                if (BuiltInToolNames.Contains(tool.Name))
                    throw new InvalidOperationException(
                        $"Agent tool '{tool.Name}' collides with a built-in tool name. " +
                        "Choose a different name.");

                if (mcpRoutes.ContainsKey(tool.Name))
                    throw new InvalidOperationException(
                        $"Agent tool '{tool.Name}' collides with an MCP tool of the same name.");

                agentRoutes[tool.Name] = tool;
                agentToolDefs.Add(BuildAgentFunctionDeclaration(tool));
            }
        }

        _agentToolRoutes = agentRoutes;

        var builtinTools = BuiltInToolDefinitions.For(sandbox.GetPermissions())
            .Select(s => new FunctionDeclaration(
                s.Name,
                s.Description,
                new FunctionParameters(
                    "object",
                    s.Parameters.ToDictionary(p => p.Name, p => new ParameterProperty(p.Type, p.Description)),
                    [.. s.Required])))
            .ToList();
        _tools = [.. builtinTools, .. mcpToolDefs, .. agentToolDefs];
    }

    // ── Single-turn interface ────────────────────────────────────────────────

    public async Task<string> InvokeAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var contents = new List<Content>
        {
            new("user", [new Part { Text = userMessage }]),
        };
        return await RunAsync(systemPrompt, contents, ct);
    }

    // ── Multi-turn interface ─────────────────────────────────────────────────

    public async Task<(string Reply, List<object> UpdatedHistory)> InvokeMultiTurnAsync(
        string systemPrompt,
        List<object> conversationHistory,
        string userMessage,
        CancellationToken ct = default)
    {
        var contents = conversationHistory.OfType<Content>().ToList();
        contents.Add(new Content("user", [new Part { Text = userMessage }]));

        var reply = await RunAsync(systemPrompt, contents, ct);

        contents.Add(new Content("model", [new Part { Text = reply }]));
        return (reply, contents.Cast<object>().ToList());
    }

    // ── Core loop ────────────────────────────────────────────────────────────

    private async Task<string> RunAsync(
        string systemPrompt, List<Content> contents, CancellationToken ct)
    {
        for (int i = 0; i < MaxIterations; i++)
        {
            var request = new GenerateContentRequest(
                SystemInstruction: new SystemInstruction([new Part { Text = systemPrompt }]),
                Contents: contents,
                Tools: _tools.Count > 0 ? [new ToolSet([.. _tools])] : null);

            var response  = await PostAsync(request, ct);
            var candidate = response.Candidates.FirstOrDefault()
                ?? throw new InvalidOperationException("Gemini API returned no candidates.");

            var content             = candidate.Content;
            var functionCallParts   = content.Parts.Where(p => p.FunctionCall is not null).ToList();

            if (functionCallParts.Count > 0)
            {
                contents.Add(content);

                var resultParts = new List<Part>();
                foreach (var part in functionCallParts)
                {
                    var fc     = part.FunctionCall!;
                    var result = await ExecuteToolAsync(fc.Name, fc.Args, ct);
                    resultParts.Add(new Part
                    {
                        FunctionResponse = new FunctionResponse(fc.Name,
                            new FunctionResponseContent(result)),
                    });
                }

                contents.Add(new Content("user", resultParts));
                continue;
            }

            return content.Parts.FirstOrDefault(p => p.Text is not null)?.Text
                ?? throw new InvalidOperationException("Gemini API returned no text content.");
        }

        throw new InvalidOperationException(
            $"Tool-call loop exceeded {MaxIterations} iterations without completing.");
    }

    // ── Tool dispatch ─────────────────────────────────────────────────────────

    private async Task<string> ExecuteToolAsync(
        string toolName, JsonElement? args, CancellationToken ct)
    {
        // Built-in tools are handled by the shared executor.
        if (BuiltInToolNames.Contains(toolName))
            return await _executor.ExecuteAsync(toolName, args, ct);

        if (_agentToolRoutes.TryGetValue(toolName, out var agentTool))
        {
            try   { return await agentTool.ExecuteAsync(args?.GetRawText() ?? "{}", ct); }
            catch (Exception ex) { return $"Error: agent tool '{toolName}' failed — {ex.Message}"; }
        }

        if (_mcpToolRoutes.TryGetValue(toolName, out var mcpClient))
        {
            if (!_sandbox.CanUseMcpTool(toolName))
                return $"Error: permission denied — MCP tool '{toolName}' is not in the agent's tools.mcp allowlist.";
            try   { return await mcpClient.CallToolAsync(toolName, args?.GetRawText() ?? "{}", ct); }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[GeminiAdapter] MCP tool '{toolName}' call failed (server may have dropped): {ex.Message}");
                return $"Error: MCP tool '{toolName}' failed — {ex.Message}. The MCP server may be unreachable.";
            }
        }

        return $"Error: unknown tool '{toolName}'.";
    }

    // ── MCP / agent tool schema translation ──────────────────────────────────

    private static FunctionDeclaration BuildMcpFunctionDeclaration(McpToolInfo tool)
    {
        var doc = JsonDocument.Parse(tool.InputSchemaJson);
        return ParseSchema(tool.Name, tool.Description, doc.RootElement);
    }

    private static FunctionDeclaration BuildAgentFunctionDeclaration(IAgentTool tool)
    {
        var doc = JsonDocument.Parse(tool.InputSchemaJson);
        return ParseSchema(tool.Name, tool.Description, doc.RootElement);
    }

    private static FunctionDeclaration ParseSchema(string name, string description, JsonElement root)
    {
        var properties = new Dictionary<string, ParameterProperty>();
        if (root.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                var type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                properties[prop.Name] = new ParameterProperty(type, desc);
            }
        }

        var required = Array.Empty<string>();
        if (root.TryGetProperty("required", out var req))
            required = req.EnumerateArray().Select(e => e.GetString()!).ToArray();

        return new FunctionDeclaration(name, description,
            new FunctionParameters("object", properties, required));
    }

    // ── HTTP helper ───────────────────────────────────────────────────────────

    private const int MaxRetries = 5;

    private async Task<GenerateContentResponse> PostAsync(
        GenerateContentRequest requestBody, CancellationToken ct)
    {
        var url = $"{ApiEndpointBase}/{_model}:generateContent?key={_apiKey}";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content   = JsonContent.Create(requestBody, options: s_options);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                Console.Error.WriteLine(
                    $"[GeminiAdapter] Request timeout (attempt {attempt}/{MaxRetries})");
                if (attempt == MaxRetries)
                    throw new InvalidOperationException(
                        $"Gemini API request timed out after {MaxRetries} attempts.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 60)), ct);
                continue;
            }

            if ((int)response.StatusCode == 429)
            {
                Console.Error.WriteLine(
                    $"[GeminiAdapter] Rate limited 429 (attempt {attempt}/{MaxRetries})");
                response.Dispose();
                if (attempt == MaxRetries)
                    throw new InvalidOperationException(
                        $"Gemini API rate limited after {MaxRetries} attempts.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 60)), ct);
                continue;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException($"Gemini API {(int)response.StatusCode}: {body}");
                }

                return await response.Content.ReadFromJsonAsync<GenerateContentResponse>(s_options, ct)
                    ?? throw new InvalidOperationException("Gemini API returned an empty response.");
            }
        }

        throw new InvalidOperationException("Unreachable: retry loop exited without result.");
    }

    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Request / response shapes ─────────────────────────────────────────────

    private sealed record GenerateContentRequest(
        SystemInstruction SystemInstruction,
        List<Content>     Contents,
        List<ToolSet>?    Tools);

    private sealed record SystemInstruction(List<Part> Parts);

    private sealed record Content(string Role, List<Part> Parts);

    private sealed class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("functionCall")]
        public FunctionCallPart? FunctionCall { get; init; }

        [JsonPropertyName("functionResponse")]
        public FunctionResponse? FunctionResponse { get; init; }
    }

    private sealed record FunctionCallPart(string Name, JsonElement? Args);

    private sealed record FunctionResponse(string Name, FunctionResponseContent Response);

    private sealed record FunctionResponseContent(string Result);

    private sealed record GenerateContentResponse(List<Candidate> Candidates);

    private sealed record Candidate(Content Content, string? FinishReason);

    private sealed record ToolSet(
        [property: JsonPropertyName("functionDeclarations")]
        List<FunctionDeclaration> FunctionDeclarations);

    private sealed record FunctionDeclaration(
        string Name, string Description, FunctionParameters Parameters);

    private sealed record FunctionParameters(
        string Type,
        Dictionary<string, ParameterProperty> Properties,
        string[] Required);

    private sealed record ParameterProperty(string Type, string Description);
}
