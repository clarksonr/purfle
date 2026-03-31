using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tools;
using Purfle.Sdk;

namespace Purfle.Runtime.Anthropic;

/// <summary>
/// Inference adapter for the Anthropic Messages API (https://api.anthropic.com/v1/messages).
///
/// <para>
/// On construction the adapter inspects the agent's <see cref="AgentSandbox"/> and builds a
/// list of tools it will advertise to the model:
/// <list type="bullet">
///   <item><term>read_file</term><description>offered when <c>permissions.filesystem.read</c> is non-empty</description></item>
///   <item><term>write_file</term><description>offered when <c>permissions.filesystem.write</c> is non-empty</description></item>
///   <item><term>http_get</term><description>offered when <c>permissions.network.allow</c> is non-empty</description></item>
///   <item><term>MCP tools</term><description>offered when connected MCP servers expose tools that pass <c>sandbox.CanUseMcpTool()</c></description></item>
///   <item><term>Agent tools</term><description>custom tools contributed by <see cref="IAgent.Tools"/></description></item>
/// </list>
/// </para>
///
/// <para>
/// When tools are present, <see cref="InvokeAsync(string, string, CancellationToken)"/> runs a
/// tool-call loop: it sends the user message to the API with the tool definitions, executes any
/// <c>tool_use</c> blocks the model emits (checking the sandbox before every execution), feeds
/// the results back, and repeats until the model returns <c>stop_reason: end_turn</c>. If no
/// tools are configured (e.g. a minimal chat agent) the loop is skipped and a single-turn
/// request is made instead.
/// </para>
///
/// Requirements:
///   - The manifest must declare <c>ANTHROPIC_API_KEY</c> in
///     <c>permissions.environment.allow</c>; the sandbox enforces this.
///   - <c>runtime.model</c> in the manifest selects the Anthropic model.
///     Defaults to <c>claude-sonnet-4-6</c> when not specified.
/// </summary>
public sealed class AnthropicAdapter : IInferenceAdapter
{
    private const string ApiEndpoint  = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion   = "2023-06-01";
    private const string EnvApiKey    = "ANTHROPIC_API_KEY";
    private const string DefaultModel = "claude-sonnet-4-6";

    /// <summary>Maximum tool-call iterations before the loop is aborted.</summary>
    private const int MaxIterations = 10;

    private readonly HttpClient          _http;
    private readonly string              _model;
    private readonly string              _apiKey;
    private readonly AgentSandbox        _sandbox;
    private readonly BuiltInToolExecutor _executor;

    /// <summary>Tools built from manifest permissions + MCP servers + agent custom tools.</summary>
    private readonly IReadOnlyList<ToolDefinition> _tools;

    /// <summary>MCP tool name → the client that owns it.</summary>
    private readonly IReadOnlyDictionary<string, IMcpClient> _mcpToolRoutes;

    /// <summary>Agent custom tool name → the IAgentTool implementation.</summary>
    private readonly IReadOnlyDictionary<string, IAgentTool> _agentToolRoutes;

    /// <inheritdoc/>
    public string EngineId => "anthropic";

    private static readonly HashSet<string> BuiltInToolNames =
        new(["read_file", "write_file", "http_get", "search_files", "find_files"], StringComparer.Ordinal);

