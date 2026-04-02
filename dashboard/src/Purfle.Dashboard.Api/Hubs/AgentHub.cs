using Microsoft.AspNetCore.SignalR;

namespace Purfle.Dashboard.Api.Hubs;

public class AgentHub : Hub
{
    /// <summary>
    /// Broadcasts a status update for a specific agent to all connected clients.
    /// </summary>
    public async Task SendStatusUpdate(string agentId, string status)
    {
        await Clients.All.SendAsync("StatusUpdate", agentId, status);
    }

    /// <summary>
    /// Broadcasts a log entry for a specific agent to all connected clients.
    /// </summary>
    public async Task SendLogEntry(string agentId, string timestamp, string level, string message)
    {
        await Clients.All.SendAsync("LogEntry", agentId, timestamp, level, message);
    }
}
