namespace HoldThatThread.Application;

/// <summary>
/// Configuration options for OpenAI service (non-Azure).
/// Supports separate models for reasoning vs digression responses.
/// </summary>
public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>
    /// OpenAI API key (starts with sk-...)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name for main reasoning calls (e.g., "o1", "o3-mini")
    /// Uses extended thinking/reasoning capabilities.
    /// </summary>
    public string ReasoningModel { get; set; } = string.Empty;

    /// <summary>
    /// Model name for quick digression responses (e.g., "gpt-4o-mini")
    /// Optimized for speed and cost, no reasoning mode.
    /// </summary>
    public string DigressionModel { get; set; } = string.Empty;

    /// <summary>
    /// Optional: OpenAI API base URL (defaults to https://api.openai.com/v1)
    /// </summary>
    public string? BaseUrl { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("OpenAI API key is required");

        if (string.IsNullOrWhiteSpace(ReasoningModel))
            throw new InvalidOperationException("Reasoning model name is required");

        if (string.IsNullOrWhiteSpace(DigressionModel))
            throw new InvalidOperationException("Digression model name is required");

        if (!string.IsNullOrWhiteSpace(BaseUrl) && !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("OpenAI BaseUrl must be a valid URL");
    }
}
