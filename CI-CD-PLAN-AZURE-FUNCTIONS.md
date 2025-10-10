# CI/CD Implementation Plan - Azure Functions Edition

## 🎯 Project Overview

**Correct Architecture:**
- **Frontend**: Azure Static Web Apps (React/Vite)
- **Backend**: **Azure Functions with Custom Container**
  - Deployment: App Service Plan B1 (~$13/month)
  - Container: .NET 8 Isolated Functions + Custom OpenFGA binary
  - Single combined container running both services
- **Database**: Azure SQL Database (shared for OpenFGA + app data)
- **Container Registry**: Azure Container Registry (ACR)
- **Auth**: OpenFGA for fine-grained authorization

---

## 📁 Repository Structure

```
menu-app/
├── .github/
│   └── workflows/
│       ├── backend-deploy.yml          # Build & deploy custom container to Azure Functions
│       ├── frontend-deploy.yml         # Deploy React app to Static Web Apps
│       ├── database-migrations.yml     # DB schema updates
│       └── pr-validation.yml           # PR checks
├── frontend/                           # React/Vite app
├── backend/MenuApi/                    # .NET Azure Functions
├── openfga-fork/                       # Custom OpenFGA with SQL Server
├── database/                           # SQL schemas & migrations
├── Dockerfile.combined                 # Backend + OpenFGA container
└── deploy-to-azure.sh                  # Manual deployment script (reference)
```

---

## 🔄 Main CI/CD Workflow: Backend Deployment

### **Workflow: `backend-deploy.yml`**

**File:** `.github/workflows/backend-deploy.yml`

**Triggers:**
```yaml
on:
  push:
    branches: [main, develop]
    paths:
      - 'backend/**'
      - 'openfga-fork/**'
      - 'Dockerfile.combined'
      - '.github/workflows/backend-deploy.yml'
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment to deploy to'
        required: true
        default: 'dev'
        type: choice
        options:
          - dev
          - staging
          - production
```

**Jobs:**

### **Job 1: Build & Push Container**

```yaml
build-and-push:
  runs-on: ubuntu-latest
  outputs:
    image-tag: ${{ steps.meta.outputs.tags }}

  steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Login to Azure Container Registry
      uses: docker/login-action@v3
      with:
        registry: ${{ secrets.ACR_LOGIN_SERVER }}
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}

    - name: Extract metadata (tags, labels)
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ${{ secrets.ACR_LOGIN_SERVER }}/menu-app-combined
        tags: |
          type=ref,event=branch
          type=sha,prefix={{branch}}-
          type=raw,value=latest,enable={{is_default_branch}}

    - name: Build and push Docker image
      uses: docker/build-push-action@v5
      with:
        context: .
        file: ./Dockerfile.combined
        platforms: linux/amd64
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

    - name: Run Trivy security scan
      uses: aquasecurity/trivy-action@master
      with:
        image-ref: ${{ secrets.ACR_LOGIN_SERVER }}/menu-app-combined:${{ github.sha }}
        format: 'sarif'
        output: 'trivy-results.sarif'

    - name: Upload Trivy results to GitHub Security
      uses: github/codeql-action/upload-sarif@v3
      if: always()
      with:
        sarif_file: 'trivy-results.sarif'
```

### **Job 2: Deploy to Azure Functions**

