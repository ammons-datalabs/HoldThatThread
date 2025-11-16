// ============================================================================
// Azure Key Vault Module
// ============================================================================
// Creates Azure Key Vault for secure storage of:
// - Azure OpenAI API keys
// - Connection strings
// - Other application secrets
//
// Security: Public access disabled by default. Access via Managed Identity
// from App Service or local development via Azure CLI authentication.
// ============================================================================

@description('Name of the Key Vault resource')
param keyVaultName string

@description('Azure region for the Key Vault resource')
param location string = 'westus3'

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU for the Key Vault')
@allowed([
  'standard'
  'premium'
])
param skuName string = 'standard'

@description('Enable public network access. Default is Disabled for security.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Disabled'

@description('Object ID of the user/service principal for development access')
param developerObjectId string = ''

@description('Tenant ID for Azure AD')
param tenantId string = tenant().tenantId

@description('Enable RBAC authorization (recommended over access policies)')
param enableRbacAuthorization bool = true

@description('Enable soft delete for Key Vault')
param enableSoftDelete bool = true

@description('Soft delete retention period in days')
param softDeleteRetentionInDays int = 90

@description('Enable purge protection')
param enablePurgeProtection bool = true

// ============================================================================
// Key Vault Resource
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: skuName
    }
    enableRbacAuthorization: enableRbacAuthorization
    enableSoftDelete: enableSoftDelete
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection
    publicNetworkAccess: publicNetworkAccess
    networkAcls: {
      defaultAction: publicNetworkAccess == 'Disabled' ? 'Deny' : 'Allow'
      bypass: 'AzureServices'
    }
    // Access policies only used if RBAC is disabled
    accessPolicies: enableRbacAuthorization ? [] : (developerObjectId != '' ? [
      {
        tenantId: tenantId
        objectId: developerObjectId
        permissions: {
          secrets: [
            'get'
            'list'
            'set'
            'delete'
          ]
        }
      }
    ] : [])
  }
}

// ============================================================================
// RBAC Role Assignment for Developer (if using RBAC and developer ID provided)
// ============================================================================

// Key Vault Secrets Officer role for developer
resource developerSecretsOfficerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableRbacAuthorization && developerObjectId != '') {
  name: guid(keyVault.id, developerObjectId, 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7') // Key Vault Secrets Officer
    principalId: developerObjectId
    principalType: 'User'
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('The URI of the Key Vault')
output keyVaultUri string = keyVault.properties.vaultUri

@description('The name of the Key Vault')
output keyVaultName string = keyVault.name

@description('The resource ID of the Key Vault')
output keyVaultResourceId string = keyVault.id

// ============================================================================
// NOTES: Storing Secrets
// ============================================================================
// After deployment, store secrets using Azure CLI:
//   az keyvault secret set --vault-name <name> --name "AzureOpenAI--ApiKey" --value "<key>"
//
// For API access via Managed Identity:
// 1. Deploy API to App Service/Container Apps with Managed Identity enabled
// 2. Grant Managed Identity "Key Vault Secrets User" role on this vault
// 3. API reads secrets using DefaultAzureCredential (works with Managed Identity)
//
// For local development:
// 1. Authenticate with Azure CLI: az login
// 2. Ensure your user has "Key Vault Secrets User" or "Secrets Officer" role
// 3. Use DefaultAzureCredential in code (automatically uses Azure CLI auth locally)
// 4. Or temporarily enable publicNetworkAccess for development
// ============================================================================