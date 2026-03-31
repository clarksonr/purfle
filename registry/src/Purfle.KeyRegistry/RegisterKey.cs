using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Purfle.KeyRegistry;

public class RegisterKey
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<RegisterKey> _logger;

    public RegisterKey(TableServiceClient tableService, ILogger<RegisterKey> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("RegisterKey")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "keys")] HttpRequestData req)
    {
        if (!IsAuthorized(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteAsJsonAsync(new { error = "unauthorized" });
            return unauth;
        }

        string body = await new StreamReader(req.Body).ReadToEndAsync();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "invalid_json" });
            return bad;
        }

        if (!doc.RootElement.TryGetProperty("key_id", out var keyIdEl) ||
            !doc.RootElement.TryGetProperty("jwk", out var jwkEl))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "missing_fields", required = new[] { "key_id", "jwk" } });
            return bad;
        }

        var keyId = keyIdEl.GetString()!;
        var jwk   = jwkEl.GetRawText();

        var table = _tableService.GetTableClient("keys");
        await table.CreateIfNotExistsAsync();

        var entity = new KeyEntity
        {
            PartitionKey = "keys",
            RowKey = keyId.Replace("/", "__"),
            Jwk          = jwk,
            RevokedAt    = null
        };

        await table.UpsertEntityAsync(entity);

        _logger.LogInformation("RegisterKey: registered {KeyId}", keyId);

        var ok = req.CreateResponse(HttpStatusCode.Created);
        await ok.WriteAsJsonAsync(new { key_id = keyId, status = "registered" });
        return ok;
    }

    private static bool IsAuthorized(HttpRequestData req)
    {
        var apiKey = Environment.GetEnvironmentVariable("PURFLE_REGISTRY_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) return false;
        req.Headers.TryGetValues("X-Purfle-Key", out var values);
        return values?.FirstOrDefault() == apiKey;
    }
}
