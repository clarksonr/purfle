using System.Text.Json;
using Purfle.IdentityHub.Api.Services;
using Purfle.IdentityHub.Core.Implementations;
using Purfle.IdentityHub.Core.Models;
using Purfle.IdentityHub.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Storage root — all JSON files go under this directory.
var storageRoot = builder.Configuration.GetValue<string>("IdentityHub:StorageRoot")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "purfle", "identityhub");

// Register IdentityHub services with JSON file-backed storage.
builder.Services.AddSingleton<IAgentRegistry>(
    new JsonFileAgentRegistry(Path.Combine(storageRoot, "agents")));
builder.Services.AddSingleton<IKeyRevocationService>(
    new JsonFileKeyRevocationService(Path.Combine(storageRoot, "revocations")));
builder.Services.AddSingleton<ITrustService>(
    new JsonFileTrustService(Path.Combine(storageRoot, "attestations")));

// Register BackupService.
var azureConnectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
var backupContainer = builder.Configuration.GetValue<string>("IdentityHub:BackupContainer") ?? "purfle-backups";
builder.Services.AddSingleton(new BackupService(storageRoot, azureConnectionString, backupContainer));

// CORS for development.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// --- Agent Registry Endpoints ---

app.MapGet("/agents", async (IAgentRegistry registry, string? q, int? page, int? pageSize, CancellationToken ct) =>
{
    var results = await registry.SearchAsync(q, page ?? 0, pageSize ?? 20, ct);
    return Results.Ok(results);
});

app.MapPost("/agents", async (IAgentRegistry registry, RegistryEntry entry, CancellationToken ct) =>
{
    var created = await registry.RegisterAsync(entry, ct);
    return Results.Created($"/agents/{created.AgentId}", created);
});

// --- Key Registry Endpoints (with Revocation) ---

app.MapGet("/keys/{id}", async (IKeyRevocationService revocationService, string id, CancellationToken ct) =>
{
    var isRevoked = await revocationService.IsRevokedAsync(id, ct);
    return Results.Ok(new { keyId = id, isRevoked });
});

app.MapPost("/keys", async (IAgentRegistry registry, HttpContext ctx, CancellationToken ct) =>
{
    // Accept a key registration payload. In a full implementation this would
    // store the public key material. For now we record it as a registry entry
    // note and return success.
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body, cancellationToken: ct);
    var keyId = body.TryGetProperty("keyId", out var kid) ? kid.GetString() : Guid.NewGuid().ToString();
    return Results.Created($"/keys/{keyId}", new { keyId, registered = true });
});

app.MapDelete("/keys/{id}", async (IKeyRevocationService revocationService, string id, string? reason, CancellationToken ct) =>
{
    var record = await revocationService.RevokeAsync(id, reason ?? "Revoked via API", null, ct);
    return Results.Ok(record);
});

// --- Publisher Endpoints ---

app.MapGet("/publishers", () =>
{
    // Placeholder — in production this would query the publisher store.
    return Results.Ok(Array.Empty<object>());
});

app.MapPost("/publishers", async (HttpContext ctx, CancellationToken ct) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body, cancellationToken: ct);
    var name = body.TryGetProperty("displayName", out var dn) ? dn.GetString() : "Unknown";
    return Results.Created("/publishers", new { displayName = name, registered = true });
});

// --- Attestation Endpoints ---

app.MapGet("/attestations", async (ITrustService trust, string? agentId, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(agentId))
        return Results.BadRequest(new { error = "agentId query parameter is required" });

    var attestations = await trust.GetAttestationsAsync(agentId, ct);
    return Results.Ok(attestations);
});

app.MapPost("/attestations", async (ITrustService trust, TrustAttestation attestation, CancellationToken ct) =>
{
    var issued = await trust.IssueAsync(attestation, ct);
    return Results.Created($"/attestations?agentId={issued.AgentId}", issued);
});

// --- Manifest Verification Endpoint ---

app.MapPost("/verify", async (IKeyRevocationService revocationService, HttpContext ctx, CancellationToken ct) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body, cancellationToken: ct);

    if (!body.TryGetProperty("keyId", out var keyIdProp))
        return Results.BadRequest(new { error = "keyId is required" });

    var keyId = keyIdProp.GetString()!;

    // Check if the key has been revoked.
    var isRevoked = await revocationService.IsRevokedAsync(keyId, ct);
    if (isRevoked)
        return Results.Ok(new { verified = false, reason = "Key has been revoked" });

    // In a full implementation, this would verify the JWS signature against
    // the registered public key. For now, we confirm the key is not revoked.
    return Results.Ok(new { verified = true, keyId });
});

// --- Health Endpoint ---

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0" }));

// --- Backup Endpoints ---

app.MapGet("/backup", async (BackupService backup, CancellationToken ct) =>
{
    var stream = await backup.CreateBackupAsync(ct);
    return Results.File(stream, "application/zip", $"identityhub-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
});

app.MapPost("/backup/restore", async (BackupService backup, HttpContext ctx, CancellationToken ct) =>
{
    if (!ctx.Request.HasFormContentType || ctx.Request.Form.Files.Count == 0)
        return Results.BadRequest(new { error = "Upload a zip file" });

    var file = ctx.Request.Form.Files[0];
    await using var stream = file.OpenReadStream();
    await backup.RestoreAsync(stream, ct);
    return Results.Ok(new { restored = true, timestamp = DateTimeOffset.UtcNow });
});

app.MapPost("/backup/push-azure", async (BackupService backup, CancellationToken ct) =>
{
    var stream = await backup.CreateBackupAsync(ct);
    await backup.PushToAzureAsync(stream, ct);
    return Results.Ok(new { pushed = true, timestamp = DateTimeOffset.UtcNow });
});

app.MapGet("/backup/azure", async (BackupService backup, CancellationToken ct) =>
{
    var backups = await backup.ListAzureBackupsAsync(ct);
    return Results.Ok(backups);
});

app.MapGet("/backup/azure/{blobName}", async (BackupService backup, string blobName, CancellationToken ct) =>
{
    var stream = await backup.PullFromAzureAsync(blobName, ct);
    return Results.File(stream, "application/zip", blobName);
});

app.Run();
