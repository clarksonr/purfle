namespace Purfle.Marketplace.Core.Repositories;

public sealed record AgentSearchItem(
    string AgentId,
    string Name,
    string Description,
    string PublisherDisplayName,
    string? LatestVersion,
    DateTimeOffset? LatestPublishedAt,
    long TotalDownloads
);

public sealed record AgentSearchPage(
    IReadOnlyList<AgentSearchItem> Items,
    int TotalCount
);
