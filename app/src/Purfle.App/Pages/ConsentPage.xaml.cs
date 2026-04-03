using System.Text.Json;
using Purfle.App.Services;

namespace Purfle.App.Pages;

[QueryProperty(nameof(ManifestPath), "manifestPath")]
[QueryProperty(nameof(ManifestJson), "manifestJson")]
public partial class ConsentPage : ContentPage
{
    private readonly AgentStore _store;
    private JsonDocument? _manifest;
    private string? _rawJson;

    public string ManifestPath { get; set; } = "";
    public string ManifestJson { get; set; } = "";

    public ConsentPage(AgentStore store)
    {
        InitializeComponent();
        _store = store;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadManifest();
    }

    private void LoadManifest()
    {
        try
        {
            if (!string.IsNullOrEmpty(ManifestPath) && File.Exists(ManifestPath))
                _rawJson = File.ReadAllText(ManifestPath);
            else if (!string.IsNullOrEmpty(ManifestJson))
                _rawJson = Uri.UnescapeDataString(ManifestJson);

            if (string.IsNullOrEmpty(_rawJson)) return;

            _manifest = JsonDocument.Parse(_rawJson);
            var root = _manifest.RootElement;

            AgentNameLabel.Text = root.GetProperty("name").GetString() ?? "Unknown Agent";
            VersionLabel.Text = $"v{root.GetProperty("version").GetString() ?? "0.0.0"}";
            DescriptionLabel.Text = root.TryGetProperty("description", out var desc)
                ? desc.GetString() ?? "" : "";

            // Author
            if (root.TryGetProperty("identity", out var identity))
            {
                AuthorLabel.Text = $"by {identity.GetProperty("author").GetString() ?? "Unknown"}";
                ParseSignature(identity);
            }

            // Schedule
            if (root.TryGetProperty("schedule", out var schedule))
                ScheduleLabel.Text = FormatSchedule(schedule);

            // Capabilities & Permissions
            ParsePermissions(root);

            // MCP tools
            if (root.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0)
                ParseMcpServers(tools);

            // Raw manifest
            RawManifestEditor.Text = JsonSerializer.Serialize(
                root, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            DescriptionLabel.Text = $"Error loading manifest: {ex.Message}";
        }
    }

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

    private void ParseSignature(JsonElement identity)
    {
        if (identity.TryGetProperty("signature", out var sig) &&
            !string.IsNullOrEmpty(sig.GetString()))
        {
            SignatureBadge.BackgroundColor = Colors.Green;
            SignatureLabel.Text = "Signed";
            SignatureLabel.TextColor = Colors.White;
        }
        else
        {
            SignatureBadge.BackgroundColor = Colors.Orange;
            SignatureLabel.Text = "Unsigned";
            SignatureLabel.TextColor = Colors.White;
        }
    }

    private void ParsePermissions(JsonElement root)
    {
        if (!root.TryGetProperty("capabilities", out var caps)) return;

        var permissions = root.TryGetProperty("permissions", out var perms)
            ? perms : default;

        foreach (var cap in caps.EnumerateArray())
        {
            var capStr = cap.GetString() ?? "";
            var description = TranslateCapability(capStr, permissions);
            AddPermissionRow(description);
        }
    }

    private static string TranslateCapability(string capability, JsonElement permissions)
    {
        return capability switch
        {
            "llm.chat" => "Use AI inference (costs tokens)",
            "llm.completion" => "Use single-turn AI completion (costs tokens)",
            "network.outbound" when TryGetHosts(permissions, out var hosts)
                => $"Connect to: {hosts}",
            "network.outbound" => "Make outbound network connections",
            "fs.read" when TryGetPaths(permissions, "fs.read", out var paths)
                => $"Read files in: {paths}",
            "fs.read" => "Read files",
            "fs.write" when TryGetPaths(permissions, "fs.write", out var paths)
                => $"Write files to: {paths}",
            "fs.write" => "Write files",
            "env.read" when TryGetVars(permissions, out var vars)
                => $"Read environment variables: {vars}",
            "env.read" => "Read environment variables",
            "mcp.tool" => "Invoke MCP tool bindings",
            _ => capability,
        };
    }

    private static bool TryGetHosts(JsonElement permissions, out string hosts)
    {
        hosts = "";
        if (permissions.ValueKind == JsonValueKind.Undefined) return false;
        if (!permissions.TryGetProperty("network.outbound", out var net)) return false;
        if (!net.TryGetProperty("hosts", out var h)) return false;
        hosts = string.Join(", ", h.EnumerateArray().Select(x => x.GetString()));
        return true;
    }

    private static bool TryGetPaths(JsonElement permissions, string key, out string paths)
    {
        paths = "";
        if (permissions.ValueKind == JsonValueKind.Undefined) return false;
        if (!permissions.TryGetProperty(key, out var perm)) return false;
        if (!perm.TryGetProperty("paths", out var p)) return false;
        paths = string.Join(", ", p.EnumerateArray().Select(x => x.GetString()));
        return true;
    }

    private static bool TryGetVars(JsonElement permissions, out string vars)
    {
        vars = "";
        if (permissions.ValueKind == JsonValueKind.Undefined) return false;
        if (!permissions.TryGetProperty("env.read", out var env)) return false;
        if (!env.TryGetProperty("vars", out var v)) return false;
        vars = string.Join(", ", v.EnumerateArray().Select(x => x.GetString()));
        return true;
    }

    private void AddPermissionRow(string description)
    {
        PermissionsStack.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "\u2022", FontSize = 14, VerticalOptions = LayoutOptions.Center },
                new Label { Text = description, FontSize = 13, VerticalOptions = LayoutOptions.Center },
            }
        });
    }

    private void ParseMcpServers(JsonElement tools)
    {
        McpServersHeader.IsVisible = true;

        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString() ?? "Unknown";
            var server = tool.TryGetProperty("server", out var s) ? s.GetString() ?? "" : "";
            var desc = tool.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

            McpServersStack.Children.Add(new Border
            {
                Stroke = Colors.LightGray,
                Padding = new Thickness(8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = name, FontAttributes = FontAttributes.Bold, FontSize = 13 },
                        new Label { Text = server, FontSize = 12, TextColor = Colors.Gray },
                        new Label { Text = desc, FontSize = 12, TextColor = Colors.Gray },
                    }
                }
            });
        }
    }

    private async void OnAllow(object? sender, EventArgs e)
    {
        if (_manifest is null || _rawJson is null) return;

        try
        {
            var root = _manifest.RootElement;
            var agentId = root.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Manifest has no 'id' field.");

            // Mark consent as granted
            Preferences.Set($"consent_{agentId}", true);

            _store.Install(agentId, _rawJson);
            await DisplayAlertAsync("Installed", $"Agent installed and permissions granted.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnToggleRawManifest(object? sender, EventArgs e)
    {
        RawManifestEditor.IsVisible = !RawManifestEditor.IsVisible;
    }
}
