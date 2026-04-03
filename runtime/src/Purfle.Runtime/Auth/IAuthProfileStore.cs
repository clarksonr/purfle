namespace Purfle.Runtime.Auth;

/// <summary>
/// Manages auth profiles for LLM providers.
/// Thread-safe. Persists to disk + platform keychain.
/// </summary>
public interface IAuthProfileStore
{
    /// <summary>
    /// Gets the currently active profile for a provider, or null if none.
    /// </summary>
    Task<AuthProfile?> GetActiveProfileAsync(string provider, CancellationToken ct = default);

    /// <summary>
    /// Gets all profiles for a provider, ordered by preference (active first).
    /// </summary>
    Task<IReadOnlyList<AuthProfile>> GetProfilesAsync(string provider, CancellationToken ct = default);

    /// <summary>
    /// Gets all profiles across all providers.
    /// </summary>
    Task<IReadOnlyList<AuthProfile>> GetAllProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new profile. Throws if profile ID already exists.
    /// </summary>
    Task<AuthProfile> AddProfileAsync(
        string provider,
        string name,
        AuthCredential credential,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing profile's credential or status.
    /// </summary>
    Task<AuthProfile> UpdateProfileAsync(AuthProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Removes a profile by ID.
    /// </summary>
    Task<bool> RemoveProfileAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Sets which profile is active for a provider.
    /// </summary>
    Task SetActiveProfileAsync(string provider, string profileId, CancellationToken ct = default);

    /// <summary>
    /// Marks a profile as being in cooldown (rate limited).
    /// </summary>
    Task MarkCooldownAsync(string profileId, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Updates a profile's status (e.g., after verification).
    /// </summary>
    Task UpdateStatusAsync(string profileId, ProfileStatus status, CancellationToken ct = default);

    /// <summary>
    /// Raised when any profile changes.
    /// </summary>
    event EventHandler<AuthProfileChangedEventArgs>? ProfileChanged;
}

/// <summary>Event args for profile changes.</summary>
public sealed class AuthProfileChangedEventArgs : EventArgs
{
    /// <summary>The profile that changed.</summary>
    public required string ProfileId { get; init; }

    /// <summary>The type of change.</summary>
    public required AuthProfileChangeType ChangeType { get; init; }
}

/// <summary>Types of profile change.</summary>
public enum AuthProfileChangeType
{
    /// <summary>A new profile was added.</summary>
    Added,

    /// <summary>An existing profile was updated.</summary>
    Updated,

    /// <summary>A profile was removed.</summary>
    Removed,

    /// <summary>A profile's status changed.</summary>
    StatusChanged
}