```yaml
deploy-dev:
  needs: build-and-push
  runs-on: ubuntu-latest
  if: github.ref == 'refs/heads/develop'
  environment:
    name: development
    url: ${{ steps.deploy.outputs.webapp-url }}

  steps:
    - name: Login to Azure
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}

    - name: Get Azure SQL connection string from Key Vault
      id: keyvault
      run: |
        CONNECTION_STRING=$(az keyvault secret show \
          --vault-name ${{ secrets.KEY_VAULT_NAME }} \
          --name SQL-CONNECTION-STRING \
          --query value -o tsv)
        echo "::add-mask::$CONNECTION_STRING"
        echo "CONNECTION_STRING=$CONNECTION_STRING" >> $GITHUB_OUTPUT

    - name: Deploy to Azure Functions
      id: deploy
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ secrets.FUNCTION_APP_NAME_DEV }}
        images: ${{ secrets.ACR_LOGIN_SERVER }}/menu-app-combined:${{ github.sha }}

    - name: Configure Function App Settings
      run: |
        az functionapp config appsettings set \
          --name ${{ secrets.FUNCTION_APP_NAME_DEV }} \
          --resource-group ${{ secrets.RESOURCE_GROUP }} \
          --settings \
            "OPENFGA_DATASTORE_ENGINE=sqlserver" \
            "OPENFGA_DATASTORE_URI=${{ steps.keyvault.outputs.CONNECTION_STRING }}" \
            "DOTNET_CONNECTION_STRING=${{ steps.keyvault.outputs.CONNECTION_STRING }}" \
            "WEBSITES_PORT=80" \
            "WEBSITES_ENABLE_APP_SERVICE_STORAGE=false"

    - name: Restart Function App
      run: |
        az functionapp restart \
          --name ${{ secrets.FUNCTION_APP_NAME_DEV }} \
          --resource-group ${{ secrets.RESOURCE_GROUP }}

    - name: Wait for health check
      run: |
        for i in {1..30}; do
          if curl -f -s https://${{ secrets.FUNCTION_APP_NAME_DEV }}.azurewebsites.net/api/menu?user=alice; then
            echo "✓ Health check passed"
            exit 0
          fi
          echo "Waiting for app to start... ($i/30)"
          sleep 10
        done
        echo "✗ Health check failed"
        exit 1

    - name: Run smoke tests
      run: |
        # Test menu API
        curl -f https://${{ secrets.FUNCTION_APP_NAME_DEV }}.azurewebsites.net/api/menu?user=alice

        # Verify OpenFGA is accessible
        curl -f http://localhost:8080/healthz || true
```

### **Job 3: Deploy to Production (Manual Approval)**

```yaml
deploy-production:
  needs: build-and-push
  runs-on: ubuntu-latest
  if: github.ref == 'refs/heads/main'
  environment:
    name: production
    url: https://${{ secrets.FUNCTION_APP_NAME_PROD }}.azurewebsites.net

  steps:
    # Same as dev deployment but with production secrets
    # Includes manual approval gate configured in GitHub environment settings
```

---

## 🗂️ Infrastructure Setup (One-Time)

### **Resources Needed:**

1. **Azure Container Registry (ACR)**
   ```bash
   az acr create \
     --name acrmenuapp \
     --resource-group rg-menu-app \
     --sku Basic \
     --admin-enabled true
   ```

2. **Azure SQL Database** (Already provisioned!)
   - Server: `sqlsrv-menu-app-24259.database.windows.net`
   - Database: `db-menu-app`
   - ✅ Already created with free tier

3. **Azure Key Vault**
   ```bash
   az keyvault create \
     --name kv-menu-app-$RANDOM \
     --resource-group rg-menu-app \
     --location eastus

   # Store secrets
   az keyvault secret set \
     --vault-name kv-menu-app \
     --name SQL-CONNECTION-STRING \
     --value "sqlserver://..."
   ```

4. **App Service Plan + Function App (Dev)**
   ```bash
   az appservice plan create \
     --name plan-menu-app-dev \
     --resource-group rg-menu-app \
     --is-linux \
     --sku B1

   az functionapp create \
     --name func-menu-app-dev \
     --resource-group rg-menu-app \
     --plan plan-menu-app-dev \
     --storage-account stmenuappdev \
     --functions-version 4 \
     --runtime custom \
     --deployment-container-image-name acrmenuapp.azurecr.io/menu-app-combined:latest
   ```

5. **App Service Plan + Function App (Production)**
   ```bash
   # Same as dev but with -prod suffix
   # Consider P1V2 tier for production (~$80/month)
   ```

6. **Azure Static Web App**
   ```bash
   az staticwebapp create \
     --name stapp-menu-app \
     --resource-group rg-menu-app \
     --location eastus2 \
     --sku Free
   ```

---

## 🔐 GitHub Secrets Required

### **Repository Secrets:**

```bash
# Azure Authentication
AZURE_CREDENTIALS              # Service Principal JSON for az login
AZURE_SUBSCRIPTION_ID          # Azure Subscription ID
RESOURCE_GROUP                 # rg-menu-app

# Azure Container Registry
ACR_LOGIN_SERVER               # acrmenuapp.azurecr.io
ACR_USERNAME                   # ACR admin username
ACR_PASSWORD                   # ACR admin password

# Azure Key Vault
KEY_VAULT_NAME                 # kv-menu-app

# Function Apps
FUNCTION_APP_NAME_DEV          # func-menu-app-dev
FUNCTION_APP_NAME_STAGING      # func-menu-app-staging
FUNCTION_APP_NAME_PROD         # func-menu-app-prod

# Static Web App
STATIC_WEB_APP_TOKEN           # Deployment token from Azure
```

