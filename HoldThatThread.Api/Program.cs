using Azure.Identity;
using HoldThatThread.Application;
using HoldThatThread.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault for secrets management in production
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = builder.Configuration["KeyVault:VaultUri"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        // Use Managed Identity (DefaultAzureCredential) to authenticate to Key Vault
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
    }
}

// Add services to the container
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "HoldThatThread API",
            Version = "v1",
            Description = """
                API for managing reasoning conversations with digression support.

                Features:
                - Streaming reasoning responses with extended thinking (o3-mini)
                - Quick digression mini-chats for clarifications (gpt-4o-mini)
                - Session management with conversation history
                - Server-Sent Events (SSE) for real-time streaming

                Architecture:
                - Dual Azure OpenAI deployment strategy (reasoning vs digression)
                - Azure Key Vault for secrets management
                - Managed Identity authentication
                """,
            Contact = new()
            {
                Name = "Ammons DataLabs",
                Email = "jaybea@gmail.com"
            }
        };
        return Task.CompletedTask;
    });
});

// Register application services
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IDigressionStore, InMemoryDigressionStore>();

// Configure Azure OpenAI
var azureOpenAiOptions = new AzureOpenAiOptions();
builder.Configuration.GetSection(AzureOpenAiOptions.SectionName).Bind(azureOpenAiOptions);
builder.Services.AddSingleton(azureOpenAiOptions);

// Register OpenAI client factory with deployment-specific configuration
builder.Services.AddSingleton<IOpenAiClientFactory, AzureOpenAiClientFactory>();

// Register domain services (they use the factory to get deployment-specific clients)
builder.Services.AddSingleton<IReasoningService, ReasoningService>();
builder.Services.AddSingleton<IDigressionService, DigressionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Chat endpoints
app.MapPost("/api/chat/main/stream", async (
    IReasoningService reasoningService,
    MainChatRequest request,
    HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    await foreach (var evt in reasoningService.MainCallStreamAsync(request.SessionId, request.Message))
    {
        var eventType = evt.Type.ToString().ToLower();
        var data = $"event: {eventType}\ndata: {System.Text.Json.JsonSerializer.Serialize(evt)}\n\n";
        await context.Response.WriteAsync(data);
        await context.Response.Body.FlushAsync();
    }
})
.WithName("MainChatStream")
.WithSummary("Stream main conversation with reasoning")
.WithDescription("""
    Streams AI responses with extended reasoning using Server-Sent Events (SSE).

    The response includes three types of events:
    - 'reasoning': Internal thinking process (streamed in real-time)
    - 'delimiter': Marks the transition from reasoning to final answer
    - 'answer': Final response to the user (streamed in real-time)

    Uses the reasoning deployment (e.g., o3-mini) for extended thinking capabilities.
    """)
.WithTags("Main Chat")
.Produces(200, contentType: "text/event-stream");

// Digression endpoints - mini-chat for clarifications
app.MapPost("/api/chat/digress/start", async (
    IDigressionService digressionService,
    StartDigressionRequest request) =>
{
    var digressionId = await digressionService.StartDigressionAsync(
        request.SessionId,
        request.SelectedText,
        request.InitialUserMessage);

    return Results.Ok(new StartDigressionResponse(digressionId));
})
.WithName("StartDigression")
.WithSummary("Start a new digression mini-chat")
.WithDescription("""
    Creates a temporary conversation thread to explore or clarify specific text from the main conversation.

    Digressions are lightweight, ephemeral chat sessions that:
    - Focus on a specific piece of selected text
    - Use the faster digression deployment (e.g., gpt-4o-mini)
    - Can be merged back into main conversation or discarded
    - Don't use streaming (returns complete responses)

    Perfect for quick clarifications without disrupting the main conversation flow.
    """)
.WithTags("Digressions")
.Produces<StartDigressionResponse>(200);

app.MapPost("/api/chat/digress/{digressionId:guid}", async (
    IDigressionService digressionService,
    Guid digressionId,
    DigressionTurnRequest request) =>
{
    var result = await digressionService.ContinueDigressionAsync(
        digressionId,
        request.UserMessage);

    return Results.Ok(new DigressionTurnResponse(
        result.DigressionId,
        result.Messages));
})
.WithName("ContinueDigression")
.WithSummary("Continue an existing digression")
.WithDescription("""
    Sends another message in an ongoing digression conversation.

    Returns the complete message history including:
    - System message with context
    - All user messages
    - All assistant responses

    Non-streaming for faster, more focused responses.
    """)
.WithTags("Digressions")
.Produces<DigressionTurnResponse>(200);

app.MapPost("/api/chat/digress/{digressionId:guid}/merge", async (
    IDigressionService digressionService,
    Guid digressionId) =>
{
    var sessionId = await digressionService.MergeDigressionIntoMainAsync(digressionId);
    return Results.Ok(new { sessionId });
})
.WithName("MergeDigression")
.WithSummary("Merge digression back into main conversation")
.WithDescription("""
    Adds the final assistant response from the digression into the main conversation history.

    This allows insights gained during the digression to inform future main conversation turns.
    The digression is automatically deleted after merging (it's ephemeral).

    Only the final assistant message is merged; intermediate back-and-forth is discarded.
    """)
.WithTags("Digressions")
.Produces(200);

app.MapDelete("/api/chat/digress/{digressionId:guid}", async (
    IDigressionService digressionService,
    Guid digressionId) =>
{
    await digressionService.DiscardDigressionAsync(digressionId);
    return Results.NoContent();
})
.WithName("DiscardDigression")
.WithSummary("Discard a digression without merging")
.WithDescription("""
    Deletes a digression session without adding anything to the main conversation.

    Use this when:
    - The digression didn't provide useful information
    - You want to keep the main conversation focused
    - The exploration was just for your own understanding

    The digression is permanently deleted and cannot be recovered.
    """)
.WithTags("Digressions")
.Produces(204);

app.Run();

// Request/Response models
record MainChatRequest(Guid? SessionId, string Message);

// Digression DTOs
record StartDigressionRequest(Guid SessionId, string SelectedText, string? InitialUserMessage);
record StartDigressionResponse(Guid DigressionId);
record DigressionTurnRequest(string UserMessage);
record DigressionTurnResponse(Guid DigressionId, IReadOnlyList<ChatMessageDto> Messages);

// Make Program accessible to tests
public partial class Program { }
