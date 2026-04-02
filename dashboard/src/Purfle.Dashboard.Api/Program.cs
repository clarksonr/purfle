using Microsoft.AspNetCore.SignalR;
using Purfle.Dashboard.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ── In-memory mock data ──────────────────────────────────────────────

var agents = new List<AgentData>
{
    new("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa", "Purfle Chat", "1.0.0",
        "A general-purpose conversational agent.", "gemini", "gemini-2.5-flash",
        "interval", 30, "Running"),
    new("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb", "Downloads File Search", "1.0.0",
        "Searches your Downloads folder for files by name or content.", "gemini", "gemini-2.5-flash",
        "interval", 60, "Stopped"),
    new("cccccccc-cccc-4ccc-cccc-cccccccccccc", "Purfle Web Research", "1.0.0",
        "Fetches web pages and synthesises research.", "gemini", "gemini-2.5-flash",
        "interval", 45, "Running"),
    new("dddddddd-dddd-4ddd-dddd-dddddddddddd", "File Summarizer", "1.0.0",
        "Reads a local file and returns a concise summary with word count.", "gemini", "gemini-2.5-flash",
        "interval", 30, "Stopped"),
    new("a1b2c3d4-e5f6-4789-abcd-ef0123456789", "Email Monitor", "0.1.0",
        "Monitors a Gmail inbox on a 15-minute interval and summarises new messages.", "anthropic", "claude-sonnet-4-20250514",
        "interval", 15, "Running"),
    new("11111111-1111-4111-a111-111111111111", "Hello World", "0.1.0",
        "Minimal agent for local demonstration.", "anthropic", "claude-sonnet-4-20250514",
        "startup", null, "Stopped"),
    new("22222222-2222-4222-a222-222222222222", "PR Watcher", "0.2.0",
        "Checks GitHub every 30 minutes for new pull requests.", "anthropic", "claude-sonnet-4-20250514",
        "interval", 30, "Running"),
    new("33333333-3333-4333-a333-333333333333", "Report Builder", "0.1.0",
        "Runs at 07:00, reads agent outputs, writes a morning report.", "anthropic", "claude-sonnet-4-20250514",
        "cron", null, "Error"),
    new("44444444-4444-4444-a444-444444444444", "File Assistant", "1.0.0",
        "Reads, lists, searches, and summarizes files in workspace.", "gemini", "gemini-2.5-flash",
        "interval", 20, "Running"),
    new("55555555-5555-4555-a555-555555555555", "Voice Assistant", "0.1.0",
        "Listens for voice commands and responds with speech.", "anthropic", "claude-sonnet-4-20250514",
        "startup", null, "Stopped")
};

// Generate some mock log entries
var logs = new Dictionary<string, List<LogEntry>>();
var rng = new Random(42);
var logLevels = new[] { "INFO", "INFO", "INFO", "WARN", "DEBUG" };
var logMessages = new Dictionary<string, string[]>
{
    ["Running"] = [
        "Agent started successfully",
        "Inference call completed in {0}ms",
        "Processing input batch",
        "Output written to sandbox path",
        "Heartbeat OK",
        "Schedule tick fired",
        "Capability check passed: llm.chat",
        "Tool invocation: read_file"
    ],
    ["Stopped"] = [
        "Agent stopped by user",
        "Shutdown complete",
        "Resources released"
    ],
    ["Error"] = [
        "Agent started successfully",
        "Inference call completed in {0}ms",
        "ERROR: Connection timeout after 30s",
        "Retry attempt 1/3",
        "ERROR: API returned 503 Service Unavailable",
        "Agent entering error state"
    ]
};

