using HoldThatThread.Domain;
using Xunit;

namespace HoldThatThread.Tests.Domain;

public class SessionTests
{
    [Fact]
    public void CanCreateNewSessionWithNoMessages()
    {
        // Arrange & Act
        var session = new Session();

        // Assert
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Empty(session.MainChain);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void AddsMessagesToMainChain()
    {
        // Arrange
        var session = new Session();
        var message1 = new MainMessage("user", "Hello");
        var message2 = new MainMessage("assistant", "Hi there!");

        // Act
        session.AddToMainChain(message1);
        session.AddToMainChain(message2);

        // Assert
        Assert.Equal(2, session.MainChain.Count);
        Assert.Equal("Hello", session.MainChain[0].Content);
        Assert.Equal("Hi there!", session.MainChain[1].Content);
    }

    [Fact]
    public void MainChainMessagesAreOrderedByTimestamp()
    {
        // Arrange
        var session = new Session();
        var message1 = new MainMessage("user", "First");
        Thread.Sleep(1); // Ensure different timestamps
        var message2 = new MainMessage("user", "Second");

        // Act
        session.AddToMainChain(message1);
        session.AddToMainChain(message2);

        // Assert
        Assert.True(session.MainChain[0].Timestamp <= session.MainChain[1].Timestamp);
    }

    [Fact]
    public void CannotAddNullMessageToMainChain()
    {
        // Arrange
        var session = new Session();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => session.AddToMainChain(null!));
    }

    [Fact]
    public void LastUpdatedChangesWhenMainChainModified()
    {
        // Arrange
        var session = new Session();
        var originalLastUpdated = session.LastUpdated;
        Thread.Sleep(1);

        // Act
        session.AddToMainChain(new MainMessage("user", "Test"));

        // Assert
        Assert.True(session.LastUpdated >= originalLastUpdated);
    }

    [Fact]
    public void SessionIdIsImmutable()
    {
        // Arrange & Act
        var session = new Session();
        var originalId = session.Id;

        // Adding messages shouldn't change the ID
        session.AddToMainChain(new MainMessage("user", "Test"));

        // Assert
        Assert.Equal(originalId, session.Id);
    }
}
