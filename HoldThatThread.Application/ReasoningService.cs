using HoldThatThread.Domain;

namespace HoldThatThread.Application;

public class ReasoningService : IReasoningService
{
    private readonly ISessionStore _sessionStore;
    private readonly ITurnStore _turnStore;
    private readonly IOpenAiClient _openAiClient;

    public ReasoningService(
        ISessionStore sessionStore,
        ITurnStore turnStore,
        IOpenAiClientFactory clientFactory)
    {
        _sessionStore = sessionStore;
        _turnStore = turnStore;
        _openAiClient = clientFactory.CreateReasoningClient();
    }

    public async IAsyncEnumerable<ReasoningStreamEvent> MainCallStreamAsync(
        Guid? sessionId,
        string userMessage)
    {
        Session session;

        if (sessionId == null || sessionId == Guid.Empty)
        {
            // Create new session
            session = new Session();
            await _sessionStore.CreateAsync(session);
        }
        else
        {
            // Get existing session
            session = await _sessionStore.GetAsync(sessionId.Value);
        }

        // Add user message to the chain
        var userMsg = new MainMessage("user", userMessage);
        session.AddToMainChain(userMsg);

        // Collect the final answer to add to the session
        var answerChunks = new List<string>();

        // Stream from OpenAI and yield chunks
        await foreach (var chunk in _openAiClient.ReasonAsyncStreaming(session.MainChain))
        {
            if (chunk.Type == StreamChunkType.Reasoning)
            {
                // Yield thought chunks to client
                yield return new ReasoningStreamEvent
                {
                    SessionId = session.Id,
                    Type = StreamEventType.Thought,
                    Text = chunk.Content
                };
            }
            else if (chunk.Type == StreamChunkType.Answer)
            {
                // Collect answer chunks and yield them
                answerChunks.Add(chunk.Content);
                yield return new ReasoningStreamEvent
                {
                    SessionId = session.Id,
                    Type = StreamEventType.Answer,
                    Text = chunk.Content
                };
            }
        }

        // Add the complete answer to the session chain
        var fullAnswer = string.Concat(answerChunks);
        var assistantMsg = new MainMessage("assistant", fullAnswer);
        session.AddToMainChain(assistantMsg);

        // Update the session
        await _sessionStore.UpdateAsync(session);

        // Emit done event to signal completion
        yield return new ReasoningStreamEvent
        {
            SessionId = session.Id,
            Type = StreamEventType.Done,
            Text = string.Empty
        };
    }

    public async Task<ConversationTurn> StartTurnAsync(Guid? sessionId, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new ArgumentException("User message cannot be empty", nameof(userMessage));
        }

        var turn = new ConversationTurn(sessionId, userMessage);
        await _turnStore.CreateAsync(turn);
        return turn;
    }

    public async IAsyncEnumerable<ReasoningStreamEvent> StreamTurnAsync(Guid turnId)
    {
        // Get the turn
        var turn = await _turnStore.GetAsync(turnId);
        if (turn == null)
        {
            throw new InvalidOperationException($"Turn {turnId} not found");
        }

        // Use the existing streaming logic
        await foreach (var evt in MainCallStreamAsync(turn.SessionId, turn.UserMessage))
        {
            yield return evt;
        }

        // Clean up the turn after streaming
        await _turnStore.DeleteAsync(turnId);
    }
}
