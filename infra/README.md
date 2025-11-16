# HoldThatThread - Infrastructure as Code

This directory contains Bicep templates for deploying the HoldThatThread API infrastructure to Azure.

## Overview

The infrastructure includes:

- **Azure OpenAI** - Two model deployments:
  - `o3-pro` - Advanced reasoning model for main conversation threads
  - `gpt-5.1` - Fast conversational model for digression explanations
- **Azure Key Vault** - Secure storage for API keys and secrets
- **Resource Group** - Container for all resources

## Directory Structure

```
infra/
├── main.bicep                 # Main orchestrator template
├── modules/                   # Reusable Bicep modules
│   ├── openai.bicep          # Azure OpenAI configuration
│   └── keyvault.bicep        # Key Vault configuration
├── parameters/                # Environment-specific parameters
│   ├── dev.bicepparam        # Development environment
│   └── prod.bicepparam       # Production environment
├── scripts/                   # Deployment automation
│   └── deploy.sh             # Deployment script
└── README.md                 # This file
```

## Prerequisites

1. **Azure CLI** - [Install Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
2. **Azure Subscription** - Active Azure subscription with appropriate permissions
3. **Bicep** - Installed automatically with Azure CLI 2.20.0+
4. **jq** - JSON processor for deployment script (optional but recommended)

## Quick Start

### 1. Login to Azure

```bash
az login
az account set --subscription <your-subscription-id>
```

### 2. (Optional) Configure Developer Access

To grant yourself access to Key Vault, find your Object ID:

```bash
az ad signed-in-user show --query id -o tsv
```

Edit the parameter file (`parameters/dev.bicepparam`) and add your Object ID:

```bicep
param developerObjectId = 'your-object-id-here'
```

### 3. Deploy Infrastructure

```bash
cd infra/scripts
./deploy.sh dev
```

The script will:
- Validate your Azure login
- Deploy all resources
- Store the OpenAI API key in Key Vault
- Generate configuration for local development

### 4. Configure Local Development

After deployment, use the commands from `deployment-config.txt`:

```bash
cd ../../HoldThatThread.Api

# Set user secrets
dotnet user-secrets set "AzureOpenAI:Endpoint" "<endpoint-from-output>"
dotnet user-secrets set "AzureOpenAI:ReasoningDeployment" "o3-pro"
dotnet user-secrets set "AzureOpenAI:DigressionDeployment" "gpt-5-1"
dotnet user-secrets set "KeyVault:VaultUri" "<keyvault-uri-from-output>"

# Grant yourself Key Vault access (if not set during deployment)
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope "<keyvault-resource-id>"

# Run the API
dotnet run
```

## Manual Deployment

If you prefer to deploy manually without the script:

```bash
# Deploy to development
az deployment sub create \
  --name "holdthread-dev-$(date +%Y%m%d-%H%M%S)" \
  --location westus3 \
  --template-file main.bicep \
  --parameters @parameters/dev.bicepparam

# Store API key in Key Vault
az keyvault secret set \
  --vault-name <keyvault-name> \
  --name "AzureOpenAI--ApiKey" \
  --value "<api-key-from-output>"
```

## Environments

### Development (`dev`)
- **Public Access**: Enabled (for easier local development)
- **Location**: West US 3
- **Purge Protection**: Disabled (allows quick teardown/rebuild)

### Production (`prod`)
- **Public Access**: Disabled (security best practice)
- **Location**: West US 3
- **Purge Protection**: Enabled (protects against accidental deletion)
- **Access Method**: Managed Identity + Private Endpoints

## Architecture

### Development Architecture
```
Developer Machine
    | (HTTPS)
Azure OpenAI (Public) <- API Key from Key Vault
    ^
Key Vault (Public)
```

### Production Architecture (Future)
```
Public Internet
    | (HTTPS)
Azure API Management (APIM)
    | (Private Endpoint/VNet)
App Service / Container Apps
    | (Managed Identity)
Key Vault (Private)
    |
Azure OpenAI (Private)
```

## Resource Naming Convention

Resources follow the pattern: `<type>-<project>-<environment>`

| Resource Type | Prefix | Example Dev Name | Example Prod Name |
|--------------|--------|------------------|-------------------|
| Resource Group | rg- | rg-holdthread-dev | rg-holdthread-prod |
| Azure OpenAI | oai- | oai-holdthread-dev | oai-holdthread-prod |
| Key Vault | kv- | kv-holdthread-dev | kv-holdthread-prod |

## Security Considerations

### Development
- Public network access is enabled for convenience
- Developer accounts have direct Key Vault access
- API keys are still stored securely in Key Vault

### Production
- Public network access is **disabled** by default
- Access only via Private Endpoints and Managed Identity
- No API keys in configuration files
- Purge protection prevents accidental deletion
- Network isolation via VNet integration

## Model Configuration

### Reasoning Model (o3-pro)
- **Purpose**: Main conversation threads requiring deep reasoning
- **Deployment Name**: `o3-pro`
- **Model**: `o3-pro`
- **Version**: `2025-01-31`
- **Capacity**: 10K TPM (adjustable)

### Digression Model (gpt-5.1)
- **Purpose**: Fast explanations without forced reasoning overhead
- **Deployment Name**: `gpt-5-1`
- **Model**: `gpt-5.1`
- **Version**: `2025-02-01`
- **Capacity**: 10K TPM (adjustable)

## Customization

### Changing Model Versions

Edit `main.bicep` and update the version parameters:

```bicep
reasoningModelVersion: '2025-01-31'  // Update to new version
digressionModelVersion: '2025-02-01'  // Update to new version
```

### Adjusting Capacity

Edit the parameter file or `main.bicep`:

```bicep
reasoningCapacity: 20     // Increase to 20K TPM
digressionCapacity: 20    // Increase to 20K TPM
```

### Using Different Regions

Edit the parameter file:

```bicep
param location = 'eastus'  // Change from westus3
```

**Note**: Ensure the region supports Azure OpenAI and the specific models.

## Troubleshooting

### Model Version Not Available

If deployment fails due to model version:

1. Check available versions:
   ```bash
   az cognitiveservices account list-models \
     --resource-group <rg-name> \
     --name <openai-name> \
     --query "[?name=='o3-pro'].version"
   ```

2. Update version in `main.bicep`

### Key Vault Access Denied

If you can't access Key Vault:

```bash
# Grant yourself Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope /subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.KeyVault/vaults/<kv-name>
```

### Public Access Issues

If you can't access resources with public access disabled:

**Option 1**: Temporarily enable public access (dev only)
```bicep
param enablePublicAccess = true
```

**Option 2**: Use Azure VPN or Bastion for private access

## Cost Estimation

### Development Environment
- Azure OpenAI: Pay-per-token (varies by usage)
- Key Vault: ~$0.03/10K operations
- **Estimated Monthly**: $20-100 (depending on usage)

### Production Environment
- Azure OpenAI: Pay-per-token
- Key Vault: ~$0.03/10K operations
- App Service: $50-200/month (depending on tier)
- APIM: $500+/month (if using dedicated tier)

## Cleanup

To delete all resources:

```bash
# Development
az group delete --name rg-holdthread-dev --yes --no-wait

# Production (be careful!)
az group delete --name rg-holdthread-prod --yes --no-wait
```

**Note**: If purge protection is enabled on Key Vault, you may need to purge it separately after deletion.

## Next Steps

After deploying infrastructure:

1. Configure local development (user secrets)
2. Run API locally to verify Azure OpenAI integration
3. Deploy API to Azure App Service (Phase 6)
4. Configure Managed Identity for App Service
5. Set up Azure API Management (APIM)
6. Configure Private Endpoints
7. Set up monitoring and logging

## Support

For issues or questions:
- Review deployment output in `deployment-config.txt`
- Check Azure Portal for resource status
- Review Bicep deployment logs: `az deployment sub show --name <deployment-name>`

## References

- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure OpenAI Service Documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Managed Identity Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)