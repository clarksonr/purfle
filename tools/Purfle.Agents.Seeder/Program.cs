using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// ── Locate directories ────────────────────────────────────────────────────────

// When run via `dotnet run` the cwd is the project dir (tools/Purfle.Agents.Seeder).
// The solution root is two levels up.
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var agentsDir    = Path.Combine(solutionRoot, "agents");
var dataDir      = Path.Combine(solutionRoot, "marketplace", "src", "Purfle.Marketplace.Api", "data");

Console.WriteLine("=== Purfle Agents Seeder ===");
Console.WriteLine($"  agents : {agentsDir}");
Console.WriteLine($"  data   : {dataDir}");
Console.WriteLine();

if (!Directory.Exists(agentsDir))
{
    Console.Error.WriteLine("[error] agents/ directory not found.");
    return 1;
}

// ── Generate a P-256 key pair ─────────────────────────────────────────────────

const string KeyId = "purfle-samples-key-001";

using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var ecParams = ecKey.ExportParameters(includePrivateParameters: false);

Console.WriteLine($"[key]  Generated P-256 key pair  key_id={KeyId}");

// ── Sign each manifest in agents/ ─────────────────────────────────────────────

var manifestFiles = Directory.GetFiles(agentsDir, "*.agent.json");
if (manifestFiles.Length == 0)
{
    Console.Error.WriteLine("[error] No *.agent.json files found in agents/.");
    return 1;
}

var signedManifests = new List<(string AgentId, string Name, string Description, string Version, string SignedJson)>();

foreach (var path in manifestFiles)
{
    var raw = await File.ReadAllTextAsync(path);
    var signed = Sign(raw, ecKey, KeyId);

    // Extract id/name/version from the signed JSON for storage records.
    using var doc = JsonDocument.Parse(signed);
    var root = doc.RootElement;
    var agentId    = root.GetProperty("id").GetString()!;
    var name       = root.GetProperty("name").GetString()!;
    var description = root.GetProperty("description").GetString()!;
    var version    = root.GetProperty("version").GetString()!;

    signedManifests.Add((agentId, name, description, version, signed));
    Console.WriteLine($"[sign] {Path.GetFileName(path)}  id={agentId}  v{version}");
}

Console.WriteLine();

// ── Build storage records ─────────────────────────────────────────────────────

const string PublisherId = "purfle-samples-publisher";
var now = DateTimeOffset.UtcNow;

var publisher = new Publisher
{
    Id          = PublisherId,
    DisplayName = "Purfle Samples",
    Email       = "roman@purfle.dev",
    IsVerified  = true,
    CreatedAt   = now,
};

var signingKey = new SigningKey
{
    Id           = Guid.NewGuid(),
    KeyId        = KeyId,
    PublisherId  = PublisherId,
    Algorithm    = "ES256",
    PublicKeyX   = ecParams.Q.X!,
    PublicKeyY   = ecParams.Q.Y!,
    IsRevoked    = false,
    CreatedAt    = now,
};

var listings = new List<AgentListing>();
var versions = new List<AgentVersion>();

foreach (var (agentId, name, description, version, _) in signedManifests)
{
    var listingId = Guid.NewGuid();
    listings.Add(new AgentListing
    {
        Id          = listingId,
        AgentId     = agentId,
        PublisherId = PublisherId,
        Name        = name,
        Description = description,
        IsListed    = true,
        CreatedAt   = now,
        UpdatedAt   = now,
    });

    versions.Add(new AgentVersion
    {
        Id             = Guid.NewGuid(),
        AgentListingId = listingId,
        Version        = version,
        ManifestBlobRef = $"manifests/{agentId}/{version}.json",
        SigningKeyId   = signingKey.Id,
        PublishedAt    = now,
        Downloads      = 0,
    });
}

// ── Write blob files ──────────────────────────────────────────────────────────

foreach (var (agentId, _, _, version, signedJson) in signedManifests)
{
    var blobPath = Path.Combine(dataDir, "blobs", "manifests", agentId, $"{version}.json");
    Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
    await File.WriteAllTextAsync(blobPath, signedJson);
    Console.WriteLine($"[blob] Wrote {Path.GetRelativePath(dataDir, blobPath)}");
}

Console.WriteLine();

// ── Write / merge JSON data files ─────────────────────────────────────────────

Directory.CreateDirectory(dataDir);

await MergeJsonFile<Publisher>(
    Path.Combine(dataDir, "publishers.json"),
    publisher,
    existing => existing.Id != PublisherId);

await MergeJsonFile<SigningKey>(
    Path.Combine(dataDir, "signing-keys.json"),
    signingKey,
    existing => existing.KeyId != KeyId);

