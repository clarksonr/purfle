using System.Text.Json;
using Json.Schema;
using Purfle.Runtime.Lifecycle;

namespace Purfle.Runtime.Manifest;

/// <summary>
/// Implements load sequence steps 1 (parse) and 2 (schema validation).
/// </summary>
public sealed class ManifestLoader
{
    private static readonly JsonSchema s_manifestSchema;
    private static readonly JsonSerializerOptions s_deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    static ManifestLoader()
    {
        var identitySchema = JsonSchema.FromText(EmbeddedSchemas.AgentIdentity);
        SchemaRegistry.Global.Register(identitySchema);
        s_manifestSchema = JsonSchema.FromText(EmbeddedSchemas.AgentManifest);
    }

    public ParseResult Load(string manifestJson)
    {
        // Step 1 — parse: use JsonDocument to get a JsonElement for schema validation
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException ex)
        {
            return ParseResult.Fail(LoadFailureReason.MalformedJson, ex.Message);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return ParseResult.Fail(LoadFailureReason.MalformedJson, "Manifest JSON root must be an object.");

            // Step 2 — schema validation
            var evaluation = s_manifestSchema.Evaluate(root, new EvaluationOptions
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

                return ParseResult.Fail(
                    LoadFailureReason.SchemaValidationFailed,
                    string.Join("; ", errors.DefaultIfEmpty("Schema validation failed.")));
            }
        }

        // Deserialize into typed manifest (re-parse from string; doc is disposed)
        AgentManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AgentManifest>(manifestJson, s_deserializeOptions)
                ?? throw new JsonException("Deserialization returned null.");
        }
        catch (JsonException ex)
        {
            return ParseResult.Fail(LoadFailureReason.MalformedJson, $"Deserialization failed: {ex.Message}");
        }

        return ParseResult.Ok(manifest, manifestJson);
    }
}

public sealed class ParseResult
{
    public bool Success { get; private init; }
    public AgentManifest? Manifest { get; private init; }
    public string? RawJson { get; private init; }
    public LoadFailureReason? FailureReason { get; private init; }
    public string? FailureMessage { get; private init; }

    public static ParseResult Ok(AgentManifest manifest, string rawJson) => new()
    {
        Success = true,
        Manifest = manifest,
        RawJson = rawJson,
    };

    public static ParseResult Fail(LoadFailureReason reason, string message) => new()
    {
        Success = false,
        FailureReason = reason,
        FailureMessage = message,
    };
}
