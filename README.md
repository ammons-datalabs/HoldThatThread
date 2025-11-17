# HoldThatThread

![Status](https://img.shields.io/badge/status-work%20in%20progress-yellow)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-blue)

A .NET API for managing reasoning conversations with digression support, powered by Azure OpenAI's advanced models.

> **Note**: This project is currently a work in progress. APIs and features may change.

## Overview

HoldThatThread enables deep reasoning conversations with AI while supporting quick clarification "digressions" without disrupting the main conversation flow. It uses dual Azure OpenAI deployments: extended thinking models (o3-mini) for main conversations and fast models (gpt-4o-mini) for digressions.

## Features

- **Streaming Reasoning Responses**: Real-time streaming with extended thinking using Server-Sent Events (SSE)
- **Digression Mini-Chats**: Quick clarification conversations that can be merged back or discarded
- **Session Management**: Persistent conversation history with turn-based interactions
- **EventSource Compatible**: Two-step GET SSE pattern for browser EventSource API
- **Dual Deployment Strategy**: Separate Azure OpenAI deployments optimized for reasoning vs. quick responses
- **Flexible Provider Support**: Works with both Azure OpenAI and OpenAI API
- **Enterprise Ready**: Azure Key Vault integration, Managed Identity authentication, and API Management support

## Architecture

Built using Clean Architecture principles:

- **Domain Layer**: Core business entities (Session, Message, ConversationTurn, DigressionSession)
- **Application Layer**: Business logic and services (ReasoningService, DigressionService)
- **Infrastructure Layer**: External integrations (Azure OpenAI client, in-memory stores)
- **API Layer**: RESTful endpoints with OpenAPI documentation

## Tech Stack

- .NET 9.0
- Azure OpenAI Service
- Server-Sent Events (SSE)
- Azure Key Vault
- Azure API Management (optional)
- Bicep (Infrastructure as Code)
- xUnit (Testing)

## Prerequisites

- .NET 9.0 SDK
- Azure subscription (for production deployment)
- Azure OpenAI Service with deployments (or OpenAI API key for development)

## Quick Start

### Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd HoldThatThread
   ```

2. **Configure settings**

   For Azure OpenAI:
   ```json
   {
     "OpenAIProvider": "AzureOpenAI",
     "AzureOpenAI": {
       "Endpoint": "https://your-resource.openai.azure.com/",
       "ReasoningDeployment": "o3-mini",
       "DigressionDeployment": "gpt-4o-mini",
       "ApiKey": "your-api-key"
     }
   }
   ```

   For OpenAI:
   ```json
   {
     "OpenAIProvider": "OpenAI",
     "OpenAI": {
       "ApiKey": "your-openai-api-key",
       "ReasoningModel": "o3-mini",
       "DigressionModel": "gpt-4o-mini"
     }
   }
   ```

3. **Run the API**
   ```bash
   cd HoldThatThread.Api
   dotnet run
   ```

4. **Access the API**
   - API: http://localhost:5146
   - OpenAPI docs: http://localhost:5146/openapi/v1.json

## API Usage

See [docs/endpoints.md](docs/endpoints.md) for detailed API documentation.

### Quick Example

```javascript
// Step 1: Start a conversation turn
const response = await fetch('/api/chat/main/turn', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    sessionId: null,
    userInput: "Explain quantum entanglement"
  })
});
const { turnId, sessionId } = await response.json();

// Step 2: Stream the response
const eventSource = new EventSource(`/api/chat/main/stream/${turnId}`);

eventSource.addEventListener('thought', (e) => {
  const data = JSON.parse(e.data);
  console.log('Thinking:', data.text);
});

eventSource.addEventListener('answer', (e) => {
  const data = JSON.parse(e.data);
  console.log('Answer:', data.text);
});

eventSource.addEventListener('done', () => {
  eventSource.close();
});
```

## Deployment

### Azure Deployment

The project includes complete Infrastructure as Code (Bicep templates) for automated Azure deployment.

1. **Review infrastructure setup**
   ```bash
   cd infra
   cat README.md
   ```

2. **Deploy to Azure**
   ```bash
   cd infra/scripts
   ./deploy.sh
   ```

The deployment includes:
- Azure App Service (Linux)
- Azure OpenAI Service with two deployments
- Azure Key Vault for secrets management
- Application Insights for monitoring
- Azure API Management (optional)

See [KEYVAULT-SETUP.md](KEYVAULT-SETUP.md) and [APIM-SETUP.md](APIM-SETUP.md) for additional configuration details.

## Testing

The project includes 82 comprehensive tests covering:
- Domain logic
- Application services
- Infrastructure components
- API endpoints

```bash
dotnet test
```

### Test Coverage

- **Domain**: Session management, message handling, digression logic
- **Application**: Reasoning and digression services
- **Infrastructure**: Azure OpenAI client integration, storage implementations
- **API**: Endpoint behavior and request/response handling

## Project Structure

```
HoldThatThread/
├── HoldThatThread.Domain/          # Core business entities
├── HoldThatThread.Application/     # Business logic and services
├── HoldThatThread.Infrastructure/  # External integrations
├── HoldThatThread.Api/             # REST API endpoints
├── HoldThatThread.Web/             # Web UI (optional)
├── HoldThatThread.Tests/           # Test suite
├── infra/                          # Bicep templates for Azure
├── docs/                           # API documentation
└── apim-policies/                  # API Management policies
```

## Configuration

### Environment Variables

- `OpenAIProvider`: "AzureOpenAI" or "OpenAI"
- `KeyVault__VaultUri`: Azure Key Vault URI (production)
- `ASPNETCORE_ENVIRONMENT`: Development/Production

### Key Vault Secrets (Production)

- `AzureOpenAI--Endpoint`
- `AzureOpenAI--ApiKey`
- `AzureOpenAI--ReasoningDeployment`
- `AzureOpenAI--DigressionDeployment`

## Contributing

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## Contact

**Ammons DataLabs**
Email: jaybea@gmail.com

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2025 Ammons DataLabs