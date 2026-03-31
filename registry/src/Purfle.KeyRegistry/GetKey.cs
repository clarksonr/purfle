using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Purfle.KeyRegistry;

public class GetKey
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<GetKey> _logger;

    public GetKey(TableServiceClient tableService, ILogger<GetKey> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("GetKey")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "keys/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("GetKey: {Id}", id);

        var table = _tableService.GetTableClient("keys");
        await table.CreateIfNotExistsAsync();

        try
        {
           var entity = await table.GetEntityAsync<KeyEntity>("keys", id.Replace("/", "__"));

            if (entity.Value.RevokedAt is not null)
            {
                var revoked = req.CreateResponse(HttpStatusCode.Gone);
                await revoked.WriteAsJsonAsync(new { error = "key_revoked", revoked_at = entity.Value.RevokedAt });
                return revoked;
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            await ok.WriteStringAsync(entity.Value.Jwk);
            return ok;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "key_not_found", key_id = id });
            return notFound;
        }
    }
}
