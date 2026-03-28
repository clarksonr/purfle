namespace Purfle.Marketplace.Shared;

// --- Key Registry DTOs ---

public sealed record PublicKeyResponse(
    string KeyId,
    string Algorithm,
    string X,   // Base64url-encoded
    string Y,   // Base64url-encoded
    bool IsRevoked
);

public sealed record RegisterKeyRequest(
    string KeyId,
    string Algorithm,
    string X,   // Base64url-encoded
    string Y    // Base64url-encoded
);

// --- Agent Registry DTOs ---

public sealed record AgentSearchResult(
    string AgentId,
    string Name,
    string Description,
    string LatestVersion,
    string Author,
    DateTimeOffset PublishedAt,
    long TotalDownloads
);

public sealed record AgentSearchResponse(
    IReadOnlyList<AgentSearchResult> Agents,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record AgentDetailResponse(
    string AgentId,
    string Name,
    string Description,
    string PublisherName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AgentVersionSummary> Versions
);

public sealed record AgentVersionSummary(
    string Version,
    DateTimeOffset PublishedAt,
    long Downloads
);

// --- Auth DTOs ---

public sealed record RegisterRequest(
    string DisplayName,
    string Email,
    string Password
);

public sealed record LoginRequest(
    string Email,
    string Password
);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);

public sealed record RefreshRequest(
    string RefreshToken
);
