namespace Purfle.App.Services;

/// <summary>
/// Cross-platform notification service. Sends system tray / toast notifications
/// when agents complete or error.
/// </summary>
public sealed class NotificationService
{
    /// <summary>
    /// Sends a system notification with the given title and message.
    /// On Windows, uses shell toast notifications. On macOS, uses UNUserNotificationCenter.
    /// </summary>
    public void Notify(string title, string message)
    {
#if WINDOWS
        try
        {
            var builder = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
                .AddText(title)
                .AddText(message);
            var notification = builder.BuildNotification();
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Windows toast failed: {ex.Message}");
            // Fall back to console
            Console.WriteLine($"[Notification] {title}: {message}");
        }
#elif MACCATALYST
        try
        {
            var content = new UserNotifications.UNMutableNotificationContent
            {
                Title = title,
                Body = message,
                Sound = UserNotifications.UNNotificationSound.Default
            };

            var trigger = UserNotifications.UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
            var request = UserNotifications.UNNotificationRequest.FromIdentifier(
                Guid.NewGuid().ToString(), content, trigger);

            UserNotifications.UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
            {
                if (error != null)
                    System.Diagnostics.Debug.WriteLine($"[Notification] macOS notification error: {error.LocalizedDescription}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] macOS notification failed: {ex.Message}");
            Console.WriteLine($"[Notification] {title}: {message}");
        }
#else
        // Fallback for unsupported platforms
        System.Diagnostics.Debug.WriteLine($"[Notification] {title}: {message}");
#endif
    }

    /// <summary>
    /// Request notification permission on macOS. Call once at app startup.
    /// </summary>
    public void RequestPermission()
    {
#if MACCATALYST
        UserNotifications.UNUserNotificationCenter.Current.RequestAuthorization(
            UserNotifications.UNAuthorizationOptions.Alert |
            UserNotifications.UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Notification] macOS permission: granted={granted}, error={error?.LocalizedDescription}");
            });
#endif
    }

    /// <summary>Notify that an agent run completed successfully.</summary>
    public void NotifyCompletion(string agentName)
        => Notify("Purfle Agent Complete", $"{agentName} finished successfully.");

    /// <summary>Notify that an agent run errored.</summary>
    public void NotifyError(string agentName, string errorMessage)
        => Notify("Purfle Agent Error", $"{agentName}: {errorMessage}");
}
