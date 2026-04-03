namespace Purfle.App.Services;

public interface INotificationService
{
    /// <summary>Request notification permission (macOS). No-op on other platforms.</summary>
    void RequestPermission();

    Task NotifyAgentCompletedAsync(string agentId, string agentName, string outputPreview);
    Task NotifyAgentErrorAsync(string agentId, string agentName, string errorSummary);
    Task NotifyAgentInstalledAsync(string agentId, string agentName);
}
