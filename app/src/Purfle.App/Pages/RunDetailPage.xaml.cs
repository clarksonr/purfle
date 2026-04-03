namespace Purfle.App.Pages;

public partial class RunDetailPage : ContentPage
{
    private string _agentId = "";
    private string _agentName = "";
    private RunHistoryItem? _item;

    public RunDetailPage()
    {
        InitializeComponent();
    }

    public void Initialize(string agentId, string agentName, RunHistoryItem item)
    {
        _agentId = agentId;
        _agentName = agentName;
        _item = item;

        RunAgentLabel.Text = agentName;

        // Status
        var isSuccess = item.Status == "success";
        RunStatusDot.Color = isSuccess ? Colors.Green : Colors.Red;
        RunStatusLabel.Text = isSuccess ? "SUCCESS" : "ERROR";
        RunStatusLabel.TextColor = isSuccess ? Color.FromArgb("#4CAF50") : Color.FromArgb("#CC0000");

        // Metadata
        if (DateTimeOffset.TryParse(item.TriggerTime, out var started))
        {
            StartedLabel.Text = started.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            var ended = started.AddMilliseconds(item.DurationMs);
            EndedLabel.Text = ended.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            StartedLabel.Text = item.TriggerTime;
            EndedLabel.Text = "—";
        }

        DurationLabel.Text = $"{item.DurationMs / 1000.0:F1}s ({item.DurationMs}ms)";
        TokensLabel.Text = $"Prompt: {item.InputTokens} · Completion: {item.OutputTokens} · Total: {item.InputTokens + item.OutputTokens}";
        RunIdLabel.Text = item.TriggerTime;

        // Error
        if (!string.IsNullOrEmpty(item.Error))
        {
            ErrorSection.IsVisible = true;
            ErrorLabel.Text = item.Error;
        }

        // Load output
        LoadOutput();
    }

    private void LoadOutput()
    {
        if (_item == null) return;

        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aivm", "output", _agentId);
        var logPath = Path.Combine(outputDir, "run.log");

        if (File.Exists(logPath))
        {
            try
            {
                var content = File.ReadAllText(logPath);
                // Find the entry matching this run's trigger time
                var entries = content.Split("=== ", StringSplitOptions.RemoveEmptyEntries);
                var matchingEntry = entries
                    .FirstOrDefault(e => e.Contains(_item.TriggerTime));

                if (matchingEntry != null)
                {
                    var newlineIdx = matchingEntry.IndexOf(Environment.NewLine, StringComparison.Ordinal);
                    if (newlineIdx >= 0)
                        OutputEditor.Text = matchingEntry[(newlineIdx + Environment.NewLine.Length)..].TrimEnd();
                    else
                        OutputEditor.Text = matchingEntry.TrimEnd();
                }
                else
                {
                    // Fall back to showing the most recent entry
                    var last = entries.LastOrDefault();
                    if (last != null)
                    {
                        var nl = last.IndexOf(Environment.NewLine, StringComparison.Ordinal);
                        OutputEditor.Text = nl >= 0 ? last[(nl + Environment.NewLine.Length)..].TrimEnd() : last.TrimEnd();
                    }
                    else
                    {
                        OutputEditor.Text = "(No output content found)";
                    }
                }
            }
            catch (Exception ex)
            {
                OutputEditor.Text = $"Error reading output: {ex.Message}";
            }
        }
        else
        {
            OutputEditor.Text = "(No output file found. Agent may not have produced output yet.)";
        }
    }

    private async void OnRetry(object? sender, EventArgs e)
    {
        // Navigate back and trigger an immediate run
        var agentDetailPage = Navigation.NavigationStack
            .OfType<AgentDetailPage>()
            .FirstOrDefault();

        if (agentDetailPage != null)
        {
            await Navigation.PopToRootAsync();
        }
        else
        {
            await Navigation.PopAsync();
        }
    }

    private async void OnOpenFolder(object? sender, EventArgs e)
    {
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aivm", "output", _agentId);

        if (Directory.Exists(outputDir))
        {
            await Launcher.OpenAsync(new Uri($"file://{outputDir}"));
        }
    }
}
