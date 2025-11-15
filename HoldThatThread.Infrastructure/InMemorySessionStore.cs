using HoldThatThread.Application;
using HoldThatThread.Domain;
using System.Collections.Concurrent;

namespace HoldThatThread.Infrastructure;

public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

    public Task<Guid> CreateAsync(Session session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        _sessions[session.Id] = session;
        return Task.FromResult(session.Id);
    }

    public Task<Session> GetAsync(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Session with ID {sessionId} not found");

        return Task.FromResult(session);
    }

    public Task UpdateAsync(Session session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (!_sessions.ContainsKey(session.Id))
            throw new KeyNotFoundException($"Session with ID {session.Id} not found");

        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out _))
            throw new KeyNotFoundException($"Session with ID {sessionId} not found");

        return Task.CompletedTask;
    }
}
