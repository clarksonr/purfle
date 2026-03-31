using System.Runtime.Loader;
using System.Text.Json;
using Json.Schema;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Assembly;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;

namespace Purfle.Runtime;

/// <summary>
/// Orchestrates the full agent load sequence defined in spec §4.
/// Steps execute in order; failure at any step aborts loading.
/// </summary>
public sealed class AgentLoader(
    IdentityVerifier identityVerifier,
    IReadOnlySet<string> runtimeCapabilities,
    IAdapterFactory? adapterFactory = null)
{
    private const string AgentDllName = "agent.dll";

    private static readonly JsonSchema s_manifestSchema;

    static AgentLoader()
    {
        var identitySchema = JsonSchema.FromText(EmbeddedSchemas.AgentIdentity);
        SchemaRegistry.Global.Register(identitySchema);
        s_manifestSchema = JsonSchema.FromText(EmbeddedSchemas.AgentManifest);
    }

    public async Task<LoadResult> LoadAsync(
        string manifestJson,
        string? assembliesDirectory = null,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Step 1 — parse JSON
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException ex)
        {
            return LoadResult.Fail(LoadFailureReason.MalformedJson, ex.Message);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return LoadResult.Fail(LoadFailureReason.MalformedJson, "Manifest root must be a JSON object.");

            // Step 2 — schema validation
            var evaluation = s_manifestSchema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                RequireFormatValidation = true,
            });

            if (!evaluation.IsValid)
            {
                var details = evaluation.Details ?? [];
                var errors = details
                    .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
                    .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"))
                    .ToList();

                return LoadResult.Fail(
                    LoadFailureReason.SchemaValidationFailed,
                    string.Join("; ", errors.DefaultIfEmpty("Schema validation failed.")));
            }
        }

        // Deserialize into typed manifest
        AgentManifest manifest;
        try
        {
            manifest = ManifestLoader.ParseJson(manifestJson);
        }
        catch (ManifestParseException ex)
        {
            return LoadResult.Fail(LoadFailureReason.MalformedJson, ex.Message);
        }

        var rawJson = manifestJson;

        // Step 3 — identity verification
        var verified = await identityVerifier.VerifyAsync(manifest, rawJson, ct);
        if (!verified.Success)
            return LoadResult.Fail(verified.FailureReason!.Value, verified.FailureMessage!);

        // Step 4 — capability negotiation
        var negotiation = CapabilityNegotiator.Negotiate(manifest.Capabilities, runtimeCapabilities);

        foreach (var absent in negotiation.MissingOptional)
            warnings.Add($"Optional capability '{absent}' is not available on this runtime.");

        if (!negotiation.Success)
        {
            var ids = string.Join(", ", negotiation.MissingRequired.Select(c => $"'{c}'"));
            return LoadResult.Fail(
                LoadFailureReason.CapabilityMissing,
                $"Required capabilities not available on this runtime: {ids}.",
                warnings);
        }

        // Step 5 — permission binding (sandbox construction)
        var sandbox = new AgentSandbox(manifest.Permissions);

        // Step 6 — I/O schema compilation (deferred in v0.1)

        // Step 7 — load assembly (if present) then resolve the inference adapter
        IAgent? agentInstance = null;
        AssemblyLoadContext? loadContext = null;

        if (assembliesDirectory is not null && Directory.Exists(assembliesDirectory))
        {
            var assemblyPath = Path.Combine(assembliesDirectory, AgentDllName);

            if (!File.Exists(assemblyPath))
                return LoadResult.Fail(
                    LoadFailureReason.AssemblyLoadFailed,
                    $"Expected agent assembly not found at '{assemblyPath}'.",
                    warnings);

            try
            {
                var alc      = new AgentAssemblyLoadContext(assembliesDirectory);
                var assembly = alc.LoadFromAssemblyPath(assemblyPath);

                var agentType = assembly.GetExportedTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => typeof(IAgent).IsAssignableFrom(t))
                    .ToList();

                if (agentType.Count == 0)
                    return LoadResult.Fail(
                        LoadFailureReason.AssemblyEntryPointMissing,
                        "No exported type implementing Purfle.Sdk.IAgent was found in the agent assembly.",
                        warnings);

                if (agentType.Count > 1)
                    return LoadResult.Fail(
                        LoadFailureReason.AssemblyEntryPointMissing,
                        $"Multiple types implementing Purfle.Sdk.IAgent were found: " +
                        $"{string.Join(", ", agentType.Select(t => t.FullName))}. " +
                        "An agent assembly must contain exactly one.",
                        warnings);

                agentInstance = (IAgent)Activator.CreateInstance(agentType[0])!;
                loadContext   = alc;
            }
            catch (Exception ex)
            {
                return LoadResult.Fail(
                    LoadFailureReason.AssemblyLoadFailed,
                    $"Failed to load agent assembly: {ex.Message}",
                    warnings);
            }
        }

        IInferenceAdapter? adapter = null;
        if (adapterFactory is not null)
        {
            try
            {
                adapter = adapterFactory.Create(manifest, sandbox, agentInstance);
            }
            catch (NotSupportedException ex)
            {
                return LoadResult.Fail(
                    LoadFailureReason.EngineNotSupported,
                    ex.Message,
                    warnings);
            }
            catch (Exception ex)
            {
                return LoadResult.Fail(
                    LoadFailureReason.InitFailed,
                    ex.Message,
                    warnings);
            }
        }

        return LoadResult.Ok(manifest, sandbox, warnings, adapter, agentInstance, loadContext);
    }
}