foreach (var listing in listings)
    await MergeJsonFile<AgentListing>(
        Path.Combine(dataDir, "agent-listings.json"),
        listing,
        existing => existing.AgentId != listing.AgentId);

foreach (var version in versions)
    await MergeJsonFile<AgentVersion>(
        Path.Combine(dataDir, "agent-versions.json"),
        version,
        existing => existing.ManifestBlobRef != version.ManifestBlobRef);

Console.WriteLine();
Console.WriteLine("[done] Seeding complete.");
Console.WriteLine($"       {signedManifests.Count} agent(s) now listed in the marketplace.");
Console.WriteLine();
Console.WriteLine("  Start the marketplace API and verify:");
Console.WriteLine("    dotnet run --project marketplace/src/Purfle.Marketplace.Api");
Console.WriteLine("    curl http://localhost:5000/api/agents");
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task MergeJsonFile<T>(string path, T newItem, Func<T, bool> keepFilter)
{
    var opts = StorageJsonOptions();
    List<T> existing = [];

    if (File.Exists(path))
    {
        var raw = await File.ReadAllTextAsync(path);
        existing = JsonSerializer.Deserialize<List<T>>(raw, opts) ?? [];
    }

    // Remove stale entry (same logical key) then append the new one.
    existing = existing.Where(keepFilter).ToList();
    existing.Add(newItem);

    var json = JsonSerializer.Serialize(existing, opts);
    var tmp = path + ".tmp";
    await File.WriteAllTextAsync(tmp, json);
    File.Move(tmp, path, overwrite: true);

    Console.WriteLine($"[data] Wrote {Path.GetFileName(path)}  ({existing.Count} record(s))");
}

string Sign(string manifestJson, ECDsa key, string keyId)
{
    var dict     = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(manifestJson)!;
    var identity = ToDictionary(dict["identity"]);

    identity["key_id"]    = StringElement(keyId);
    identity["signature"] = StringElement("placeholder");
    dict["identity"]      = ObjectElement(identity);

    var withPlaceholder = JsonSerializer.Serialize(dict);
    var sig = ComputeJws(withPlaceholder, key, keyId);

    identity["signature"] = StringElement(sig);
    dict["identity"]      = ObjectElement(identity);
    return JsonSerializer.Serialize(dict);
}

string ComputeJws(string manifestJson, ECDsa key, string keyId)
{
    var canonical  = Purfle.Runtime.Manifest.CanonicalJson.ForSigning(manifestJson);
    var header     = $$$"""{"alg":"ES256","kid":"{{{keyId}}}"}""";
    var headerB64  = B64(Encoding.UTF8.GetBytes(header));
    var payloadB64 = B64(canonical);
    var input      = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
    var sig        = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    return $"{headerB64}.{payloadB64}.{B64(sig)}";
}

static string B64(byte[] b) =>
    Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

static Dictionary<string, JsonElement> ToDictionary(JsonElement el) =>
    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(el.GetRawText())!;

static JsonElement StringElement(string s) =>
    JsonDocument.Parse(JsonSerializer.Serialize(s)).RootElement;

static JsonElement ObjectElement(Dictionary<string, JsonElement> d) =>
    JsonDocument.Parse(JsonSerializer.Serialize(d)).RootElement;

static JsonSerializerOptions StorageJsonOptions() => new()
{
    WriteIndented   = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters      = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
};

// ── Inline storage entity types ───────────────────────────────────────────────
// Mirrors the marketplace entities so the seeder needs no project reference to them.

sealed class Publisher
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string? Email      { get; set; }
    public required string DisplayName { get; set; }
    public bool IsVerified    { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

sealed class SigningKey
{
    public Guid Id            { get; set; }
    public required string KeyId       { get; set; }
    public string PublisherId { get; set; } = null!;
    public required string Algorithm   { get; set; }
    public required byte[] PublicKeyX  { get; set; }
    public required byte[] PublicKeyY  { get; set; }
    public bool IsRevoked     { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt  { get; set; }
}

sealed class AgentListing
{
    public Guid Id            { get; set; }
    public required string AgentId     { get; set; }
    public string PublisherId { get; set; } = null!;
    public required string Name        { get; set; }
    public required string Description { get; set; }
    public bool IsListed      { get; set; } = true;
    public DateTimeOffset CreatedAt  { get; set; }
    public DateTimeOffset UpdatedAt  { get; set; }
}

sealed class AgentVersion
{
    public Guid Id               { get; set; }
    public Guid AgentListingId   { get; set; }
    public required string Version          { get; set; }
    public required string ManifestBlobRef  { get; set; }
    public Guid SigningKeyId     { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public long Downloads        { get; set; }
}
