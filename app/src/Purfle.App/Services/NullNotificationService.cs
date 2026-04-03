namespace Purfle.App.Services;

/// <summary>
/// No-op notification service for unsupported platforms.
/// </summary>
public sealed class NullNotificationService : INotificationService
{
    public void RequestPermission() { }

    public Task NotifyAgentCompletedAsync(string agentId, string agentName, string outputPreview)
        => Task.CompletedTask;

    public Task NotifyAgentErrorAsync(string agentId, string agentName, string errorSummary)
        => Task.CompletedTask;

    public Task NotifyAgentInstalledAsync(string agentId, string agentName)
        => Task.CompletedTask;
}
