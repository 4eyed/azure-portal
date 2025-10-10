# GitHub Actions CI/CD Setup Guide

This guide walks you through setting up automated deployments for your Azure Static Web App (frontend) and Azure Functions (backend + OpenFGA).

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     GitHub Repository                        │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  frontend/          → Azure Static Web App (already setup)   │
│  backend/MenuApi/   → Azure Functions (custom container)     │
│  openfga-fork/      → Custom OpenFGA with SQL Server         │
│  Dockerfile.combined → Multi-stage container build           │
│                                                               │
└─────────────────────────────────────────────────────────────┘
                              ↓
                    GitHub Actions Workflows
                              ↓
        ┌─────────────────────────────────────────┐
        │                                         │
   Frontend Path          Backend Path Trigger    │
   (already working)      (new workflow)          │
        │                      │                  │
        ↓                      ↓                  │
┌──────────────┐    ┌──────────────────────────┐ │
│ Static Web   │    │ Azure Container Registry │ │
│ App          │    │ (ACR)                    │ │
└──────────────┘    └──────────┬───────────────┘ │
                               │                  │
                               ↓                  │
                    ┌──────────────────────────┐  │
                    │ Azure Functions          │  │
                    │ - .NET 8 API             │  │
                    │ - OpenFGA (SQL Server)   │  │
                    └──────────────────────────┘  │
                               │                  │
                               ↓                  │
                    ┌──────────────────────────┐  │
                    │ Azure SQL Database       │  │
                    │ (OpenFGA storage)        │  │
                    └──────────────────────────┘  │
                                                  │