    /// <summary>
    /// Creates an Anthropic adapter.
    /// </summary>
    /// <param name="manifest">The loaded agent manifest.</param>
    /// <param name="sandbox">The agent's permission sandbox.</param>
    /// <param name="http">Optional <see cref="HttpClient"/>.</param>
    /// <param name="mcpClients">Optional connected MCP clients.</param>
    /// <param name="agent">
    /// Optional agent entry point. When provided, custom tools from
    /// <see cref="IAgent.Tools"/> are registered alongside built-in tools.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sandbox does not permit reading <c>ANTHROPIC_API_KEY</c>,
    /// when the environment variable is not set, or when an agent tool name
    /// collides with a built-in tool name.
    /// </exception>
    public AnthropicAdapter(
        AgentManifest manifest,
        AgentSandbox sandbox,
        HttpClient? http = null,
        IReadOnlyList<IMcpClient>? mcpClients = null,
        IAgent? agent = null)
    {
        if (!sandbox.CanReadEnv(EnvApiKey))
            throw new InvalidOperationException(
                $"Anthropic adapter requires '{EnvApiKey}' in permissions.environment.allow.");

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
        var mcpToolDefs = new List<ToolDefinition>();

        if (mcpClients is not null)
        {
            foreach (var client in mcpClients)
            {
                var tools = client.ListToolsAsync().GetAwaiter().GetResult();
                foreach (var tool in tools)
                {
                    if (!sandbox.CanUseMcpTool(tool.Name)) continue;
                    mcpRoutes[tool.Name] = client;
                    mcpToolDefs.Add(BuildMcpToolDefinition(tool));
                }
            }
        }

        _mcpToolRoutes = mcpRoutes;

        // ── Agent custom tools ───────────────────────────────────────────────
        var agentRoutes   = new Dictionary<string, IAgentTool>(StringComparer.Ordinal);
        var agentToolDefs = new List<ToolDefinition>();

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
                agentToolDefs.Add(BuildAgentToolDefinition(tool));
            }
        }

        _agentToolRoutes = agentRoutes;

        var builtinTools = BuiltInToolDefinitions.For(sandbox.GetPermissions())
            .Select(s => new ToolDefinition(
                Name:        s.Name,
                Description: s.Description,
                InputSchema: new InputSchema(
                    Type:       "object",
                    Properties: s.Parameters.ToDictionary(p => p.Name, p => new ToolProperty(p.Type, p.Description)),
                    Required:   [.. s.Required])))
            .ToList();
        _tools = [.. builtinTools, .. mcpToolDefs, .. agentToolDefs];
    }

    // ── Single-turn interface ────────────────────────────────────────────────────

    public async Task<string> InvokeAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        if (_tools.Count == 0)
            return await InvokeSingleTurnAsync(systemPrompt, userMessage, ct);

        return await InvokeWithToolsAsync(systemPrompt, userMessage, ct);
    }

    // ── Multi-turn interface ─────────────────────────────────────────────────────

    public async Task<(string Reply, List<object> UpdatedHistory)> InvokeMultiTurnAsync(
        string systemPrompt,
        List<object> conversationHistory,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = new List<object>(conversationHistory)
        {
            new TextMessage("user", userMessage),
        };

        if (_tools.Count == 0)
        {
            var request = new MessagesRequest(
                Model:     _model,
                MaxTokens: 4096,
                System:    systemPrompt,
                Messages:  messages,
                Tools:     null);

            var response = await PostAsync(request, ct);
            var text = response.Content.First(b => b.Type == "text").Text
                       ?? throw new InvalidOperationException("Anthropic API returned no text content.");

            messages.Add(new TextMessage("assistant", text));
            return (text, messages);
        }

        for (int i = 0; i < MaxIterations; i++)
        {
            var request = new MessagesRequest(
                Model:     _model,
                MaxTokens: 4096,
                System:    systemPrompt,
                Messages:  messages,
                Tools:     _tools);

            var response = await PostAsync(request, ct);

            if (response.StopReason == "end_turn")
            {
                var textBlock = response.Content.FirstOrDefault(b => b.Type == "text");
                var text = textBlock?.Text
                           ?? throw new InvalidOperationException("Anthropic API returned end_turn with no text block.");

                messages.Add(new ContentArrayMessage("assistant", response.Content));
                return (text, messages);
            }

            if (response.StopReason == "tool_use")
            {
                messages.Add(new ContentArrayMessage("assistant", response.Content));

                var results = new List<ToolResultContent>();
                foreach (var block in response.Content.Where(b => b.Type == "tool_use"))
                {
                    var result = await ExecuteToolAsync(block.Name!, block.Input, ct);
                    results.Add(new ToolResultContent("tool_result", block.Id!, result));
                }

                messages.Add(new ToolResultMessage("user", results.ToArray()));
                continue;
            }

            throw new InvalidOperationException(
                $"Anthropic API returned unexpected stop_reason: '{response.StopReason}'.");
        }

        throw new InvalidOperationException(
            $"Tool-call loop exceeded {MaxIterations} iterations without reaching end_turn.");
    }

    // ── Single-turn path ──────────────────────────────────────────────────────

    private async Task<string> InvokeSingleTurnAsync(
        string systemPrompt, string userMessage, CancellationToken ct)
    {
        var request = new MessagesRequest(
            Model:     _model,
            MaxTokens: 4096,
            System:    systemPrompt,
            Messages:  [new TextMessage("user", userMessage)],
            Tools:     null);

        var response = await PostAsync(request, ct);
        return response.Content.First(b => b.Type == "text").Text
               ?? throw new InvalidOperationException("Anthropic API returned no text content.");
    }

    // ── Tool-call loop ────────────────────────────────────────────────────────

    private async Task<string> InvokeWithToolsAsync(
        string systemPrompt, string userMessage, CancellationToken ct)
    {
        var messages = new List<object>
        {
            new TextMessage("user", userMessage),
        };

        for (int i = 0; i < MaxIterations; i++)
        {
            var request = new MessagesRequest(
                Model:     _model,
                MaxTokens: 4096,
                System:    systemPrompt,
                Messages:  messages,
                Tools:     _tools);

            var response = await PostAsync(request, ct);

            if (response.StopReason == "end_turn")
            {
                var textBlock = response.Content.FirstOrDefault(b => b.Type == "text");
                return textBlock?.Text
                       ?? throw new InvalidOperationException("Anthropic API returned end_turn with no text block.");
            }

            if (response.StopReason == "tool_use")
            {
                messages.Add(new ContentArrayMessage("assistant", response.Content));

                var results = new List<ToolResultContent>();
                foreach (var block in response.Content.Where(b => b.Type == "tool_use"))
                {
                    var result = await ExecuteToolAsync(block.Name!, block.Input, ct);
                    results.Add(new ToolResultContent("tool_result", block.Id!, result));
                }

                messages.Add(new ToolResultMessage("user", results.ToArray()));
                continue;
            }

            throw new InvalidOperationException(
                $"Anthropic API returned unexpected stop_reason: '{response.StopReason}'.");
        }

        throw new InvalidOperationException(
            $"Tool-call loop exceeded {MaxIterations} iterations without reaching end_turn.");
    }

    // ── Tool dispatch ─────────────────────────────────────────────────────────

    private async Task<string> ExecuteToolAsync(
        string toolName, JsonElement? input, CancellationToken ct)
    {
        // Built-in tools are handled by the shared executor.
        if (BuiltInToolNames.Contains(toolName))
            return await _executor.ExecuteAsync(toolName, input, ct);

        // Agent custom tools.
        if (_agentToolRoutes.TryGetValue(toolName, out var agentTool))
        {
            try   { return await agentTool.ExecuteAsync(input?.GetRawText() ?? "{}", ct); }
            catch (Exception ex) { return $"Error: agent tool '{toolName}' failed — {ex.Message}"; }
        }

        // MCP tools.
        if (_mcpToolRoutes.TryGetValue(toolName, out var mcpClient))
        {
            if (!_sandbox.CanUseMcpTool(toolName))
                return $"Error: permission denied — MCP tool '{toolName}' is not in the agent's tools.mcp allowlist.";
            try   { return await mcpClient.CallToolAsync(toolName, input?.GetRawText() ?? "{}", ct); }
            catch (Exception ex) { return $"Error: MCP tool '{toolName}' failed — {ex.Message}"; }
        }

        return $"Error: unknown tool '{toolName}'.";
    }

    // ── MCP / agent tool schema translation ──────────────────────────────────

    private static ToolDefinition BuildMcpToolDefinition(McpToolInfo tool)
    {
        var schemaDoc = JsonDocument.Parse(tool.InputSchemaJson);
        return ParseSchemaToToolDefinition(tool.Name, tool.Description, schemaDoc.RootElement);
    }

    private static ToolDefinition BuildAgentToolDefinition(IAgentTool tool)
    {
        var schemaDoc = JsonDocument.Parse(tool.InputSchemaJson);
        return ParseSchemaToToolDefinition(tool.Name, tool.Description, schemaDoc.RootElement);
    }

    private static ToolDefinition ParseSchemaToToolDefinition(
        string name, string description, JsonElement root)
    {
        var properties = new Dictionary<string, ToolProperty>();
        if (root.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                var type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                properties[prop.Name] = new ToolProperty(type, desc);
            }
        }

        var required = Array.Empty<string>();
        if (root.TryGetProperty("required", out var req))
            required = req.EnumerateArray().Select(e => e.GetString()!).ToArray();

        return new ToolDefinition(
            Name: name,
            Description: description,
            InputSchema: new InputSchema(
                Type: "object",
                Properties: properties,
                Required: required));
    }

    // ── HTTP helper ───────────────────────────────────────────────────────────

    private async Task<MessagesResponse> PostAsync(MessagesRequest requestBody, CancellationToken ct)
    {
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

        return await response.Content.ReadFromJsonAsync<MessagesResponse>(s_serializerOptions, ct)
               ?? throw new InvalidOperationException("Anthropic API returned an empty response.");
    }

    // ── Serializer options ────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Request / response shapes ─────────────────────────────────────────────

    private sealed record MessagesRequest(
        string Model,
        int MaxTokens,
        string System,
        IEnumerable<object> Messages,
        IReadOnlyList<ToolDefinition>? Tools);

    private sealed record TextMessage(string Role, string Content);

    private sealed record ContentArrayMessage(string Role, ContentBlock[] Content);

    private sealed record ToolResultMessage(string Role, ToolResultContent[] Content);

    private sealed record ContentBlock(
        string Type,
        string? Text,
        string? Id,
        string? Name,
        JsonElement? Input);

    private sealed record MessagesResponse(
        ContentBlock[] Content,
        string StopReason);

    private sealed record ToolResultContent(string Type, string ToolUseId, string Content);

    private sealed record ToolDefinition(string Name, string Description, InputSchema InputSchema);

    private sealed record InputSchema(
        string Type,
        Dictionary<string, ToolProperty> Properties,
        string[] Required);

    private sealed record ToolProperty(string Type, string Description);
}
