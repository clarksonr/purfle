namespace Purfle.App.ViewModels;

using Purfle.Runtime.Auth;
using System.Collections.ObjectModel;

/// <summary>
/// ViewModel for the Connected Accounts section in Settings.
/// </summary>
public sealed class ConnectedAccountsViewModel
{
    private readonly IAuthProfileStore _profileStore;
    private readonly UserProviderPreferences _preferences;

    public ObservableCollection<ProviderAccountItem> Providers { get; } = [];

    public ConnectedAccountsViewModel(
        IAuthProfileStore profileStore,
        UserProviderPreferences preferences)
    {
        _profileStore = profileStore;
        _preferences = preferences;
    }

    public async Task LoadAsync()
    {
        Providers.Clear();

        var allProfiles = await _profileStore.GetAllProfilesAsync();
        var allProviderIds = new[] { "gemini", "anthropic", "openai", "ollama" };

        foreach (var provider in _preferences.ProviderOrder.Union(allProviderIds).Distinct())
        {
            var profiles = allProfiles.Where(p => p.Provider == provider).ToList();
            var active = profiles.FirstOrDefault(p => p.IsUsable);

            Providers.Add(new ProviderAccountItem
            {
                Provider = provider,
                DisplayName = GetDisplayName(provider),
                IsConnected = profiles.Count > 0,
                IsNotConnected = profiles.Count == 0,
                ActiveProfile = active,
                StatusSummary = GetStatusSummary(active, profiles.Count > 0),
                StatusColor = GetStatusColor(active, profiles.Count > 0)
            });
        }
    }

    public async Task AddApiKeyAsync(string provider)
    {
        var apiKey = await Shell.Current.DisplayPromptAsync(
            $"Add {GetDisplayName(provider)} API Key",
            "Enter your API key:",
            keyboard: Keyboard.Default);

        if (string.IsNullOrWhiteSpace(apiKey)) return;

        try
        {
            await _profileStore.AddProfileAsync(provider, "default", new ApiKeyCredential(apiKey));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    public async Task RemoveProfileAsync(string profileId)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Remove Account",
            "Are you sure you want to remove this account?",
            "Remove", "Cancel");

        if (!confirm) return;

        await _profileStore.RemoveProfileAsync(profileId);
        await LoadAsync();
    }

    public async Task SaveReorderAsync()
    {
        var newOrder = Providers.Select(p => p.Provider).ToList();
        await _preferences.SetOrderAsync(newOrder);
    }

    private static string GetDisplayName(string provider) => provider switch
    {
        "gemini" => "Google Gemini",
        "anthropic" => "Anthropic Claude",
        "openai" => "OpenAI",
        "ollama" => "Ollama (Local)",
        _ => provider
    };

    private static string GetStatusSummary(AuthProfile? profile, bool hasProfile)
    {
        if (!hasProfile) return "Not connected";

        if (profile == null) return "No usable profile";

        var credSummary = profile.Credential switch
        {
            ApiKeyCredential ak => $"API Key: {ak.Masked}",
            OAuthCredential oa => $"OAuth (expires {oa.TimeRemaining.TotalMinutes:0}m)",
            LocalServiceCredential ls => ls.BaseUrl,
            _ => ""
        };

        var statusText = profile.Status switch
        {
            ProfileStatus.Active => "Connected",
            ProfileStatus.Unknown => "Ready",
            ProfileStatus.Expired => "Token expired",
            ProfileStatus.Invalid => "Invalid",
            ProfileStatus.Cooldown => $"Rate limited until {profile.CooldownUntilUtc:HH:mm}",
            _ => "Unknown"
        };

        return $"{statusText} — {credSummary}";
    }

    private static Color GetStatusColor(AuthProfile? profile, bool hasProfile)
    {
        if (!hasProfile) return Colors.Gray;
        if (profile == null) return Colors.Red;

        return profile.Status switch
        {
            ProfileStatus.Active or ProfileStatus.Unknown => Colors.Green,
            ProfileStatus.Expired => Colors.Orange,
            ProfileStatus.Invalid => Colors.Red,
            ProfileStatus.Cooldown => Colors.Orange,
            _ => Colors.Gray
        };
    }
}

/// <summary>
/// View item for a single provider in the Connected Accounts list.
/// </summary>
public sealed class ProviderAccountItem
{
    public required string Provider { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsConnected { get; init; }
    public required bool IsNotConnected { get; init; }
    public AuthProfile? ActiveProfile { get; init; }
    public required string StatusSummary { get; init; }
    public required Color StatusColor { get; init; }
}
