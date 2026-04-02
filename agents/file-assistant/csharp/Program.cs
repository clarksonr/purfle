using System.Text.Json;
using System.Text.Json.Serialization;

// Purfle File Assistant — IPC stdin/stdout agent
// Reads JSON requests from stdin, writes JSON responses to stdout.
// Protocol:
//   -> { "type": "execute", "input": { "message": "..." } }
//   <- { "type": "response", "toolCall": { "name": "list_directory", "args": { "path": "./workspace" } } }
//   -> { "type": "toolResult", "callId": "...", "result": "..." }
//   <- { "type": "response", "content": "...", "done": true }

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

while (true)
{
    var line = Console.ReadLine();
    if (line is null)
        break;

    IpcRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<IpcRequest>(line, options);
    }
    catch
    {
        WriteResponse(new IpcResponse
        {
            Type = "error",
            Content = "Invalid JSON input",
            Done = true
        });
        continue;
    }

    if (request is null)
        continue;

    switch (request.Type)
    {
        case "execute":
            // First step: request a tool call to list the workspace directory
            WriteResponse(new IpcResponse
            {
                Type = "response",
                ToolCall = new ToolCall
                {
                    Name = "list_directory",
                    Args = new Dictionary<string, string> { ["path"] = "./workspace" }
                }
            });
            break;

        case "toolResult":
            // Tool result received — return final response
            var summary = request.Result ?? "(no result)";
            WriteResponse(new IpcResponse
            {
                Type = "response",
                Content = $"Here are the files I found:\n{summary}",
                Done = true
            });
            break;

        default:
            WriteResponse(new IpcResponse
            {
                Type = "error",
                Content = $"Unknown request type: {request.Type}",
                Done = true
            });
            break;
    }
}

void WriteResponse(IpcResponse response)
{
    var json = JsonSerializer.Serialize(response, options);
    Console.WriteLine(json);
    Console.Out.Flush();
}

// ── Models ──────────────────────────────────────────────────────────────

record IpcRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("input")]
    public JsonElement? Input { get; init; }

    [JsonPropertyName("callId")]
    public string? CallId { get; init; }

    [JsonPropertyName("result")]
    public string? Result { get; init; }
}

record IpcResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("toolCall")]
    public ToolCall? ToolCall { get; init; }

    [JsonPropertyName("done")]
    public bool? Done { get; init; }
}

record ToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("args")]
    public Dictionary<string, string>? Args { get; init; }
}
