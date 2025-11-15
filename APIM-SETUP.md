# Azure API Management (APIM) Setup Guide

This guide explains how to deploy and configure Azure API Management for the HoldThatThread API.

## Why APIM?

Azure API Management provides:
- **Rate Limiting & Throttling**: Protect backend from abuse
- **Authentication & Authorization**: OAuth, API keys, JWT validation
- **Analytics & Monitoring**: Request tracking, performance metrics
- **Developer Portal**: Auto-generated API documentation
- **Caching**: Reduce backend load (where appropriate)
- **API Versioning**: Manage breaking changes gracefully
- **Transformation**: Request/response manipulation

## Architecture

```
Client Applications
    ↓
Azure API Management (APIM)
    ├─ Rate Limiting
    ├─ Authentication
    ├─ Caching (selective)
    └─ Policies
        ↓
HoldThatThread API (App Service / Container Apps)
    ├─ Azure OpenAI (Reasoning Deployment)
    └─ Azure OpenAI (Digression Deployment)
```

## Prerequisites

- Azure subscription
- HoldThatThread API deployed (App Service or Container Apps)
- Azure CLI installed
- OpenAPI definition available

## Step 1: Create APIM Instance

### Option A: Development Tier (Cost-effective for testing)

```bash
# Set variables
RESOURCE_GROUP="rg-holdthatthread"
LOCATION="eastus"
APIM_NAME="apim-holdthatthread"  # Must be globally unique
PUBLISHER_EMAIL="jaybea@gmail.com"
PUBLISHER_NAME="Ammons DataLabs"

# Create APIM instance (Developer tier for testing)
az apim create \
  --name $APIM_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --publisher-email $PUBLISHER_EMAIL \
  --publisher-name "$PUBLISHER_NAME" \
  --sku-name Developer \
  --enable-managed-identity true

# Note: Creation takes 30-45 minutes
```

### Option B: Standard/Premium Tier (Production)

```bash
# For production with SLA, use Standard or Premium
az apim create \
  --name $APIM_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --publisher-email $PUBLISHER_EMAIL \
  --publisher-name "$PUBLISHER_NAME" \
  --sku-name Standard \
  --sku-capacity 1 \
  --enable-managed-identity true
```

## Step 2: Import OpenAPI Definition

### Get OpenAPI Specification

The HoldThatThread API generates OpenAPI spec at `/openapi/v1.json` (development mode).

```bash
# Export OpenAPI spec from running API
curl https://your-api.azurewebsites.net/openapi/v1.json > holdthatthread-openapi.json
```

### Import into APIM

```bash
# Import API from OpenAPI specification
az apim api import \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --path "holdthatthread" \
  --specification-path holdthatthread-openapi.json \
  --specification-format OpenApiJson \
  --display-name "HoldThatThread API v1"
```

## Step 3: Configure Backend

```bash
# Set backend URL to your deployed API
BACKEND_URL="https://your-api.azurewebsites.net"

az apim api update \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --service-url $BACKEND_URL
```

## Step 4: Apply Policies

### Global (All APIs) Policy

```bash
# Upload global policy
az apim policy create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --policy-file apim-policies/cors-policy.xml
```

### API-Level Policy

```bash
# Apply rate limiting to entire API
az apim api policy create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --policy-file apim-policies/rate-limit-policy.xml
```

### Operation-Specific Policy

```bash
# Apply stricter limits to expensive streaming endpoint
az apim api operation policy create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --operation-id MainChatStream \
  --policy-xml @apim-policies/streaming-operation-policy.xml
```

## Step 5: Configure Rate Limiting Strategy

### Recommended Tiers

| Tier | Requests/Minute | Requests/Day | Use Case |
|------|----------------|--------------|----------|
| **Free** | 10 | 1,000 | Testing, personal use |
| **Standard** | 100 | 10,000 | Small teams, development |
| **Premium** | 1,000 | 100,000 | Production, enterprise |

### Operation-Specific Limits

| Operation | Recommended Limit | Reasoning |
|-----------|------------------|-----------|
| `/api/chat/main/stream` | 20/min | Expensive, uses reasoning deployment |
| `/api/chat/digress/start` | 50/min | Moderate cost, fast deployment |
| `/api/chat/digress/*` | 100/min | Lightweight operations |

## Step 6: Configure Subscription Keys

### Create Products

```bash
# Create Free tier product
az apim product create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --product-id free-tier \
  --product-name "Free Tier" \
  --description "10 requests per minute" \
  --subscription-required true \
  --approval-required false \
  --state published

# Add API to product
az apim product api add \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --product-id free-tier \
  --api-id holdthatthread-v1

# Create Standard tier product
az apim product create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --product-id standard-tier \
  --product-name "Standard Tier" \
  --description "100 requests per minute" \
  --subscription-required true \
  --approval-required true \
  --state published

az apim product api add \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --product-id standard-tier \
  --api-id holdthatthread-v1
```

### Create Test Subscription

```bash
# Create a test subscription for development
az apim subscription create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --subscription-id test-subscription \
  --display-name "Test Subscription" \
  --scope /products/free-tier \
  --state active
```

## Step 7: Test the API

### Get APIM Gateway URL

```bash
GATEWAY_URL=$(az apim show \
  --resource-group $RESOURCE_GROUP \
  --name $APIM_NAME \
  --query gatewayUrl \
  --output tsv)

echo "APIM Gateway: $GATEWAY_URL"
```

