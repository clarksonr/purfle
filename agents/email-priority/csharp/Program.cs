using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Agents.EmailPriority;

/// <summary>
/// IPC agent that requests an email list via tool call, receives results,
/// categorizes by priority using inference prompt patterns, and returns
/// a prioritized summary.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output) { AutoFlush = true };

        // Read the IPC request from stdin (AIVM sends JSON)
        var requestJson = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            await WriteError(writer, "No input received on stdin");
            return;
        }

        var request = JsonSerializer.Deserialize<IpcRequest>(requestJson, JsonOptions);
        if (request is null)
        {
            await WriteError(writer, "Failed to parse IPC request");
            return;
        }

        // Step 1: Request email list via MCP tool call
        var listToolCall = new ToolCallRequest
        {
            Method = "tool/call",
            Tool = "mcp://localhost:8101/email/list",
            Parameters = new Dictionary<string, object>
            {
                ["limit"] = 50,
                ["unread_only"] = true
            }
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(listToolCall, JsonOptions));

        // Step 2: Read tool response (AIVM sends back results)
        var listResponseJson = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(listResponseJson))
        {
            await WriteError(writer, "No response from email list tool");
            return;
        }

        var listResponse = JsonSerializer.Deserialize<ToolCallResponse>(listResponseJson, JsonOptions);
        var emails = listResponse?.Emails ?? [];

        // Step 3: For each email, request full body via read tool
        var fullEmails = new List<EmailMessage>();
        foreach (var email in emails)
        {
            var readToolCall = new ToolCallRequest
            {
                Method = "tool/call",
                Tool = "mcp://localhost:8102/email/read",
                Parameters = new Dictionary<string, object>
                {
                    ["message_id"] = email.Id
                }
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(readToolCall, JsonOptions));

            var readResponseJson = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(readResponseJson))
            {
                var readResponse = JsonSerializer.Deserialize<EmailReadResponse>(readResponseJson, JsonOptions);
                if (readResponse?.Email is not null)
                {
                    fullEmails.Add(readResponse.Email);
                }
            }
        }

        // Step 4: Build inference request for priority categorization
        var systemPrompt = LoadSystemPrompt();
        var emailBlock = FormatEmailsForInference(fullEmails);

        var inferenceRequest = new InferenceRequest
        {
            Method = "inference/complete",
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = $"Triage the following emails:\n\n{emailBlock}" }
            ]
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(inferenceRequest, JsonOptions));

        // Step 5: Read inference response
        var inferenceResponseJson = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(inferenceResponseJson))
        {
            await WriteError(writer, "No response from inference");
            return;
        }

        var inferenceResponse = JsonSerializer.Deserialize<InferenceResponse>(inferenceResponseJson, JsonOptions);

        // Step 6: Write final result to stdout
        var result = new AgentResult
        {
            Status = "ok",
            AgentId = "dev.purfle.email-priority",
            Output = inferenceResponse?.Content ?? "No triage result produced",
            EmailCount = fullEmails.Count,
            Timestamp = DateTime.UtcNow.ToString("o")
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static string LoadSystemPrompt()
    {
        var promptPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts", "system.md");
        if (File.Exists(promptPath))
            return File.ReadAllText(promptPath);

        // Fallback inline prompt
        return """
            You are an email triage assistant. Categorize each email as URGENT, IMPORTANT, NORMAL, or LOW.
            Summarize key points and flag action items.
            """;
    }

    private static string FormatEmailsForInference(List<EmailMessage> emails)
    {
        var parts = new List<string>();
        foreach (var email in emails)
        {
            parts.Add($"""
                ---
                From: {email.From}
                Subject: {email.Subject}
                Date: {email.Date}
                Body:
                {email.Body}
                ---
                """);
        }
        return string.Join("\n", parts);
    }

    private static async Task WriteError(StreamWriter writer, string message)
    {
        var error = new AgentResult
        {
            Status = "error",
            AgentId = "dev.purfle.email-priority",
            Output = message,
            EmailCount = 0,
            Timestamp = DateTime.UtcNow.ToString("o")
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(error, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

// --- IPC message types ---

public record IpcRequest
{
    public string Method { get; init; } = "";
    public string AgentId { get; init; } = "";
    public Dictionary<string, object>? Parameters { get; init; }
}

public record ToolCallRequest
{
    public string Method { get; init; } = "";
    public string Tool { get; init; } = "";
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public record ToolCallResponse
{
    public List<EmailSummary> Emails { get; init; } = [];
}

public record EmailSummary
{
    public string Id { get; init; } = "";
    public string From { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Date { get; init; } = "";
}

public record EmailReadResponse
{
    public EmailMessage? Email { get; init; }
}

public record EmailMessage
{
    public string Id { get; init; } = "";
    public string From { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Date { get; init; } = "";
    public string Body { get; init; } = "";
}

public record InferenceRequest
{
    public string Method { get; init; } = "";
    public List<ChatMessage> Messages { get; init; } = [];
}

public record ChatMessage
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
}

public record InferenceResponse
{
    public string Content { get; init; } = "";
}

public record AgentResult
{
    public string Status { get; init; } = "";
    public string AgentId { get; init; } = "";
    public string Output { get; init; } = "";
    public int EmailCount { get; init; }
    public string Timestamp { get; init; } = "";
}
