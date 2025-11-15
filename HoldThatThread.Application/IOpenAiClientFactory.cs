namespace HoldThatThread.Application;

/// <summary>
/// Factory for creating Azure OpenAI clients with deployment-specific configuration.
/// Supports separate deployments for reasoning (extended thinking) vs digression (quick responses).
/// </summary>
public interface IOpenAiClientFactory
{
    /// <summary>
    /// Creates an OpenAI client configured for main reasoning calls.
    /// Uses the reasoning deployment (e.g., o3-mini) with extended thinking capabilities.
    /// </summary>
    IOpenAiClient CreateReasoningClient();

    /// <summary>
    /// Creates an OpenAI client configured for digression calls.
    /// Uses the digression deployment (e.g., gpt-4o-mini) optimized for speed and cost.
    /// </summary>
    IOpenAiClient CreateDigressionClient();
}