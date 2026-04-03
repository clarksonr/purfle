#if MACCATALYST
using UserNotifications;

namespace Purfle.App.Services;

/// <summary>
/// macOS notification service using UNUserNotificationCenter.
/// Sends notifications with deep-link actions to purfle://agent/{agentId}.
/// </summary>
public sealed class MacNotificationService : INotificationService
{
    public void RequestPermission()
    {
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Notification] macOS permission: granted={granted}, error={error?.LocalizedDescription}");
            });
    }

    public Task NotifyAgentCompletedAsync(string agentId, string agentName, string outputPreview)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_success", true))
            return Task.CompletedTask;

        var preview = Truncate(outputPreview, 200);
        var body = string.IsNullOrWhiteSpace(preview) ? "Agent finished successfully." : preview;
        return SendNotificationAsync(agentId, $"{agentName} — Completed", body);
    }

    public Task NotifyAgentErrorAsync(string agentId, string agentName, string errorSummary)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_error", true))
            return Task.CompletedTask;

        var summary = Truncate(errorSummary, 200);
        var body = string.IsNullOrWhiteSpace(summary) ? "Agent encountered an error." : summary;
        return SendNotificationAsync(agentId, $"{agentName} — Error", body);
    }

    public Task NotifyAgentInstalledAsync(string agentId, string agentName)
    {
        if (!IsEnabled() || !Preferences.Get("purfle_notify_install", true))
            return Task.CompletedTask;

        return SendNotificationAsync(agentId, $"{agentName} — Installed",
            "Agent installed successfully and is ready to run.");
    }

    private static Task SendNotificationAsync(string agentId, string title, string body)
    {
        var tcs = new TaskCompletionSource();

        try
        {
            var content = new UNMutableNotificationContent
            {
                Title = title,
                Body = body,
                Sound = UNNotificationSound.Default,
                UserInfo = new Foundation.NSDictionary("deepLink", $"purfle://agent/{agentId}")
            };

            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
            var request = UNNotificationRequest.FromIdentifier(
                Guid.NewGuid().ToString(), content, trigger);

            UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
            {
                if (error != null)
                    System.Diagnostics.Debug.WriteLine(
                        $"[Notification] macOS notification error: {error.LocalizedDescription}");
                tcs.TrySetResult();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] macOS notification failed: {ex.Message}");
            tcs.TrySetResult();
        }

        return tcs.Task;
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
