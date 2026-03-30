using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.Sandbox;

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

    private readonly HttpClient  _http;
    private readonly string      _model;
    private readonly string      _apiKey;
    private readonly AgentSandbox _sandbox;

    /// <summary>Tools built from the manifest permissions + MCP servers. Empty for agents with no OS access.</summary>
    private readonly IReadOnlyList<ToolDefinition> _tools;

    /// <summary>MCP tool name → the client that owns it. Used for routing tool calls.</summary>
    private readonly IReadOnlyDictionary<string, IMcpClient> _mcpToolRoutes;

    /// <inheritdoc/>
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
    /// <param name="mcpClients">
    /// Optional list of connected MCP clients. The adapter will discover tools from each,
    /// filter them through the sandbox, and advertise permitted ones to the model.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sandbox does not permit reading <c>ANTHROPIC_API_KEY</c>
    /// or when the environment variable is not set.
    /// </exception>
    public AnthropicAdapter(
        AgentManifest manifest,
        AgentSandbox sandbox,
        HttpClient? http = null,
        IReadOnlyList<IMcpClient>? mcpClients = null)
    {
        if (!sandbox.CanReadEnv(EnvApiKey))
            throw new InvalidOperationException(
                $"Anthropic adapter requires '{EnvApiKey}' in permissions.environment.allow.");

        var key = Environment.GetEnvironmentVariable(EnvApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Environment variable '{EnvApiKey}' is not set.");

        _apiKey  = key;
        _model   = manifest.Runtime.Model ?? DefaultModel;
        _http    = http ?? new HttpClient();
        _sandbox = sandbox;

        // Build MCP tool routes and tool definitions.
        var mcpRoutes = new Dictionary<string, IMcpClient>();
        var mcpToolDefs = new List<ToolDefinition>();

        if (mcpClients is not null)
        {
            foreach (var client in mcpClients)
            {
                // ListToolsAsync is called synchronously during construction.
                // MCP servers should respond quickly to tools/list.
                var tools = client.ListToolsAsync().GetAwaiter().GetResult();
                foreach (var tool in tools)
                {
                    if (!sandbox.CanUseMcpTool(tool.Name))
                        continue;

                    mcpRoutes[tool.Name] = client;
                    mcpToolDefs.Add(BuildMcpToolDefinition(tool));
                }
            }
        }

        _mcpToolRoutes = mcpRoutes;

        var builtinTools = BuildTools(manifest.Permissions);
        _tools = [.. builtinTools, .. mcpToolDefs];
    }

    // ── Single-turn interface ────────────────────────────────────────────────────

    /// <summary>
    /// Sends a single-turn inference request. Stateless — no conversation history is retained.
    /// </summary>
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

    /// <summary>
    /// Sends an inference request with full conversation history. Each element in
    /// <paramref name="conversationHistory"/> is a message object (role + content).
    /// The new <paramref name="userMessage"/> is appended automatically.
    /// Returns the model's text reply and the updated message list (including the
    /// assistant turn) so the caller can feed it back for subsequent turns.
    /// </summary>
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
                Model:    _model,
                MaxTokens: 4096,
                System:   systemPrompt,
                Messages: messages,
                Tools:    null);

            var response = await PostAsync(request, ct);
            var text = response.Content.First(b => b.Type == "text").Text
                       ?? throw new InvalidOperationException("Anthropic API returned no text content.");

            messages.Add(new TextMessage("assistant", text));
            return (text, messages);
        }

        // Tool-call loop with history.
        for (int i = 0; i < MaxIterations; i++)
        {
            var request = new MessagesRequest(
                Model:    _model,
                MaxTokens: 4096,
                System:   systemPrompt,
                Messages: messages,
                Tools:    _tools);

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

    // ── Single-turn path (no tools configured) ───────────────────────────────

    private async Task<string> InvokeSingleTurnAsync(
        string systemPrompt, string userMessage, CancellationToken ct)
    {
        var request = new MessagesRequest(
            Model:    _model,
            MaxTokens: 4096,
            System:   systemPrompt,
            Messages: [new TextMessage("user", userMessage)],
            Tools:    null);

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
                Model:    _model,
                MaxTokens: 4096,
                System:   systemPrompt,
                Messages: messages,
                Tools:    _tools);

            var response = await PostAsync(request, ct);

            if (response.StopReason == "end_turn")
            {
                var textBlock = response.Content.FirstOrDefault(b => b.Type == "text");
                return textBlock?.Text
                       ?? throw new InvalidOperationException("Anthropic API returned end_turn with no text block.");
            }

            if (response.StopReason == "tool_use")
            {
                // Record the full assistant turn (may contain both text and tool_use blocks).
                messages.Add(new ContentArrayMessage("assistant", response.Content));

                // Execute each tool call and collect results.
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
        // Check built-in tools first.
        switch (toolName)
        {
            case "read_file":
                return ExecuteReadFile(input?.GetProperty("path").GetString() ?? "");
            case "write_file":
                return ExecuteWriteFile(
                    input?.GetProperty("path").GetString() ?? "",
                    input?.GetProperty("content").GetString() ?? "");
            case "http_get":
                return await ExecuteHttpGetAsync(
                    input?.GetProperty("url").GetString() ?? "", ct);
        }

        // Route to MCP client if the tool is registered.
        if (_mcpToolRoutes.TryGetValue(toolName, out var mcpClient))
        {
            if (!_sandbox.CanUseMcpTool(toolName))
                return $"Error: permission denied — MCP tool '{toolName}' is not in the agent's tools.mcp allowlist.";

            try
            {
                var arguments = input?.GetRawText() ?? "{}";
                return await mcpClient.CallToolAsync(toolName, arguments, ct);
            }
            catch (Exception ex)
            {
                return $"Error: MCP tool '{toolName}' failed — {ex.Message}";
            }
        }

        return $"Error: unknown tool '{toolName}'.";
    }

    /// <summary>
    /// Reads a file from disk. The sandbox is checked before the read; if the path is not
    /// covered by <c>permissions.filesystem.read</c> the error string is returned to the
    /// model rather than raising an exception, allowing the model to recover gracefully.
    /// </summary>
    private string ExecuteReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path must not be empty.";

        if (!_sandbox.CanReadPath(path))
            return $"Error: permission denied — '{path}' is not in the agent's filesystem.read allowlist.";

        if (!File.Exists(path))
            return $"Error: file not found — '{path}'.";

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Writes content to a file. The sandbox is checked before the write; if the path is
    /// not covered by <c>permissions.filesystem.write</c> the error string is returned to
    /// the model rather than raising an exception.
    /// </summary>
    private string ExecuteWriteFile(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path must not be empty.";

        if (!_sandbox.CanWritePath(path))
            return $"Error: permission denied — '{path}' is not in the agent's filesystem.write allowlist.";

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, content);
        return "OK";
    }

    /// <summary>
    /// Fetches a URL via HTTP GET. The sandbox is checked before the request; if the URL
    /// is not covered by <c>permissions.network.allow</c> (or is explicitly denied) the
    /// error string is returned to the model rather than raising an exception.
    /// </summary>
    private async Task<string> ExecuteHttpGetAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: url must not be empty.";

        if (!_sandbox.CanAccessUrl(url))
            return $"Error: permission denied — '{url}' is not in the agent's network.allow list.";

        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        catch (Exception ex)
        {
            return $"Error: HTTP GET failed — {ex.Message}";
        }
    }

    // ── Tool building ─────────────────────────────────────────────────────────

    /// <summary>
    /// Inspects the agent's permission block and constructs the list of built-in tools to
    /// advertise to the model. Only tools backed by at least one permission pattern are
    /// included — a chat agent with no filesystem or network permissions receives an empty
    /// list and takes the single-turn path instead.
    /// </summary>
    private static IReadOnlyList<ToolDefinition> BuildTools(AgentPermissions permissions)
    {
        var tools = new List<ToolDefinition>();

        if (permissions.Filesystem?.Read.Count > 0)
        {
            tools.Add(new ToolDefinition(
                Name: "read_file",
                Description: "Read the full text contents of a file on the local filesystem. " +
                             "Only paths matching the agent's filesystem.read permission patterns are accessible.",
                InputSchema: new InputSchema(
                    Type: "object",
                    Properties: new Dictionary<string, ToolProperty>
                    {
                        ["path"] = new ToolProperty("string", "Absolute path to the file to read."),
                    },
                    Required: ["path"])));
        }

        if (permissions.Filesystem?.Write.Count > 0)
        {
            tools.Add(new ToolDefinition(
                Name: "write_file",
                Description: "Write text content to a file on the local filesystem. " +
                             "Only paths matching the agent's filesystem.write permission patterns are accessible.",
                InputSchema: new InputSchema(
                    Type: "object",
                    Properties: new Dictionary<string, ToolProperty>
                    {
                        ["path"]    = new ToolProperty("string", "Absolute path to the file to write."),
                        ["content"] = new ToolProperty("string", "Text content to write to the file."),
                    },
                    Required: ["path", "content"])));
        }

        if (permissions.Network?.Allow.Count > 0)
        {
            tools.Add(new ToolDefinition(
                Name: "http_get",
                Description: "Fetch a URL via HTTP GET and return the response body as text. " +
                             "Only URLs matching the agent's network.allow patterns are accessible.",
                InputSchema: new InputSchema(
                    Type: "object",
                    Properties: new Dictionary<string, ToolProperty>
                    {
                        ["url"] = new ToolProperty("string", "The URL to fetch."),
                    },
                    Required: ["url"])));
        }

        return tools;
    }

    /// <summary>
    /// Converts an <see cref="McpToolInfo"/> into an Anthropic tool definition.
    /// The MCP tool's input schema JSON is parsed and decomposed into the Anthropic format.
    /// </summary>
    private static ToolDefinition BuildMcpToolDefinition(McpToolInfo tool)
    {
        var schemaDoc = JsonDocument.Parse(tool.InputSchemaJson);
        var root = schemaDoc.RootElement;

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
        {
            required = req.EnumerateArray().Select(e => e.GetString()!).ToArray();
        }

        return new ToolDefinition(
            Name: tool.Name,
            Description: tool.Description,
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
        PropertyNamingPolicy          = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition        = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Request / response shapes ─────────────────────────────────────────────

    private sealed record MessagesRequest(
        string Model,
        int MaxTokens,
        string System,
        IEnumerable<object> Messages,
        IReadOnlyList<ToolDefinition>? Tools);

    /// <summary>Regular text message (initial user turn, or any assistant turn without tools).</summary>
    private sealed record TextMessage(string Role, string Content);

    /// <summary>
    /// Assistant message whose content is an array of blocks (text + tool_use mix).
    /// Sent back verbatim so the model retains its own reasoning.
    /// </summary>
    private sealed record ContentArrayMessage(string Role, ContentBlock[] Content);

    /// <summary>User turn carrying tool results back to the model.</summary>
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
