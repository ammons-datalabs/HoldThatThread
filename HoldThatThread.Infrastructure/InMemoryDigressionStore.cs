using System.Collections.Concurrent;
using HoldThatThread.Application;
using HoldThatThread.Domain;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// In-memory thread-safe implementation of IDigressionStore.
/// For production, replace with persistent storage (e.g., Cosmos DB, SQL Azure).
/// </summary>
public class InMemoryDigressionStore : IDigressionStore
{
    private readonly ConcurrentDictionary<Guid, DigressionSession> _digressions = new();

    public Task<Guid> CreateAsync(DigressionSession digression, CancellationToken ct = default)
    {
        if (digression == null)
            throw new ArgumentNullException(nameof(digression));

        _digressions[digression.DigressionId] = digression;
        return Task.FromResult(digression.DigressionId);
    }

    public Task<DigressionSession> GetAsync(Guid digressionId, CancellationToken ct = default)
    {
        if (!_digressions.TryGetValue(digressionId, out var digression))
            throw new KeyNotFoundException($"Digression with ID {digressionId} not found");

        return Task.FromResult(digression);
    }

    public Task UpdateAsync(DigressionSession digression, CancellationToken ct = default)
    {
        if (digression == null)
            throw new ArgumentNullException(nameof(digression));

        if (!_digressions.ContainsKey(digression.DigressionId))
            throw new KeyNotFoundException($"Digression with ID {digression.DigressionId} not found");

        _digressions[digression.DigressionId] = digression;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid digressionId, CancellationToken ct = default)
    {
        if (!_digressions.TryRemove(digressionId, out _))
            throw new KeyNotFoundException($"Digression with ID {digressionId} not found");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DigressionSession>> GetByParentSessionIdAsync(Guid parentSessionId, CancellationToken ct = default)
    {
        var digressions = _digressions.Values
            .Where(d => d.ParentSessionId == parentSessionId)
            .ToList();

        return Task.FromResult<IReadOnlyList<DigressionSession>>(digressions);
    }
}