```

## Workflows

### 1. Frontend Deploy (Already Working ✅)
- **File:** [.github/workflows/azure-static-web-apps-witty-flower-068de881e.yml](.github/workflows/azure-static-web-apps-witty-flower-068de881e.yml)
- **Triggers:** Changes to `frontend/**`
- **Deploys to:** Azure Static Web App

### 2. Backend Deploy (New 🆕)
- **File:** [.github/workflows/azure-backend-deploy.yml](.github/workflows/azure-backend-deploy.yml)
- **Triggers:**
  - Push to `main` when these paths change:
    - `backend/**`
    - `openfga-fork/**`
    - `openfga-config/**`
    - `Dockerfile.combined`
  - Manual dispatch (workflow_dispatch)
- **Steps:**
  1. Build OpenFGA binary (with caching)
  2. Build combined Docker image (.NET + OpenFGA)
  3. Push to Azure Container Registry
  4. Update Azure Function App
  5. Configure environment variables
  6. Restart and smoke test

## Prerequisites

Before setting up CI/CD, ensure you have:

- [x] Azure Static Web App (already deployed)
- [x] Azure SQL Database (provisioned)
- [ ] Azure Function App with custom container support
- [ ] Azure Container Registry (ACR)
- [ ] Azure Service Principal with appropriate permissions

## Required GitHub Secrets

You need to configure these secrets in your GitHub repository settings:

**Settings → Secrets and variables → Actions → New repository secret**

### 1. AZURE_CREDENTIALS
Service Principal credentials for Azure CLI authentication.

**Create the Service Principal:**
```bash
# Set your subscription ID
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
RESOURCE_GROUP="rg-menu-app"

# Create service principal with Contributor role
az ad sp create-for-rbac \
  --name "github-actions-openfga" \
  --role contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth
```

**Output format (copy entire JSON):**
```json
{
  "clientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "clientSecret": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "subscriptionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "tenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
```

### 2. ACR_NAME
Your Azure Container Registry name (without `.azurecr.io`)

**Get it:**
```bash
az acr list --resource-group rg-menu-app --query "[0].name" -o tsv
```

Example: `acrmenuapp12345`

### 3. ACR_USERNAME
Azure Container Registry admin username

**Get it:**
```bash
ACR_NAME=$(az acr list --resource-group rg-menu-app --query "[0].name" -o tsv)
az acr credential show --name $ACR_NAME --query username -o tsv
```

### 4. ACR_PASSWORD
Azure Container Registry admin password

**Get it:**
```bash
ACR_NAME=$(az acr list --resource-group rg-menu-app --query "[0].name" -o tsv)
az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv
```

### 5. AZURE_FUNCTIONAPP_NAME
Your Azure Function App name

**Get it:**
```bash
az functionapp list --resource-group rg-menu-app --query "[0].name" -o tsv
```

Example: `func-menu-app-12345`

### 6. AZURE_RESOURCE_GROUP
Your Azure Resource Group name

Example: `rg-menu-app`

### 7. SQL_CONNECTION_STRING
Azure SQL Database connection string for OpenFGA

**Format:**
```
sqlserver://USERNAME:PASSWORD@SERVER.database.windows.net:1433?database=DBNAME&encrypt=true
```

**Get from your `.env.azure-sql` file:**
```bash
source .env.azure-sql
echo $OPENFGA_DATASTORE_URI
```

⚠️ **IMPORTANT:** Keep this secret secure! Never commit it to git.

### 8. AZURE_STATIC_WEB_APPS_API_TOKEN_WITTY_FLOWER_068DE881E
Static Web App deployment token (already configured ✅)

## Quick Setup Script

Run this script to get all the values you need:

```bash
#!/bin/bash
# collect-secrets.sh - Run this to collect all secret values

set -e

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-menu-app}"

echo "================================================"
echo "GitHub Actions Secrets Collection"
echo "================================================"
echo ""

# Source SQL credentials if available
if [ -f .env.azure-sql ]; then
    source .env.azure-sql
fi

# 1. Azure Credentials
echo "1. AZURE_CREDENTIALS"
echo "   Run this command and copy the ENTIRE JSON output:"
echo "   ---"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "   az ad sp create-for-rbac \\"
echo "     --name github-actions-openfga \\"
echo "     --role contributor \\"
echo "     --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP \\"
echo "     --sdk-auth"
echo ""

# 2. ACR Name
echo "2. ACR_NAME"
ACR_NAME=$(az acr list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)
echo "   Value: $ACR_NAME"
echo ""

# 3. ACR Username
echo "3. ACR_USERNAME"
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
echo "   Value: $ACR_USERNAME"
echo ""

# 4. ACR Password
echo "4. ACR_PASSWORD"
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)
echo "   Value: $ACR_PASSWORD"
echo ""

# 5. Function App Name
echo "5. AZURE_FUNCTIONAPP_NAME"
FUNC_NAME=$(az functionapp list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)
echo "   Value: $FUNC_NAME"
echo ""

# 6. Resource Group
echo "6. AZURE_RESOURCE_GROUP"
echo "   Value: $RESOURCE_GROUP"
echo ""

# 7. SQL Connection String
echo "7. SQL_CONNECTION_STRING"
if [ -z "$OPENFGA_DATASTORE_URI" ]; then
    echo "   ⚠️  Not found! Source .env.azure-sql first"
else
    echo "   Value: $OPENFGA_DATASTORE_URI"
fi
echo ""

echo "================================================"
echo "Next Steps:"
echo "1. Go to GitHub repo → Settings → Secrets → Actions"
echo "2. Click 'New repository secret'"
echo "3. Add each secret above with its value"
echo "================================================"
```

**Make it executable and run:**
```bash
chmod +x collect-secrets.sh
./collect-secrets.sh
```

## Adding Secrets to GitHub

1. Go to your GitHub repository
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add each secret from the list above
5. Click **Add secret**

## Testing the Workflow

### Option 1: Manual Trigger
1. Go to **Actions** tab in GitHub
2. Select **Azure Backend Deploy**
3. Click **Run workflow**
4. Select branch: `main`
5. Click **Run workflow**

### Option 2: Trigger by Push
1. Make a change to any file in `backend/` or `openfga-fork/`
2. Commit and push to `main`
3. Workflow will automatically start

### Option 3: Test Path Filter
```bash
# This will trigger backend deployment
echo "# Test deployment" >> backend/README.md
git add backend/README.md
git commit -m "test: Trigger backend deployment"
git push origin main

# This will NOT trigger backend deployment (frontend only)
echo "<!-- test -->" >> frontend/index.html
git add frontend/index.html
git commit -m "test: Update frontend"
git push origin main
```

## Monitoring Deployments

### GitHub Actions UI
- Go to **Actions** tab
- Click on the running workflow
- View real-time logs for each step

### Azure Portal
- Navigate to your Function App
- **Deployment Center** → View deployment logs
- **Log stream** → See application logs
- **Metrics** → Monitor performance

### Test the Deployed API
```bash
# Get your Function App URL
FUNC_URL="https://func-menu-app-12345.azurewebsites.net"

# Test the menu endpoint
curl "$FUNC_URL/api/menu?user=alice"
curl "$FUNC_URL/api/menu?user=bob"
curl "$FUNC_URL/api/menu?user=charlie"
```

## Workflow Features

### 1. Path Filtering ✅
Only deploys when relevant files change:
- `backend/**` - .NET API changes
- `openfga-fork/**` - OpenFGA changes
- `openfga-config/**` - Authorization model changes
- `Dockerfile.combined` - Container changes

### 2. Caching ✅
- **OpenFGA binary:** Cached based on Go source files hash
- **Docker layers:** Registry cache for faster builds
- **Go modules:** Cached by `setup-go` action

### 3. Manual Dispatch ✅
Run deployments on-demand:
- From GitHub Actions UI
- Choose environment (production/staging)

### 4. Smoke Testing ✅
Automatically verifies deployment:
- Waits for Function App to start
- Tests API health endpoint
- Retries up to 10 times

### 5. Deployment Summary ✅
Creates a summary with:
- Function App URL
- Container image tag
- Commit SHA
- Test endpoints

## Troubleshooting

### Workflow fails at "Build OpenFGA"
**Symptom:** Go build fails
**Solution:**
- Check Go version matches (1.24.x)
- Verify `openfga-fork/` submodule is checked out
- Check build logs for missing dependencies

### Workflow fails at "Build and push Docker image"
**Symptom:** Docker build fails
**Solution:**
- Verify ACR credentials are correct
- Check if OpenFGA binary artifact was uploaded
- Ensure Dockerfile.combined exists

### Workflow fails at "Update Azure Function App"
**Symptom:** Azure CLI commands fail
**Solution:**
- Verify `AZURE_CREDENTIALS` secret is correct JSON
- Check service principal has Contributor role
- Verify resource names are correct

### Function App returns 500 errors
**Symptom:** Smoke test fails
**Solution:**
- Check Function App logs in Azure Portal
- Verify `SQL_CONNECTION_STRING` is correct
- Check if OpenFGA migrations ran successfully
- Ensure firewall rules allow Function App to access SQL

### Changes don't trigger workflow
**Symptom:** Workflow doesn't run on push
**Solution:**
- Verify you pushed to `main` branch
- Check if changed files match path filters
- Look for workflow syntax errors in Actions tab

## Cost Optimization

The workflows are optimized to minimize costs:

✅ **OpenFGA binary caching** - Rebuilds only when Go code changes
✅ **Docker layer caching** - Faster builds, less data transfer
✅ **Path filtering** - Only deploys what changed
✅ **Shared resource group** - All resources in one place

**Estimated GitHub Actions Usage:**
- Frontend deploy: ~2 minutes (already running)
- Backend deploy: ~5-8 minutes (new)
- Monthly estimate: ~100-200 minutes/month (well within free tier)

## Next Steps

Once CI/CD is working:

1. **Update frontend URL** - Point `frontend/index.html` to production Function App URL
2. **Add staging environment** - Create staging Function App and SQL database
3. **Add environment variables** - Use GitHub Environments for different configs
4. **Add Slack notifications** - Get notified on deployment success/failure
5. **Add performance testing** - Load test after deployment
6. **Add rollback capability** - Deploy previous container version if needed

## Support

If you run into issues:

1. Check the [Troubleshooting](#troubleshooting) section
2. Review GitHub Actions logs for error messages
3. Check Azure Function App logs
4. Verify all secrets are configured correctly

## Architecture Decisions

### Why path filters?
- Prevents unnecessary deployments
- Saves CI/CD minutes
- Faster feedback loop

### Why cache OpenFGA binary?
- Go builds are slow (~2-3 minutes)
- OpenFGA code changes rarely
- Cache hit = instant binary

### Why multi-stage Docker build?
- Smaller final image
- Faster container startup
- Better security (no build tools in production)

### Why combined container?
- Single deployment unit
- Simpler orchestration
- No inter-container networking needed
- Function App supports single container

---

**Last Updated:** 2025-10-10
**Workflow Version:** 1.0.0
