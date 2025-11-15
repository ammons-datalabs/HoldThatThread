using HoldThatThread.Domain;
using HoldThatThread.Infrastructure;
using Xunit;

namespace HoldThatThread.Tests.Infrastructure;

public class InMemoryDigressionStoreTests
{
    [Fact]
    public async Task CreateAsync_ReturnsDigressionId()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var digression = new DigressionSession(Guid.NewGuid(), "selected text");

        // Act
        var id = await store.CreateAsync(digression);

        // Assert
        Assert.Equal(digression.DigressionId, id);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenDigressionIsNull()
    {
        // Arrange
        var store = new InMemoryDigressionStore();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!));
    }

    [Fact]
    public async Task GetAsync_RetrievesCreatedDigression()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var parentId = Guid.NewGuid();
        var digression = new DigressionSession(parentId, "test text");
        await store.CreateAsync(digression);

        // Act
        var retrieved = await store.GetAsync(digression.DigressionId);

        // Assert
        Assert.Equal(digression.DigressionId, retrieved.DigressionId);
        Assert.Equal(parentId, retrieved.ParentSessionId);
        Assert.Equal("test text", retrieved.SelectedText);
    }

    [Fact]
    public async Task GetAsync_ThrowsWhenDigressionNotFound()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.GetAsync(nonExistentId));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingDigression()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var digression = new DigressionSession(Guid.NewGuid(), "original");
        await store.CreateAsync(digression);

        // Act
        digression.UpdateSelectedText("modified");
        await store.UpdateAsync(digression);

        // Assert
        var retrieved = await store.GetAsync(digression.DigressionId);
        Assert.Equal("modified", retrieved.SelectedText);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenDigressionIsNull()
    {
        // Arrange
        var store = new InMemoryDigressionStore();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpdateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenDigressionNotFound()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var digression = new DigressionSession(Guid.NewGuid(), "test");

        // Act & Assert - trying to update without creating first
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.UpdateAsync(digression));
    }

    [Fact]
    public async Task DeleteAsync_RemovesDigression()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var digression = new DigressionSession(Guid.NewGuid());
        await store.CreateAsync(digression);

        // Act
        await store.DeleteAsync(digression.DigressionId);

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.GetAsync(digression.DigressionId));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenDigressionNotFound()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.DeleteAsync(nonExistentId));
    }

    [Fact]
    public async Task GetByParentSessionIdAsync_ReturnsEmptyListWhenNoDigressions()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var parentId = Guid.NewGuid();

        // Act
        var digressions = await store.GetByParentSessionIdAsync(parentId);

        // Assert
        Assert.Empty(digressions);
    }

    [Fact]
    public async Task GetByParentSessionIdAsync_ReturnsOnlyMatchingDigressions()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var parentId1 = Guid.NewGuid();
        var parentId2 = Guid.NewGuid();

        var digression1 = new DigressionSession(parentId1, "text 1");
        var digression2 = new DigressionSession(parentId1, "text 2");
        var digression3 = new DigressionSession(parentId2, "text 3");

        await store.CreateAsync(digression1);
        await store.CreateAsync(digression2);
        await store.CreateAsync(digression3);

        // Act
        var digressions = await store.GetByParentSessionIdAsync(parentId1);

        // Assert
        Assert.Equal(2, digressions.Count);
        Assert.All(digressions, d => Assert.Equal(parentId1, d.ParentSessionId));
    }

    [Fact]
    public async Task ConcurrentAccess_HandlesMultipleCreates()
    {
        // Arrange
        var store = new InMemoryDigressionStore();
        var parentId = Guid.NewGuid();
        var tasks = new List<Task>();

        // Act - Create 100 digressions concurrently
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var digression = new DigressionSession(parentId, $"text {i}");
                await store.CreateAsync(digression);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var digressions = await store.GetByParentSessionIdAsync(parentId);
        Assert.Equal(100, digressions.Count);
    }
}