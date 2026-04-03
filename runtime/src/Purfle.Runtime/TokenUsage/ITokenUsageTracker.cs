namespace Purfle.Runtime.TokenUsage;

public interface ITokenUsageTracker
{
    Task RecordAsync(string agentId, string engine, string model,
                     int promptTokens, int completionTokens,
                     DateTimeOffset timestamp);
}
