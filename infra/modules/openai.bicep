// ============================================================================
// Azure OpenAI Module
// ============================================================================
// Creates Azure OpenAI account with two deployments:
// - o3-pro: Advanced reasoning model for main conversation threads
// - gpt-5.1: Fast conversational model for digression explanations
//
// Security: Public access disabled by default. Use Private Endpoints or
// Managed Identity from APIM/App Service to access this resource.
// ============================================================================

@description('Name of the Azure OpenAI resource')
param openAiName string

@description('Azure region for the OpenAI resource')
param location string = 'westus3'

@description('Tags to apply to the resource')
param tags object = {}

@description('Public network access setting. Default is Disabled for security.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Disabled'

@description('SKU name for Azure OpenAI')
param skuName string = 'S0'

@description('Name for the reasoning model deployment')
param reasoningDeploymentName string = 'gpt-4o'

@description('Model name for reasoning deployment')
param reasoningModelName string = 'gpt-4o'

@description('Model version for reasoning deployment')
param reasoningModelVersion string = '2024-08-06'

@description('Capacity (in thousands of tokens per minute) for reasoning deployment')
param reasoningCapacity int = 10

@description('Name for the digression model deployment')
param digressionDeploymentName string = 'gpt-35-turbo'

@description('Model name for digression deployment')
param digressionModelName string = 'gpt-35-turbo'

@description('Model version for digression deployment')
param digressionModelVersion string = '0125'

@description('Capacity (in thousands of tokens per minute) for digression deployment')
param digressionCapacity int = 10

// ============================================================================
// Azure OpenAI Account
// ============================================================================

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAiName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: skuName
  }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: publicNetworkAccess
    networkAcls: {
      defaultAction: publicNetworkAccess == 'Disabled' ? 'Deny' : 'Allow'
    }
  }
}

// ============================================================================
// Model Deployments
// ============================================================================

// Reasoning Model Deployment (o3-pro)
// For main conversation threads requiring advanced reasoning capabilities
resource reasoningDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAiAccount
  name: reasoningDeploymentName
  sku: {
    name: 'Standard'
    capacity: reasoningCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: reasoningModelName
      version: reasoningModelVersion
    }
  }
}

// Digression Model Deployment (gpt-5.1)
// For fast, conversational explanations without forced reasoning overhead
resource digressionDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAiAccount
  name: digressionDeploymentName
  sku: {
    name: 'Standard'
    capacity: digressionCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: digressionModelName
      version: digressionModelVersion
    }
  }
  dependsOn: [
    reasoningDeployment
  ]
}

// ============================================================================
// Outputs
// ============================================================================

@description('The endpoint URL for Azure OpenAI')
output openAiEndpoint string = openAiAccount.properties.endpoint

@description('The name of the reasoning model deployment')
output reasoningDeploymentName string = reasoningDeployment.name

@description('The name of the digression model deployment')
output digressionDeploymentName string = digressionDeployment.name

@description('The resource ID of the OpenAI account (for Private Endpoint configuration)')
output openAiResourceId string = openAiAccount.id

@description('The name of the OpenAI account')
output openAiAccountName string = openAiAccount.name

// ============================================================================
// NOTES: Private Endpoint Integration
// ============================================================================
// When deploying with APIM or App Service:
// 1. Set publicNetworkAccess to 'Disabled'
// 2. Create a Private Endpoint in your VNet
// 3. Link the Private Endpoint to this OpenAI account using openAiResourceId
// 4. Configure Private DNS Zone for privatelink.openai.azure.com
// 5. APIM/App Service accesses OpenAI via the Private Endpoint
//
// For local development:
// - Temporarily set publicNetworkAccess to 'Enabled'
// - Or use Azure VPN/Bastion to access private resources
// ============================================================================
