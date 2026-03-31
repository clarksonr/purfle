using Purfle.App.Services;

namespace Purfle.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly AgentStore _store;

    public SettingsPage(MarketplaceService marketplace, AgentStore store)
    {
        InitializeComponent();
        _marketplace = marketplace;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RegistryUrlEntry.Text = _marketplace.BaseUrl;
        var anthropicKey = await SecureStorage.GetAsync("anthropic_api_key");
        if (!string.IsNullOrEmpty(anthropicKey))
            AnthropicKeyEntry.Text = anthropicKey;

        var geminiKey = await SecureStorage.GetAsync("gemini_api_key");
        if (!string.IsNullOrEmpty(geminiKey))
            GeminiKeyEntry.Text = geminiKey;

        EnginePicker.SelectedIndex = Preferences.Get("preferred_engine", "") switch
        {
            "anthropic" => 1,
            "gemini"    => 2,
            _           => 0,
        };

        UpdateAuthStatus();
    }

    private async void OnInstallBundle(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a .purfle bundle",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = [".purfle"],
                    [DevicePlatform.macOS] = ["purfle"],
                }),
            });

            if (result is null) return;

            // Extract the agent ID from the bundle's manifest.
            string agentId;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(result.FullPath))
            {
                var entry = zip.GetEntry("agent.manifest.json");
                if (entry is null)
                {
                    await DisplayAlertAsync("Error", "Bundle does not contain agent.manifest.json.", "OK");
                    return;
                }

                using var stream = entry.Open();
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
                agentId = doc.RootElement.GetProperty("id").GetString()
                          ?? throw new InvalidOperationException("Manifest has no 'id' field.");
            }

            _store.InstallBundle(agentId, result.FullPath);
            await DisplayAlertAsync("Installed", $"Agent '{agentId}' installed successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnSaveEngine(object? sender, EventArgs e)
    {
        var engineKey = EnginePicker.SelectedIndex switch
        {
            1 => "anthropic",
            2 => "gemini",
            _ => "",
        };
        Preferences.Set("preferred_engine", engineKey);
        await DisplayAlertAsync("Saved", "Engine preference saved.", "OK");
    }

    private async void OnSaveApiKeys(object? sender, EventArgs e)
    {
        var anthropic = AnthropicKeyEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(anthropic))
            SecureStorage.Remove("anthropic_api_key");
        else
            await SecureStorage.SetAsync("anthropic_api_key", anthropic);
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY",
            string.IsNullOrEmpty(anthropic) ? null : anthropic);

        var gemini = GeminiKeyEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(gemini))
            SecureStorage.Remove("gemini_api_key");
        else
            await SecureStorage.SetAsync("gemini_api_key", gemini);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY",
            string.IsNullOrEmpty(gemini) ? null : gemini);

        await DisplayAlertAsync("Saved", "API keys saved.", "OK");
    }

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

    private async void OnLogin(object? sender, EventArgs e)
    {
        try
        {
            // Use WebAuthenticator for PKCE flow.
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

            // Exchange code for tokens.
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
        catch (TaskCanceledException)
        {
            // User cancelled.
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

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
