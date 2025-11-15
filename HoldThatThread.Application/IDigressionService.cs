namespace HoldThatThread.Application;

/// <summary>
/// Service for managing digression mini-chat sessions.
/// </summary>
public interface IDigressionService
{
    /// <summary>
    /// Starts a new digression session from a main session, optionally with an initial user message.
    /// </summary>
    Task<Guid> StartDigressionAsync(
        Guid mainSessionId,
        string selectedText,
        string? initialUserMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Continues an existing digression with a new user message (non-streaming).
    /// Returns the complete digression message history.
    /// </summary>
    Task<DigressionTurnResult> ContinueDigressionAsync(
        Guid digressionId,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Merges the final assistant message from a digression into the main session.
    /// Deletes the digression after merging.
    /// </summary>
    Task<Guid> MergeDigressionIntoMainAsync(
        Guid digressionId,
        CancellationToken ct = default);

    /// <summary>
    /// Discards a digression session without merging.
    /// </summary>
    Task DiscardDigressionAsync(Guid digressionId, CancellationToken ct = default);
}

public record DigressionTurnResult(Guid DigressionId, IReadOnlyList<ChatMessageDto> Messages);

public record ChatMessageDto(string Role, string Content, DateTime Timestamp);
