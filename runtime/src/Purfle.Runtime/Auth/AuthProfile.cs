namespace Purfle.Runtime.Auth;

/// <summary>
/// A single credential profile for an LLM provider.
/// </summary>
public sealed record AuthProfile
{
    /// <summary>
    /// Unique profile ID in format "provider:name" (e.g., "gemini:default", "anthropic:work").
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// Provider name: gemini, anthropic, openai, ollama.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Human-readable name for this profile (e.g., "Personal", "Work", "default").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The actual credential.
    /// </summary>
    public required AuthCredential Credential { get; init; }

    /// <summary>
    /// Current status of this profile.
    /// </summary>
    public ProfileStatus Status { get; init; } = ProfileStatus.Unknown;

    /// <summary>
    /// When the credential was last verified with the provider.
    /// </summary>
    public DateTime? LastVerifiedUtc { get; init; }

    /// <summary>
    /// If status is Cooldown, when to retry.
    /// </summary>
    public DateTime? CooldownUntilUtc { get; init; }

    /// <summary>
    /// ISO 8601 timestamp when this profile was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Returns true if the profile can be used for requests right now.
    /// </summary>
    public bool IsUsable => Status == ProfileStatus.Active ||
        (Status == ProfileStatus.Unknown && Credential.IsWellFormed) ||
        (Status == ProfileStatus.Cooldown && DateTime.UtcNow >= CooldownUntilUtc);

    /// <summary>
    /// Creates a new profile with updated status.
    /// </summary>
    public AuthProfile WithStatus(ProfileStatus status, DateTime? cooldownUntil = null) =>
        this with { Status = status, CooldownUntilUtc = cooldownUntil, LastVerifiedUtc = DateTime.UtcNow };
}
