using HoldThatThread.Domain;

namespace HoldThatThread.Application;

public class ReasoningService : IReasoningService
{
    private readonly ISessionStore _sessionStore;
    private readonly IOpenAiClient _openAiClient;
    private const string DELIMITER = "---FINAL ANSWER---";

    public ReasoningService(ISessionStore sessionStore, IOpenAiClientFactory clientFactory)
    {
        _sessionStore = sessionStore;
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
                // Yield reasoning chunks to client
                yield return new ReasoningStreamEvent
                {
                    SessionId = session.Id,
                    Type = StreamEventType.Reasoning,
                    Content = chunk.Content
                };
            }
            else if (chunk.Type == StreamChunkType.Answer)
            {
                // First answer chunk - emit delimiter
                if (answerChunks.Count == 0)
                {
                    yield return new ReasoningStreamEvent
                    {
                        SessionId = session.Id,
                        Type = StreamEventType.Delimiter,
                        Content = DELIMITER
                    };
                }

                // Collect answer chunks and yield them
                answerChunks.Add(chunk.Content);
                yield return new ReasoningStreamEvent
                {
                    SessionId = session.Id,
                    Type = StreamEventType.Answer,
                    Content = chunk.Content
                };
            }
        }

        // Add the complete answer to the session chain
        var fullAnswer = string.Concat(answerChunks);
        var assistantMsg = new MainMessage("assistant", fullAnswer);
        session.AddToMainChain(assistantMsg);

        // Update the session
        await _sessionStore.UpdateAsync(session);
    }
}
