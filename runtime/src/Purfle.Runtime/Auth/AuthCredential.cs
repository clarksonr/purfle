namespace Purfle.Runtime.Auth;

/// <summary>
/// Base record for all credential types. Sealed hierarchy.
/// </summary>
public abstract record AuthCredential
{
    /// <summary>
    /// Returns true if the credential appears syntactically valid.
    /// Does NOT verify with the provider.
    /// </summary>
    public abstract bool IsWellFormed { get; }
}

/// <summary>
/// Static API key credential.
/// </summary>
public sealed record ApiKeyCredential(string ApiKey) : AuthCredential
{
    /// <inheritdoc/>
    public override bool IsWellFormed => !string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Length >= 10;

    /// <summary>
    /// Returns masked version for display: "sk-ant-...abc123"
    /// </summary>
    public string Masked => ApiKey.Length > 10
        ? $"{ApiKey[..7]}...{ApiKey[^6..]}"
        : "***";
}

/// <summary>
/// OAuth 2.0 token credential with refresh capability.
/// </summary>
public sealed record OAuthCredential(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc
) : AuthCredential
{
    /// <inheritdoc/>
    public override bool IsWellFormed =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(RefreshToken);

    /// <summary>Whether the access token has expired.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    /// <summary>Whether the token expires within the given window.</summary>
    public bool ExpiresWithin(TimeSpan window) => DateTime.UtcNow >= ExpiresAtUtc - window;

    /// <summary>Time remaining until expiry (zero if already expired).</summary>
    public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAtUtc - DateTime.UtcNow;
}

/// <summary>
/// Local service credential (Ollama, vLLM, etc.) — no auth, just URL.
/// </summary>
public sealed record LocalServiceCredential(string BaseUrl) : AuthCredential
{
    /// <inheritdoc/>
    public override bool IsWellFormed =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "http" || uri.Scheme == "https");
}
