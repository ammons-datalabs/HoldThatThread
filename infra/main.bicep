// ============================================================================
// HoldThatThread Infrastructure - Main Orchestrator
// ============================================================================
// Deploys all Azure resources for the HoldThatThread API:
// - Azure OpenAI (o3-pro + gpt-5.1 deployments)
// - Azure Key Vault (for secrets storage)
//
// Usage:
//   az deployment sub create \
//     --location westus3 \
//     --template-file main.bicep \
//     --parameters @parameters/dev.bicepparam
// ============================================================================

targetScope = 'subscription'

// ============================================================================
// Parameters
// ============================================================================

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'dev'

@description('Azure region for all resources')
param location string = 'westus3'

@description('Base name for resources (will be prefixed with environment)')
@minLength(3)
@maxLength(20)
param projectName string = 'holdthread'

@description('Enable public network access (for development only)')
param enablePublicAccess bool = false

@description('Object ID of developer/user for Key Vault access (optional)')
param developerObjectId string = ''

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Project: 'HoldThatThread'
  ManagedBy: 'Bicep'
}

// ============================================================================
// Variables
// ============================================================================

var resourceGroupName = 'rg-${projectName}-${environmentName}'
var openAiName = 'oai-${projectName}-${environmentName}'
var keyVaultName = 'kv-${projectName}-${environmentName}'

// ============================================================================
// Resource Group
// ============================================================================

resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// ============================================================================
// Azure OpenAI Module
// ============================================================================

module openAi 'modules/openai.bicep' = {
  name: 'openai-deployment'
  scope: resourceGroup
  params: {
    openAiName: openAiName
    location: location
    tags: tags
    publicNetworkAccess: enablePublicAccess ? 'Enabled' : 'Disabled'
    reasoningDeploymentName: 'o3-pro'
    reasoningModelName: 'o3-pro'
    reasoningModelVersion: '2025-01-31'
    reasoningCapacity: 10
    digressionDeploymentName: 'gpt-5-1'
    digressionModelName: 'gpt-5.1'
    digressionModelVersion: '2025-02-01'
    digressionCapacity: 10
  }
}

// ============================================================================
// Key Vault Module
// ============================================================================

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  scope: resourceGroup
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
    publicNetworkAccess: enablePublicAccess ? 'Enabled' : 'Disabled'
    developerObjectId: developerObjectId
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: environmentName == 'prod'
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('Resource group name')
output resourceGroupName string = resourceGroupName

@description('Azure OpenAI endpoint URL')
output openAiEndpoint string = openAi.outputs.openAiEndpoint

@description('Azure OpenAI API key (SENSITIVE - store in Key Vault)')
@secure()
output openAiKey string = openAi.outputs.openAiKey

@description('Reasoning model deployment name')
output reasoningDeploymentName string = openAi.outputs.reasoningDeploymentName

@description('Digression model deployment name')
output digressionDeploymentName string = openAi.outputs.digressionDeploymentName

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Key Vault name')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('OpenAI account name')
output openAiAccountName string = openAi.outputs.openAiAccountName

// ============================================================================
// Configuration Summary (for appsettings/user secrets)
// ============================================================================

output configurationSummary object = {
  AzureOpenAI: {
    Endpoint: openAi.outputs.openAiEndpoint
    ReasoningDeployment: openAi.outputs.reasoningDeploymentName
    DigressionDeployment: openAi.outputs.digressionDeploymentName
  }
  KeyVault: {
    VaultUri: keyVault.outputs.keyVaultUri
  }
}

// ============================================================================
// Post-Deployment Steps
// ============================================================================
// 1. Store OpenAI key in Key Vault:
//    az keyvault secret set \
//      --vault-name {keyVaultName} \
//      --name "AzureOpenAI--ApiKey" \
//      --value "{openAiKey}"
//
// 2. Update user secrets for local development:
//    dotnet user-secrets set "AzureOpenAI:Endpoint" "{openAiEndpoint}"
//    dotnet user-secrets set "AzureOpenAI:ReasoningDeployment" "o3-pro"
//    dotnet user-secrets set "AzureOpenAI:DigressionDeployment" "gpt-5-1"
//    dotnet user-secrets set "KeyVault:VaultUri" "{keyVaultUri}"
//
// 3. For production, ensure:
//    - publicNetworkAccess is Disabled
//    - App Service has Managed Identity enabled
//    - Managed Identity has "Key Vault Secrets User" role
//    - Private Endpoint configured for OpenAI (if using APIM)
// ============================================================================