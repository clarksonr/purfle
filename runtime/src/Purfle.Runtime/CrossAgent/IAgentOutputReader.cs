namespace Purfle.Runtime.CrossAgent;

/// <summary>
/// Reads output files from peer agents. The AIVM provides a scoped instance
/// that only allows reading agents declared in the manifest's io.reads list.
/// </summary>
public interface IAgentOutputReader
{
    /// <summary>
    /// Reads the most recent output for <paramref name="agentId"/>.
    /// Returns null if the agent has not produced output yet.
    /// </summary>
    /// <exception cref="AgentSandboxViolationException">
    /// Thrown if <paramref name="agentId"/> is not in the manifest's io.reads allowlist.
    /// </exception>
    Task<string?> ReadLatestAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Reads the most recent <paramref name="maxRuns"/> output records for
    /// <paramref name="agentId"/>, newest first.
    /// </summary>
    /// <exception cref="AgentSandboxViolationException">
    /// Thrown if <paramref name="agentId"/> is not in the manifest's io.reads allowlist.
    /// </exception>
    Task<IReadOnlyList<AgentOutputRecord>> ReadHistoryAsync(
        string agentId, int maxRuns, CancellationToken ct = default);
}
