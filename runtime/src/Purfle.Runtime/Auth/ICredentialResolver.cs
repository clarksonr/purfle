namespace Purfle.Runtime.Auth;

using Purfle.Runtime.Manifest;

/// <summary>
/// Resolves credentials for agent execution using fallback cascade.
/// </summary>
public interface ICredentialResolver
{
    /// <summary>
    /// Resolves a usable credential for the agent, following the fallback cascade:
    /// 1. Agent's declared engine preference
    /// 2. Agent's engine_fallback list (in order)
    /// 3. User's provider preference order
    /// 4. Any available provider
    ///
    /// Returns null if no usable credential is found.
    /// </summary>
    Task<ResolvedCredential?> ResolveAsync(AgentManifest manifest, CancellationToken ct = default);

    /// <summary>
    /// Gets the list of providers tried and their status for diagnostics.
    /// </summary>
    Task<IReadOnlyList<ProviderResolutionAttempt>> GetResolutionAttemptsAsync(
        AgentManifest manifest,
        CancellationToken ct = default);
}

/// <summary>
/// A successfully resolved credential with provider info.
/// </summary>
public sealed record ResolvedCredential
{
    /// <summary>
    /// The provider that was resolved (gemini, anthropic, openai, ollama).
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// The model to use (from manifest or default for provider).
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The auth profile that was used.
    /// </summary>
    public required AuthProfile Profile { get; init; }

    /// <summary>
    /// How the provider was selected.
    /// </summary>
    public required ResolutionSource Source { get; init; }
}

/// <summary>How the provider was selected during resolution.</summary>
public enum ResolutionSource
{
    /// <summary>Agent's primary engine preference.</summary>
    AgentPreferred,

    /// <summary>Agent's fallback list.</summary>
    AgentFallback,

    /// <summary>User's provider preference order.</summary>
    UserPreference,

    /// <summary>Last resort — any available.</summary>
    AnyAvailable
}

/// <summary>
/// Diagnostic info about a resolution attempt.
/// </summary>
public sealed record ProviderResolutionAttempt
{
    /// <summary>The provider that was tried.</summary>
    public required string Provider { get; init; }

    /// <summary>Whether a profile exists for this provider.</summary>
    public required bool HasProfile { get; init; }

    /// <summary>The profile's status, if one exists.</summary>
    public required ProfileStatus? ProfileStatus { get; init; }

    /// <summary>Why resolution failed for this provider, if it did.</summary>
    public required string? FailureReason { get; init; }
}
