namespace Purfle.Runtime.Adapters;

/// <summary>
/// Thrown when an <see cref="ILlmAdapter"/> encounters an API-level error.
/// </summary>
public sealed class LlmAdapterException : Exception
{
    public LlmAdapterException(string message) : base(message) { }
    public LlmAdapterException(string message, Exception inner) : base(message, inner) { }
}
