using HoldThatThread.Domain;

namespace HoldThatThread.Application;

/// <summary>
/// Store for pending conversation turns (ephemeral, for GET SSE pattern).
/// </summary>
public interface ITurnStore
{
    Task<ConversationTurn> CreateAsync(ConversationTurn turn);
    Task<ConversationTurn?> GetAsync(Guid turnId);
    Task DeleteAsync(Guid turnId);
}