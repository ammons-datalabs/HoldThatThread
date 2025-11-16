using HoldThatThread.Application;
using HoldThatThread.Domain;
using System.Collections.Concurrent;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// In-memory store for pending conversation turns.
/// Turns are ephemeral - they exist only long enough to initiate the SSE stream.
/// </summary>
public class InMemoryTurnStore : ITurnStore
{
    private readonly ConcurrentDictionary<Guid, ConversationTurn> _turns = new();

    public Task<ConversationTurn> CreateAsync(ConversationTurn turn)
    {
        if (turn == null) throw new ArgumentNullException(nameof(turn));

        if (!_turns.TryAdd(turn.TurnId, turn))
        {
            throw new InvalidOperationException($"Turn {turn.TurnId} already exists");
        }

        return Task.FromResult(turn);
    }

    public Task<ConversationTurn?> GetAsync(Guid turnId)
    {
        _turns.TryGetValue(turnId, out var turn);
        return Task.FromResult(turn);
    }

    public Task DeleteAsync(Guid turnId)
    {
        _turns.TryRemove(turnId, out _);
        return Task.CompletedTask;
    }
}