foreach (var agent in agents)
{
    var entries = new List<LogEntry>();
    var status = agent.Status;
    var messages = logMessages.GetValueOrDefault(status, logMessages["Running"]);
    var count = status == "Running" ? rng.Next(8, 15) : status == "Error" ? 6 : rng.Next(3, 5);
    var baseTime = DateTime.UtcNow.AddMinutes(-count * 2);

    for (int i = 0; i < count; i++)
    {
        var msg = messages[i % messages.Length];
        if (msg.Contains("{0}"))
            msg = string.Format(msg, rng.Next(200, 1500));
        var level = msg.StartsWith("ERROR") ? "ERROR" : logLevels[rng.Next(logLevels.Length)];
        entries.Add(new LogEntry(
            baseTime.AddMinutes(i * 2).ToString("o"),
            level,
            msg));
    }

    logs[agent.Id] = entries;

    // Set last run time
    agent.LastRun = baseTime.AddMinutes((count - 1) * 2).ToString("o");
    if (agent.Status == "Running" && agent.Trigger == "interval" && agent.IntervalMinutes.HasValue)
        agent.NextRun = DateTime.UtcNow.AddMinutes(agent.IntervalMinutes.Value).ToString("o");
    else if (agent.Status == "Running" && agent.Trigger == "cron")
        agent.NextRun = DateTime.UtcNow.Date.AddDays(1).AddHours(7).ToString("o");
}

// ── API endpoints ────────────────────────────────────────────────────

app.MapGet("/api/agents", () => agents.Select(a => new
{
    a.Id, a.Name, a.Version, a.Description, a.Engine, a.Model,
    a.Trigger, a.IntervalMinutes, a.Status, a.LastRun, a.NextRun
}));

app.MapGet("/api/agents/{id}", (string id) =>
{
    var agent = agents.FirstOrDefault(a => a.Id == id);
    return agent is null
        ? Results.NotFound(new { error = "Agent not found" })
        : Results.Ok(new
        {
            agent.Id, agent.Name, agent.Version, agent.Description,
            agent.Engine, agent.Model, agent.Trigger, agent.IntervalMinutes,
            agent.Status, agent.LastRun, agent.NextRun
        });
});

app.MapGet("/api/agents/{id}/logs", (string id) =>
{
    if (!logs.TryGetValue(id, out var entries))
        return Results.NotFound(new { error = "Agent not found" });
    return Results.Ok(entries);
});

app.MapPost("/api/agents/{id}/start", async (string id, IHubContext<AgentHub> hub) =>
{
    var agent = agents.FirstOrDefault(a => a.Id == id);
    if (agent is null)
        return Results.NotFound(new { error = "Agent not found" });

    agent.Status = "Running";
    agent.LastRun = DateTime.UtcNow.ToString("o");
    if (agent.Trigger == "interval" && agent.IntervalMinutes.HasValue)
        agent.NextRun = DateTime.UtcNow.AddMinutes(agent.IntervalMinutes.Value).ToString("o");

    var entry = new LogEntry(DateTime.UtcNow.ToString("o"), "INFO", "Agent started by user");
    if (!logs.ContainsKey(id)) logs[id] = new List<LogEntry>();
    logs[id].Add(entry);

    await hub.Clients.All.SendAsync("StatusUpdate", id, "Running");
    await hub.Clients.All.SendAsync("LogEntry", id, entry.Timestamp, entry.Level, entry.Message);

    return Results.Ok(new { agent.Id, agent.Status });
});

app.MapPost("/api/agents/{id}/stop", async (string id, IHubContext<AgentHub> hub) =>
{
    var agent = agents.FirstOrDefault(a => a.Id == id);
    if (agent is null)
        return Results.NotFound(new { error = "Agent not found" });

    agent.Status = "Stopped";
    agent.NextRun = null;

    var entry = new LogEntry(DateTime.UtcNow.ToString("o"), "INFO", "Agent stopped by user");
    if (!logs.ContainsKey(id)) logs[id] = new List<LogEntry>();
    logs[id].Add(entry);

    await hub.Clients.All.SendAsync("StatusUpdate", id, "Stopped");
    await hub.Clients.All.SendAsync("LogEntry", id, entry.Timestamp, entry.Level, entry.Message);

    return Results.Ok(new { agent.Id, agent.Status });
});

app.MapHub<AgentHub>("/hubs/agents");

app.Run();

// ── Models ───────────────────────────────────────────────────────────

class AgentData(
    string id, string name, string version, string description,
    string engine, string model, string trigger, int? intervalMinutes, string status)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Version { get; } = version;
    public string Description { get; } = description;
    public string Engine { get; } = engine;
    public string Model { get; } = model;
    public string Trigger { get; } = trigger;
    public int? IntervalMinutes { get; } = intervalMinutes;
    public string Status { get; set; } = status;
    public string? LastRun { get; set; }
    public string? NextRun { get; set; }
}

record LogEntry(string Timestamp, string Level, string Message);
