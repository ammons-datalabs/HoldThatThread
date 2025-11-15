namespace HoldThatThread.Application;

/// <summary>
/// Configuration options for Azure OpenAI service.
/// Supports separate deployments for reasoning (main) vs digression (quick) responses.
/// </summary>
public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL (e.g., https://your-resource.openai.azure.com/)
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for main reasoning calls (e.g., "o3-mini", "gpt-4o")
    /// Uses extended thinking/reasoning capabilities.
    /// </summary>
    public string ReasoningDeployment { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for quick digression responses (e.g., "gpt-4o-mini")
    /// Optimized for speed and cost, no reasoning mode.
    /// </summary>
    public string DigressionDeployment { get; set; } = string.Empty;

    /// <summary>
    /// API key for Azure OpenAI.
    /// In production, this should be retrieved from Azure Key Vault via Managed Identity.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("Azure OpenAI Endpoint is required");

        if (string.IsNullOrWhiteSpace(ReasoningDeployment))
            throw new InvalidOperationException("Reasoning deployment name is required");

        if (string.IsNullOrWhiteSpace(DigressionDeployment))
            throw new InvalidOperationException("Digression deployment name is required");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Azure OpenAI API key is required");

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
            throw new InvalidOperationException("Azure OpenAI Endpoint must be a valid URL");
    }
}