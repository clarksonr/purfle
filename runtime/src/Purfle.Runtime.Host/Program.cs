using Purfle.Runtime;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Host;
using Purfle.Runtime.Sandbox;

Console.WriteLine("=== Purfle Runtime Host ===");
Console.WriteLine();

// ── Wire the live key registry ────────────────────────────────────────────────

var registryBaseUrl = Environment.GetEnvironmentVariable("PURFLE_REGISTRY_URL")
    ?? "https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net";

var registry = new HttpKeyRegistryClient(registryBaseUrl);
Console.WriteLine($"[registry] {registryBaseUrl}");

// ── Load a pre-signed manifest ────────────────────────────────────────────────

var manifestFile = Environment.GetEnvironmentVariable("PURFLE_MANIFEST")
    ?? "demo-agent.agent.json";

var manifestPath = Path.IsPathRooted(manifestFile)
    ? manifestFile
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
        "..", "..", "..", "spec", "examples", manifestFile));

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"[error] Manifest not found at: {manifestPath}");
    Console.Error.WriteLine("        Copy a signed manifest to spec/examples/demo-agent.agent.json");
    return 1;
}

var signedJson = await File.ReadAllTextAsync(manifestPath);
Console.WriteLine($"[load]  Read {Path.GetFileName(manifestPath)}");

// ── Build the loader ──────────────────────────────────────────────────────────

var loader = new AgentLoader(
    new IdentityVerifier(registry),
    new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
    },
    new AdapterFactory());

// ── Happy path ────────────────────────────────────────────────────────────────

Console.WriteLine("[load]  Running load sequence...");
Console.WriteLine();

var result = await loader.LoadAsync(signedJson);

if (result.Success)
{
    var m = result.Manifest!;
    Console.WriteLine("  Step 1  PARSE              ✓");
    Console.WriteLine("  Step 2  SCHEMA VALIDATION  ✓");
    Console.WriteLine("  Step 3  IDENTITY           ✓  signature valid · not expired · key from live registry");
    Console.WriteLine("  Step 4  CAPABILITY NEG.    ✓");
    Console.WriteLine("  Step 5  PERMISSION BIND    ✓");
    Console.WriteLine("  Step 6  I/O SCHEMA         ✓");
    Console.WriteLine();
    Console.WriteLine("  Agent loaded.");
    Console.WriteLine($"    name:    {m.Name}");
    Console.WriteLine($"    version: {m.Version}");
    Console.WriteLine($"    engine:  {m.Runtime.Engine}  model={m.Runtime.Model ?? "(none)"}");
    Console.WriteLine($"    author:  {m.Identity.Author} <{m.Identity.Email}>");
    Console.WriteLine($"    key_id:  {m.Identity.KeyId}");
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

Console.WriteLine("--- Single-turn invocation ---");
Console.WriteLine();

var reply = await result.Adapter!.InvokeAsync(
    systemPrompt: "You are a helpful assistant running inside the Purfle AIVM. Be concise.",
    userMessage:  "Say hello and describe yourself in one sentence.");

Console.WriteLine($"[agent]  {reply}");
Console.WriteLine();

// ── Tamper demo ───────────────────────────────────────────────────────────────

Console.WriteLine("--- Tamper demo ---");
Console.WriteLine();

var tampered = signedJson.Replace(result.Manifest!.Name, "Tampered Agent");
var tamperResult = await loader.LoadAsync(tampered);

Console.WriteLine(tamperResult.Success
    ? "  BUG: tampered manifest loaded."
    : $"  ✓  Rejected [{tamperResult.FailureReason}]");

Console.WriteLine();
return 0;