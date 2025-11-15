using Azure;
using Azure.AI.OpenAI;
using HoldThatThread.Application;
using HoldThatThread.Domain;
using OpenAI.Chat;
using System.ClientModel;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// Azure OpenAI client with support for deployment-specific calls.
/// Supports both streaming (for reasoning with extended thinking) and
/// non-streaming (for quick digression responses).
/// </summary>
public class AzureOpenAiClient : IOpenAiClient
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;

    /// <summary>
    /// Creates an Azure OpenAI client for a specific deployment.
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint URL</param>
    /// <param name="apiKey">Azure OpenAI API key</param>
    /// <param name="deploymentName">Name of the deployment to use</param>
    public AzureOpenAiClient(string endpoint, string apiKey, string deploymentName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentNullException(nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));
        if (string.IsNullOrWhiteSpace(deploymentName))
            throw new ArgumentNullException(nameof(deploymentName));

        _client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));
        _deploymentName = deploymentName;
    }

    public async IAsyncEnumerable<StreamChunk> ReasonAsyncStreaming(List<MainMessage> messages)
    {
        var chatClient = _client.GetChatClient(_deploymentName);

        var chatMessages = messages.Select(m => MapToChatMessage(m)).ToList();

        var completionOptions = new ChatCompletionOptions
        {
            // Enable reasoning/thinking for supported models (o1, o3, etc.)
            // For models that don't support it, this will be ignored
            Temperature = 1.0f,
            MaxOutputTokenCount = 2000
        };

        var streamingResult = chatClient.CompleteChatStreamingAsync(
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
        var chatClient = _client.GetChatClient(_deploymentName);

        var chatMessages = messages.Select(m => MapToChatMessage(m)).ToList();

        var completionOptions = new ChatCompletionOptions
        {
            // Optimize for speed and cost - no extended thinking
            Temperature = 0.7f,
            MaxOutputTokenCount = 500
        };

        var completion = await chatClient.CompleteChatAsync(
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