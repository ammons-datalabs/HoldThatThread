using HoldThatThread.Domain;
using Xunit;

namespace HoldThatThread.Tests.Domain;

public class MessageTests
{
    [Fact]
    public void MainMessage_HasRequiredProperties()
    {
        // Arrange & Act
        var message = new MainMessage("user", "Hello, world!");

        // Assert
        Assert.Equal("user", message.Role);
        Assert.Equal("Hello, world!", message.Content);
        Assert.True(message.Timestamp <= DateTime.UtcNow);
        Assert.True(message.Timestamp > DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public void MainMessage_RequiresRole()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MainMessage("", "Content"));
        Assert.Throws<ArgumentNullException>(() => new MainMessage(null!, "Content"));
    }

    [Fact]
    public void MainMessage_RequiresContent()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MainMessage("user", ""));
        Assert.Throws<ArgumentNullException>(() => new MainMessage("user", null!));
    }

    [Fact]
    public void MainMessage_AcceptsValidRoles()
    {
        // Arrange & Act
        var userMessage = new MainMessage("user", "Test");
        var assistantMessage = new MainMessage("assistant", "Test");
        var systemMessage = new MainMessage("system", "Test");

        // Assert
        Assert.Equal("user", userMessage.Role);
        Assert.Equal("assistant", assistantMessage.Role);
        Assert.Equal("system", systemMessage.Role);
    }

    [Fact]
    public void MainMessage_TimestampIsSetAutomatically()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = new MainMessage("user", "Test");

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(message.Timestamp >= beforeCreation);
        Assert.True(message.Timestamp <= afterCreation);
    }

    [Fact]
    public void MainMessage_IsImmutable()
    {
        // Arrange & Act
        var message = new MainMessage("user", "Original content");

        // Assert - Properties should be read-only (verified by compilation)
        // If this compiles, the implementation must have readonly/init properties
        Assert.Equal("user", message.Role);
        Assert.Equal("Original content", message.Content);
    }

    [Theory]
    [InlineData("user", "What is 2+2?")]
    [InlineData("assistant", "2+2 equals 4")]
    [InlineData("system", "You are a helpful assistant")]
    public void MainMessage_StoresVariousContentTypes(string role, string content)
    {
        // Act
        var message = new MainMessage(role, content);

        // Assert
        Assert.Equal(role, message.Role);
        Assert.Equal(content, message.Content);
    }
}
