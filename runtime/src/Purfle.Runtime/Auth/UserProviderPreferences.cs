namespace Purfle.Runtime.Auth;

using System.Text.Json;

/// <summary>
/// User's preferred provider order for fallback resolution.
/// </summary>
public sealed class UserProviderPreferences
{
    private static readonly string[] DefaultOrder = ["gemini", "anthropic", "openai", "ollama"];

    /// <summary>
    /// Ordered list of providers. First = highest priority.
    /// </summary>
    public IReadOnlyList<string> ProviderOrder { get; private set; } = DefaultOrder;

    private readonly string _prefsPath;

    /// <summary>Creates a new UserProviderPreferences instance.</summary>
    public UserProviderPreferences(string? prefsPath = null)
    {
        _prefsPath = prefsPath ?? GetDefaultPath();
    }

    /// <summary>Loads preferences from disk.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (File.Exists(_prefsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_prefsPath, ct);
                var data = JsonSerializer.Deserialize<PrefsData>(json);
                if (data?.ProviderOrder?.Count > 0)
                {
                    ProviderOrder = data.ProviderOrder;
                }
            }
            catch { /* Use default */ }
        }
    }

    /// <summary>Sets the provider order and persists to disk.</summary>
    public async Task SetOrderAsync(IReadOnlyList<string> order, CancellationToken ct = default)
    {
        ProviderOrder = order.ToList();

        var dir = Path.GetDirectoryName(_prefsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(new PrefsData { ProviderOrder = order.ToList() },
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_prefsPath, json, ct);
    }

    private static string GetDefaultPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".purfle", "provider-preferences.json");
    }

    private sealed record PrefsData
    {
        public List<string>? ProviderOrder { get; init; }
    }
}
