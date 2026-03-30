using Purfle.Runtime.Adapters;

namespace Purfle.Runtime.Sessions;

/// <summary>
/// Wraps an <see cref="IInferenceAdapter"/> to maintain multi-turn conversation history.
/// Each call to <see cref="SendAsync"/> appends the user message and the model's reply
/// to an internal message list that is sent with every subsequent request, giving the
/// model full context of the conversation so far.
///
/// <para>
/// Usage:
/// <code>
/// var session = new ConversationSession(adapter, "You are a helpful assistant.");
/// var reply1 = await session.SendAsync("Hello!");
/// var reply2 = await session.SendAsync("What did I just say?"); // model sees prior turn
/// </code>
/// </para>
/// </summary>
public sealed class ConversationSession
{
    private readonly IInferenceAdapter _adapter;
    private readonly string _systemPrompt;
    private List<object> _history = [];

    /// <summary>
    /// The number of conversation turns (user + assistant pairs) in the session.
    /// </summary>
    public int TurnCount { get; private set; }

    /// <summary>
    /// Creates a new conversation session.
    /// </summary>
    /// <param name="adapter">The inference adapter to use (must support multi-turn).</param>
    /// <param name="systemPrompt">System-level instructions for the agent.</param>
    public ConversationSession(IInferenceAdapter adapter, string systemPrompt)
    {
        _adapter = adapter;
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Sends a user message and returns the model's reply. The full conversation
    /// history is included in the request, enabling multi-turn context.
    /// </summary>
    /// <param name="userMessage">The user's input message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The model's text reply.</returns>
    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        var (reply, updatedHistory) = await _adapter.InvokeMultiTurnAsync(
            _systemPrompt, _history, userMessage, ct);

        _history = updatedHistory;
        TurnCount++;
        return reply;
    }

    /// <summary>
    /// Clears the conversation history, starting a fresh session while keeping
    /// the same adapter and system prompt.
    /// </summary>
    public void Reset()
    {
        _history = [];
        TurnCount = 0;
    }
}
