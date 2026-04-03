#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;

namespace Purfle.App.Services;

/// <summary>
/// Windows toast notification service using Microsoft.Toolkit.Uwp.Notifications.
/// Sends toast notifications with deep-link actions to purfle://agent/{agentId}.
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    public void RequestPermission()
    {
        // Windows does not require explicit permission for toast notifications.
    }

    public Task NotifyAgentCompletedAsync(string agentId, string agentName, string outputPreview)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_success", true))
            return Task.CompletedTask;

        try
        {
            var preview = Truncate(outputPreview, 200);

            new ToastContentBuilder()
                .AddArgument("action", "viewAgent")
                .AddArgument("agentId", agentId)
                .AddText($"{agentName} — Completed")
                .AddText(string.IsNullOrWhiteSpace(preview) ? "Agent finished successfully." : preview)
                .SetProtocolActivation(new Uri($"purfle://agent/{agentId}"))
                .Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Windows toast failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task NotifyAgentErrorAsync(string agentId, string agentName, string errorSummary)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_error", true))
            return Task.CompletedTask;

        try
        {
            var summary = Truncate(errorSummary, 200);

            new ToastContentBuilder()
                .AddArgument("action", "viewAgent")
                .AddArgument("agentId", agentId)
                .AddText($"{agentName} — Error")
                .AddText(string.IsNullOrWhiteSpace(summary) ? "Agent encountered an error." : summary)
                .SetProtocolActivation(new Uri($"purfle://agent/{agentId}"))
                .Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Windows toast failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task NotifyAgentInstalledAsync(string agentId, string agentName)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_install", true))
            return Task.CompletedTask;

        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewAgent")
                .AddArgument("agentId", agentId)
                .AddText($"{agentName} — Installed")
                .AddText("Agent installed successfully and is ready to run.")
                .SetProtocolActivation(new Uri($"purfle://agent/{agentId}"))
                .Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Windows toast failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static bool IsEnabled()
        => Preferences.Get("purfle_notifications_enabled", true);

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
#endif
