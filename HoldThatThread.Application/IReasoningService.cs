namespace HoldThatThread.Application;

public interface IReasoningService
{
    IAsyncEnumerable<ReasoningStreamEvent> MainCallStreamAsync(Guid? sessionId, string userMessage);
}

public class ReasoningStreamEvent
{
    public Guid SessionId { get; set; }
    public StreamEventType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public enum StreamEventType
{
    Reasoning,
    Delimiter,
    Answer
}
