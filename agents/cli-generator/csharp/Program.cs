using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// CLI Generator agent — IPC stdin/stdout protocol.
/// Reads JSON messages from stdin, calls MCP tools (scaffold, add-command, generate-help),
/// writes JSON responses to stdout.
/// </summary>

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

await SendMessage(new AgentMessage("status", "cli-generator agent ready"));

while (true)
{
    var line = Console.ReadLine();
    if (line is null) break;

    try
    {
        var request = JsonSerializer.Deserialize<AgentRequest>(line, jsonOptions);
        if (request is null)
        {
            await SendMessage(new AgentMessage("error", "Failed to parse request"));
            continue;
        }

        switch (request.Action)
        {
            case "execute":
                await HandleExecute(request, jsonOptions);
                break;

            case "ping":
                await SendMessage(new AgentMessage("pong", "ok"));
                break;

            default:
                await SendMessage(new AgentMessage("error", $"Unknown action: {request.Action}"));
                break;
        }
    }
    catch (Exception ex)
    {
        await SendMessage(new AgentMessage("error", ex.Message));
    }
}

return 0;

static async Task HandleExecute(AgentRequest request, JsonSerializerOptions jsonOptions)
{
    // Step 1: Call cli/scaffold to create project structure
    var scaffoldParams = new McpToolCall("mcp://localhost:8110/cli/scaffold", new Dictionary<string, object?>
    {
        ["framework"] = request.Framework ?? "dotnet",
        ["projectName"] = request.ProjectName ?? "my-cli",
        ["outputDir"] = "./output"
    });
    await SendMessage(new AgentMessage("tool_call", JsonSerializer.Serialize(scaffoldParams, jsonOptions)));

    // Step 2: Add commands based on user input
    var commands = request.Commands ?? ["greet", "version"];
    foreach (var command in commands)
    {
        var addCommandParams = new McpToolCall("mcp://localhost:8110/cli/add-command", new Dictionary<string, object?>
        {
            ["projectName"] = request.ProjectName ?? "my-cli",
            ["commandName"] = command,
            ["framework"] = request.Framework ?? "dotnet",
            ["outputDir"] = "./output"
        });
        await SendMessage(new AgentMessage("tool_call", JsonSerializer.Serialize(addCommandParams, jsonOptions)));
    }

    // Step 3: Generate help documentation
    var helpParams = new McpToolCall("mcp://localhost:8110/cli/generate-help", new Dictionary<string, object?>
    {
        ["projectName"] = request.ProjectName ?? "my-cli",
        ["framework"] = request.Framework ?? "dotnet",
        ["format"] = "markdown",
        ["outputDir"] = "./output"
    });
    await SendMessage(new AgentMessage("tool_call", JsonSerializer.Serialize(helpParams, jsonOptions)));

    await SendMessage(new AgentMessage("done", $"CLI project '{request.ProjectName ?? "my-cli"}' generated with {commands.Length} command(s)"));
}

static Task SendMessage(AgentMessage message)
{
    var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });
    Console.Out.WriteLine(json);
    Console.Out.Flush();
    return Task.CompletedTask;
}

record AgentMessage(string Type, string Payload);

record AgentRequest
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("framework")]
    public string? Framework { get; init; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; init; }

    [JsonPropertyName("commands")]
    public string[]? Commands { get; init; }
}

record McpToolCall(string Tool, Dictionary<string, object?> Parameters);
