using Purfle.Runtime.Lifecycle;

namespace Purfle.Runtime.Identity;

public sealed class VerificationResult
{
    public bool Success { get; private init; }
    public LoadFailureReason? FailureReason { get; private init; }
    public string? FailureMessage { get; private init; }

    public static VerificationResult Ok() => new() { Success = true };

    public static VerificationResult Fail(LoadFailureReason reason, string message) => new()
    {
        Success = false,
        FailureReason = reason,
        FailureMessage = message,
    };
}
