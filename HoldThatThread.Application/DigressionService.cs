using HoldThatThread.Domain;

namespace HoldThatThread.Application;

/// <summary>
/// Service for managing digression mini-chat sessions.
/// Digressions allow users to explore specific points without disrupting the main conversation.
/// </summary>
public class DigressionService : IDigressionService
{
    private readonly ISessionStore _sessionStore;
    private readonly IDigressionStore _digressionStore;
    private readonly IOpenAiClient _openAiClient;

    public DigressionService(
        ISessionStore sessionStore,
        IDigressionStore digressionStore,
        IOpenAiClientFactory clientFactory)
    {
        _sessionStore = sessionStore;
        _digressionStore = digressionStore;
        _openAiClient = clientFactory.CreateDigressionClient();
    }

    public async Task<Guid> StartDigressionAsync(
        Guid mainSessionId,
        string selectedText,
        string? initialUserMessage = null,
        CancellationToken ct = default)
    {
        // Validate main session exists
        var mainSession = await _sessionStore.GetAsync(mainSessionId);

        if (mainSession.MainChain.Count == 0)
        {
            throw new InvalidOperationException("Cannot start a digression on an empty conversation");
        }

        // Create new digression session
        var digression = new DigressionSession(mainSessionId, selectedText);

        // Seed with system message describing the digression context
        var systemMessage = new MainMessage(
            "system",
            $"This is a brief digression to clarify or explore the following selected text:\n\n" +
            $"\"{selectedText}\"\n\n" +
            $"The main conversation context is:\n" +
            $"{GetConversationSummary(mainSession.MainChain)}\n\n" +
            $"Please provide concise, focused answers about the selected text.");
        digression.AddMessage(systemMessage);

        // Add optional initial user message
        if (!string.IsNullOrWhiteSpace(initialUserMessage))
        {
            digression.AddMessage(new MainMessage("user", initialUserMessage));
        }

        // Store and return digression ID
        await _digressionStore.CreateAsync(digression, ct);
        return digression.DigressionId;
    }

    public async Task<DigressionTurnResult> ContinueDigressionAsync(
        Guid digressionId,
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be empty", nameof(userMessage));

        // Load digression
        var digression = await _digressionStore.GetAsync(digressionId, ct);

        // Add user message
        digression.AddMessage(new MainMessage("user", userMessage));

        // Call OpenAI (non-streaming) for quick answer
        var answer = await _openAiClient.DigressAsync(
            digression.Messages.ToList());

        // Add assistant response
        digression.AddMessage(new MainMessage("assistant", answer));

        // Save updated digression
        await _digressionStore.UpdateAsync(digression, ct);

        // Return complete message history
        var messageDtos = digression.Messages
            .Select(m => new ChatMessageDto(m.Role, m.Content, m.Timestamp))
            .ToList();

        return new DigressionTurnResult(digression.DigressionId, messageDtos);
    }

    public async Task<Guid> MergeDigressionIntoMainAsync(
        Guid digressionId,
        CancellationToken ct = default)
    {
        // Load digression
        var digression = await _digressionStore.GetAsync(digressionId, ct);

        // Load parent session
        var mainSession = await _sessionStore.GetAsync(digression.ParentSessionId);

        // Extract ONLY the final assistant message from the digression
        var finalAssistantMessage = digression.Messages
            .Where(m => m.Role == "assistant")
            .LastOrDefault();

        if (finalAssistantMessage == null)
        {
            throw new InvalidOperationException(
                "Cannot merge digression: no assistant messages found");
        }

        // Add to main session
        mainSession.AddToMainChain(finalAssistantMessage);
        await _sessionStore.UpdateAsync(mainSession);

        // Delete digression (it's ephemeral)
        await _digressionStore.DeleteAsync(digressionId, ct);

        return mainSession.Id;
    }

    public async Task DiscardDigressionAsync(Guid digressionId, CancellationToken ct = default)
    {
        await _digressionStore.DeleteAsync(digressionId, ct);
    }

    private string GetConversationSummary(List<MainMessage> messages)
    {
        // Take last 2-3 exchanges to provide context without overwhelming the prompt
        var recentMessages = messages.TakeLast(6).ToList();
        var summary = string.Join("\n", recentMessages.Select(m => $"{m.Role}: {Truncate(m.Content, 100)}"));
        return summary;
    }

    private string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }
}