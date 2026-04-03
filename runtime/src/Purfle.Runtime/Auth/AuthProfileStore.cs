namespace Purfle.Runtime.Auth;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Purfle.Runtime.Platform;

/// <summary>
/// File-backed auth profile store with keychain credential storage.
///
/// Storage layout:
///   - Profile metadata: ~/.purfle/auth-profiles.json
///   - Credentials: Platform keychain via ICredentialStore (key = profileId)
/// </summary>
public sealed class AuthProfileStore : IAuthProfileStore, IDisposable
{
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<AuthProfileStore> _logger;
    private readonly string _profilesPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ConcurrentDictionary<string, AuthProfileMetadata> _profiles = new();
    private Dictionary<string, string> _activeProfiles = new(); // provider -> profileId

    /// <inheritdoc/>
    public event EventHandler<AuthProfileChangedEventArgs>? ProfileChanged;

    /// <summary>Creates a new AuthProfileStore.</summary>
    public AuthProfileStore(
        ICredentialStore credentialStore,
        ILogger<AuthProfileStore> logger,
        string? profilesPath = null)
    {
        _credentialStore = credentialStore;
        _logger = logger;
        _profilesPath = profilesPath ?? GetDefaultProfilesPath();
    }

    /// <summary>
    /// Loads persisted profiles from disk. Call once on startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(_profilesPath))
            {
                var json = await File.ReadAllTextAsync(_profilesPath, ct);
                var data = JsonSerializer.Deserialize<AuthProfilesFile>(json, JsonOptions);
                if (data != null)
                {
                    _profiles = new ConcurrentDictionary<string, AuthProfileMetadata>(
                        data.Profiles.ToDictionary(p => p.ProfileId));
                    _activeProfiles = new Dictionary<string, string>(data.ActiveProfiles);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load auth profiles from {Path}", _profilesPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<AuthProfile?> GetActiveProfileAsync(string provider, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_activeProfiles.TryGetValue(provider, out var profileId))
            {
                // Try first profile for this provider
                profileId = _profiles.Values
                    .Where(p => p.Provider == provider)
                    .OrderBy(p => p.CreatedAtUtc)
                    .FirstOrDefault()?.ProfileId;
            }

            if (profileId == null) return null;

            return await LoadProfileAsync(profileId, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuthProfile>> GetProfilesAsync(string provider, CancellationToken ct = default)
    {
        var profiles = new List<AuthProfile>();

        await _lock.WaitAsync(ct);
        try
        {
            var metadatas = _profiles.Values
                .Where(p => p.Provider == provider)
                .OrderBy(p => _activeProfiles.GetValueOrDefault(provider) == p.ProfileId ? 0 : 1)
                .ThenBy(p => p.CreatedAtUtc);

            foreach (var meta in metadatas)
            {
                var profile = await LoadProfileAsync(meta.ProfileId, ct);
                if (profile != null) profiles.Add(profile);
            }
        }
        finally
        {
            _lock.Release();
        }

        return profiles;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuthProfile>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        var profiles = new List<AuthProfile>();

        await _lock.WaitAsync(ct);
        try
        {
            foreach (var meta in _profiles.Values.OrderBy(p => p.Provider).ThenBy(p => p.CreatedAtUtc))
            {
                var profile = await LoadProfileAsync(meta.ProfileId, ct);
                if (profile != null) profiles.Add(profile);
            }
        }
        finally
        {
            _lock.Release();
        }

        return profiles;
    }

    /// <inheritdoc/>
    public async Task<AuthProfile> AddProfileAsync(
        string provider,
        string name,
        AuthCredential credential,
        CancellationToken ct = default)
    {
        var profileId = $"{provider}:{name}";

        await _lock.WaitAsync(ct);
        try
        {
            if (_profiles.ContainsKey(profileId))
            {
                throw new InvalidOperationException($"Profile '{profileId}' already exists");
            }

            var metadata = new AuthProfileMetadata
            {
                ProfileId = profileId,
                Provider = provider,
                Name = name,
                CredentialType = credential.GetType().Name,
                Status = ProfileStatus.Unknown,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Store credential in keychain
            var credentialJson = JsonSerializer.Serialize(credential, credential.GetType(), JsonOptions);
            await _credentialStore.SetAsync($"purfle:auth:{profileId}", credentialJson);

            // Store metadata
            _profiles[profileId] = metadata;

            // Set as active if first for this provider
            if (!_activeProfiles.ContainsKey(provider))
            {
                _activeProfiles[provider] = profileId;
            }

            await SaveAsync(ct);

            var profile = await LoadProfileAsync(profileId, ct);
            OnProfileChanged(profileId, AuthProfileChangeType.Added);

            return profile!;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<AuthProfile> UpdateProfileAsync(AuthProfile profile, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_profiles.TryGetValue(profile.ProfileId, out var existing))
            {
                throw new InvalidOperationException($"Profile '{profile.ProfileId}' not found");
            }

            // Update credential in keychain
            var credentialJson = JsonSerializer.Serialize(profile.Credential, profile.Credential.GetType(), JsonOptions);
            await _credentialStore.SetAsync($"purfle:auth:{profile.ProfileId}", credentialJson);

            // Update metadata
            existing = existing with
            {
                Status = profile.Status,
                LastVerifiedUtc = profile.LastVerifiedUtc,
                CooldownUntilUtc = profile.CooldownUntilUtc
            };
            _profiles[profile.ProfileId] = existing;

            await SaveAsync(ct);
            OnProfileChanged(profile.ProfileId, AuthProfileChangeType.Updated);

            return profile;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveProfileAsync(string profileId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_profiles.TryRemove(profileId, out var removed))
            {
                return false;
            }

            // Remove from keychain
            await _credentialStore.DeleteAsync($"purfle:auth:{profileId}");

            // Remove from active if applicable
            if (_activeProfiles.TryGetValue(removed.Provider, out var activeId) && activeId == profileId)
            {
                _activeProfiles.Remove(removed.Provider);

                // Set next available as active
                var next = _profiles.Values.FirstOrDefault(p => p.Provider == removed.Provider);
                if (next != null)
                {
                    _activeProfiles[removed.Provider] = next.ProfileId;
                }
            }

            await SaveAsync(ct);
            OnProfileChanged(profileId, AuthProfileChangeType.Removed);

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SetActiveProfileAsync(string provider, string profileId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_profiles.TryGetValue(profileId, out var profile) || profile.Provider != provider)
            {
                throw new InvalidOperationException($"Profile '{profileId}' not found for provider '{provider}'");
            }

            _activeProfiles[provider] = profileId;
            await SaveAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task MarkCooldownAsync(string profileId, TimeSpan duration, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_profiles.TryGetValue(profileId, out var metadata))
            {
                _profiles[profileId] = metadata with
                {
                    Status = ProfileStatus.Cooldown,
                    CooldownUntilUtc = DateTime.UtcNow + duration
                };
                await SaveAsync(ct);
                OnProfileChanged(profileId, AuthProfileChangeType.StatusChanged);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(string profileId, ProfileStatus status, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_profiles.TryGetValue(profileId, out var metadata))
            {
                _profiles[profileId] = metadata with
                {
                    Status = status,
                    LastVerifiedUtc = DateTime.UtcNow,
                    CooldownUntilUtc = status == ProfileStatus.Cooldown ? metadata.CooldownUntilUtc : null
                };
                await SaveAsync(ct);
                OnProfileChanged(profileId, AuthProfileChangeType.StatusChanged);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Seeds profiles from environment variables for backward compatibility.
    /// Only creates profiles if no profile exists for the provider.
    /// </summary>
    public async Task SeedFromEnvironmentAsync(CancellationToken ct = default)
    {
        var envMappings = new Dictionary<string, string>
        {
            ["GEMINI_API_KEY"] = "gemini",
            ["ANTHROPIC_API_KEY"] = "anthropic",
            ["OPENAI_API_KEY"] = "openai"
        };

        foreach (var (envVar, provider) in envMappings)
        {
            var apiKey = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(apiKey)) continue;

            var existing = await GetActiveProfileAsync(provider, ct);
            if (existing == null)
            {
                try
                {
                    await AddProfileAsync(provider, "env", new ApiKeyCredential(apiKey), ct);
                    _logger.LogInformation(
                        "Created profile {ProfileId} from environment variable {EnvVar}",
                        $"{provider}:env", envVar);
                }
                catch (InvalidOperationException)
                {
                    // Profile already exists, skip
                }
            }
        }

        // Check Ollama
        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
        var ollamaProfile = await GetActiveProfileAsync("ollama", ct);
        if (ollamaProfile == null)
        {
            try
            {
                await AddProfileAsync("ollama", "local", new LocalServiceCredential(ollamaUrl), ct);
            }
            catch (InvalidOperationException) { }
        }
    }

    private async Task<AuthProfile?> LoadProfileAsync(string profileId, CancellationToken ct)
    {
        if (!_profiles.TryGetValue(profileId, out var metadata))
        {
            return null;
        }

        var credentialJson = await _credentialStore.GetAsync($"purfle:auth:{profileId}");
        if (credentialJson == null)
        {
            _logger.LogWarning("Credential missing from keychain for profile {ProfileId}", profileId);
            return null;
        }

        AuthCredential? credential = metadata.CredentialType switch
        {
            nameof(ApiKeyCredential) => JsonSerializer.Deserialize<ApiKeyCredential>(credentialJson, JsonOptions),
            nameof(OAuthCredential) => JsonSerializer.Deserialize<OAuthCredential>(credentialJson, JsonOptions),
            nameof(LocalServiceCredential) => JsonSerializer.Deserialize<LocalServiceCredential>(credentialJson, JsonOptions),
            _ => throw new InvalidOperationException($"Unknown credential type: {metadata.CredentialType}")
        };

        if (credential == null) return null;

        return new AuthProfile
        {
            ProfileId = metadata.ProfileId,
            Provider = metadata.Provider,
            Name = metadata.Name,
            Credential = credential,
            Status = metadata.Status,
            LastVerifiedUtc = metadata.LastVerifiedUtc,
            CooldownUntilUtc = metadata.CooldownUntilUtc,
            CreatedAtUtc = metadata.CreatedAtUtc
        };
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_profilesPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var data = new AuthProfilesFile
        {
            Profiles = _profiles.Values.ToList(),
            ActiveProfiles = new Dictionary<string, string>(_activeProfiles)
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_profilesPath, json, ct);
    }

    private void OnProfileChanged(string profileId, AuthProfileChangeType changeType)
    {
        ProfileChanged?.Invoke(this, new AuthProfileChangedEventArgs
        {
            ProfileId = profileId,
            ChangeType = changeType
        });
    }

    private static string GetDefaultProfilesPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".purfle", "auth-profiles.json");
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        _lock.Dispose();
    }
}

// Internal DTOs for file storage
internal sealed record AuthProfileMetadata
{
    public required string ProfileId { get; init; }
    public required string Provider { get; init; }
    public required string Name { get; init; }
    public required string CredentialType { get; init; }
    public ProfileStatus Status { get; init; }
    public DateTime? LastVerifiedUtc { get; init; }
    public DateTime? CooldownUntilUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

internal sealed record AuthProfilesFile
{
    public List<AuthProfileMetadata> Profiles { get; init; } = new();
    public Dictionary<string, string> ActiveProfiles { get; init; } = new();
}
