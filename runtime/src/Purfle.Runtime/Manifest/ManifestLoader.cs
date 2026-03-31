using System.Text.Json;

namespace Purfle.Runtime.Manifest;

/// <summary>
/// Loads an agent manifest from a file path on disk.
/// Performs JSON deserialization only; schema and identity validation are
/// handled by <see cref="AgentLoader"/> during the full load sequence.
/// </summary>
public sealed class ManifestLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>
    /// Reads and deserializes an agent manifest from <paramref name="manifestPath"/>.
    /// </summary>
    /// <param name="manifestPath">Absolute or relative path to the manifest JSON file.</param>
    /// <returns>The deserialized <see cref="AgentManifest"/>.</returns>
    /// <exception cref="ManifestNotFoundException">
    /// Thrown when the file does not exist or cannot be opened for reading.
    /// </exception>
    /// <exception cref="ManifestParseException">
    /// Thrown when the JSON is malformed or a required field is missing.
    /// </exception>
    public AgentManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new ManifestNotFoundException(manifestPath);

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ManifestNotFoundException(manifestPath, ex);
        }

        return ParseJson(json);
    }

    /// <summary>
    /// Deserializes <paramref name="json"/> into an <see cref="AgentManifest"/>.
    /// Used internally by <see cref="AgentLoader"/> for the JSON-string based load path.
    /// </summary>
    internal static AgentManifest ParseJson(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<AgentManifest>(json, s_options)
                ?? throw new ManifestParseException("Manifest JSON deserialized to null.");
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new ManifestParseException($"Manifest JSON is invalid: {ex.Message}", ex);
        }
    }
}
