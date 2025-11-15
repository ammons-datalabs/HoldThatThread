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
builder.Services.AddOpenApi();

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
.WithName("MainChatStream");

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
.WithName("StartDigression");

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
.WithName("ContinueDigression");

app.MapPost("/api/chat/digress/{digressionId:guid}/merge", async (
    IDigressionService digressionService,
    Guid digressionId) =>
{
    var sessionId = await digressionService.MergeDigressionIntoMainAsync(digressionId);
    return Results.Ok(new { sessionId });
})
.WithName("MergeDigression");

app.MapDelete("/api/chat/digress/{digressionId:guid}", async (
    IDigressionService digressionService,
    Guid digressionId) =>
{
    await digressionService.DiscardDigressionAsync(digressionId);
    return Results.NoContent();
})
.WithName("DiscardDigression");

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
