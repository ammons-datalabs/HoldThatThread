namespace HoldThatThread.Domain;

/// <summary>
/// Represents a pending conversation turn waiting to be streamed.
/// </summary>
public class ConversationTurn
{
    public Guid TurnId { get; init; }
    public Guid? SessionId { get; init; }
    public string UserMessage { get; init; }
    public DateTime CreatedAt { get; init; }

    public ConversationTurn(Guid? sessionId, string userMessage)
    {
        TurnId = Guid.NewGuid();
        SessionId = sessionId;
        UserMessage = userMessage ?? throw new ArgumentNullException(nameof(userMessage));
        CreatedAt = DateTime.UtcNow;
    }
}