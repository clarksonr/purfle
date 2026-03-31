using Azure;
using Azure.Data.Tables;

namespace Purfle.KeyRegistry;

public class KeyEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "keys";
    public string RowKey { get; set; } = default!;   // = key_id
    public string Jwk { get; set; } = default!;       // JWK JSON string
    public string? RevokedAt { get; set; }             // ISO 8601 or null
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
