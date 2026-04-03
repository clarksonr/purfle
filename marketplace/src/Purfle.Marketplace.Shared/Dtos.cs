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
    long Downloads,
    string? BundleHash = null
);

// --- Publisher Verification DTOs ---

public sealed record RegisterPublisherRequest(
    string DisplayName,
    string Domain,
    string Email,
    string Password
);

public sealed record VerificationChallengeResponse(
    string Challenge,
    string Instructions
);

public sealed record VerifyDomainRequest(
    string Domain
);

public sealed record PublisherDetailResponse(
    string Id,
    string DisplayName,
    string? Domain,
    bool IsVerified,
    DateTimeOffset CreatedAt
);

// --- Attestation DTOs ---

public sealed record AttestationResponse(
    Guid Id,
    string AgentId,
    string Type,
    string IssuedBy,
    DateTimeOffset IssuedAt
);

public sealed record RequestAttestationRequest(
    string AgentId,
    string Type  // "publisher-verified" or "marketplace-listed"
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
