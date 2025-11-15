using HoldThatThread.Domain;

namespace HoldThatThread.Application;

/// <summary>
/// Store for managing digression mini-chat sessions.
/// Digressions are ephemeral branches off the main conversation for quick clarifications.
/// </summary>
public interface IDigressionStore
{
    /// <summary>
    /// Creates a new digression session and returns its ID.
    /// </summary>
    Task<Guid> CreateAsync(DigressionSession digression, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a digression session by ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">If digression doesn't exist.</exception>
    Task<DigressionSession> GetAsync(Guid digressionId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing digression session.
    /// </summary>
    /// <exception cref="KeyNotFoundException">If digression doesn't exist.</exception>
    Task UpdateAsync(DigressionSession digression, CancellationToken ct = default);

    /// <summary>
    /// Deletes a digression session.
    /// </summary>
    /// <exception cref="KeyNotFoundException">If digression doesn't exist.</exception>
    Task DeleteAsync(Guid digressionId, CancellationToken ct = default);

    /// <summary>
    /// Gets all digressions for a given parent session (optional, for debugging/admin).
    /// </summary>
    Task<IReadOnlyList<DigressionSession>> GetByParentSessionIdAsync(Guid parentSessionId, CancellationToken ct = default);
}