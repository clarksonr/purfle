using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using Purfle.App.Services;

namespace Purfle.App.Pages;

[QueryProperty(nameof(AgentId), "agentId")]
public partial class AgentDetailPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly AgentStore _store;

    public string AgentId { get; set; } = "";

    private JsonElement _root;
    private string _agentDir = "";
    private string _outputDir = "";

    public AgentDetailPage(MarketplaceService marketplace, AgentStore store)
    {
        InitializeComponent();
        _marketplace = marketplace;
        _store = store;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadAgent();
    }

    private void LoadAgent()
    {
        if (string.IsNullOrEmpty(AgentId)) return;

        var installed = _store.ListInstalled().FirstOrDefault(a => a.AgentId == AgentId);
        if (installed is null)
        {
            AgentName.Text = AgentId;
            DescriptionLabel.Text = "Agent not installed.";
            return;
        }

        try
        {
            var json = File.ReadAllText(installed.ManifestPath);
            _root = JsonDocument.Parse(json).RootElement;
            _agentDir = Path.GetDirectoryName(installed.ManifestPath)!;
            _outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "aivm", "output", AgentId);

            AgentName.Text = installed.Name;
            VersionLabel.Text = $"v{installed.Version}";
            DescriptionLabel.Text = _root.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";

            if (_root.TryGetProperty("identity", out var id))
                AuthorLabel.Text = $"by {id.GetProperty("author").GetString() ?? "Unknown"}";

            if (_root.TryGetProperty("schedule", out var sched))
                ScheduleLabel.Text = FormatSchedule(sched);

            LoadOverviewTab();
        }
        catch (Exception ex)
        {
            DescriptionLabel.Text = $"Error: {ex.Message}";
        }
    }

    // --- Tab switching ---

    private void SelectTab(string tab)
    {
        OverviewPanel.IsVisible = tab == "overview";
        PermissionsPanel.IsVisible = tab == "permissions";
        FilesPanel.IsVisible = tab == "files";
        HistoryPanel.IsVisible = tab == "history";
        SystemMdPanel.IsVisible = tab == "system";
        UsagePanel.IsVisible = tab == "usage";
        InstallPanel.IsVisible = tab == "install";

        TabOverview.TextColor = tab == "overview" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabPermissions.TextColor = tab == "permissions" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabFiles.TextColor = tab == "files" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabHistory.TextColor = tab == "history" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabSystem.TextColor = tab == "system" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabUsage.TextColor = tab == "usage" ? Color.FromArgb("#5B5EA6") : Colors.Gray;
        TabInstall.TextColor = tab == "install" ? Color.FromArgb("#5B5EA6") : Colors.Gray;

        TabOverview.FontAttributes = tab == "overview" ? FontAttributes.Bold : FontAttributes.None;
        TabPermissions.FontAttributes = tab == "permissions" ? FontAttributes.Bold : FontAttributes.None;
        TabFiles.FontAttributes = tab == "files" ? FontAttributes.Bold : FontAttributes.None;
        TabHistory.FontAttributes = tab == "history" ? FontAttributes.Bold : FontAttributes.None;
        TabSystem.FontAttributes = tab == "system" ? FontAttributes.Bold : FontAttributes.None;
        TabUsage.FontAttributes = tab == "usage" ? FontAttributes.Bold : FontAttributes.None;
        TabInstall.FontAttributes = tab == "install" ? FontAttributes.Bold : FontAttributes.None;
    }

    private void OnTabOverview(object? s, EventArgs e) { SelectTab("overview"); LoadOverviewTab(); }
    private void OnTabPermissions(object? s, EventArgs e) { SelectTab("permissions"); LoadPermissionsTab(); }
    private void OnTabFiles(object? s, EventArgs e) { SelectTab("files"); LoadFilesTab(); }
    private void OnTabHistory(object? s, EventArgs e) { SelectTab("history"); LoadHistoryTab(); }
    private void OnTabSystem(object? s, EventArgs e) { SelectTab("system"); LoadSystemMdTab(); }
    private void OnTabUsage(object? s, EventArgs e) { SelectTab("usage"); LoadUsageTab(); }
    private void OnTabInstall(object? s, EventArgs e) { SelectTab("install"); LoadInstallTab(); }

    // --- Overview tab ---

    private void LoadOverviewTab()
    {
        if (_root.TryGetProperty("runtime", out var rt))
        {
            var engine = rt.TryGetProperty("engine", out var eng) ? eng.GetString() ?? "unknown" : "unknown";
            var model = rt.TryGetProperty("model", out var mod) ? mod.GetString() ?? "default" : "default";
            EngineLabel.Text = $"{engine} / {model}";
        }

        OutputPathLabel.Text = _outputDir;

        // Token stats from run.jsonl
        long lastIn = 0, lastOut = 0, todayIn = 0, todayOut = 0, allIn = 0, allOut = 0;
        string lastRun = "Never", nextRun = "Unknown";
        var jsonlPath = Path.Combine(_outputDir, "run.jsonl");

        if (File.Exists(jsonlPath))
        {
            var today = DateTime.UtcNow.Date;
            foreach (var line in File.ReadLines(jsonlPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonDocument.Parse(line).RootElement;
                    var inTok = entry.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
                    var outTok = entry.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
                    allIn += inTok; allOut += outTok;

                    if (entry.TryGetProperty("trigger_time", out var tt))
                    {
                        lastRun = tt.GetString() ?? "Unknown";
                        if (DateTime.TryParse(lastRun, out var dt) && dt.Date == today)
                        { todayIn += inTok; todayOut += outTok; }
                    }

                    lastIn = inTok; lastOut = outTok;
                }
                catch { }
            }
        }

        TokenLastRunLabel.Text = $"Last run: {lastIn} in / {lastOut} out";
        TokenTodayLabel.Text = $"Today: {todayIn} in / {todayOut} out";
        TokenAllTimeLabel.Text = $"All time: {allIn} in / {allOut} out";
        LastRunLabel.Text = $"Last: {lastRun}";
        NextRunLabel.Text = $"Next: {nextRun}";
    }

    // --- Permissions tab ---

    private void LoadPermissionsTab()
    {
        CapabilitiesStack.Children.Clear();
        McpStack.Children.Clear();

        if (_root.TryGetProperty("capabilities", out var caps))
        {
            var perms = _root.TryGetProperty("permissions", out var p) ? p : default;
            foreach (var cap in caps.EnumerateArray())
            {
                var capStr = cap.GetString() ?? "";
                var description = TranslateCapability(capStr, perms);
                CapabilitiesStack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "\u2022", FontSize = 14, VerticalOptions = LayoutOptions.Center },
                        new Label { Text = $"{capStr}", FontSize = 13, FontAttributes = FontAttributes.Bold,
                                    VerticalOptions = LayoutOptions.Center },
                        new Label { Text = $"\u2192 {description}", FontSize = 13, TextColor = Colors.Gray,
                                    VerticalOptions = LayoutOptions.Center },
                    }
                });
            }
        }

        if (_root.TryGetProperty("tools", out var tools))
        {
            foreach (var tool in tools.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString() ?? "";
                var server = tool.TryGetProperty("server", out var s) ? s.GetString() ?? "" : "";
                McpStack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4,
                                      Color = Colors.Gray, VerticalOptions = LayoutOptions.Center },
                        new Label { Text = name, FontSize = 13, FontAttributes = FontAttributes.Bold,
                                    VerticalOptions = LayoutOptions.Center },
                        new Label { Text = server, FontSize = 12, TextColor = Colors.Gray,
                                    VerticalOptions = LayoutOptions.Center },
                    }
                });
            }
        }
    }

    // --- Files tab ---

    private void LoadFilesTab()
    {
        ReadPathsStack.Children.Clear();
        WritePathsStack.Children.Clear();
        RecentFilesStack.Children.Clear();

        if (_root.TryGetProperty("permissions", out var perms))
        {
            if (perms.TryGetProperty("fs.read", out var fsRead) &&
                fsRead.TryGetProperty("paths", out var readPaths))
            {
                foreach (var p in readPaths.EnumerateArray())
                {
                    var path = p.GetString() ?? "";
                    var exists = Directory.Exists(path) || File.Exists(path);
                    ReadPathsStack.Children.Add(new Label
                    {
                        Text = $"{path} {(exists ? "(exists)" : "(not found)")}",
                        FontSize = 13,
                        TextColor = exists ? Colors.Gray : Colors.Orange,
                    });
                }
            }

            if (perms.TryGetProperty("fs.write", out var fsWrite) &&
                fsWrite.TryGetProperty("paths", out var writePaths))
            {
                foreach (var p in writePaths.EnumerateArray())
                {
                    var path = p.GetString() ?? "";
                    var exists = Directory.Exists(path);
                    long sizeKb = 0;
                    if (exists)
                    {
                        try { sizeKb = new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024; }
                        catch { }
                    }
                    WritePathsStack.Children.Add(new Label
                    {
                        Text = $"{path} {(exists ? $"({sizeKb} KB)" : "(not found)")}",
                        FontSize = 13,
                        TextColor = exists ? Colors.Gray : Colors.Orange,
                    });
                }
            }
        }

        // Recent output files
        if (Directory.Exists(_outputDir))
        {
            var files = new DirectoryInfo(_outputDir)
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(5);

            foreach (var file in files)
            {
                var label = new Label
                {
                    Text = $"{file.Name}  ({file.Length / 1024} KB, {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm})",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5B5EA6"),
                    TextDecorations = TextDecorations.Underline,
                };
                var filePath = file.FullName;
                label.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                        await Shell.Current.GoToAsync($"LogViewPage?outputPath={Uri.EscapeDataString(_outputDir)}")),
                });
                RecentFilesStack.Children.Add(label);
            }

            if (!files.Any())
                RecentFilesStack.Children.Add(new Label { Text = "No output files yet.", FontSize = 13, TextColor = Colors.Gray });
        }
        else
        {
            RecentFilesStack.Children.Add(new Label { Text = "Output directory does not exist yet.", FontSize = 13, TextColor = Colors.Gray });
        }
    }

    // --- Run History tab ---

    private void LoadHistoryTab()
    {
        var entries = new List<RunHistoryEntry>();
        var jsonlPath = Path.Combine(_outputDir, "run.jsonl");

        if (File.Exists(jsonlPath))
        {
            foreach (var line in File.ReadLines(jsonlPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonDocument.Parse(line).RootElement;
                    entries.Add(new RunHistoryEntry
                    {
                        Timestamp = entry.TryGetProperty("trigger_time", out var t) ? t.GetString() ?? "" : "",
                        DurationMs = entry.TryGetProperty("duration_ms", out var d) ? d.GetInt64() : 0,
                        Status = entry.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                        InputTokens = entry.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0,
                        OutputTokens = entry.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0,
                        OutputPath = _outputDir,
                    });
                }
                catch { }
            }
        }

        entries.Reverse();
        var last20 = entries.Take(20).ToList();
        HistoryList.ItemsSource = new ObservableCollection<RunHistoryEntry>(last20);

        if (entries.Count > 0)
        {
            var successCount = entries.Count(e => e.Status == "success");
            var avgDuration = entries.Average(e => e.DurationMs);
            var avgTokens = entries.Average(e => e.InputTokens + e.OutputTokens);
            HistoryStatsLabel.Text = $"{entries.Count} total runs  |  " +
                $"Success rate: {successCount * 100 / entries.Count}%  |  " +
                $"Avg duration: {avgDuration:F0}ms  |  " +
                $"Avg tokens: {avgTokens:F0}";
        }
        else
        {
            HistoryStatsLabel.Text = "No runs recorded yet.";
        }
    }

    // --- System.md tab ---

    private void LoadSystemMdTab()
    {
        var systemMdPath = Path.Combine(_agentDir, "prompts", "system.md");
        if (File.Exists(systemMdPath))
            SystemMdEditor.Text = File.ReadAllText(systemMdPath);
        else
            SystemMdEditor.Text = "(No system.md found in this agent's prompts directory)";
    }

    // --- Usage tab ---

    private void LoadUsageTab()
    {
        var usagePath = Path.Combine(_outputDir, "usage.jsonl");
        if (!File.Exists(usagePath))
        {
            UsageSummaryLabel.Text = "No usage recorded yet. This agent hasn't run.";
            UsageList.ItemsSource = null;
            return;
        }

        var entries = new List<UsageEntry>();
        long totalPrompt = 0, totalCompletion = 0;

        foreach (var line in File.ReadLines(usagePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var doc = JsonDocument.Parse(line).RootElement;
                var entry = new UsageEntry
                {
                    Timestamp = doc.TryGetProperty("ts", out var ts) ? ts.GetString() ?? "" : "",
                    Engine = doc.TryGetProperty("engine", out var eng) ? eng.GetString() ?? "" : "",
                    Model = doc.TryGetProperty("model", out var mod) ? mod.GetString() ?? "" : "",
                    PromptTokens = doc.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                    CompletionTokens = doc.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0,
                    TotalTokens = doc.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0,
                };
                entries.Add(entry);
                totalPrompt += entry.PromptTokens;
                totalCompletion += entry.CompletionTokens;
            }
            catch { }
        }

        entries.Reverse();
        UsageList.ItemsSource = new ObservableCollection<UsageEntry>(entries);

        var totalTotal = totalPrompt + totalCompletion;
        var estCost = entries.Sum(e => CostConstants.EstimateCost(e.Engine, e.Model, e.PromptTokens, e.CompletionTokens));
        UsageSummaryLabel.Text = $"All time: {totalPrompt:N0} prompt + {totalCompletion:N0} completion = {totalTotal:N0} total. Estimated: ${estCost:F4}";
    }

    // --- Actions ---

    private async void OnRunNow(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"AgentRunPage?agentId={Uri.EscapeDataString(AgentId)}");
    }

    private void OnPause(object? sender, EventArgs e)
    {
        var isPaused = Preferences.Get($"paused_{AgentId}", false);
        Preferences.Set($"paused_{AgentId}", !isPaused);
        PauseButton.Text = isPaused ? "Pause" : "Resume";
    }

    private async void OnReviewPermissions(object? sender, EventArgs e)
    {
        var manifestPath = Path.Combine(_agentDir, AgentStore.ManifestFileName);
        await Shell.Current.GoToAsync($"ConsentPage?manifestPath={Uri.EscapeDataString(manifestPath)}");
    }

    private async void OnUninstall(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Uninstall",
            $"Remove agent '{AgentName.Text}'? This cannot be undone.", "Uninstall", "Cancel");
        if (!confirm) return;

        _store.Uninstall(AgentId);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnOpenOutputFolder(object? sender, EventArgs e)
    {
        if (!Directory.Exists(_outputDir))
        {
            await DisplayAlertAsync("Not Found", "Output directory does not exist yet.", "OK");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = _outputDir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnRecheckMcp(object? sender, EventArgs e)
    {
        if (!_root.TryGetProperty("tools", out var tools)) return;

        McpStack.Children.Clear();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString() ?? "";
            var server = tool.TryGetProperty("server", out var s) ? s.GetString() ?? "" : "";

            var isRunning = false;
            if (!string.IsNullOrEmpty(server))
            {
                try
                {
                    var resp = await http.GetAsync(server);
                    isRunning = true;
                }
                catch { }
            }

            McpStack.Children.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4,
                                  Color = isRunning ? Colors.Green : Colors.Red,
                                  VerticalOptions = LayoutOptions.Center },
                    new Label { Text = name, FontSize = 13, FontAttributes = FontAttributes.Bold,
                                VerticalOptions = LayoutOptions.Center },
                    new Label { Text = $"{server} ({(isRunning ? "running" : "not running")})",
                                FontSize = 12, TextColor = Colors.Gray,
                                VerticalOptions = LayoutOptions.Center },
                }
            });
        }
    }

    // --- Install tab ---

    private async void LoadInstallTab()
    {
        // Reset state
        InstallSuccessBanner.IsVisible = false;
        InstallErrorBorder.IsVisible = false;
        InstallLogBorder.IsVisible = false;
        InstallOfflineBorder.IsVisible = false;
        InstallButton.IsEnabled = true;

        try
        {
            var detail = await _marketplace.GetAgentAsync(AgentId);
            if (detail is null)
            {
                InstallOfflineBorder.IsVisible = true;
                InstallOfflineLabel.Text = "Agent not found in the Marketplace.";
                InstallButton.IsEnabled = false;
                return;
            }

            InstallAgentName.Text = detail.Name;
            var latest = detail.Versions.Count > 0 ? detail.Versions[0] : null;
            InstallVersion.Text = latest is not null ? $"Version: {latest.Version}" : "No versions";
            InstallAuthor.Text = $"Author: {detail.PublisherName}";
            InstallDescription.Text = detail.Description;
            InstallHash.Text = latest?.BundleHash is { Length: > 0 } hash
                ? $"SHA-256: {hash}" : "";
        }
        catch (HttpRequestException)
        {
            InstallOfflineBorder.IsVisible = true;
            InstallButton.IsEnabled = false;
        }
        catch (TaskCanceledException)
        {
            InstallOfflineBorder.IsVisible = true;
            InstallButton.IsEnabled = false;
        }
    }

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        InstallButton.IsEnabled = false;
        InstallProgress.IsRunning = true;
        InstallProgress.IsVisible = true;
        InstallLogBorder.IsVisible = true;
        InstallLogEditor.Text = "";
        InstallSuccessBanner.IsVisible = false;
        InstallErrorBorder.IsVisible = false;

        try
        {
            var purfleCliPath = FindPurfleCli();
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = purfleCliPath,
                Arguments = $"install {AgentId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            var output = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is null) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    output.AppendLine(args.Data);
                    InstallLogEditor.Text = output.ToString();
                });
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is null) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    output.AppendLine(args.Data);
                    InstallLogEditor.Text = output.ToString();
                });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            InstallProgress.IsRunning = false;
            InstallProgress.IsVisible = false;

            if (process.ExitCode == 0)
            {
                InstallSuccessBanner.IsVisible = true;
                // Reload agent list
                LoadAgent();
            }
            else
            {
                InstallErrorBorder.IsVisible = true;
                InstallErrorLabel.Text = output.ToString();
                InstallButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            InstallProgress.IsRunning = false;
            InstallProgress.IsVisible = false;
            InstallErrorBorder.IsVisible = true;
            InstallErrorLabel.Text = ex.Message;
            InstallButton.IsEnabled = true;
        }
    }

    private static string FindPurfleCli()
    {
        // Look for purfle CLI in PATH or common locations
        if (OperatingSystem.IsWindows())
        {
            var npmGlobal = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm", "purfle.cmd");
            if (File.Exists(npmGlobal)) return npmGlobal;
        }

        // Default: assume it's on PATH
        return "purfle";
    }

    // --- Helpers ---

    private static string FormatSchedule(JsonElement schedule)
    {
        var trigger = schedule.GetProperty("trigger").GetString();
        return trigger switch
        {
            "interval" when schedule.TryGetProperty("interval_minutes", out var mins)
                => $"Runs every {mins.GetInt32()} minutes",
            "cron" when schedule.TryGetProperty("cron", out var cron)
                => $"Runs on schedule: {cron.GetString()}",
            "startup" => "Runs once at startup",
            _ => trigger ?? "Unknown schedule",
        };
    }

    private static string TranslateCapability(string capability, JsonElement permissions)
    {
        return capability switch
        {
            "llm.chat" => "Use AI inference (costs tokens)",
            "llm.completion" => "Use single-turn AI completion (costs tokens)",
            "network.outbound" when TryGetArray(permissions, "network.outbound", "hosts", out var h)
                => $"Connect to: {h}",
            "network.outbound" => "Make outbound network connections",
            "fs.read" when TryGetArray(permissions, "fs.read", "paths", out var p)
                => $"Read files in: {p}",
            "fs.read" => "Read files",
            "fs.write" when TryGetArray(permissions, "fs.write", "paths", out var p)
                => $"Write files to: {p}",
            "fs.write" => "Write files",
            "env.read" when TryGetArray(permissions, "env.read", "vars", out var v)
                => $"Read environment variables: {v}",
            "env.read" => "Read environment variables",
            "mcp.tool" => "Invoke MCP tool bindings",
            _ => capability,
        };
    }

    private static bool TryGetArray(JsonElement permissions, string permKey, string arrayKey, out string result)
    {
        result = "";
        if (permissions.ValueKind == JsonValueKind.Undefined) return false;
        if (!permissions.TryGetProperty(permKey, out var perm)) return false;
        if (!perm.TryGetProperty(arrayKey, out var arr)) return false;
        result = string.Join(", ", arr.EnumerateArray().Select(x => x.GetString()));
        return true;
    }
}

