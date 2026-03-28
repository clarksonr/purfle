using Purfle.App.Services;

namespace Purfle.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly MarketplaceService _marketplace;

    public SettingsPage(MarketplaceService marketplace)
    {
        InitializeComponent();
        _marketplace = marketplace;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RegistryUrlEntry.Text = _marketplace.BaseUrl;
        UpdateAuthStatus();
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
