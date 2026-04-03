using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("marketplace", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Marketplace:BaseUrl"] ?? "http://localhost:5050");
});
builder.Services.AddHttpClient("identityhub", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["IdentityHub:BaseUrl"] ?? "http://localhost:5000");
});

var app = builder.Build();

var adminToken = app.Configuration["PURFLE_ADMIN_TOKEN"]
    ?? Environment.GetEnvironmentVariable("PURFLE_ADMIN_TOKEN")
    ?? "";

// --- Admin auth middleware for /admin API routes ---
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/admin"))
    {
        var auth = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(adminToken) || auth != $"Bearer {adminToken}")
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

// ============================================================
// API proxy endpoints — frontend JS calls these, we forward
// ============================================================

var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// --- Marketplace proxies ---

app.MapGet("/api/agents", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var qs = ctx.Request.QueryString;
    var resp = await client.GetAsync($"/api/agents{qs}");
    ctx.Response.StatusCode = (int)resp.StatusCode;
    ctx.Response.ContentType = "application/json";
    await resp.Content.CopyToAsync(ctx.Response.Body);
});

app.MapGet("/api/agents/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.GetAsync($"/api/agents/{id}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/publishers/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.GetAsync($"/api/publishers/{id}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/keys/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.GetAsync($"/api/keys/{id}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

// --- IdentityHub proxies ---

app.MapGet("/api/attestations", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var qs = ctx.Request.QueryString;
    var resp = await client.GetAsync($"/attestations{qs}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/hub/agents", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var qs = ctx.Request.QueryString;
    var resp = await client.GetAsync($"/agents{qs}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/hub/keys/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.GetAsync($"/keys/{id}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/hub/revocations", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.GetAsync("/keys");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

// ============================================================
// Admin API endpoints (bearer-protected above)
// ============================================================

// Admin stats
app.MapGet("/api/admin/stats", async (IHttpClientFactory hf) =>
{
    var mp = hf.CreateClient("marketplace");
    var ih = hf.CreateClient("identityhub");

    var agentsTask = mp.GetAsync("/api/agents?pageSize=1");
    var publishersTask = mp.GetAsync("/api/publishers/count");
    var hubAgentsTask = ih.GetAsync("/agents?pageSize=1");

    await Task.WhenAll(agentsTask, hubAgentsTask);

    int totalAgents = 0;
    try
    {
        var agentsResp = await agentsTask;
        var agentsJson = await agentsResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(agentsJson);
        if (doc.RootElement.TryGetProperty("totalCount", out var tc))
            totalAgents = tc.GetInt32();
    }
    catch { }

    return Results.Json(new { totalAgents, timestamp = DateTimeOffset.UtcNow });
});

// Admin: flag agent
app.MapPost("/api/admin/agents/{id}/flag", async (string id, HttpContext ctx, IHttpClientFactory hf) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var client = hf.CreateClient("identityhub");
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync($"/attestations", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: revoke agent (issues revocation attestation)
app.MapPost("/api/admin/agents/{id}/revoke", async (string id, HttpContext ctx, IHttpClientFactory hf) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var client = hf.CreateClient("identityhub");
    using var doc = JsonDocument.Parse(body);
    var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "Revoked by admin";

    var attestation = JsonSerializer.Serialize(new
    {
        agentId = id,
        type = "revoked",
        issuedBy = "admin",
        details = reason
    });
    var content = new StringContent(attestation, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync("/attestations", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: delete agent from marketplace
app.MapDelete("/api/admin/agents/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.DeleteAsync($"/api/agents/{id}");
    return Results.StatusCode((int)resp.StatusCode);
});

// Admin: verify publisher
app.MapPost("/api/admin/publishers/{id}/verify", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var attestation = JsonSerializer.Serialize(new
    {
        agentId = id,
        type = "publisher-verified",
        issuedBy = "admin",
        details = "Verified by admin"
    });
    var content = new StringContent(attestation, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync("/attestations", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: revoke publisher verification
app.MapPost("/api/admin/publishers/{id}/revoke", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var attestation = JsonSerializer.Serialize(new
    {
        agentId = id,
        type = "publisher-verification-revoked",
        issuedBy = "admin",
        details = "Verification revoked by admin"
    });
    var content = new StringContent(attestation, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync("/attestations", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: delete publisher
app.MapDelete("/api/admin/publishers/{id}", async (string id, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.DeleteAsync($"/api/publishers/{id}");
    return Results.StatusCode((int)resp.StatusCode);
});

// Admin: register key
app.MapPost("/api/admin/keys", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var client = hf.CreateClient("identityhub");
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync("/keys", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: revoke key
app.MapDelete("/api/admin/keys/{id}", async (string id, HttpContext ctx, IHttpClientFactory hf) =>
{
    var reason = ctx.Request.Query["reason"].FirstOrDefault() ?? "Revoked by admin";
    var client = hf.CreateClient("identityhub");
    var resp = await client.DeleteAsync($"/keys/{id}?reason={Uri.EscapeDataString(reason)}");
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: list all keys
app.MapGet("/api/admin/keys", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.GetAsync("/api/keys");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: issue attestation
app.MapPost("/api/admin/attestations", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var client = hf.CreateClient("identityhub");
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var resp = await client.PostAsync("/attestations", content);
    var result = await resp.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json", statusCode: (int)resp.StatusCode);
});

// Admin: list attestations
app.MapGet("/api/admin/attestations", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var qs = ctx.Request.QueryString;
    var resp = await client.GetAsync($"/attestations{qs}");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

// ============================================================
// Health endpoint
// ============================================================

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0" }));

// ============================================================
// Admin backup/restore endpoints (proxied to IdentityHub API)
// ============================================================

app.MapGet("/api/admin/backup/download", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.GetAsync("/backup");
    if (!resp.IsSuccessStatusCode)
        return Results.StatusCode((int)resp.StatusCode);

    var stream = await resp.Content.ReadAsStreamAsync();
    var fileName = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
        ?? $"identityhub-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
    return Results.File(stream, "application/zip", fileName);
});

app.MapPost("/api/admin/backup/push-azure", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.PostAsync("/backup/push-azure", null);
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/admin/backup/azure", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.GetAsync("/backup/azure");
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

app.MapGet("/api/admin/backup/azure/{blobName}", async (string blobName, IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("identityhub");
    var resp = await client.GetAsync($"/backup/azure/{Uri.EscapeDataString(blobName)}");
    if (!resp.IsSuccessStatusCode)
        return Results.StatusCode((int)resp.StatusCode);

    var stream = await resp.Content.ReadAsStreamAsync();
    return Results.File(stream, "application/zip", blobName);
});

app.MapPost("/api/admin/backup/restore", async (HttpContext ctx, IHttpClientFactory hf) =>
{
    if (!ctx.Request.HasFormContentType || ctx.Request.Form.Files.Count == 0)
        return Results.BadRequest(new { error = "Upload a zip file" });

    var file = ctx.Request.Form.Files[0];
    var client = hf.CreateClient("identityhub");
    using var content = new MultipartFormDataContent();
    var streamContent = new StreamContent(file.OpenReadStream());
    content.Add(streamContent, "file", file.FileName);
    var resp = await client.PostAsync("/backup/restore", content);
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
});

// ============================================================
// SVG Badge — /badge/{agentId}
// ============================================================

app.MapGet("/badge/{agentId}", async (string agentId, IHttpClientFactory hf) =>
{
    bool verified = false;
    try
    {
        var client = hf.CreateClient("identityhub");
        var resp = await client.GetAsync($"/attestations?agentId={agentId}");
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var att in doc.RootElement.EnumerateArray())
                {
                    if (att.TryGetProperty("type", out var t) &&
                        (t.GetString() == "publisher-verified" || t.GetString() == "marketplace-listed"))
                    {
                        verified = true;
                        break;
                    }
                }
            }
        }
    }
    catch { }

    var label = "Available on Purfle";
    var value = verified ? "Verified" : "Listed";
    var color = verified ? "#0D7377" : "#6e7681";
    var labelWidth = 130;
    var valueWidth = verified ? 58 : 42;
    var totalWidth = labelWidth + valueWidth;

    var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{totalWidth}"" height=""20"">
  <linearGradient id=""b"" x2=""0"" y2=""100%"">
    <stop offset=""0"" stop-color=""#bbb"" stop-opacity="".1""/>
    <stop offset=""1"" stop-opacity="".1""/>
  </linearGradient>
  <clipPath id=""r""><rect width=""{totalWidth}"" height=""20"" rx=""3""/></clipPath>
  <g clip-path=""url(#r)"">
    <rect width=""{labelWidth}"" height=""20"" fill=""#555""/>
    <rect x=""{labelWidth}"" width=""{valueWidth}"" height=""20"" fill=""{color}""/>
    <rect width=""{totalWidth}"" height=""20"" fill=""url(#b)""/>
  </g>
  <g fill=""#fff"" text-anchor=""middle"" font-family=""DejaVu Sans,Verdana,Geneva,sans-serif"" font-size=""11"">
    <text x=""{labelWidth / 2}"" y=""15"" fill=""#010101"" fill-opacity="".3"">{label}</text>
    <text x=""{labelWidth / 2}"" y=""14"">{label}</text>
    <text x=""{labelWidth + valueWidth / 2}"" y=""15"" fill=""#010101"" fill-opacity="".3"">{value}</text>
    <text x=""{labelWidth + valueWidth / 2}"" y=""14"">{value}</text>
  </g>
</svg>";

    return Results.Content(svg, "image/svg+xml");
}).CacheOutput(p => p.Expire(TimeSpan.FromMinutes(10)));

// ============================================================
// RSS/Atom Feed — /feed.xml
// ============================================================

app.MapGet("/feed.xml", async (IHttpClientFactory hf) =>
{
    var client = hf.CreateClient("marketplace");
    var resp = await client.GetAsync("/api/agents?pageSize=20&page=1");
    var body = await resp.Content.ReadAsStringAsync();

    var items = new List<SyndicationItem>();
    try
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("agents", out var agents))
        {
            foreach (var a in agents.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var desc = a.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var agentId = a.TryGetProperty("agentId", out var ai) ? ai.GetString() ?? "" : "";
                var author = a.TryGetProperty("author", out var au) ? au.GetString() ?? "" : "";
                var published = a.TryGetProperty("publishedAt", out var p)
                    ? DateTimeOffset.TryParse(p.GetString(), out var dt) ? dt : DateTimeOffset.UtcNow
                    : DateTimeOffset.UtcNow;

                var item = new SyndicationItem(
                    name,
                    desc,
                    new Uri($"/agents/{agentId}", UriKind.Relative))
                {
                    PublishDate = published,
                    Id = agentId
                };
                if (!string.IsNullOrEmpty(author))
                    item.Authors.Add(new SyndicationPerson { Name = author });
                items.Add(item);
            }
        }
    }
    catch { }

    var feed = new SyndicationFeed(
        "Purfle — Trusted AI Agents",
        "Newly listed and verified agents on the Purfle marketplace",
        new Uri("/", UriKind.Relative),
        "purfle-feed",
        items.Any() ? items.Max(i => i.PublishDate) : DateTimeOffset.UtcNow)
    {
        Items = items
    };

    using var ms = new MemoryStream();
    using var writer = XmlWriter.Create(ms, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });
    feed.SaveAsAtom10(writer);
    writer.Flush();

    return Results.Content(Encoding.UTF8.GetString(ms.ToArray()), "application/atom+xml; charset=utf-8");
});

// ============================================================
// SPA fallback — serve index.html for client-side routes
// ============================================================

app.MapFallbackToFile("index.html");

app.Run();
