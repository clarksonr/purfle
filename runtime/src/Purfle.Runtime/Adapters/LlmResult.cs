namespace Purfle.Runtime.Adapters;

/// <summary>
/// Result of an LLM completion, including the text response and token usage.
/// </summary>
public sealed record LlmResult(
    string Text,
    int InputTokens = 0,
    int OutputTokens = 0);