public sealed class RunHistoryEntry : INotifyPropertyChanged
{
    public string Timestamp { get; init; } = "";
    public long DurationMs { get; init; }
    public string Status { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string OutputPath { get; init; } = "";

    public Color StatusColor => Status == "error" ? Colors.Red : Colors.Green;
    public string DurationText => DurationMs > 0 ? $"{DurationMs}ms" : "";
    public string TokenText => InputTokens > 0 || OutputTokens > 0
        ? $"{InputTokens} in / {OutputTokens} out" : "";

    public ICommand ViewCommand { get; }

    public RunHistoryEntry()
    {
        ViewCommand = new Command(async () =>
            await Shell.Current.GoToAsync($"LogViewPage?outputPath={Uri.EscapeDataString(OutputPath)}"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class UsageEntry
{
    public string Timestamp { get; init; } = "";
    public string Engine { get; init; } = "";
    public string Model { get; init; } = "";
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }

    public string DateFormatted => DateTimeOffset.TryParse(Timestamp, out var dto)
        ? dto.LocalDateTime.ToString("MMM d HH:mm") : Timestamp;
    public string PromptTokensText => $"{PromptTokens:N0}";
    public string CompletionTokensText => $"{CompletionTokens:N0}";
    public string TotalTokensText => $"{TotalTokens:N0}";
    public string EstCost
    {
        get
        {
            var cost = CostConstants.EstimateCost(Engine, Model, PromptTokens, CompletionTokens);
            return cost > 0 ? $"${cost:F4}" : "—";
        }
    }
}

/// <summary>
/// Per-token cost rates for supported engines and models.
/// Rates sourced as of 2026-04-01 from published pricing pages.
/// </summary>
public static class CostConstants
{
    // (promptCostPerMillion, completionCostPerMillion)
    private static readonly Dictionary<string, (decimal prompt, decimal completion)> s_rates = new(StringComparer.OrdinalIgnoreCase)
    {
        // Gemini — rates from ai.google.dev/pricing (2026-04)
        ["gemini/gemini-2.0-flash"] = (0.10m, 0.40m),
        ["gemini/gemini-2.5-flash"] = (0.15m, 0.60m),
        ["gemini/gemini-2.0-pro"] = (1.25m, 5.00m),

        // Anthropic — rates from anthropic.com/pricing (2026-04)
        ["anthropic/claude-sonnet-4-20250514"] = (3.00m, 15.00m),
        ["anthropic/claude-sonnet-4-6"] = (3.00m, 15.00m),
        ["anthropic/claude-haiku-4-5-20251001"] = (0.80m, 4.00m),

        // OpenAI — rates from openai.com/pricing (2026-04)
        ["openai/gpt-4o"] = (2.50m, 10.00m),
        ["openai/gpt-4o-mini"] = (0.15m, 0.60m),

        // Ollama — local, free
        ["ollama/llama3"] = (0m, 0m),
    };

    public static decimal EstimateCost(string engine, string model, int promptTokens, int completionTokens)
    {
        var key = $"{engine}/{model}";
        if (s_rates.TryGetValue(key, out var rates))
        {
            return (promptTokens * rates.prompt / 1_000_000m) +
                   (completionTokens * rates.completion / 1_000_000m);
        }
        // Try engine-only match for common models
        if (engine.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            return 0m;
        return -1m; // unknown
    }
}
