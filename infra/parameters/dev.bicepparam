// ============================================================================
// Development Environment Parameters
// ============================================================================
// Parameters for deploying HoldThatThread infrastructure to development
//
// Usage:
//   az deployment sub create \
//     --location westus3 \
//     --template-file ../main.bicep \
//     --parameters dev.bicepparam
// ============================================================================

using '../main.bicep'

// ============================================================================
// Environment Configuration
// ============================================================================

param environmentName = 'dev'
param location = 'eastus'
param projectName = 'holdthread'

// Enable public access for easier local development
// WARNING: For production, this should be 'false'
param enablePublicAccess = true

// ============================================================================
// Developer Access (Optional)
// ============================================================================
// To grant your user account access to Key Vault, provide your Object ID
// Find your Object ID by running:
//   az ad signed-in-user show --query id -o tsv
//
// Example:
// param developerObjectId = '12345678-1234-1234-1234-123456789abc'

param developerObjectId = ''

// ============================================================================
// Resource Tags
// ============================================================================

param tags = {
  Environment: 'Development'
  Project: 'HoldThatThread'
  ManagedBy: 'Bicep'
  Owner: 'Development Team'
  CostCenter: 'Engineering'
}