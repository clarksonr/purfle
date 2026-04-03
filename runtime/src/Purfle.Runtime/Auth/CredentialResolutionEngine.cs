namespace Purfle.Runtime.Auth;

using Microsoft.Extensions.Logging;
using Purfle.Runtime.Manifest;

/// <summary>
/// Resolves credentials using fallback cascade.
/// </summary>
public sealed class CredentialResolutionEngine : ICredentialResolver
{
    private static readonly Dictionary<string, string> DefaultModels = new()
    {
        ["gemini"] = "gemini-2.0-flash",
        ["anthropic"] = "claude-sonnet-4-20250514",
        ["openai"] = "gpt-4o",
        ["ollama"] = "llama3"
    };

    private static readonly string[] AllProviders = ["gemini", "anthropic", "openai", "ollama"];

    private readonly IAuthProfileStore _profileStore;
    private readonly UserProviderPreferences _preferences;
    private readonly ILogger<CredentialResolutionEngine> _logger;

    /// <summary>Creates a new CredentialResolutionEngine.</summary>
    public CredentialResolutionEngine(
        IAuthProfileStore profileStore,
        UserProviderPreferences preferences,
        ILogger<CredentialResolutionEngine> logger)
    {
        _profileStore = profileStore;
        _preferences = preferences;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ResolvedCredential?> ResolveAsync(AgentManifest manifest, CancellationToken ct = default)
    {
        // 1. Try agent's preferred engine
        var preferredEngine = manifest.Runtime?.Engine;
        if (!string.IsNullOrEmpty(preferredEngine))
        {
            var result = await TryResolveAsync(preferredEngine, manifest, ResolutionSource.AgentPreferred, ct);
            if (result != null)
            {
                _logger.LogDebug("Resolved to agent's preferred engine: {Provider}", preferredEngine);
                return result;
            }
        }

        // 2. Try agent's fallback list
        var fallbacks = manifest.Runtime?.EngineFallback ?? [];
        foreach (var fallback in fallbacks)
        {
            var result = await TryResolveAsync(fallback, manifest, ResolutionSource.AgentFallback, ct);
            if (result != null)
            {
                _logger.LogDebug("Resolved to agent fallback: {Provider}", fallback);
                return result;
            }
        }

        // 3. Try user preference order
        foreach (var provider in _preferences.ProviderOrder)
        {
            if (provider == preferredEngine || fallbacks.Contains(provider))
                continue; // Already tried

            var result = await TryResolveAsync(provider, manifest, ResolutionSource.UserPreference, ct);
            if (result != null)
            {
                _logger.LogDebug("Resolved to user preference: {Provider}", provider);
                return result;
            }
        }

        // 4. Try any remaining providers
        foreach (var provider in AllProviders)
        {
            if (provider == preferredEngine ||
                fallbacks.Contains(provider) ||
                _preferences.ProviderOrder.Contains(provider))
                continue; // Already tried

            var result = await TryResolveAsync(provider, manifest, ResolutionSource.AnyAvailable, ct);
            if (result != null)
            {
                _logger.LogDebug("Resolved to any available: {Provider}", provider);
                return result;
            }
        }

        _logger.LogWarning("No usable credential found for agent {AgentId}", manifest.Id);
        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProviderResolutionAttempt>> GetResolutionAttemptsAsync(
        AgentManifest manifest,
        CancellationToken ct = default)
    {
        var attempts = new List<ProviderResolutionAttempt>();

        // Build ordered list of providers to try
        var providers = new List<string>();

        var preferredEngine = manifest.Runtime?.Engine;
        if (!string.IsNullOrEmpty(preferredEngine))
            providers.Add(preferredEngine);

        providers.AddRange(manifest.Runtime?.EngineFallback ?? []);
        providers.AddRange(_preferences.ProviderOrder.Where(p => !providers.Contains(p)));
        providers.AddRange(AllProviders.Where(p => !providers.Contains(p)));

        foreach (var provider in providers.Distinct())
        {
            var profile = await _profileStore.GetActiveProfileAsync(provider, ct);
            attempts.Add(new ProviderResolutionAttempt
            {
                Provider = provider,
                HasProfile = profile != null,
                ProfileStatus = profile?.Status,
                FailureReason = GetFailureReason(profile)
            });
        }

        return attempts;
    }

    private async Task<ResolvedCredential?> TryResolveAsync(
        string provider,
        AgentManifest manifest,
        ResolutionSource source,
        CancellationToken ct)
    {
        var profile = await _profileStore.GetActiveProfileAsync(provider, ct);

        if (profile == null)
        {
            _logger.LogTrace("No profile for provider {Provider}", provider);
            return null;
        }

        if (!profile.IsUsable)
        {
            _logger.LogTrace("Profile {ProfileId} not usable: {Status}", profile.ProfileId, profile.Status);
            return null;
        }

        // Determine model: use manifest if specified and matches provider, else default
        var model = manifest.Runtime?.Engine == provider && !string.IsNullOrEmpty(manifest.Runtime?.Model)
            ? manifest.Runtime.Model
            : DefaultModels.GetValueOrDefault(provider, "");

        return new ResolvedCredential
        {
            Provider = provider,
            Model = model,
            Profile = profile,
            Source = source
        };
    }

    private static string? GetFailureReason(AuthProfile? profile)
    {
        if (profile == null) return "No credential configured";

        return profile.Status switch
        {
            ProfileStatus.Expired => "Token expired",
            ProfileStatus.Invalid => "Credential invalid or revoked",
            ProfileStatus.Cooldown when profile.CooldownUntilUtc > DateTime.UtcNow
                => $"Rate limited until {profile.CooldownUntilUtc:HH:mm:ss}",
            ProfileStatus.Cooldown => null, // Cooldown expired, should be usable
            ProfileStatus.Unknown when !profile.Credential.IsWellFormed => "Malformed credential",
            _ => null
        };
    }
}
