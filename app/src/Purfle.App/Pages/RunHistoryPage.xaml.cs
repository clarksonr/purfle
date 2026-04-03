using System.Collections.ObjectModel;
using System.Text.Json;

namespace Purfle.App.Pages;

public partial class RunHistoryPage : ContentPage
{
    private string _agentId = "";
    private string _agentName = "";
    private List<RunHistoryItem> _allRuns = [];
    private string _currentFilter = "all";
    private int _pageSize = 50;
    private int _loadedCount;

    public RunHistoryPage()
    {
        InitializeComponent();
    }

    public void Initialize(string agentId, string agentName)
    {
        _agentId = agentId;
        _agentName = agentName;
        AgentNameLabel.Text = $"{agentName} — Run History";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRunsAsync();
    }

    private async Task LoadRunsAsync()
    {
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aivm", "output", _agentId);
        var jsonlPath = Path.Combine(outputDir, "run.jsonl");

        _allRuns.Clear();
        if (!File.Exists(jsonlPath))
        {
            RunCountLabel.Text = "No runs recorded yet.";
            RunList.ItemsSource = null;
            return;
        }

        var lines = await File.ReadAllLinesAsync(jsonlPath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var item = new RunHistoryItem
                {
                    AgentId = _agentId,
                    TriggerTime = root.TryGetProperty("trigger_time", out var tt) ? tt.GetString() ?? "" : "",
                    DurationMs = root.TryGetProperty("duration_ms", out var dm) ? dm.GetInt64() : 0,
                    Status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "unknown" : "unknown",
                    InputTokens = root.TryGetProperty("input_tokens", out var it2) ? it2.GetInt32() : 0,
                    OutputTokens = root.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0,
                    OutputPath = root.TryGetProperty("output_path", out var op) ? op.GetString() ?? "" : "",
                    Error = root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String
                        ? err.GetString() : null,
                    RawJson = line,
                };
                _allRuns.Add(item);
            }
            catch (JsonException) { }
        }

        _allRuns.Reverse(); // newest first
        _loadedCount = 0;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _currentFilter switch
        {
            "success" => _allRuns.Where(r => r.Status == "success").ToList(),
            "error" => _allRuns.Where(r => r.Status == "error").ToList(),
            _ => _allRuns,
        };

        _loadedCount = Math.Min(_pageSize, filtered.Count);
        RunList.ItemsSource = new ObservableCollection<RunHistoryItem>(filtered.Take(_loadedCount));
        RunCountLabel.Text = $"{filtered.Count} runs total";
        LoadMoreButton.IsVisible = _loadedCount < filtered.Count;
    }

    private void OnFilterAll(object? sender, EventArgs e) { SetFilter("all"); }
    private void OnFilterSuccess(object? sender, EventArgs e) { SetFilter("success"); }
    private void OnFilterError(object? sender, EventArgs e) { SetFilter("error"); }

    private void SetFilter(string filter)
    {
        _currentFilter = filter;
        FilterAll.BackgroundColor = filter == "all" ? Color.FromArgb("#5B5EA6") : Colors.Transparent;
        FilterAll.TextColor = filter == "all" ? Colors.White : Color.FromArgb("#5B5EA6");
        FilterSuccess.BackgroundColor = filter == "success" ? Color.FromArgb("#4CAF50") : Colors.Transparent;
        FilterSuccess.TextColor = filter == "success" ? Colors.White : Color.FromArgb("#4CAF50");
        FilterError.BackgroundColor = filter == "error" ? Color.FromArgb("#CC0000") : Colors.Transparent;
        FilterError.TextColor = filter == "error" ? Colors.White : Color.FromArgb("#CC0000");
        _loadedCount = 0;
        ApplyFilter();
    }

    private void OnLoadMore(object? sender, EventArgs e)
    {
        var filtered = _currentFilter switch
        {
            "success" => _allRuns.Where(r => r.Status == "success").ToList(),
            "error" => _allRuns.Where(r => r.Status == "error").ToList(),
            _ => _allRuns,
        };

        _loadedCount = Math.Min(_loadedCount + _pageSize, filtered.Count);
        RunList.ItemsSource = new ObservableCollection<RunHistoryItem>(filtered.Take(_loadedCount));
        LoadMoreButton.IsVisible = _loadedCount < filtered.Count;
    }

    private async void OnViewRun(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is RunHistoryItem item)
        {
            var detailPage = new RunDetailPage();
            detailPage.Initialize(_agentId, _agentName, item);
            await Navigation.PushAsync(detailPage);
        }
    }
}

public class RunHistoryItem
{
    public string AgentId { get; init; } = "";
    public string TriggerTime { get; init; } = "";
    public long DurationMs { get; init; }
    public string Status { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string OutputPath { get; init; } = "";
    public string? Error { get; init; }
    public string RawJson { get; init; } = "";

    public Color StatusColor => Status == "success" ? Colors.Green : Colors.Red;
    public string StatusTextColor => Status == "success" ? "#4CAF50" : "#CC0000";
    public string StatusText => Status == "success" ? "SUCCESS" : "ERROR";
    public string DurationText => $"{DurationMs / 1000.0:F1}s";

    public string TimestampFormatted
    {
        get
        {
            if (DateTimeOffset.TryParse(TriggerTime, out var dto))
            {
                var local = dto.LocalDateTime;
                return local.ToString("MMM d · h:mm tt");
            }
            return TriggerTime;
        }
    }

    public string OutputPreview
    {
        get
        {
            if (!string.IsNullOrEmpty(Error)) return Error.Length > 80 ? Error[..80] + "…" : Error;
            return $"Tokens: {InputTokens + OutputTokens} · Duration: {DurationText}";
        }
    }
}
