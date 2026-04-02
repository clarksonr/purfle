using System.Text.Json;
using Purfle.Sdk;

namespace Purfle.TestAgents.Hello;

/// <summary>
/// Minimal agent for testing the assembly load pipeline end-to-end.
/// Has a known system prompt and one custom tool so tests can verify both.
/// </summary>
public sealed class HelloAgent : IAgent
{
    public string? SystemPrompt => "You are HelloAgent, a test agent for the Purfle AIVM.";

    public IReadOnlyList<IAgentTool> Tools { get; } = [new GreetTool()];
}

/// <summary>
/// A trivial tool that returns a greeting. No I/O, no side effects — pure function.
/// </summary>
public sealed class GreetTool : IAgentTool
{
    public string Name => "greet";

    public string Description => "Returns a greeting for the given name.";

    public string InputSchemaJson =>
        """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";

    public Task<string> ExecuteAsync(string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var name = doc.RootElement.GetProperty("name").GetString() ?? "World";
        return Task.FromResult($"Hello, {name}!");
    }
}
