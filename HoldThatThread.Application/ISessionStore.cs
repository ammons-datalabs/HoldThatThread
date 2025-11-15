using HoldThatThread.Domain;

namespace HoldThatThread.Application;

public interface ISessionStore
{
    Task<Guid> CreateAsync(Session session);
    Task<Session> GetAsync(Guid sessionId);
    Task UpdateAsync(Session session);
    Task DeleteAsync(Guid sessionId);
}
