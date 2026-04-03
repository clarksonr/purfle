namespace Purfle.Runtime.CrossAgent;

/// <summary>
/// A single output record from a peer agent's run history.
/// </summary>
public sealed record AgentOutputRecord(
    string RunId,
    DateTimeOffset Timestamp,
    string Content,
    AgentOutputStatus Status);

public enum AgentOutputStatus
{
    Success,
    Error,
}
