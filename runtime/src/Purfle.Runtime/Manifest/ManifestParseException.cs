namespace Purfle.Runtime.Manifest;

/// <summary>
/// Thrown by <see cref="ManifestLoader"/> when the manifest JSON is malformed
/// or is missing a required field.
/// </summary>
public sealed class ManifestParseException : Exception
{
    public ManifestParseException(string message) : base(message) { }

    public ManifestParseException(string message, Exception inner) : base(message, inner) { }
}
