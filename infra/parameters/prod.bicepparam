// ============================================================================
// Production Environment Parameters
// ============================================================================
// Parameters for deploying HoldThatThread infrastructure to production
//
// Usage:
//   az deployment sub create \
//     --location westus3 \
//     --template-file ../main.bicep \
//     --parameters prod.bicepparam
// ============================================================================

using '../main.bicep'

// ============================================================================
// Environment Configuration
// ============================================================================

param environmentName = 'prod'
param location = 'westus3'
param projectName = 'holdthread'

// Production security: Disable public access
// Access via Private Endpoints and Managed Identity only
param enablePublicAccess = false

// ============================================================================
// Developer Access
// ============================================================================
// For production, consider using a break-glass admin account
// or omit this to rely solely on RBAC assignments

param developerObjectId = ''

// ============================================================================
// Resource Tags
// ============================================================================

param tags = {
  Environment: 'Production'
  Project: 'HoldThatThread'
  ManagedBy: 'Bicep'
  Owner: 'Platform Team'
  CostCenter: 'Production'
  Criticality: 'High'
  DataClassification: 'Internal'
}