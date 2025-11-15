using HoldThatThread.Application;
using HoldThatThread.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HoldThatThread.Tests.Api;

public class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MainStream_ReturnsSSEStream()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var request = new
        {
            sessionId = (Guid?)null,
            message = "What is 2+2?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/main/stream", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MainStream_StreamsReasoningThenDelimiterThenAnswer()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var request = new
        {
            sessionId = (Guid?)null,
            message = "Test question"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/main/stream", request);
        var streamContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("reasoning", streamContent.ToLower());
        Assert.Contains("---FINAL ANSWER---", streamContent);
        Assert.Contains("answer", streamContent.ToLower());
    }

    [Fact]
    public async Task StartDigression_ReturnsDigressionId()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var sessionId = Guid.NewGuid();
        var request = new
        {
            sessionId,
            selectedText = "convert light to energy",
            initialUserMessage = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/digress/start", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartDigressionResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.DigressionId);
    }

    [Fact]
    public async Task ContinueDigression_ReturnsMessageHistory()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var digressionId = Guid.NewGuid();
        var request = new
        {
            userMessage = "What does this mean?"
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/chat/digress/{digressionId}", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DigressionTurnResponse>();
        Assert.NotNull(result);
        Assert.Equal(digressionId, result.DigressionId);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public async Task MergeDigression_ReturnsSessionId()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var digressionId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/chat/digress/{digressionId}/merge", null);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SessionIdResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SessionId);
    }

    [Fact]
    public async Task DiscardDigression_ReturnsNoContent()
    {
        // Arrange
        var client = CreateClientWithMockedServices();

        var digressionId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/chat/digress/{digressionId}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    private HttpClient CreateClientWithMockedServices()
    {
        var mockReasoningService = new Mock<IReasoningService>();
        var mockDigressionService = new Mock<IDigressionService>();
        var mockSessionStore = new Mock<ISessionStore>();
        var mockDigressionStore = new Mock<IDigressionStore>();

        // Setup mock streaming
        mockReasoningService.Setup(s => s.MainCallStreamAsync(It.IsAny<Guid?>(), It.IsAny<string>()))
            .Returns(CreateMockReasoningStream());

        // Setup mock digression start
        mockDigressionService.Setup(s => s.StartDigressionAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Setup mock digression continue
        mockDigressionService.Setup(s => s.ContinueDigressionAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string msg, CancellationToken ct) => new DigressionTurnResult(
                id,
                new List<ChatMessageDto>
                {
                    new("system", "Context", DateTime.UtcNow),
                    new("user", msg, DateTime.UtcNow),
                    new("assistant", "Answer", DateTime.UtcNow)
                }));

        // Setup mock merge
        mockDigressionService.Setup(s => s.MergeDigressionIntoMainAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Setup mock discard
        mockDigressionService.Setup(s => s.DiscardDigressionAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockReasoningService.Object);
                services.AddSingleton(mockDigressionService.Object);
                services.AddSingleton(mockSessionStore.Object);
                services.AddSingleton(mockDigressionStore.Object);
            });
        }).CreateClient();
    }

    private static async IAsyncEnumerable<ReasoningStreamEvent> CreateMockReasoningStream()
    {
        var sessionId = Guid.NewGuid();

        yield return new ReasoningStreamEvent
        {
            SessionId = sessionId,
            Type = StreamEventType.Reasoning,
            Content = "Let me think..."
        };

        await Task.Yield();

        yield return new ReasoningStreamEvent
        {
            SessionId = sessionId,
            Type = StreamEventType.Delimiter,
            Content = "---FINAL ANSWER---"
        };

        await Task.Yield();

        yield return new ReasoningStreamEvent
        {
            SessionId = sessionId,
            Type = StreamEventType.Answer,
            Content = "The answer is 4"
        };
    }
}

// Response DTOs for deserialization in tests
public record StartDigressionResponse(Guid DigressionId);
public record DigressionTurnResponse(Guid DigressionId, IReadOnlyList<ChatMessageDto> Messages);
public record SessionIdResponse(Guid SessionId);