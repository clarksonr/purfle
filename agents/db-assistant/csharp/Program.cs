using System.Text.Json;
using System.Text.Json.Serialization;

// Purfle IPC agent: reads JSON commands from stdin, writes JSON responses to stdout.
// Protocol: one JSON object per line (newline-delimited JSON).

var reader = Console.In;
var writer = Console.Out;

await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage("ready", "db-assistant ready")));
await writer.FlushAsync();

while (await reader.ReadLineAsync() is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
        continue;

    IpcMessage? request;
    try
    {
        request = JsonSerializer.Deserialize<IpcMessage>(line);
    }
    catch (JsonException)
    {
        await WriteResponse(writer, "error", "Invalid JSON input");
        continue;
    }

    if (request is null)
    {
        await WriteResponse(writer, "error", "Null request");
        continue;
    }

    switch (request.Type)
    {
        case "execute":
            await HandleExecute(writer);
            break;

        case "ping":
            await WriteResponse(writer, "pong", "alive");
            break;

        case "shutdown":
            await WriteResponse(writer, "shutdown", "goodbye");
            return;

        default:
            await WriteResponse(writer, "error", $"Unknown command: {request.Type}");
            break;
    }
}

static async Task HandleExecute(TextWriter writer)
{
    // Step 1: Call db/schema tool to get table structure
    await writer.WriteLineAsync(JsonSerializer.Serialize(new ToolCall("db/schema", new { })));
    await writer.FlushAsync();

    // In a real agent, the AIVM would respond with schema data on stdin.
    // For now, we emit the tool call and proceed with analysis.

    // Step 2: Analyze schema and suggest optimizations
    var analysis = new AnalysisResult(
        Status: "done",
        ToolCalls: new[]
        {
            "db/schema — retrieve table structures",
            "db/query-explain — analyze slow queries",
            "db/suggest-index — recommend missing indexes"
        },
        Checks: new[]
        {
            "Foreign key columns missing indexes",
            "N+1 query pattern detection",
            "Full table scan identification",
            "Over-indexed table warnings",
            "Implicit type conversion detection",
            "Covering index opportunities"
        }
    );

    await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage("result", JsonSerializer.Serialize(analysis))));
    await writer.FlushAsync();
}

static async Task WriteResponse(TextWriter writer, string type, string data)
{
    await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage(type, data)));
    await writer.FlushAsync();
}

record IpcMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] string Data);

record ToolCall(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("args")] object Args);

record AnalysisResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("toolCalls")] string[] ToolCalls,
    [property: JsonPropertyName("checks")] string[] Checks);
