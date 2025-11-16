#!/bin/bash
# ============================================================================
# HoldThatThread Infrastructure Deployment Script
# ============================================================================
# Deploys Azure infrastructure using Bicep templates
#
# Usage:
#   ./deploy.sh <environment>
#
# Examples:
#   ./deploy.sh dev
#   ./deploy.sh prod
# ============================================================================

set -e  # Exit on error

# ============================================================================
# Configuration
# ============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
LOCATION="westus3"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ============================================================================
# Functions
# ============================================================================

print_header() {
    echo -e "${BLUE}============================================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}============================================================================${NC}"
}

print_success() {
    echo -e "${GREEN}[OK] $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}[WARNING] $1${NC}"
}

print_error() {
    echo -e "${RED}[ERROR] $1${NC}"
}

print_info() {
    echo -e "${BLUE}[INFO] $1${NC}"
}

# ============================================================================
# Validate Input
# ============================================================================

if [ $# -eq 0 ]; then
    print_error "No environment specified"
    echo "Usage: $0 <environment>"
    echo "Valid environments: dev, prod"
    exit 1
fi

ENVIRONMENT=$1

if [[ ! "$ENVIRONMENT" =~ ^(dev|prod)$ ]]; then
    print_error "Invalid environment: $ENVIRONMENT"
    echo "Valid environments: dev, prod"
    exit 1
fi

PARAM_FILE="${INFRA_DIR}/parameters/${ENVIRONMENT}.bicepparam"
TEMPLATE_FILE="${INFRA_DIR}/main.bicep"

# ============================================================================
# Verify Files Exist
# ============================================================================

print_header "Validating deployment files"

if [ ! -f "$TEMPLATE_FILE" ]; then
    print_error "Template file not found: $TEMPLATE_FILE"
    exit 1
fi
print_success "Template file found"

if [ ! -f "$PARAM_FILE" ]; then
    print_error "Parameter file not found: $PARAM_FILE"
    exit 1
fi
print_success "Parameter file found"

# ============================================================================
# Check Azure CLI
# ============================================================================

print_header "Checking Azure CLI"

if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed"
    echo "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi
print_success "Azure CLI is installed"

# Check if logged in
if ! az account show &> /dev/null; then
    print_error "Not logged in to Azure"
    echo "Run: az login"
    exit 1
fi

ACCOUNT_NAME=$(az account show --query name -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
print_success "Logged in as: $ACCOUNT_NAME"
print_info "Subscription: $SUBSCRIPTION_ID"

# ============================================================================
# Confirm Deployment
# ============================================================================

print_header "Deployment Configuration"
echo "Environment:    $ENVIRONMENT"
echo "Location:       $LOCATION"
echo "Template:       $TEMPLATE_FILE"
echo "Parameters:     $PARAM_FILE"
echo ""

read -p "Proceed with deployment? (yes/no): " -r
echo
if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    print_warning "Deployment cancelled"
    exit 0
fi

# ============================================================================
# Deploy Infrastructure
# ============================================================================

print_header "Deploying Infrastructure"

DEPLOYMENT_NAME="holdthread-${ENVIRONMENT}-$(date +%Y%m%d-%H%M%S)"

echo "Deployment name: $DEPLOYMENT_NAME"
echo ""

# Deploy with Bicep
az deployment sub create \
    --name "$DEPLOYMENT_NAME" \
    --location "$LOCATION" \
    --template-file "$TEMPLATE_FILE" \
    --parameters "$PARAM_FILE" \
    --output json > deployment-output.json

if [ $? -eq 0 ]; then
    print_success "Infrastructure deployed successfully"
else
    print_error "Deployment failed"
    exit 1
fi

# ============================================================================
# Extract Outputs
# ============================================================================

print_header "Deployment Outputs"

RESOURCE_GROUP=$(jq -r '.properties.outputs.resourceGroupName.value' deployment-output.json)
OPENAI_ENDPOINT=$(jq -r '.properties.outputs.openAiEndpoint.value' deployment-output.json)
OPENAI_KEY=$(jq -r '.properties.outputs.openAiKey.value' deployment-output.json)
REASONING_DEPLOYMENT=$(jq -r '.properties.outputs.reasoningDeploymentName.value' deployment-output.json)
DIGRESSION_DEPLOYMENT=$(jq -r '.properties.outputs.digressionDeploymentName.value' deployment-output.json)
KEYVAULT_URI=$(jq -r '.properties.outputs.keyVaultUri.value' deployment-output.json)
KEYVAULT_NAME=$(jq -r '.properties.outputs.keyVaultName.value' deployment-output.json)

echo "Resource Group:          $RESOURCE_GROUP"
echo "OpenAI Endpoint:         $OPENAI_ENDPOINT"
echo "Reasoning Deployment:    $REASONING_DEPLOYMENT"
echo "Digression Deployment:   $DIGRESSION_DEPLOYMENT"
echo "Key Vault URI:           $KEYVAULT_URI"
echo "Key Vault Name:          $KEYVAULT_NAME"
echo ""

# ============================================================================
# Store API Key in Key Vault
# ============================================================================

print_header "Storing Secrets in Key Vault"

print_info "Storing OpenAI API key in Key Vault..."

az keyvault secret set \
    --vault-name "$KEYVAULT_NAME" \
    --name "AzureOpenAI--ApiKey" \
    --value "$OPENAI_KEY" \
    --output none

if [ $? -eq 0 ]; then
    print_success "API key stored in Key Vault"
else
    print_warning "Failed to store API key in Key Vault (may need to grant access)"
fi

# ============================================================================
# Generate Configuration
# ============================================================================

print_header "Next Steps"

cat > deployment-config.txt <<EOF
# ============================================================================
# HoldThatThread - Deployment Configuration
# Environment: $ENVIRONMENT
# Deployed: $(date)
# ============================================================================

# Azure Resources
Resource Group:          $RESOURCE_GROUP
Location:                $LOCATION

# Azure OpenAI
Endpoint:                $OPENAI_ENDPOINT
Reasoning Deployment:    $REASONING_DEPLOYMENT
Digression Deployment:   $DIGRESSION_DEPLOYMENT

# Key Vault
URI:                     $KEYVAULT_URI
Name:                    $KEYVAULT_NAME

# ============================================================================
# Local Development Setup
# ============================================================================

# 1. Set user secrets for local development:
cd HoldThatThread.Api
dotnet user-secrets set "AzureOpenAI:Endpoint" "$OPENAI_ENDPOINT"
dotnet user-secrets set "AzureOpenAI:ReasoningDeployment" "$REASONING_DEPLOYMENT"
dotnet user-secrets set "AzureOpenAI:DigressionDeployment" "$DIGRESSION_DEPLOYMENT"
dotnet user-secrets set "KeyVault:VaultUri" "$KEYVAULT_URI"

# 2. Ensure you have Key Vault access:
az keyvault set-policy --name "$KEYVAULT_NAME" --object-id \$(az ad signed-in-user show --query id -o tsv) --secret-permissions get list

# Or if using RBAC:
az role assignment create --role "Key Vault Secrets User" --assignee \$(az ad signed-in-user show --query id -o tsv) --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEYVAULT_NAME

# 3. Run the API locally:
dotnet run

# ============================================================================
# Production Deployment (Future)
# ============================================================================

# 1. Deploy API to Azure App Service with Managed Identity
# 2. Grant Managed Identity "Key Vault Secrets User" role
# 3. Configure App Settings with endpoints (not secrets!)
# 4. Set up Private Endpoints for OpenAI (if using APIM)

EOF

print_success "Configuration saved to: deployment-config.txt"
echo ""
print_info "To configure local development, run the commands in deployment-config.txt"
echo ""
print_success "Deployment complete!"

# Clean up sensitive output
rm deployment-output.json