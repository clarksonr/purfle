namespace Purfle.Runtime.CrossAgent;

/// <summary>
/// Thrown when an agent attempts to read another agent's output without
/// declaring it in io.reads. The AIVM enforces this at the sandbox layer.
/// </summary>
public sealed class AgentSandboxViolationException : Exception
{
    public string RequestingAgentId { get; }
    public string TargetAgentId { get; }

    public AgentSandboxViolationException(string requestingAgentId, string targetAgentId)
        : base($"Agent '{requestingAgentId}' attempted to read output of '{targetAgentId}' " +
               $"but '{targetAgentId}' is not declared in io.reads. " +
               $"Add 'agent.read' capability and declare '{targetAgentId}' in io.reads to allow this.")
    {
        RequestingAgentId = requestingAgentId;
        TargetAgentId = targetAgentId;
    }
}
