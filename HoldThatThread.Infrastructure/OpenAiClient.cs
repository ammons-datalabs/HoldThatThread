using HoldThatThread.Application;
using HoldThatThread.Domain;
using OpenAI.Chat;
using System.ClientModel;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// OpenAI client (non-Azure) with support for model-specific calls.
/// Supports both streaming (for reasoning with extended thinking) and
/// non-streaming (for quick digression responses).
/// </summary>
public class OpenAiClient : IOpenAiClient
{
    private readonly ChatClient _chatClient;

    /// <summary>
    /// Creates an OpenAI client for a specific model.
    /// </summary>
    /// <param name="apiKey">OpenAI API key</param>
    /// <param name="modelName">Name of the model to use (e.g., "o1", "gpt-4o-mini")</param>
    /// <param name="baseUrl">Optional: Custom base URL (defaults to OpenAI API)</param>
    public OpenAiClient(string apiKey, string modelName, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName));

        var credential = new ApiKeyCredential(apiKey);

        _chatClient = string.IsNullOrWhiteSpace(baseUrl)
            ? new ChatClient(modelName, credential)
            : new ChatClient(modelName, credential, new OpenAI.OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
    }

    public async IAsyncEnumerable<StreamChunk> ReasonAsyncStreaming(List<MainMessage> messages)
    {
        var chatMessages = messages.Select(m => MapToChatMessage(m)).ToList();

        var completionOptions = new ChatCompletionOptions
        {
            // Enable reasoning/thinking for supported models (o1, o3, etc.)
            Temperature = 1.0f,
            MaxOutputTokenCount = 2000
        };

        var streamingResult = _chatClient.CompleteChatStreamingAsync(
            chatMessages,
            completionOptions);

        var inReasoningPhase = true;

        await foreach (var update in streamingResult.ConfigureAwait(false))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    // Simple heuristic: if we see certain patterns, switch to answer phase
                    // In production with o1/o3 models, use the actual API indicators
                    var isStillReasoning = inReasoningPhase &&
                        (contentPart.Text.Contains("Let me") ||
                         contentPart.Text.Contains("I need to") ||
                         contentPart.Text.Contains("Analyzing"));

                    if (inReasoningPhase && !isStillReasoning)
                    {
                        inReasoningPhase = false;
                    }

                    yield return new StreamChunk
                    {
                        Type = inReasoningPhase ? StreamChunkType.Reasoning : StreamChunkType.Answer,
                        Content = contentPart.Text
                    };
                }
            }

            // For now, treat finish reason as signal to move to answer phase
            if (update.FinishReason.HasValue && inReasoningPhase)
            {
                inReasoningPhase = false;
            }
        }
    }

    public async Task<string> DigressAsync(List<MainMessage> messages)
    {
        var chatMessages = messages.Select(m => MapToChatMessage(m)).ToList();

        var completionOptions = new ChatCompletionOptions
        {
            // Optimize for speed and cost - no extended thinking
            Temperature = 0.7f,
            MaxOutputTokenCount = 500
        };

        var completion = await _chatClient.CompleteChatAsync(
            chatMessages,
            completionOptions);

        var response = completion.Value;
        return response.Content[0].Text ?? string.Empty;
    }

    private static ChatMessage MapToChatMessage(MainMessage message)
    {
        return message.Role.ToLowerInvariant() switch
        {
            "system" => ChatMessage.CreateSystemMessage(message.Content),
            "user" => ChatMessage.CreateUserMessage(message.Content),
            "assistant" => ChatMessage.CreateAssistantMessage(message.Content),
            _ => throw new ArgumentException($"Unknown message role: {message.Role}")
        };
    }
}