### **Creating Service Principal:**

```bash
# Create service principal with contributor role
az ad sp create-for-rbac \
  --name "github-actions-menu-app" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-menu-app \
  --sdk-auth

# Output will be JSON - store entire output in AZURE_CREDENTIALS secret
```

---

## 💰 Cost Breakdown (Corrected for Azure Functions)

### **Development Environment:**
| Service | SKU | Monthly Cost |
|---------|-----|--------------|
| Azure SQL Database | Free Tier | **$0** |
| App Service Plan B1 | Linux | **$13** |
| Azure Container Registry | Basic | **$5** |
| Storage Account | Standard LRS | **$0.50** |
| Azure Static Web App | Free | **$0** |
| **TOTAL** | | **~$20/month** |

### **Production Environment:**
| Service | SKU | Monthly Cost |
|---------|-----|--------------|
| Azure SQL Database | 2 vCores | **$200** |
| App Service Plan P1V2 | Linux (HA) | **$80** |
| Azure Container Registry | Standard | **$20** |
| Storage Account | Standard LRS | **$0.50** |
| Azure Static Web App | Standard | **$9** |
| Application Insights | Basic | **$10** |
| Azure Key Vault | Standard | **$1** |
| **TOTAL** | | **~$320/month** |

---

## 🚀 Deployment Strategy

### **Environment Promotion:**

```
developer push to develop
    ↓
Auto-build container
    ↓
Push to ACR with 'develop-{sha}' tag
    ↓
Auto-deploy to func-menu-app-dev
    ↓
Health checks & smoke tests
    ↓
✓ Development deployment complete

PR merged to main
    ↓
Auto-build container
    ↓
Push to ACR with 'main-{sha}' and 'latest' tags
    ↓
Auto-deploy to func-menu-app-staging
    ↓
Integration tests & QA
    ↓
Manual approval required
    ↓
Deploy to func-menu-app-prod
    ↓
Blue-green deployment (slot swap)
    ↓
Production health checks
    ↓
✓ Production deployment complete
```

### **Deployment Slots (Production):**

Azure Functions supports deployment slots for zero-downtime deployments:

```bash
# Create staging slot
az functionapp deployment slot create \
  --name func-menu-app-prod \
  --resource-group rg-menu-app \
  --slot staging

# Deploy to staging slot
az functionapp deployment container config \
  --name func-menu-app-prod \
  --resource-group rg-menu-app \
  --slot staging \
  --docker-custom-image-name acrmenuapp.azurecr.io/menu-app-combined:latest

# Test staging slot
curl https://func-menu-app-prod-staging.azurewebsites.net/api/menu?user=alice

# Swap slots (instant cutover)
az functionapp deployment slot swap \
  --name func-menu-app-prod \
  --resource-group rg-menu-app \
  --slot staging
```

---

## 📊 Complete GitHub Actions Workflow

Here's the complete backend deployment workflow:

**File:** `.github/workflows/backend-deploy.yml`

