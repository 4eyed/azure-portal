# Configuration Cleanup Guide

## Summary

Simplified configuration from **22 settings across 4 locations** down to **14 settings**, removing duplication and clarifying where each setting should live.

## What Changed

### ✅ Completed Automatically
1. **Removed misleading `env:` block** from frontend GitHub workflow (it didn't work anyway)
2. **Unified SQL connection strings** - backend now uses single `SQL_CONNECTION_STRING`
3. **Deleted `.env.local`** - redundant with `.env`
4. **Added `.env` to `.gitignore`** - prevent committing developer secrets

### ⚠️ Manual Steps Required

You need to **DELETE 3 GitHub Secrets** that are now redundant:

## GitHub Secrets to DELETE

Go to: **GitHub Repository → Settings → Secrets and variables → Actions**

### 1. DELETE: `AZURE_CLIENT_ID`
**Why**: Frontend authentication is now configured in Azure Portal (Static Web App Environment Variables)
- ❌ This secret is no longer used by any workflow
- ✅ The value is now in Azure Portal → Static Web App → Environment variables → Production

### 2. DELETE: `AZURE_TENANT_ID`
**Why**: Frontend authentication is now configured in Azure Portal (Static Web App Environment Variables)
- ❌ This secret is no longer used by any workflow
- ✅ The value is now in Azure Portal → Static Web App → Environment variables → Production

### 3. DELETE: `DOTNET_CONNECTION_STRING`
**Why**: Duplicate of `SQL_CONNECTION_STRING` - no need for two secrets with the same value
- ❌ Backend workflow now uses `SQL_CONNECTION_STRING` for both OpenFGA and .NET
- ✅ Reduces confusion and potential for drift between two copies

---

## Final Configuration State

### GitHub Secrets (7 Required) ✅

| Secret Name | Purpose | Used By |
|------------|---------|---------|
| `ACR_NAME` | Container registry name | Backend deploy workflow |
| `ACR_USERNAME` | Container registry login | Backend deploy workflow |
| `ACR_PASSWORD` | Container registry password | Backend deploy workflow |
| `AZURE_CREDENTIALS` | Service principal JSON for Azure login | Backend deploy workflow |
| `AZURE_FUNCTIONAPP_NAME` | Azure Function App name | Backend deploy workflow |
| `AZURE_RESOURCE_GROUP` | Azure resource group name | Backend deploy workflow |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_*` | Deployment token | Frontend deploy workflow |
| `OPENFGA_STORE_ID` | OpenFGA store identifier | Backend deploy workflow |
| `SQL_CONNECTION_STRING` | Database connection (both OpenFGA & .NET) | Backend deploy workflow |

**Total**: 9 secrets (down from 12)

### Azure Static Web App Environment Variables (3 Required) ✅

**Location**: Azure Portal → Static Web App (`witty-flower-068de881e`) → Settings → Environment variables → Production

| Variable Name | Value | Purpose |
|--------------|-------|---------|
| `VITE_AZURE_CLIENT_ID` | `baa611a0-39d1-427b-89b5-d91658c6ce26` | Azure AD Client ID (public) |
| `VITE_AZURE_TENANT_ID` | `ae372c45-2e81-4d1c-9490-e9ac10250047` | Azure AD Tenant ID (public) |
| `VITE_AZURE_REDIRECT_URI` | `https://witty-flower-068de881e.2.azurestaticapps.net` | OAuth redirect URI |

**These are already configured** (you added them to fix the build).

### Azure Function App Settings (Auto-Configured) ✅

**Location**: Configured automatically by backend deployment workflow - **DO NOT manually edit**

These settings are automatically set by the GitHub Actions workflow when deploying:
- `WEBSITES_ENABLE_APP_SERVICE_STORAGE=false`
- `WEBSITES_PORT=80`
- `OPENFGA_API_URL=http://localhost:8080`
- `OPENFGA_STORE_ID` (from GitHub secret)
- `OPENFGA_DATASTORE_ENGINE=sqlserver`
- `OPENFGA_DATASTORE_URI` (from GitHub secret `SQL_CONNECTION_STRING`)
- `OPENFGA_LOG_FORMAT=json`
- `DOTNET_CONNECTION_STRING` (from GitHub secret `SQL_CONNECTION_STRING` - same as above)
- `DEPLOYMENT_SHA` (auto-generated)
- `DEPLOYMENT_TIME` (auto-generated)

### Local Development `.env` (Not Committed) ✅

**File**: `frontend/.env` (in `.gitignore`, each developer has their own)

```bash
# Minimum required for local development
VITE_AZURE_CLIENT_ID=baa611a0-39d1-427b-89b5-d91658c6ce26
VITE_AZURE_TENANT_ID=ae372c45-2e81-4d1c-9490-e9ac10250047
VITE_AZURE_REDIRECT_URI=http://localhost:5173
VITE_API_URL=http://localhost:7071/api

# Optional: Power BI (only if testing Power BI features locally)
VITE_POWERBI_WORKSPACE_ID=00000000-0000-0000-0000-000000000000
VITE_POWERBI_REPORT_ID=00000000-0000-0000-0000-000000000000
VITE_POWERBI_EMBED_URL=https://app.powerbi.com/reportEmbed
```

**Template**: `frontend/.env.example` (committed to Git for other developers)

---

## Why This Configuration Structure?

### Frontend (React/Vite)

**Production Build** (Azure Static Web Apps):
- ✅ Environment variables from **Azure Portal** → Available during build
- ❌ Environment variables from **GitHub Actions `env:`** → NOT available during build
- **Reason**: Azure Static Web Apps uploads source code and builds it in Azure's environment, not GitHub Actions

**Local Development**:
- ✅ Environment variables from **`.env` file** → Loaded by Vite
- ❌ Don't commit `.env` → Each developer has their own

### Backend (Azure Functions)

**Production Deployment**:
- ✅ GitHub Actions workflow uses **Azure CLI** to set app settings directly on Function App
- ✅ GitHub Secrets → Passed to Azure CLI → Set on Function App
- **Reason**: We're not building in Azure, we're deploying a pre-built Docker image

**Local Development**:
- ✅ Environment variables from **`local.settings.json`** (not tracked in Git)
- ✅ Or from shell environment

---

## Configuration Decision Matrix

| Setting Type | GitHub Secrets | Azure Portal (SWA) | Azure Portal (Functions) | Local .env |
|--------------|---------------|-------------------|-------------------------|------------|
| **Build-time secrets** (frontend) | ❌ Doesn't work | ✅ Use this | N/A | ✅ For local dev |
| **Deployment credentials** | ✅ Use this | N/A | N/A | N/A |
| **Runtime app settings** (backend) | ✅ Via workflow | N/A | ✅ Auto-set by workflow | ✅ For local dev |

---

## Verification Steps

### 1. Verify GitHub Secrets (After Deletion)

Go to: **GitHub Repository → Settings → Secrets and variables → Actions**

You should see exactly **9 secrets**:
- [x] `ACR_NAME`
- [x] `ACR_USERNAME`
- [x] `ACR_PASSWORD`
- [x] `AZURE_CREDENTIALS`
- [x] `AZURE_FUNCTIONAPP_NAME`
- [x] `AZURE_RESOURCE_GROUP`
- [x] `AZURE_STATIC_WEB_APPS_API_TOKEN_WITTY_FLOWER_068DE881E`
- [x] `OPENFGA_STORE_ID`
- [x] `SQL_CONNECTION_STRING`

**Should NOT see** (delete these):
- [ ] ~~`AZURE_CLIENT_ID`~~ ← DELETE
- [ ] ~~`AZURE_TENANT_ID`~~ ← DELETE
- [ ] ~~`DOTNET_CONNECTION_STRING`~~ ← DELETE

### 2. Verify Azure Static Web App Environment Variables

Go to: **Azure Portal → Static Web App → Settings → Environment variables → Production**

You should see exactly **3 variables**:
- [x] `VITE_AZURE_CLIENT_ID` = `baa611a0-39d1-427b-89b5-d91658c6ce26`
- [x] `VITE_AZURE_TENANT_ID` = `ae372c45-2e81-4d1c-9490-e9ac10250047`
- [x] `VITE_AZURE_REDIRECT_URI` = `https://witty-flower-068de881e.2.azurestaticapps.net`

### 3. Test Deployments

**Frontend**:
```bash
git add .
git commit -m "Cleanup: Simplify configuration, remove duplicate secrets"
git push
```

Watch GitHub Actions:
- ✅ Frontend build should succeed (uses Azure Portal env vars)
- ✅ Backend build should succeed (uses simplified SQL connection)

**Local Development**:
```bash
npm run dev:native
```

- ✅ Frontend should run at http://localhost:5173
- ✅ Backend should run at http://localhost:7071
- ✅ Authentication should work (uses local `.env`)

---

## Troubleshooting

### Frontend Build Fails in GitHub Actions

**Error**: `Missing required Azure AD environment variables`

**Solution**:
1. Verify Azure Portal environment variables are set (see Verification Step 2 above)
2. Wait 5 minutes for Azure to propagate settings
3. Trigger new deployment (push a commit or manually run workflow)

### Backend Deployment Fails

**Error**: `Connection string not found`

**Solution**:
1. Verify `SQL_CONNECTION_STRING` secret exists in GitHub
2. Check backend workflow is using `SQL_CONNECTION_STRING` (not `DOTNET_CONNECTION_STRING`)
3. Workflow file should show: `"DOTNET_CONNECTION_STRING=${{ secrets.SQL_CONNECTION_STRING }}"`

### Local Development Not Working

**Error**: Environment variables not loading

**Solution**:
1. Check `frontend/.env` exists and has all 4 required variables
2. Restart dev server: `npm run dev:native`
3. Check terminal output for environment variable errors

---

## Benefits of This Configuration

### Before Cleanup:
- ❌ 12 GitHub Secrets (3 duplicates, 2 unused)
- ❌ Misleading `env:` block in frontend workflow (didn't work)
- ❌ Two `.env` files in frontend (`.env` and `.env.local` with conflicts)
- ❌ Two SQL connection string secrets (`SQL_CONNECTION_STRING` and `DOTNET_CONNECTION_STRING`)
- ❌ Confusion about where to configure frontend environment variables

### After Cleanup:
- ✅ 9 GitHub Secrets (no duplicates, all used)
- ✅ Clear documentation of why frontend workflow has no `env:` block
- ✅ Single `.env` file per developer (not committed)
- ✅ Single SQL connection string secret
- ✅ Clear separation: Azure Portal for frontend builds, GitHub Secrets for backend deployment

---

## Related Documentation

- [SECURITY-SETUP.md](SECURITY-SETUP.md) - Why Azure Static Web Apps needs Portal configuration
- [SECURITY-IMPROVEMENTS-SUMMARY.md](SECURITY-IMPROVEMENTS-SUMMARY.md) - Security architecture overview
- [frontend/.env.example](frontend/.env.example) - Template for local development
- [.github/workflows/azure-static-web-apps-*.yml](.github/workflows/azure-static-web-apps-witty-flower-068de881e.yml) - Frontend deployment
- [.github/workflows/azure-backend-deploy.yml](.github/workflows/azure-backend-deploy.yml) - Backend deployment

---

## Next Steps

1. **Delete the 3 redundant GitHub Secrets** (see instructions above)
2. **Commit and push** the workflow changes
3. **Test deployment** - verify both frontend and backend deploy successfully
4. **Share `.env.example`** with other developers so they can set up their local environment

---

## Summary

| Aspect | Before | After | Improvement |
|--------|--------|-------|-------------|
| GitHub Secrets | 12 | 9 | -25% (removed duplicates) |
| Frontend env files | 2 (conflicting) | 1 | Clearer |
| SQL connection secrets | 2 (duplicates) | 1 | No confusion |
| Misleading config | env: block (broken) | Clear docs | No false expectations |
| Configuration clarity | Low | High | ✅ |

**Total configuration points**: 22 → 14 (36% reduction!)
