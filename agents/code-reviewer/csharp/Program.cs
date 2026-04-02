using System.Text.Json;
using System.Text.Json.Serialization;

// Purfle Code Reviewer — IPC stdin/stdout agent
// Reads JSON requests from stdin, writes JSON responses to stdout.
// Protocol:
//   -> { "type": "execute", "input": { "code": "...", "language": "csharp" } }
//   <- { "type": "response", "toolCall": { "name": "code/analyze", "args": { "code": "...", "language": "..." } } }
//   -> { "type": "toolResult", "callId": "analyze-1", "result": "..." }
//   <- { "type": "response", "toolCall": { "name": "code/lint", "args": { ... } } }
//   -> { "type": "toolResult", "callId": "lint-1", "result": "..." }
//   <- { "type": "response", "toolCall": { "name": "code/security-scan", "args": { ... } } }
//   -> { "type": "toolResult", "callId": "scan-1", "result": "..." }
//   <- { "type": "response", "content": "<formatted review>", "done": true }

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

// Track review state across the multi-step tool-call conversation
string pendingCode = "";
string pendingLanguage = "";
string analyzeResult = "";
string lintResult = "";
ReviewPhase phase = ReviewPhase.Idle;

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
            HandleExecute(request);
            break;

        case "toolResult":
            HandleToolResult(request);
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

void HandleExecute(IpcRequest request)
{
    // Extract code and language from input
    if (request.Input.HasValue)
    {
        var input = request.Input.Value;
        if (input.TryGetProperty("code", out var codeEl))
            pendingCode = codeEl.GetString() ?? "";
        if (input.TryGetProperty("language", out var langEl))
            pendingLanguage = langEl.GetString() ?? "unknown";
    }

    if (string.IsNullOrWhiteSpace(pendingCode))
    {
        WriteResponse(new IpcResponse
        {
            Type = "error",
            Content = "Missing 'code' in input",
            Done = true
        });
        return;
    }

    // Step 1: Call code/analyze
    phase = ReviewPhase.WaitingAnalyze;
    WriteResponse(new IpcResponse
    {
        Type = "response",
        ToolCall = new ToolCall
        {
            Name = "code/analyze",
            Args = new Dictionary<string, string>
            {
                ["code"] = pendingCode,
                ["language"] = pendingLanguage
            }
        }
    });
}

void HandleToolResult(IpcRequest request)
{
    var result = request.Result ?? "(no result)";

    switch (phase)
    {
        case ReviewPhase.WaitingAnalyze:
            analyzeResult = result;
            // Step 2: Call code/lint
            phase = ReviewPhase.WaitingLint;
            WriteResponse(new IpcResponse
            {
                Type = "response",
                ToolCall = new ToolCall
                {
                    Name = "code/lint",
                    Args = new Dictionary<string, string>
                    {
                        ["code"] = pendingCode,
                        ["language"] = pendingLanguage
                    }
                }
            });
            break;

        case ReviewPhase.WaitingLint:
            lintResult = result;
            // Step 3: Call code/security-scan
            phase = ReviewPhase.WaitingScan;
            WriteResponse(new IpcResponse
            {
                Type = "response",
                ToolCall = new ToolCall
                {
                    Name = "code/security-scan",
                    Args = new Dictionary<string, string>
                    {
                        ["code"] = pendingCode,
                        ["language"] = pendingLanguage
                    }
                }
            });
            break;

        case ReviewPhase.WaitingScan:
            // All three tool results collected — format the review
            var review = FormatReview(analyzeResult, lintResult, result);
            phase = ReviewPhase.Idle;
            WriteResponse(new IpcResponse
            {
                Type = "response",
                Content = review,
                Done = true
            });
            break;

        default:
            WriteResponse(new IpcResponse
            {
                Type = "error",
                Content = "Unexpected tool result — no pending review phase",
                Done = true
            });
            break;
    }
}

string FormatReview(string analyze, string lint, string securityScan)
{
    return $"""
        ## Code Review Results

        ### Analysis (bugs, performance, maintainability)
        {analyze}

        ### Lint (style violations)
        {lint}

        ### Security Scan
        {securityScan}

        ---
        Severity legend: **critical** = must fix | **warning** = should fix | **info** = nice to fix
        """;
}

void WriteResponse(IpcResponse response)
{
    var json = JsonSerializer.Serialize(response, options);
    Console.WriteLine(json);
    Console.Out.Flush();
}

// ── State ──────────────────────────────────────────────────────────────

enum ReviewPhase
{
    Idle,
    WaitingAnalyze,
    WaitingLint,
    WaitingScan
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
