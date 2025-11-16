using HoldThatThread.Application;
using HoldThatThread.Domain;
using Moq;
using Xunit;

namespace HoldThatThread.Tests.Application;

public class ReasoningServiceTests
{
    [Fact]
    public async Task MainCallStream_AppendsUserMessageToChain()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var session = new Session();
        mockStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Answer, Content = "The answer is 4" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        await foreach (var _ in service.MainCallStreamAsync(session.Id, "What is 2+2?"))
        {
            // Consume stream
        }

        // Assert - User message should be in chain
        Assert.True(session.MainChain.Count >= 1);
        Assert.Equal("user", session.MainChain[0].Role);
        Assert.Equal("What is 2+2?", session.MainChain[0].Content);
    }

    [Fact]
    public async Task MainCallStream_StoresFinalAnswerInChain()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var session = new Session();
        mockStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Answer, Content = "The answer" },
                new StreamChunk { Type = StreamChunkType.Answer, Content = " is 4" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        await foreach (var _ in service.MainCallStreamAsync(session.Id, "What is 2+2?"))
        {
            // Consume stream
        }

        // Assert - Should have user message and complete assistant answer
        Assert.Equal(2, session.MainChain.Count);
        Assert.Equal("assistant", session.MainChain[1].Role);
        Assert.Equal("The answer is 4", session.MainChain[1].Content);
    }

    [Fact]
    public async Task MainCallStream_EmitsReasoningChunks()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var session = new Session();
        mockStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Reasoning, Content = "Let me think..." },
                new StreamChunk { Type = StreamChunkType.Reasoning, Content = " 2+2 is 4" },
                new StreamChunk { Type = StreamChunkType.Answer, Content = "The answer is 4" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        var events = new List<ReasoningStreamEvent>();
        await foreach (var evt in service.MainCallStreamAsync(session.Id, "What is 2+2?"))
        {
            events.Add(evt);
        }

        // Assert - Should have thought events
        var thoughtEvents = events.Where(e => e.Type == StreamEventType.Thought).ToList();
        Assert.Equal(2, thoughtEvents.Count);
        Assert.Equal("Let me think...", thoughtEvents[0].Text);
        Assert.Equal(" 2+2 is 4", thoughtEvents[1].Text);
    }

    [Fact]
    public async Task MainCallStream_EmitsDoneEventAtEnd()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var session = new Session();
        mockStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Reasoning, Content = "Thinking..." },
                new StreamChunk { Type = StreamChunkType.Answer, Content = "Answer" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        var events = new List<ReasoningStreamEvent>();
        await foreach (var evt in service.MainCallStreamAsync(session.Id, "Test?"))
        {
            events.Add(evt);
        }

        // Assert - Should have done event as last event
        Assert.Contains(events, e => e.Type == StreamEventType.Done);
        var doneEvent = events.Last();
        Assert.Equal(StreamEventType.Done, doneEvent.Type);
        Assert.Equal(string.Empty, doneEvent.Text);
    }

    [Fact]
    public async Task MainCallStream_DoesNotStoreReasoningInChain()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var session = new Session();
        mockStore.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Reasoning, Content = "Internal reasoning that should not be stored" },
                new StreamChunk { Type = StreamChunkType.Answer, Content = "Final answer" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        await foreach (var _ in service.MainCallStreamAsync(session.Id, "Test?"))
        {
            // Consume stream
        }

        // Assert - Main chain should only have user message and final answer
        Assert.Equal(2, session.MainChain.Count);
        Assert.All(session.MainChain, msg =>
            Assert.DoesNotContain("Internal reasoning", msg.Content));
    }

    [Fact]
    public async Task MainCallStream_CreatesNewSessionIfNoneProvided()
    {
        // Arrange
        var mockStore = new Mock<ISessionStore>();
        var mockOpenAi = new Mock<IOpenAiClient>();

        var newSession = new Session();
        mockStore.Setup(s => s.CreateAsync(It.IsAny<Session>())).ReturnsAsync(newSession.Id);
        mockStore.Setup(s => s.GetAsync(newSession.Id)).ReturnsAsync(newSession);
        mockStore.Setup(s => s.UpdateAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);

        mockOpenAi.Setup(ai => ai.ReasonAsyncStreaming(It.IsAny<List<MainMessage>>()))
            .Returns(CreateMockStream(new[] {
                new StreamChunk { Type = StreamChunkType.Answer, Content = "Hello!" }
            }));

        var mockFactory = CreateMockFactory(mockOpenAi.Object);
        var mockTurnStore = new Mock<ITurnStore>();
        var service = new ReasoningService(mockStore.Object, mockTurnStore.Object, mockFactory.Object);

        // Act
        var events = new List<ReasoningStreamEvent>();
        await foreach (var evt in service.MainCallStreamAsync(null, "Hi there!"))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.NotEqual(Guid.Empty, events[0].SessionId);
        mockStore.Verify(s => s.CreateAsync(It.IsAny<Session>()), Times.Once);
    }

    private static Mock<IOpenAiClientFactory> CreateMockFactory(IOpenAiClient client)
    {
        var mockFactory = new Mock<IOpenAiClientFactory>();
        mockFactory.Setup(f => f.CreateReasoningClient()).Returns(client);
        return mockFactory;
    }

    private static async IAsyncEnumerable<StreamChunk> CreateMockStream(StreamChunk[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }
}