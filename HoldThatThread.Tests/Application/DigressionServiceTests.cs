using HoldThatThread.Application;
using HoldThatThread.Domain;
using Moq;
using Xunit;

namespace HoldThatThread.Tests.Application;

public class DigressionServiceTests
{
    [Fact]
    public async Task StartDigressionAsync_CreatesDigressionWithSystemMessage()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mainSession = new Session();
        mainSession.AddToMainChain(new MainMessage("user", "What is photosynthesis?"));
        mainSession.AddToMainChain(new MainMessage("assistant", "It's the process plants use to convert light to energy."));

        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(mainSession);
        mockDigressionStore.Setup(s => s.CreateAsync(It.IsAny<DigressionSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DigressionSession d, CancellationToken ct) => d.DigressionId);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        var digressionId = await service.StartDigressionAsync(mainSession.Id, "convert light to energy");

        // Assert
        Assert.NotEqual(Guid.Empty, digressionId);
        mockDigressionStore.Verify(s => s.CreateAsync(
            It.Is<DigressionSession>(d =>
                d.ParentSessionId == mainSession.Id &&
                d.SelectedText == "convert light to energy" &&
                d.Messages.Count >= 1 &&
                d.Messages[0].Role == "system"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDigressionAsync_IncludesInitialUserMessage()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mainSession = new Session();
        mainSession.AddToMainChain(new MainMessage("user", "Question"));
        mainSession.AddToMainChain(new MainMessage("assistant", "Answer"));

        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(mainSession);
        mockDigressionStore.Setup(s => s.CreateAsync(It.IsAny<DigressionSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DigressionSession d, CancellationToken ct) => d.DigressionId);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        var digressionId = await service.StartDigressionAsync(
            mainSession.Id,
            "selected text",
            "What does this mean?");

        // Assert
        mockDigressionStore.Verify(s => s.CreateAsync(
            It.Is<DigressionSession>(d =>
                d.Messages.Count == 2 &&
                d.Messages[0].Role == "system" &&
                d.Messages[1].Role == "user" &&
                d.Messages[1].Content == "What does this mean?"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDigressionAsync_ThrowsWhenMainSessionIsEmpty()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var emptySession = new Session();
        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(emptySession);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartDigressionAsync(emptySession.Id, "text"));
    }

    [Fact]
    public async Task ContinueDigressionAsync_AddsUserAndAssistantMessages()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var digression = new DigressionSession(Guid.NewGuid(), "selected text");
        digression.AddMessage(new MainMessage("system", "Context"));

        mockDigressionStore.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(digression);
        mockOpenAi.Setup(ai => ai.DigressAsync(It.IsAny<List<MainMessage>>()))
            .ReturnsAsync("This is the answer");

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        var result = await service.ContinueDigressionAsync(digression.DigressionId, "What is this?");

        // Assert
        Assert.Equal(digression.DigressionId, result.DigressionId);
        Assert.Equal(3, result.Messages.Count); // system + user + assistant
        Assert.Equal("user", result.Messages[1].Role);
        Assert.Equal("What is this?", result.Messages[1].Text);
        Assert.Equal("assistant", result.Messages[2].Role);
        Assert.Equal("This is the answer", result.Messages[2].Text);
    }

    [Fact]
    public async Task ContinueDigressionAsync_UpdatesDigressionStore()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var digression = new DigressionSession(Guid.NewGuid(), "text");
        digression.AddMessage(new MainMessage("system", "Context"));

        mockDigressionStore.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(digression);
        mockOpenAi.Setup(ai => ai.DigressAsync(It.IsAny<List<MainMessage>>()))
            .ReturnsAsync("Answer");

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        await service.ContinueDigressionAsync(digression.DigressionId, "Question");

        // Assert
        mockDigressionStore.Verify(s => s.UpdateAsync(
            It.Is<DigressionSession>(d => d.Messages.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContinueDigressionAsync_ThrowsWhenUserMessageIsEmpty()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ContinueDigressionAsync(Guid.NewGuid(), ""));
    }

    [Fact]
    public async Task MergeDigressionIntoMainAsync_AddsFinalAssistantMessageToMainChain()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mainSession = new Session();
        mainSession.AddToMainChain(new MainMessage("user", "Original question"));
        mainSession.AddToMainChain(new MainMessage("assistant", "Original answer"));

        var digression = new DigressionSession(mainSession.Id, "text");
        digression.AddMessage(new MainMessage("system", "Context"));
        digression.AddMessage(new MainMessage("user", "Digression Q1"));
        digression.AddMessage(new MainMessage("assistant", "Digression A1"));
        digression.AddMessage(new MainMessage("user", "Digression Q2"));
        digression.AddMessage(new MainMessage("assistant", "Final digression answer"));

        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(mainSession);
        mockDigressionStore.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(digression);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        var sessionId = await service.MergeDigressionIntoMainAsync(digression.DigressionId);

        // Assert
        Assert.Equal(mainSession.Id, sessionId);
        Assert.Equal(3, mainSession.MainChain.Count); // original 2 + 1 final assistant message
        Assert.Equal("assistant", mainSession.MainChain[2].Role);
        Assert.Equal("Final digression answer", mainSession.MainChain[2].Content);
    }

    [Fact]
    public async Task MergeDigressionIntoMainAsync_DeletesDigressionAfterMerge()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mainSession = new Session();
        mainSession.AddToMainChain(new MainMessage("user", "Q"));
        mainSession.AddToMainChain(new MainMessage("assistant", "A"));

        var digression = new DigressionSession(mainSession.Id, "text");
        digression.AddMessage(new MainMessage("system", "Context"));
        digression.AddMessage(new MainMessage("user", "Q"));
        digression.AddMessage(new MainMessage("assistant", "Final answer"));

        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(mainSession);
        mockDigressionStore.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(digression);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        await service.MergeDigressionIntoMainAsync(digression.DigressionId);

        // Assert
        mockDigressionStore.Verify(s => s.DeleteAsync(digression.DigressionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeDigressionIntoMainAsync_ThrowsWhenNoAssistantMessages()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var mainSession = new Session();
        mainSession.AddToMainChain(new MainMessage("user", "Q"));

        var digression = new DigressionSession(mainSession.Id, "text");
        digression.AddMessage(new MainMessage("system", "Context"));
        digression.AddMessage(new MainMessage("user", "Question")); // No assistant response

        mockSessionStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(mainSession);
        mockDigressionStore.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(digression);

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MergeDigressionIntoMainAsync(digression.DigressionId));
    }

    [Fact]
    public async Task DiscardDigressionAsync_DeletesDigression()
    {
        // Arrange
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var digressionId = Guid.NewGuid();

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var service = new DigressionService(mockSessionStore.Object, mockDigressionStore.Object, mockFactory.Object);

        // Act
        await service.DiscardDigressionAsync(digressionId);

        // Assert
        mockDigressionStore.Verify(s => s.DeleteAsync(digressionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IOpenAiClientFactory> CreateMockFactory(IOpenAiClient client)
    {
        var mockFactory = new Mock<IOpenAiClientFactory>();
        mockFactory.Setup(f => f.CreateDigressionClient()).Returns(client);
        return mockFactory;
    }
}