using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Purfle.KeyRegistry;

public class RevokeKey
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<RevokeKey> _logger;

    public RevokeKey(TableServiceClient tableService, ILogger<RevokeKey> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("RevokeKey")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "keys/{id}")] HttpRequestData req,
        string id)
    {
        if (!IsAuthorized(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteAsJsonAsync(new { error = "unauthorized" });
            return unauth;
        }

        var table = _tableService.GetTableClient("keys");
        await table.CreateIfNotExistsAsync();

        try
        {
            var entity = await table.GetEntityAsync<KeyEntity>("keys", id.Replace("/", "__"));
            entity.Value.RevokedAt = DateTimeOffset.UtcNow.ToString("O");
            await table.UpdateEntityAsync(entity.Value, entity.Value.ETag);

            _logger.LogInformation("RevokeKey: revoked {KeyId}", id);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new { key_id = id, status = "revoked", revoked_at = entity.Value.RevokedAt });
            return ok;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "key_not_found", key_id = id });
            return notFound;
        }
    }

    private static bool IsAuthorized(HttpRequestData req)
    {
        var apiKey = Environment.GetEnvironmentVariable("PURFLE_REGISTRY_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) return false;
        req.Headers.TryGetValues("X-Purfle-Key", out var values);
        return values?.FirstOrDefault() == apiKey;
    }
}
