using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Purfle.Runtime;
using Purfle.Runtime.Host;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

// ── Generate a signing key ────────────────────────────────────────────────────

Console.WriteLine("=== Purfle Runtime Host ===");
Console.WriteLine();

using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var p = ecKey.ExportParameters(includePrivateParameters: false);
var publicKey = new PublicKey
{
    KeyId     = "demo-key-001",
    Algorithm = "ES256",
    X = p.Q.X!,
    Y = p.Q.Y!,
};

Console.WriteLine($"[key]  Generated P-256 key pair  key_id={publicKey.KeyId}");

// ── Load the hello-world manifest from spec/examples/ ─────────────────────────

// When invoked via `dotnet run` from runtime/, cwd is the runtime directory.
var manifestPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(),
        "..", "spec", "examples", "demo-agent.agent.json"));

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"[error] Manifest not found at: {manifestPath}");
    return 1;
}

var rawManifestJson = await File.ReadAllTextAsync(manifestPath);
Console.WriteLine($"[load] Read {Path.GetFileName(manifestPath)}");

// ── Sign the manifest with the demo key ───────────────────────────────────────

var signedJson = Sign(rawManifestJson, ecKey, publicKey.KeyId);
Console.WriteLine("[sign] Signed with demo key");

// ── Build the loader ──────────────────────────────────────────────────────────

var registry = new StaticKeyRegistry([publicKey]);
var loader = new AgentLoader(
    manifestLoader:      new ManifestLoader(),
    identityVerifier:    new IdentityVerifier(registry),
    runtimeCapabilities: new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.WebSearch,
    },
    adapterFactory:      new AdapterFactory());

// ── Happy path ────────────────────────────────────────────────────────────────

Console.WriteLine("[load] Running load sequence...");
Console.WriteLine();

var result = await loader.LoadAsync(signedJson);

if (result.Success)
{
    var m = result.Manifest!;
    Console.WriteLine("  Step 1  PARSE              ✓");
    Console.WriteLine("  Step 2  SCHEMA VALIDATION  ✓");
    Console.WriteLine("  Step 3  IDENTITY           ✓  signature valid · not expired");
    Console.WriteLine("  Step 4  CAPABILITY NEG.    ✓  no required capabilities declared");
    Console.WriteLine("  Step 5  PERMISSION BIND    ✓  sandbox ready (empty permissions)");
    Console.WriteLine("  Step 6  I/O SCHEMA         ✓");
    Console.WriteLine();
    Console.WriteLine("  Agent loaded.");
    Console.WriteLine($"    name:    {m.Name}");
    Console.WriteLine($"    version: {m.Version}");
    Console.WriteLine($"    engine:  {m.Runtime.Engine}  model={m.Runtime.Model ?? "(none)"}");
    Console.WriteLine($"    author:  {m.Identity.Author} <{m.Identity.Email}>");
    Console.WriteLine($"    expires: {m.Identity.ExpiresAt:yyyy-MM-dd}");

    foreach (var w in result.Warnings)
        Console.WriteLine($"    ! {w}");
}
else
{
    Console.WriteLine($"  FAILED [{result.FailureReason}] {result.FailureMessage}");
    return 1;
}

Console.WriteLine();

// ── Invoke the agent ──────────────────────────────────────────────────────────

Console.WriteLine("--- Invocation ---");
Console.WriteLine();
Console.WriteLine("[invoke] Sending message to agent...");

var reply = await result.Adapter!.InvokeAsync(
    systemPrompt: "You are a helpful assistant running inside the Purfle AIVM. Be concise.",
    userMessage:  "Say hello and describe yourself in one sentence.");

Console.WriteLine($"[agent]  {reply}");
Console.WriteLine();

// ── Tamper demo ───────────────────────────────────────────────────────────────

Console.WriteLine("--- Tamper demo ---");
Console.WriteLine();

var tampered = signedJson.Replace("\"Hello World\"", "\"Tampered Agent\"");
var tamperResult = await loader.LoadAsync(tampered);

Console.WriteLine(tamperResult.Success
    ? "  BUG: tampered manifest loaded."
    : $"  ✓  Rejected [{tamperResult.FailureReason}]");

Console.WriteLine();

// ── Capability negotiation failure demo ───────────────────────────────────────

Console.WriteLine("--- Capability negotiation demo ---");
Console.WriteLine();

// Build a manifest that requires code-execution, which this runtime doesn't offer
var capJson = AddRequiredCapability(signedJson, ecKey, publicKey.KeyId, "code-execution");
var capResult = await loader.LoadAsync(capJson);

Console.WriteLine(capResult.Success
    ? "  BUG: agent with unsatisfied required capability loaded."
    : $"  ✓  Rejected [{capResult.FailureReason}] {capResult.FailureMessage}");

Console.WriteLine();
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static string Sign(string manifestJson, ECDsa key, string keyId)
{
    // Inject key_id and a placeholder signature, then sign the canonical form.
    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(manifestJson)!;
    var identity = ToDictionary(dict["identity"]);
    identity["key_id"]    = StringElement(keyId);
    identity["signature"] = StringElement("placeholder");
    dict["identity"] = ObjectElement(identity);

    var withPlaceholder = JsonSerializer.Serialize(dict);
    var sig = ComputeJws(withPlaceholder, key, keyId);

    identity["signature"] = StringElement(sig);
    dict["identity"] = ObjectElement(identity);
    return JsonSerializer.Serialize(dict);
}

static string AddRequiredCapability(string manifestJson, ECDsa key, string keyId, string capId)
{
    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(manifestJson)!;
    var caps = new[] { new Dictionary<string, object> { ["id"] = capId, ["required"] = true } };
    dict["capabilities"] = JsonDocument.Parse(JsonSerializer.Serialize(caps)).RootElement;

    // Re-sign with the new capabilities.
    var identity = ToDictionary(dict["identity"]);
    identity["signature"] = StringElement("placeholder");
    dict["identity"] = ObjectElement(identity);

    var withPlaceholder = JsonSerializer.Serialize(dict);
    var sig = ComputeJws(withPlaceholder, key, keyId);

    identity["signature"] = StringElement(sig);
    dict["identity"] = ObjectElement(identity);
    return JsonSerializer.Serialize(dict);
}

static string ComputeJws(string manifestJson, ECDsa key, string keyId)
{
    var canonical  = CanonicalJson.ForSigning(manifestJson);
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
