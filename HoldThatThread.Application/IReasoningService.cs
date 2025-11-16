using HoldThatThread.Domain;

namespace HoldThatThread.Application;

public interface IReasoningService
{
    /// <summary>
    /// Legacy streaming endpoint (POST with body).
    /// Use StartTurnAsync + StreamTurnAsync for GET SSE pattern.
    /// </summary>
    IAsyncEnumerable<ReasoningStreamEvent> MainCallStreamAsync(Guid? sessionId, string userMessage);

    /// <summary>
    /// Step 1: Create a conversation turn (for GET SSE pattern).
    /// Returns a turnId for use with StreamTurnAsync.
    /// </summary>
    Task<ConversationTurn> StartTurnAsync(Guid? sessionId, string userMessage);

    /// <summary>
    /// Step 2: Stream the response for a conversation turn (GET SSE).
    /// </summary>
    IAsyncEnumerable<ReasoningStreamEvent> StreamTurnAsync(Guid turnId);
}

public class ReasoningStreamEvent
{
    public Guid SessionId { get; set; }
    public StreamEventType Type { get; set; }
    public string Text { get; set; } = string.Empty;
}

public enum StreamEventType
{
    Thought,
    Answer,
    Done
}
