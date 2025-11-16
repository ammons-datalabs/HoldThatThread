using HoldThatThread.Application;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// Factory that creates OpenAI (non-Azure) clients with model-specific configuration.
/// </summary>
public class OpenAiClientFactory : IOpenAiClientFactory
{
    private readonly OpenAiOptions _options;

    public OpenAiClientFactory(OpenAiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public IOpenAiClient CreateReasoningClient()
    {
        return new OpenAiClient(
            _options.ApiKey,
            _options.ReasoningModel,
            _options.BaseUrl);
    }

    public IOpenAiClient CreateDigressionClient()
    {
        return new OpenAiClient(
            _options.ApiKey,
            _options.DigressionModel,
            _options.BaseUrl);
    }
}