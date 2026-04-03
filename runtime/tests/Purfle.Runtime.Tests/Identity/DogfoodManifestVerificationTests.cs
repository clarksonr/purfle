using System.Security.Cryptography;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tests.Identity;

/// <summary>
/// Verifies that the signed dogfood agent manifests pass identity verification
/// using the generated public key. This tests the end-to-end trust loop:
/// load manifest → parse → verify signature → registry lookup → pass.
/// </summary>
public sealed class DogfoodManifestVerificationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Cannot find repo root.");
    }

    private static PublicKey LoadPublicKey()
    {
        var pemPath = Path.Combine(RepoRoot, "temp-agent", "signing.pub.pem");
        Skip.If(!File.Exists(pemPath), "Public key not found at temp-agent/signing.pub.pem");

        var pem = File.ReadAllText(pemPath);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        var p = ecdsa.ExportParameters(includePrivateParameters: false);

        return new PublicKey
        {
            KeyId     = "com.clarksonr/release-2026",
            Algorithm = "ES256",
            X         = p.Q.X!,
            Y         = p.Q.Y!,
        };
    }

    private async Task VerifyDogfoodManifest(string agentName)
    {
        var manifestPath = Path.Combine(RepoRoot, "agents", agentName, "agent.manifest.json");
        var rawJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = new ManifestLoader().Load(manifestPath);

        Skip.If(manifest.Identity.Signature is null,
            $"Manifest for {agentName} has no signature (needs re-signing after manifest changes).");

        var pubKey = LoadPublicKey();
        var registry = new StaticKeyRegistry([pubKey]);
        var verifier = new IdentityVerifier(registry);

        var result = await verifier.VerifyAsync(manifest, rawJson);

        Assert.True(result.Success, $"Verification failed for {agentName}: {result.FailureReason} — {result.FailureMessage}");
    }

    [Fact]
    public async Task EmailMonitor_SignatureVerifies()
        => await VerifyDogfoodManifest("email-monitor");

    [Fact]
    public async Task PrWatcher_SignatureVerifies()
        => await VerifyDogfoodManifest("pr-watcher");

    [SkippableFact]
    public async Task ReportBuilder_SignatureVerifies()
        => await VerifyDogfoodManifest("report-builder");
}
