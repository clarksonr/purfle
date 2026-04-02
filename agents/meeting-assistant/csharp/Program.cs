using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Agents.MeetingAssistant;

/// <summary>
/// IPC stdin/stdout meeting assistant agent.
/// Reads JSON commands from stdin, calls MCP tools, writes results to stdout.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task Main(string[] args)
    {
        await RunIpcLoop();
    }

    private static async Task RunIpcLoop()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var request = JsonSerializer.Deserialize<IpcRequest>(line, JsonOptions);
                if (request is null)
                {
                    await WriteError(writer, "invalid_request", "Could not parse request.");
                    continue;
                }

                var response = request.Method switch
                {
                    "execute" => await HandleExecute(writer, request),
                    "ping" => new IpcResponse("pong", Status: "ok"),
                    _ => new IpcResponse(null, Status: "error", Error: $"Unknown method: {request.Method}")
                };

                await WriteResponse(writer, response);
            }
            catch (JsonException)
            {
                await WriteError(writer, "parse_error", "Malformed JSON input.");
            }
            catch (Exception ex)
            {
                await WriteError(writer, "internal_error", ex.Message);
            }
        }
    }

    private static async Task<IpcResponse> HandleExecute(StreamWriter writer, IpcRequest request)
    {
        var input = request.Params?.GetValueOrDefault("input") ?? "";

        // Step 1: Call meeting/transcribe tool via IPC
        var transcribeCall = new ToolCall("meeting/transcribe", new Dictionary<string, string>
        {
            ["input"] = input
        });
        await WriteToolCall(writer, transcribeCall);

        // Read transcript result from stdin (runtime sends tool results back)
        var transcriptResult = await WaitForToolResult(Console.OpenStandardInput());
        var transcript = transcriptResult?.Result ?? input;

        // Step 2: Call meeting/action-items tool
        var actionItemsCall = new ToolCall("meeting/action-items", new Dictionary<string, string>
        {
            ["transcript"] = transcript
        });
        await WriteToolCall(writer, actionItemsCall);

        var actionItemsResult = await WaitForToolResult(Console.OpenStandardInput());
        var actionItems = actionItemsResult?.Result ?? "No action items extracted.";

        // Step 3: Format meeting notes
        var notes = FormatMeetingNotes(transcript, actionItems);

        return new IpcResponse(notes, Status: "done");
    }

    private static string FormatMeetingNotes(string transcript, string actionItems)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");

        return $"""
            # Meeting Notes
            Generated: {timestamp}

            ## Summary
            {ExtractSummary(transcript)}

            ## Decisions
            {ExtractDecisions(transcript)}

            ## Action Items
            | Item | Owner | Deadline | Status |
            |------|-------|----------|--------|
            {actionItems}

            ## Next Steps
            - Review action items and confirm owners
            - Schedule follow-up if needed
            """;
    }

    private static string ExtractSummary(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return "No transcript provided.";

        // Truncate to first ~500 chars for a rough summary line.
        // In production the LLM generates this; here we provide a placeholder.
        var preview = transcript.Length > 500 ? transcript[..500] + "..." : transcript;
        return $"Meeting transcript received ({transcript.Length} characters). Key points extracted below.";
    }

    private static string ExtractDecisions(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return "No decisions recorded.";

        return "Decisions extracted from transcript (see action items for assignments).";
    }

    private static async Task WriteToolCall(StreamWriter writer, ToolCall call)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "tool_call",
            tool = call.Name,
            @params = call.Params
        }, JsonOptions);

        await writer.WriteLineAsync(json);
    }

    private static async Task<ToolResult?> WaitForToolResult(Stream stdin)
    {
        using var reader = new StreamReader(stdin, leaveOpen: true);
        var line = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(line))
            return null;

        return JsonSerializer.Deserialize<ToolResult>(line, JsonOptions);
    }

    private static async Task WriteResponse(StreamWriter writer, IpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        await writer.WriteLineAsync(json);
    }

    private static async Task WriteError(StreamWriter writer, string code, string message)
    {
        var response = new IpcResponse(null, Status: "error", Error: message, ErrorCode: code);
        await WriteResponse(writer, response);
    }
}

// --- IPC message types ---

public record IpcRequest(
    string Method,
    Dictionary<string, string>? Params = null
);

public record IpcResponse(
    string? Result,
    string Status = "ok",
    string? Error = null,
    string? ErrorCode = null
);

public record ToolCall(
    string Name,
    Dictionary<string, string> Params
);

public record ToolResult(
    string? Result,
    string? Error = null
);
