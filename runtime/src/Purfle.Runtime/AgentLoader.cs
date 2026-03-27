using Purfle.Runtime.Adapters;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime;

/// <summary>
/// Orchestrates the full agent load sequence defined in spec §4.
/// Steps execute in order; failure at any step aborts loading.
/// </summary>
public sealed class AgentLoader(
    ManifestLoader manifestLoader,
    IdentityVerifier identityVerifier,
    IReadOnlySet<string> runtimeCapabilities,
    IAdapterFactory? adapterFactory = null)
{
    public async Task<LoadResult> LoadAsync(string manifestJson, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Step 1 + 2: parse and schema validation
        var parsed = manifestLoader.Load(manifestJson);
        if (!parsed.Success)
            return LoadResult.Fail(parsed.FailureReason!.Value, parsed.FailureMessage!);

        var manifest = parsed.Manifest!;
        var rawJson = parsed.RawJson!;

        // Step 3: identity verification
        var verified = await identityVerifier.VerifyAsync(manifest, rawJson, ct);
        if (!verified.Success)
            return LoadResult.Fail(verified.FailureReason!.Value, verified.FailureMessage!);

        // Step 4: capability negotiation
        var negotiation = CapabilityNegotiator.Negotiate(manifest.Capabilities, runtimeCapabilities);

        foreach (var absent in negotiation.MissingOptional)
            warnings.Add($"Optional capability '{absent.Id}' is not available on this runtime.");

        if (!negotiation.Success)
        {
            var ids = string.Join(", ", negotiation.MissingRequired.Select(c => $"'{c.Id}'"));
            return LoadResult.Fail(
                LoadFailureReason.CapabilityMissing,
                $"Required capabilities not available on this runtime: {ids}.",
                warnings);
        }

        // Step 5: permission binding (sandbox construction)
        var sandbox = new AgentSandbox(manifest.Permissions);

        // Step 6: I/O schema compilation (validate that io.input and io.output are valid JSON objects)
        // Full schema compilation into validators is deferred to the invocation layer.
        // Here we confirm the fields are present and non-empty objects (already guaranteed by schema
        // validation in step 2), so this step is a no-op in v0.1.

        // Step 7: resolve the inference adapter for runtime.engine and initialize.
        IInferenceAdapter? adapter = null;
        if (adapterFactory is not null)
        {
            try
            {
                adapter = adapterFactory.Create(manifest, sandbox);
            }
            catch (NotSupportedException ex)
            {
                return LoadResult.Fail(
                    LoadFailureReason.EngineNotSupported,
                    ex.Message,
                    warnings);
            }
        }

        return LoadResult.Ok(manifest, sandbox, warnings, adapter);
    }
}
