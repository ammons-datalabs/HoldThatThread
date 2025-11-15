using HoldThatThread.Application;

namespace HoldThatThread.Infrastructure;

/// <summary>
/// Factory that creates Azure OpenAI clients with deployment-specific configuration.
/// </summary>
public class AzureOpenAiClientFactory : IOpenAiClientFactory
{
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiClientFactory(AzureOpenAiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public IOpenAiClient CreateReasoningClient()
    {
        return new AzureOpenAiClient(
            _options.Endpoint,
            _options.ApiKey,
            _options.ReasoningDeployment);
    }

    public IOpenAiClient CreateDigressionClient()
    {
        return new AzureOpenAiClient(
            _options.Endpoint,
            _options.ApiKey,
            _options.DigressionDeployment);
    }
}