using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;

namespace Purfle.App.Pages;

[QueryProperty(nameof(OutputPath), "outputPath")]
public partial class LogViewPage : ContentPage
{
    public string? OutputPath { get; set; }

    private List<LogEntryViewModel> _allEntries = [];
    private string _filter = "all"; // "all" | "success" | "error"

    public LogViewPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLogAsync();
    }

    private async void OnRefresh(object? sender, EventArgs e) => await LoadLogAsync();

    private void OnFilterAll(object? sender, EventArgs e) { _filter = "all"; ApplyFilter(); }
    private void OnFilterSuccess(object? sender, EventArgs e) { _filter = "success"; ApplyFilter(); }
    private void OnFilterError(object? sender, EventArgs e) { _filter = "error"; ApplyFilter(); }

    private void ApplyFilter()
    {
        var filtered = _filter switch
        {
            "success" => _allEntries.Where(e => e.Status == "success").ToList(),
            "error"   => _allEntries.Where(e => e.Status == "error").ToList(),
            _         => _allEntries,
        };
        LogEntries.ItemsSource = new ObservableCollection<LogEntryViewModel>(filtered);
        CountLabel.Text = $"{filtered.Count} of {_allEntries.Count} entries";
    }

    private async Task LoadLogAsync()
    {
        if (string.IsNullOrEmpty(OutputPath)) return;

        _allEntries = [];

        // Try structured JSONL first
        var jsonlPath = Path.Combine(OutputPath, "run.jsonl");
        if (File.Exists(jsonlPath))
        {
            var lines = await File.ReadAllLinesAsync(jsonlPath);
            foreach (var line in lines.Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<JsonElement>(line);
                    _allEntries.Add(new LogEntryViewModel
                    {
                        Timestamp = entry.TryGetProperty("trigger_time", out var t) ? t.GetString() ?? "" : "",
                        DurationMs = entry.TryGetProperty("duration_ms", out var d) ? d.GetInt64() : 0,
                        Status = entry.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                        InputTokens = entry.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0,
                        OutputTokens = entry.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0,
                        Error = entry.TryGetProperty("error", out var e) ? e.GetString() : null,
                        RawJson = line,
                    });
                }
                catch { /* skip malformed lines */ }
            }
        }

        // Fall back to plain text log
        if (_allEntries.Count == 0)
        {
            var logPath = Path.Combine(OutputPath, "run.log");
            if (File.Exists(logPath))
            {
                var text = await File.ReadAllTextAsync(logPath);
                var entries = text.Split("===", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < entries.Length - 1; i += 2)
                {
                    var timestamp = entries[i].Trim();
                    var body = i + 1 < entries.Length ? entries[i + 1].Trim() : "";
                    _allEntries.Add(new LogEntryViewModel
                    {
                        Timestamp = timestamp,
                        Status = body.StartsWith("ERROR") ? "error" : "success",
                        Detail = body,
                        RawJson = $"{timestamp}\n{body}",
                    });
                }
                _allEntries.Reverse();
            }
        }

        ApplyFilter();
    }
}

public sealed class LogEntryViewModel : INotifyPropertyChanged
{
    public string Timestamp { get; init; } = "";
    public long DurationMs { get; init; }
    public string Status { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string? Error { get; init; }
    public string? Detail { get; set; }
    public string RawJson { get; init; } = "";

    private bool _isExpanded;

    public Color StatusColor => Status == "error" ? Colors.Red : Colors.Green;
    public string DurationText => DurationMs > 0 ? $"{DurationMs}ms" : "";
    public bool HasTokens => InputTokens > 0 || OutputTokens > 0;
    public string TokenText => $"Tokens: {InputTokens} in / {OutputTokens} out";
    public bool IsExpanded => _isExpanded;

    public ICommand ToggleCommand { get; }
    public ICommand CopyCommand { get; }

    public LogEntryViewModel()
    {
        ToggleCommand = new Command(() =>
        {
            _isExpanded = !_isExpanded;
            if (_isExpanded && string.IsNullOrEmpty(Detail))
                Detail = Error ?? RawJson;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        });
        CopyCommand = new Command(async () =>
        {
            await Clipboard.Default.SetTextAsync(RawJson);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