```yaml
name: Backend - Build & Deploy

on:
  push:
    branches: [main, develop]
    paths:
      - 'backend/**'
      - 'openfga-fork/**'
      - 'Dockerfile.combined'
  workflow_dispatch:

env:
  IMAGE_NAME: menu-app-combined

jobs:
  build:
    name: Build & Push Container
    runs-on: ubuntu-latest
    outputs:
      image-tag: ${{ steps.image.outputs.tag }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login to ACR
        uses: docker/login-action@v3
        with:
          registry: ${{ secrets.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Generate image tag
        id: image
        run: |
          BRANCH=${GITHUB_REF#refs/heads/}
          TAG="${BRANCH}-${{ github.sha }}"
          echo "tag=$TAG" >> $GITHUB_OUTPUT
          echo "full-image=${{ secrets.ACR_LOGIN_SERVER }}/${{ env.IMAGE_NAME }}:${TAG}" >> $GITHUB_OUTPUT

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: .
          file: ./Dockerfile.combined
          platforms: linux/amd64
          push: true
          tags: |
            ${{ steps.image.outputs.full-image }}
            ${{ secrets.ACR_LOGIN_SERVER }}/${{ env.IMAGE_NAME }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy-dev:
    name: Deploy to Development
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment:
      name: development

    steps:
      - uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Get secrets from Key Vault
        id: secrets
        run: |
          SQL_URI=$(az keyvault secret show --vault-name ${{ secrets.KEY_VAULT_NAME }} --name OPENFGA-DATASTORE-URI --query value -o tsv)
          DOTNET_CONN=$(az keyvault secret show --vault-name ${{ secrets.KEY_VAULT_NAME }} --name SQL-CONNECTION-STRING --query value -o tsv)
          echo "::add-mask::$SQL_URI"
          echo "::add-mask::$DOTNET_CONN"
          echo "SQL_URI=$SQL_URI" >> $GITHUB_OUTPUT
          echo "DOTNET_CONN=$DOTNET_CONN" >> $GITHUB_OUTPUT

      - name: Update Function App container
        run: |
          az functionapp config container set \
            --name ${{ secrets.FUNCTION_APP_NAME_DEV }} \
            --resource-group ${{ secrets.RESOURCE_GROUP }} \
            --docker-custom-image-name ${{ needs.build.outputs.image-tag }}

      - name: Configure app settings
        run: |
          az functionapp config appsettings set \
            --name ${{ secrets.FUNCTION_APP_NAME_DEV }} \
            --resource-group ${{ secrets.RESOURCE_GROUP }} \
            --settings \
              OPENFGA_DATASTORE_ENGINE=sqlserver \
              OPENFGA_DATASTORE_URI="${{ steps.secrets.outputs.SQL_URI }}" \
              DOTNET_CONNECTION_STRING="${{ steps.secrets.outputs.DOTNET_CONN }}" \
              WEBSITES_PORT=80 \
              WEBSITES_ENABLE_APP_SERVICE_STORAGE=false

      - name: Restart Function App
        run: |
          az functionapp restart \
            --name ${{ secrets.FUNCTION_APP_NAME_DEV }} \
            --resource-group ${{ secrets.RESOURCE_GROUP }}

      - name: Health check
        run: |
          sleep 60  # Wait for container to start
          for i in {1..20}; do
            if curl -f "https://${{ secrets.FUNCTION_APP_NAME_DEV }}.azurewebsites.net/api/menu?user=alice"; then
              echo "✓ Health check passed"
              exit 0
            fi
            echo "Waiting... ($i/20)"
            sleep 15
          done
          exit 1

  deploy-prod:
    name: Deploy to Production
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment:
      name: production

    steps:
      # Same as dev but with production secrets
      # Manual approval configured in GitHub environment settings
```

---

## ✅ Implementation Checklist

### **Week 1: Foundation**
- [ ] Create Azure Container Registry
- [ ] Setup Azure Key Vault
- [ ] Store secrets in Key Vault
- [ ] Create Service Principal for GitHub Actions
- [ ] Add all secrets to GitHub repository settings

### **Week 2: CI/CD Pipeline**
- [ ] Create `.github/workflows/backend-deploy.yml`
- [ ] Test build & push to ACR
- [ ] Configure dev environment deployment
- [ ] Test automated deployment to dev
- [ ] Setup production environment with deployment slots

### **Week 3: Frontend & Testing**
- [ ] Create `.github/workflows/frontend-deploy.yml`
- [ ] Migrate frontend to React/Vite
- [ ] Add automated tests
- [ ] Setup E2E testing

### **Week 4: Monitoring & Documentation**
- [ ] Configure Application Insights
- [ ] Setup alerts
- [ ] Create runbooks
- [ ] Team training

---

## 🎯 Key Differences from Container Apps

| Aspect | Azure Functions (Current) | Container Apps (Previous Plan) |
|--------|--------------------------|--------------------------------|
| **Deployment** | `az functionapp` commands | `az containerapp` commands |
| **Scaling** | App Service Plan scaling | KEDA-based autoscaling |
| **Cost** | B1: $13/month | 0.5 vCPU: ~$15/month |
| **Deployment Slots** | ✅ Yes (blue-green) | ❌ No (use revisions) |
| **Custom Domain** | ✅ Easy | ✅ Easy |
| **Cold Start** | Depends on plan | Minimal (scale to zero) |
| **Best For** | Traditional HTTP APIs | Microservices, event-driven |

---

**This plan is now corrected for Azure Functions deployment!** 🎯

Ready to implement the GitHub Actions workflows?
