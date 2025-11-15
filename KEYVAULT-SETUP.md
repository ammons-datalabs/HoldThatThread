# Azure Key Vault Setup Guide

This document explains how HoldThatThread secures sensitive configuration using Azure Key Vault and Managed Identity.

## Architecture Overview

**Local Development:**
- Uses `appsettings.Development.json` with stub API key
- User secrets available as alternative (optional)
- No Azure Key Vault required

**Production:**
- API key retrieved from Azure Key Vault
- Authenticated via Managed Identity (no credentials in code)
- Zero secrets in configuration files

## How It Works

### Configuration Priority (Highest to Lowest)

1. **Azure Key Vault** (Production only)
2. **User Secrets** (Development, optional)
3. **appsettings.Development.json** (Development)
4. **appsettings.json** (Base configuration)

### Key Vault Integration

When running in **Production** environment:

```csharp
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = builder.Configuration["KeyVault:VaultUri"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultEndpoint),
        new DefaultAzureCredential());
}
```

The `DefaultAzureCredential` automatically tries authentication methods in this order:
1. Managed Identity (in Azure App Service, Container Apps, etc.)
2. Azure CLI (for local testing with `az login`)
3. Visual Studio / VS Code credentials

## Azure Setup Instructions

### 1. Create Azure Key Vault

```bash
# Set variables
RESOURCE_GROUP="rg-holdthatthread"
LOCATION="eastus"
KEY_VAULT_NAME="kv-holdthatthread"  # Must be globally unique
APP_NAME="app-holdthatthread"

# Create resource group (if not exists)
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Key Vault
az keyvault create \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-rbac-authorization true
```

### 2. Store Azure OpenAI API Key

```bash
# Add the secret to Key Vault
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "AzureOpenAI--ApiKey" \
  --value "YOUR_AZURE_OPENAI_API_KEY"
```

**Important:** Key Vault secret names use `--` as the separator, which maps to `:` in configuration (e.g., `AzureOpenAI--ApiKey` → `AzureOpenAI:ApiKey`).

### 3. Deploy Application with Managed Identity

#### Option A: Azure App Service

```bash
# Create App Service with Managed Identity
az appservice plan create \
  --name plan-holdthatthread \
  --resource-group $RESOURCE_GROUP \
  --sku B1 \
  --is-linux

az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan plan-holdthatthread \
  --runtime "DOTNETCORE:9.0"

# Enable Managed Identity
az webapp identity assign \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Get the Managed Identity Principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId \
  --output tsv)
```

#### Option B: Azure Container Apps

```bash
# Create Container Apps Environment
az containerapp env create \
  --name env-holdthatthread \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Create Container App with Managed Identity
az containerapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment env-holdthatthread \
  --image mcr.microsoft.com/dotnet/samples:aspnetapp \
  --target-port 8080 \
  --ingress external \
  --enable-system-assigned-identity

# Get the Managed Identity Principal ID
PRINCIPAL_ID=$(az containerapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId \
  --output tsv)
```

### 4. Grant Key Vault Access to Managed Identity

```bash
# Get Key Vault resource ID
KEY_VAULT_ID=$(az keyvault show \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --query id \
  --output tsv)

# Assign "Key Vault Secrets User" role to the Managed Identity
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope $KEY_VAULT_ID
```

### 5. Configure Application Settings

```bash
# Set Key Vault URI in app configuration
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    KeyVault__VaultUri="https://$KEY_VAULT_NAME.vault.azure.net/"
```

## Local Development Options

### Option 1: Use Stub Key (Recommended)

No setup needed! The stub key in `appsettings.Development.json` works for all unit tests.

```json
{
  "AzureOpenAI": {
    "ApiKey": "stub-api-key-for-development"
  }
}
```

### Option 2: Use User Secrets

If you want to test with a real Azure OpenAI key locally:

```bash
# Set your real API key in user secrets
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR_REAL_KEY" \
  --project HoldThatThread.Api/HoldThatThread.Api.csproj

# List all secrets
dotnet user-secrets list --project HoldThatThread.Api/HoldThatThread.Api.csproj

# Remove secret
dotnet user-secrets remove "AzureOpenAI:ApiKey" \
  --project HoldThatThread.Api/HoldThatThread.Api.csproj
```

User secrets are stored in:
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **macOS/Linux:** `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

### Option 3: Test Against Real Key Vault Locally

```bash
# Login to Azure
az login

# Run the application
# It will use Azure CLI credentials to access Key Vault
ASPNETCORE_ENVIRONMENT=Production dotnet run --project HoldThatThread.Api
```

## Security Best Practices

✅ **Do:**
- Use Managed Identity in production
- Store secrets in Azure Key Vault
- Use RBAC for Key Vault access
- Rotate keys regularly
- Use separate Key Vaults for dev/staging/prod

❌ **Don't:**
- Commit API keys to source control
- Use the same key across environments
- Grant unnecessary Key Vault permissions
- Hardcode secrets in configuration files

## Configuration Reference

### appsettings.json (Production)

```json
{
  "KeyVault": {
    "VaultUri": "https://your-keyvault-name.vault.azure.net/"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ReasoningDeployment": "o3-mini",
    "DigressionDeployment": "gpt-4o-mini",
    "ApiKey": ""  // Retrieved from Key Vault
  }
}
```

### appsettings.Development.json (Local)

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://dev-holdthatthread.openai.azure.com/",
    "ReasoningDeployment": "o3-mini-dev",
    "DigressionDeployment": "gpt-4o-mini-dev",
    "ApiKey": "stub-api-key-for-development"
  }
}
```

## Troubleshooting

### "Secret not found" errors

**Problem:** Application can't find `AzureOpenAI:ApiKey` in Key Vault.

**Solution:** Ensure secret name uses `--` separator:
```bash
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "AzureOpenAI--ApiKey" \
  --value "YOUR_KEY"
```

### "Access denied" errors

**Problem:** Managed Identity doesn't have Key Vault permissions.

**Solution:** Verify role assignment:
```bash
az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope $KEY_VAULT_ID
```

### Local testing fails with Key Vault

**Problem:** Can't access Key Vault from local development.

**Solution:**
1. Run `az login` first
2. Ensure you have "Key Vault Secrets User" role
3. Or switch to Development environment: `ASPNETCORE_ENVIRONMENT=Development`

## Resources

- [Azure Key Vault Documentation](https://learn.microsoft.com/azure/key-vault/)
- [Managed Identity Overview](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [ASP.NET Core User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)