using HoldThatThread.Application;
using Xunit;

namespace HoldThatThread.Tests.Application;

public class AzureOpenAiOptionsTests
{
    [Fact]
    public void Validate_ThrowsWhenEndpointIsEmpty()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Azure OpenAI Endpoint is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenEndpointIsWhitespace()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "   ",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Azure OpenAI Endpoint is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenReasoningDeploymentIsEmpty()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Reasoning deployment name is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenReasoningDeploymentIsWhitespace()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "   ",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Reasoning deployment name is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenDigressionDeploymentIsEmpty()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Digression deployment name is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenDigressionDeploymentIsWhitespace()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "   ",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Digression deployment name is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenApiKeyIsEmpty()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = ""
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Azure OpenAI API key is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenApiKeyIsWhitespace()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "   "
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Azure OpenAI API key is required", exception.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenEndpointIsNotValidUrl()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "not-a-valid-url",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-key"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Azure OpenAI Endpoint must be a valid URL", exception.Message);
    }

    [Fact]
    public void Validate_SucceedsWhenAllFieldsAreProvided()
    {
        // Arrange
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ReasoningDeployment = "o3-mini",
            DigressionDeployment = "gpt-4o-mini",
            ApiKey = "test-api-key-12345"
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("AzureOpenAI", AzureOpenAiOptions.SectionName);
    }
}