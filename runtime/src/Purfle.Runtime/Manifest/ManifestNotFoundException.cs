namespace Purfle.Runtime.Manifest;

/// <summary>
/// Thrown by <see cref="ManifestLoader"/> when the manifest file does not exist
/// or cannot be opened for reading.
/// </summary>
public sealed class ManifestNotFoundException : Exception
{
    public string ManifestPath { get; }

    public ManifestNotFoundException(string manifestPath)
        : base($"Manifest file not found: '{manifestPath}'.")
    {
        ManifestPath = manifestPath;
    }

    public ManifestNotFoundException(string manifestPath, Exception inner)
        : base($"Cannot read manifest file: '{manifestPath}'.", inner)
    {
        ManifestPath = manifestPath;
    }
}
