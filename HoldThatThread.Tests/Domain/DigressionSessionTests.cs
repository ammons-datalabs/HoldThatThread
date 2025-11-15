using HoldThatThread.Domain;
using Xunit;

namespace HoldThatThread.Tests.Domain;

public class DigressionSessionTests
{
    [Fact]
    public void Constructor_CreatesDigressionWithValidId()
    {
        // Arrange
        var parentSessionId = Guid.NewGuid();

        // Act
        var digression = new DigressionSession(parentSessionId);

        // Assert
        Assert.NotEqual(Guid.Empty, digression.DigressionId);
        Assert.Equal(parentSessionId, digression.ParentSessionId);
        Assert.Empty(digression.Messages);
    }

    [Fact]
    public void Constructor_SetsSelectedText()
    {
        // Arrange
        var parentSessionId = Guid.NewGuid();
        var selectedText = "plants convert light energy";

        // Act
        var digression = new DigressionSession(parentSessionId, selectedText);

        // Assert
        Assert.Equal(selectedText, digression.SelectedText);
    }

    [Fact]
    public void Constructor_ThrowsWhenParentSessionIdIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DigressionSession(Guid.Empty));
    }

    [Fact]
    public void Constructor_SetsTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var parentSessionId = Guid.NewGuid();

        // Act
        var digression = new DigressionSession(parentSessionId);
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(digression.CreatedUtc >= before);
        Assert.True(digression.CreatedUtc <= after);
        Assert.True(digression.LastUpdatedUtc >= digression.CreatedUtc);
    }

    [Fact]
    public void AddMessage_AppendsMessageToList()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid());
        var message = new MainMessage("user", "What does this mean?");

        // Act
        digression.AddMessage(message);

        // Assert
        Assert.Single(digression.Messages);
        Assert.Equal("What does this mean?", digression.Messages[0].Content);
    }

    [Fact]
    public void AddMessage_UpdatesLastUpdatedUtc()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid());
        var originalTimestamp = digression.LastUpdatedUtc;
        Thread.Sleep(1);

        var message = new MainMessage("user", "Test");

        // Act
        digression.AddMessage(message);

        // Assert
        Assert.True(digression.LastUpdatedUtc >= originalTimestamp);
    }

    [Fact]
    public void AddMessage_ThrowsWhenMessageIsNull()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => digression.AddMessage(null!));
    }

    [Fact]
    public void AddMessage_MaintainsMessageOrder()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid());
        var msg1 = new MainMessage("user", "First");
        var msg2 = new MainMessage("assistant", "Second");
        var msg3 = new MainMessage("user", "Third");

        // Act
        digression.AddMessage(msg1);
        digression.AddMessage(msg2);
        digression.AddMessage(msg3);

        // Assert
        Assert.Equal(3, digression.Messages.Count);
        Assert.Equal("First", digression.Messages[0].Content);
        Assert.Equal("Second", digression.Messages[1].Content);
        Assert.Equal("Third", digression.Messages[2].Content);
    }

    [Fact]
    public void UpdateSelectedText_ChangesSelectedText()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid(), "original text");

        // Act
        digression.UpdateSelectedText("updated text");

        // Assert
        Assert.Equal("updated text", digression.SelectedText);
    }

    [Fact]
    public void UpdateSelectedText_UpdatesLastUpdatedUtc()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid());
        var originalTimestamp = digression.LastUpdatedUtc;
        Thread.Sleep(1);

        // Act
        digression.UpdateSelectedText("new text");

        // Assert
        Assert.True(digression.LastUpdatedUtc >= originalTimestamp);
    }

    [Fact]
    public void UpdateSelectedText_AcceptsNull()
    {
        // Arrange
        var digression = new DigressionSession(Guid.NewGuid(), "some text");

        // Act
        digression.UpdateSelectedText(null);

        // Assert
        Assert.Null(digression.SelectedText);
    }
}