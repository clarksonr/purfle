using System.Globalization;
using System.Text.Json;

namespace Purfle.Marketplace.Storage.Json.OpenIddict;

public sealed class OpenIddictJsonApplication
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ClientType { get; set; }
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public string? ConsentType { get; set; }
    public string? DisplayName { get; set; }
    public Dictionary<string, JsonElement>? DisplayNames { get; set; }
    public string? JsonWebKeySet { get; set; }
    public List<string>? Permissions { get; set; }
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public List<string>? RedirectUris { get; set; }
    public List<string>? PostLogoutRedirectUris { get; set; }
    public List<string>? Requirements { get; set; }
    public string? Settings { get; set; }
    public string? ApplicationType { get; set; }
}

public sealed class OpenIddictJsonAuthorization
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ApplicationId { get; set; }
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? CreationDate { get; set; }
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public List<string>? Scopes { get; set; }
    public string? Status { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
}

public sealed class OpenIddictJsonScope
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public string? Description { get; set; }
    public Dictionary<string, JsonElement>? Descriptions { get; set; }
    public string? DisplayName { get; set; }
    public Dictionary<string, JsonElement>? DisplayNames { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public List<string>? Resources { get; set; }
}

public sealed class OpenIddictJsonToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ApplicationId { get; set; }
    public string? AuthorizationId { get; set; }
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? CreationDate { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
    public string? Payload { get; set; }
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public DateTimeOffset? RedemptionDate { get; set; }
    public string? ReferenceId { get; set; }
    public string? Status { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
}
