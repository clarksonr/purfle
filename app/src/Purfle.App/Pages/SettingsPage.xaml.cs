using System.Reflection;
using Purfle.App.Services;

namespace Purfle.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly AgentStore _store;

    private static readonly string s_outputBase = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "aivm", "output");

    public SettingsPage(MarketplaceService marketplace, AgentStore store)
    {
        InitializeComponent();
        _marketplace = marketplace;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadStats();
        await LoadApiKeyIndicators();
        LoadOutputSection();
        LoadNotificationPrefs();
        LoadAboutSection();
        RegistryUrlEntry.Text = _marketplace.BaseUrl;
        OllamaUrlEntry.Text = Preferences.Get("ollama_base_url", "http://localhost:11434");
        UpdateAuthStatus();
    }

    // --- API Keys ---

    private async Task LoadApiKeyIndicators()
    {
        var gemini = await SecureStorage.GetAsync("gemini_api_key");
        GeminiDot.Color = string.IsNullOrEmpty(gemini) ? Colors.Red : Colors.Green;
        if (!string.IsNullOrEmpty(gemini)) GeminiKeyEntry.Text = gemini;

        var anthropic = await SecureStorage.GetAsync("anthropic_api_key");
        AnthropicDot.Color = string.IsNullOrEmpty(anthropic) ? Colors.Red : Colors.Green;
        if (!string.IsNullOrEmpty(anthropic)) AnthropicKeyEntry.Text = anthropic;

        var openai = await SecureStorage.GetAsync("openai_api_key");
        OpenAIDot.Color = string.IsNullOrEmpty(openai) ? Colors.Red : Colors.Green;
        if (!string.IsNullOrEmpty(openai)) OpenAIKeyEntry.Text = openai;
    }

    private async void OnSaveGeminiKey(object? sender, EventArgs e)
    {
        var key = GeminiKeyEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
            SecureStorage.Remove("gemini_api_key");
        else
            await SecureStorage.SetAsync("gemini_api_key", key);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", string.IsNullOrEmpty(key) ? null : key);
        GeminiDot.Color = string.IsNullOrEmpty(key) ? Colors.Red : Colors.Green;
    }

    private async void OnSaveAnthropicKey(object? sender, EventArgs e)
    {
        var key = AnthropicKeyEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
            SecureStorage.Remove("anthropic_api_key");
        else
            await SecureStorage.SetAsync("anthropic_api_key", key);
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", string.IsNullOrEmpty(key) ? null : key);
        AnthropicDot.Color = string.IsNullOrEmpty(key) ? Colors.Red : Colors.Green;
    }

    private async void OnSaveOpenAIKey(object? sender, EventArgs e)
    {
        var key = OpenAIKeyEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
            SecureStorage.Remove("openai_api_key");
        else
            await SecureStorage.SetAsync("openai_api_key", key);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", string.IsNullOrEmpty(key) ? null : key);
        OpenAIDot.Color = string.IsNullOrEmpty(key) ? Colors.Red : Colors.Green;
    }

    private void OnToggleGeminiVisibility(object? sender, EventArgs e)
    {
        GeminiKeyEntry.IsPassword = !GeminiKeyEntry.IsPassword;
        GeminiShowBtn.Text = GeminiKeyEntry.IsPassword ? "Show" : "Hide";
    }

    private void OnToggleAnthropicVisibility(object? sender, EventArgs e)
    {
        AnthropicKeyEntry.IsPassword = !AnthropicKeyEntry.IsPassword;
        AnthropicShowBtn.Text = AnthropicKeyEntry.IsPassword ? "Show" : "Hide";
    }

    private void OnToggleOpenAIVisibility(object? sender, EventArgs e)
    {
        OpenAIKeyEntry.IsPassword = !OpenAIKeyEntry.IsPassword;
        OpenAIShowBtn.Text = OpenAIKeyEntry.IsPassword ? "Show" : "Hide";
    }

    private async void OnTestOllama(object? sender, EventArgs e)
    {
        var url = OllamaUrlEntry.Text?.Trim() ?? "http://localhost:11434";
        Preferences.Set("ollama_base_url", url);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync($"{url.TrimEnd('/')}/api/tags");
            OllamaDot.Color = resp.IsSuccessStatusCode ? Colors.Green : Colors.Red;
        }
        catch
        {
            OllamaDot.Color = Colors.Red;
        }
    }

    // --- Output section ---

    private void LoadOutputSection()
    {
        OutputPathLabel.Text = s_outputBase;

        var retentionDays = Preferences.Get("log_retention_days", 30);
        RetentionPicker.SelectedIndex = retentionDays switch
        {
            7 => 0,
            14 => 1,
            90 => 3,
            _ => 2, // 30
        };
    }

    private async void OnOpenOutputFolder(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(s_outputBase);
        try
        {
            await Launcher.OpenAsync(new Uri($"file://{s_outputBase}"));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private void OnSaveRetention(object? sender, EventArgs e)
    {
        var days = RetentionPicker.SelectedIndex switch
        {
            0 => 7,
            1 => 14,
            3 => 90,
            _ => 30,
        };
        Preferences.Set("log_retention_days", days);
    }

    // --- Notifications ---

    private void LoadNotificationPrefs()
    {
        NotifyMasterSwitch.IsToggled = Preferences.Get("purfle_notifications_enabled", true);
        NotifySuccessSwitch.IsToggled = Preferences.Get("purfle_notify_success", true);
        NotifyErrorSwitch.IsToggled = Preferences.Get("purfle_notify_error", true);
        NotifyInstallSwitch.IsToggled = Preferences.Get("purfle_notify_install", true);

        UpdateNotifySubToggles();

        NotifyMasterSwitch.Toggled += (_, _) => Preferences.Set("purfle_notifications_enabled", NotifyMasterSwitch.IsToggled);
        NotifySuccessSwitch.Toggled += (_, _) => Preferences.Set("purfle_notify_success", NotifySuccessSwitch.IsToggled);
        NotifyErrorSwitch.Toggled += (_, _) => Preferences.Set("purfle_notify_error", NotifyErrorSwitch.IsToggled);
        NotifyInstallSwitch.Toggled += (_, _) => Preferences.Set("purfle_notify_install", NotifyInstallSwitch.IsToggled);
    }

    private void OnNotifyMasterToggled(object? sender, ToggledEventArgs e)
    {
        UpdateNotifySubToggles();
    }

    private void UpdateNotifySubToggles()
    {
        var enabled = NotifyMasterSwitch.IsToggled;
        NotifySuccessSwitch.IsEnabled = enabled;
        NotifyErrorSwitch.IsEnabled = enabled;
        NotifyInstallSwitch.IsEnabled = enabled;
    }

    // --- About section ---

    private void LoadAboutSection()
    {
        var appVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
        var runtimeVersion = typeof(Purfle.Runtime.AgentLoader).Assembly
            .GetName().Version?.ToString() ?? "unknown";
        var platform = $"{DeviceInfo.Platform} {DeviceInfo.VersionString}";

        AppVersionLabel.Text = $"App: {appVersion}";
        RuntimeVersionLabel.Text = $"Runtime: {runtimeVersion}";
        PlatformLabel.Text = $"Platform: {platform}";
    }

    private async void OnCopyDiagnostics(object? sender, EventArgs e)
    {
        var appVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
        var runtimeVersion = typeof(Purfle.Runtime.AgentLoader).Assembly
            .GetName().Version?.ToString() ?? "unknown";
        var installed = _store.ListInstalled();

        var diagnostics = System.Text.Json.JsonSerializer.Serialize(new
        {
            app_version = appVersion,
            runtime_version = runtimeVersion,
            platform = $"{DeviceInfo.Platform} {DeviceInfo.VersionString}",
            installed_agents = installed.Count,
            output_path = s_outputBase,
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await Clipboard.SetTextAsync(diagnostics);
        await DisplayAlertAsync("Copied", "Diagnostic info copied to clipboard.", "OK");
    }

    // --- Marketplace URL ---

    private async void OnSaveUrl(object? sender, EventArgs e)
    {
        var url = RegistryUrlEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            _marketplace.BaseUrl = url;
            Preferences.Set("marketplace_url", url);
        }
        await DisplayAlertAsync("Saved", "Marketplace URL updated.", "OK");
    }

    // --- Data Management ---

    private async void OnClearOutput(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Clear Output",
            "This will delete all agent output files. This cannot be undone.", "Clear", "Cancel");
        if (!confirm) return;

        if (Directory.Exists(s_outputBase))
        {
            try
            {
                Directory.Delete(s_outputBase, recursive: true);
                Directory.CreateDirectory(s_outputBase);
                await DisplayAlertAsync("Cleared", "All agent output has been deleted.", "OK");
                LoadStats();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", ex.Message, "OK");
            }
        }
    }

    private async void OnResetSetup(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Reset Setup",
            "This will re-run the setup wizard on next launch.", "Reset", "Cancel");
        if (!confirm) return;

        Preferences.Set("setup_complete", false);
        await Shell.Current.GoToAsync("SetupWizardPage");
    }

    // --- Authentication ---

    private async void OnLogin(object? sender, EventArgs e)
    {
        try
        {
            var callbackUrl = new Uri("purfle://callback");
            var authUrl = new Uri(
                $"{_marketplace.BaseUrl}/connect/authorize" +
                $"?response_type=code" +
                $"&client_id=purfle-cli" +
                $"&redirect_uri={Uri.EscapeDataString(callbackUrl.ToString())}" +
                $"&scope=openid%20email%20profile" +
                $"&code_challenge_method=S256" +
                $"&code_challenge={GenerateCodeChallenge(out var codeVerifier)}");

            var result = await WebAuthenticator.Default.AuthenticateAsync(authUrl, callbackUrl);
            var code = result.Properties.GetValueOrDefault("code");

            if (string.IsNullOrEmpty(code))
            {
                await DisplayAlertAsync("Error", "No authorization code received.", "OK");
                return;
            }

            using var http = new HttpClient();
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = callbackUrl.ToString(),
                ["client_id"] = "purfle-cli",
                ["code_verifier"] = codeVerifier,
            });

            var resp = await http.PostAsync($"{_marketplace.BaseUrl}/connect/token", tokenRequest);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                await DisplayAlertAsync("Error", $"Token exchange failed: {err}", "OK");
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();

            if (!string.IsNullOrEmpty(accessToken))
            {
                _marketplace.AccessToken = accessToken;
                await SecureStorage.SetAsync("access_token", accessToken);
                UpdateAuthStatus();
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    // --- Stats ---

    private void LoadStats()
    {
        var installed = _store.ListInstalled();
        long totalInputTokens = 0, totalOutputTokens = 0;

        if (Directory.Exists(s_outputBase))
        {
            foreach (var dir in Directory.EnumerateDirectories(s_outputBase))
            {
                var jsonlPath = Path.Combine(dir, "run.jsonl");
                if (!File.Exists(jsonlPath)) continue;
                try
                {
                    foreach (var line in File.ReadLines(jsonlPath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        using var doc = System.Text.Json.JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("input_tokens", out var it))
                            totalInputTokens += it.GetInt64();
                        if (doc.RootElement.TryGetProperty("output_tokens", out var ot))
                            totalOutputTokens += ot.GetInt64();
                    }
                }
                catch { }
            }
        }

        StatsLabel.Text = $"Installed agents: {installed.Count}\n" +
                          $"All-time tokens: {totalInputTokens} in / {totalOutputTokens} out";
    }

    // --- Helpers ---

    private void UpdateAuthStatus()
    {
        if (!string.IsNullOrEmpty(_marketplace.AccessToken))
        {
            AuthStatusLabel.Text = "Authenticated.";
            AuthStatusLabel.TextColor = Colors.Green;
            LoginButton.Text = "Re-authenticate";
        }
        else
        {
            AuthStatusLabel.Text = "Not authenticated.";
            AuthStatusLabel.TextColor = Colors.Gray;
            LoginButton.Text = "Login";
        }
    }

    private static string GenerateCodeChallenge(out string codeVerifier)
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        codeVerifier = Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
