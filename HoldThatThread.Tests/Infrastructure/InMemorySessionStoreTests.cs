using HoldThatThread.Domain;
using HoldThatThread.Infrastructure;
using Xunit;

namespace HoldThatThread.Tests.Infrastructure;

public class InMemorySessionStoreTests
{
    [Fact]
    public async Task CreateSession_ReturnsUniqueId()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var session1 = new Session();
        var session2 = new Session();

        // Act
        var id1 = await store.CreateAsync(session1);
        var id2 = await store.CreateAsync(session2);

        // Assert
        Assert.NotEqual(Guid.Empty, id1);
        Assert.NotEqual(Guid.Empty, id2);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task Get_ReturnsSavedSession()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var session = new Session();
        session.AddToMainChain(new MainMessage("user", "Hello"));

        // Act
        var sessionId = await store.CreateAsync(session);
        var retrieved = await store.GetAsync(sessionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Single(retrieved.MainChain);
        Assert.Equal("Hello", retrieved.MainChain[0].Content);
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var session = new Session();
        var sessionId = await store.CreateAsync(session);

        // Act
        var retrieved = await store.GetAsync(sessionId);
        retrieved.AddToMainChain(new MainMessage("user", "New message"));
        await store.UpdateAsync(retrieved);

        var updated = await store.GetAsync(sessionId);

        // Assert
        Assert.Single(updated.MainChain);
        Assert.Equal("New message", updated.MainChain[0].Content);
    }

    [Fact]
    public async Task GetAsync_WithMissingSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.GetAsync(nonExistentId));
    }

    [Fact]
    public async Task Delete_RemovesSession()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var session = new Session();
        var sessionId = await store.CreateAsync(session);

        // Act
        await store.DeleteAsync(sessionId);

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.GetAsync(sessionId));
    }

    [Fact]
    public async Task Delete_WithNonExistentSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.DeleteAsync(nonExistentId));
    }

    [Fact]
    public async Task Update_WithNonExistentSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var session = new Session();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.UpdateAsync(session));
    }

    [Fact]
    public async Task CreateAsync_WithNullSession_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemorySessionStore();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WithNullSession_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemorySessionStore();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpdateAsync(null!));
    }

    [Fact]
    public async Task Store_IsThreadSafe()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var tasks = new List<Task<Guid>>();

        // Act - Create multiple sessions concurrently
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var session = new Session();
                return await store.CreateAsync(session);
            }));
        }

        var sessionIds = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(100, sessionIds.Distinct().Count());
    }
}