### Get Subscription Key

```bash
SUBSCRIPTION_KEY=$(az apim subscription list \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --query "[?displayName=='Test Subscription'].primaryKey | [0]" \
  --output tsv)

echo "Subscription Key: $SUBSCRIPTION_KEY"
```

### Test Main Chat Endpoint

```bash
curl -X POST \
  "$GATEWAY_URL/holdthatthread/api/chat/main/stream" \
  -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": null,
    "message": "What is the meaning of life?"
  }' \
  --no-buffer
```

### Test Digression Endpoint

```bash
# First, get a session ID from main chat
SESSION_ID="your-session-id-here"

curl -X POST \
  "$GATEWAY_URL/holdthatthread/api/chat/digress/start" \
  -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" \
  -H "Content-Type: application/json" \
  -d "{
    \"sessionId\": \"$SESSION_ID\",
    \"selectedText\": \"meaning of life\",
    \"initialUserMessage\": \"Can you elaborate on this?\"
  }"
```

## Step 8: Enable Developer Portal

```bash
# Enable developer portal
az apim api update \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --subscription-required true

# Get developer portal URL
PORTAL_URL=$(az apim show \
  --resource-group $RESOURCE_GROUP \
  --name $APIM_NAME \
  --query developerPortalUrl \
  --output tsv)

echo "Developer Portal: $PORTAL_URL"
```

The developer portal provides:
- Interactive API documentation
- Code samples in multiple languages
- Self-service subscription management
- API testing console

## Step 9: Configure Monitoring

### Enable Application Insights

```bash
# Create Application Insights
APP_INSIGHTS_NAME="ai-holdthatthread"

az monitor app-insights component create \
  --app $APP_INSIGHTS_NAME \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app $APP_INSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey \
  --output tsv)

# Link APIM to Application Insights
az apim api logger create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id holdthatthread-v1 \
  --logger-id appinsights \
  --logger-type applicationInsights \
  --credentials instrumentationKey=$INSTRUMENTATION_KEY
```

### View Metrics

Key metrics to monitor:
- **Request Rate**: Requests per second
- **Response Time**: P50, P95, P99 latencies
- **Error Rate**: 4xx and 5xx responses
- **Throttled Requests**: Rate limit hits
- **Backend Health**: Upstream API availability

## Step 10: Advanced Configuration

### Custom Domain

```bash
# Upload custom certificate
az apim certificate create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --certificate-id custom-cert \
  --certificate-path /path/to/cert.pfx \
  --certificate-password "cert-password"

# Configure custom domain
az apim custom-domain create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --hostname api.holdthatthread.com \
  --certificate-id custom-cert
```

### OAuth 2.0 / JWT Validation

```xml
<!-- Add to policy for JWT validation -->
<validate-jwt header-name="Authorization" failed-validation-httpcode="401">
    <openid-config url="https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid-configuration" />
    <required-claims>
        <claim name="aud">
            <value>your-api-client-id</value>
        </claim>
    </required-claims>
</validate-jwt>
```

### IP Whitelisting

```xml
<!-- Restrict access to specific IP ranges -->
<ip-filter action="allow">
    <address-range from="203.0.113.0" to="203.0.113.255" />
    <address>198.51.100.10</address>
</ip-filter>
```

## Cost Optimization

| Tier | Monthly Cost (Approx) | Best For |
|------|---------------------|----------|
| **Consumption** | Pay-per-use (~$3.50/million calls) | Serverless, variable traffic |
| **Developer** | ~$50/month | Testing, non-production |
| **Basic** | ~$150/month | Small production workloads |
| **Standard** | ~$750/month | Production with SLA |
| **Premium** | ~$3,000/month | Multi-region, VNet integration |

**Recommendation for HoldThatThread:**
- **Development/Testing**: Developer tier
- **Production (small scale)**: Consumption tier
- **Production (predictable load)**: Standard tier

## Security Best Practices

✅ **Do:**
- Enable subscription keys for all products
- Use OAuth/JWT for user authentication
- Implement rate limiting on all operations
- Enable HTTPS only (disable HTTP)
- Use Managed Identity for backend authentication
- Monitor for suspicious patterns

❌ **Don't:**
- Expose backend URLs directly
- Use same subscription key across environments
- Cache personalized/streaming responses
- Allow unlimited requests
- Store credentials in policies

## Troubleshooting

### "Subscription key required" error

**Problem:** Missing `Ocp-Apim-Subscription-Key` header.

**Solution:**
```bash
# Include subscription key in request
curl -H "Ocp-Apim-Subscription-Key: YOUR_KEY" ...
```

### "Too Many Requests" (429)

**Problem:** Rate limit exceeded.

**Solution:** Wait for rate limit window to reset or upgrade subscription tier.

### Backend timeout

**Problem:** APIM timeout before streaming completes.

**Solution:** Increase APIM timeout for streaming operations:
```xml
<forward-request timeout="300" />
```

## Resources

- [APIM Documentation](https://learn.microsoft.com/azure/api-management/)
- [Policy Reference](https://learn.microsoft.com/azure/api-management/api-management-policies)
- [Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)
- [Best Practices](https://learn.microsoft.com/azure/api-management/api-management-sample-send-request)

## Next Steps

After APIM setup:
1. Configure CI/CD to auto-update API definitions
2. Set up alerts for rate limit violations
3. Create runbooks for common operations
4. Document subscription tiers for users
5. Implement API versioning strategy