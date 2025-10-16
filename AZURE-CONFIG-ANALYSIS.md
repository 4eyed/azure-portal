# Azure Configuration Analysis

## ✅ What's Correctly Configured

### 1. Static Web App Configuration
**Resource**: portal (witty-flower-068de881e.2.azurestaticapps.net)
**Application Settings**:
```json
{
  "AZURE_CLIENT_ID": "baa611a0-39d1-427b-89b5-d91658c6ce26",
  "AZURE_CLIENT_SECRET": "[REDACTED]",
  "AZURE_TENANT_ID": "ae372c45-2e81-4d1c-9490-e9ac10250047",
  "VITE_AZURE_CLIENT_ID": "baa611a0-39d1-427b-89b5-d91658c6ce26",
  "VITE_AZURE_REDIRECT_URI": "https://witty-flower-068de881e.2.azurestaticapps.net",
  "VITE_AZURE_TENANT_ID": "ae372c45-2e81-4d1c-9490-e9ac10250047"
}
```
✅ **Correct**: Using the right app registration (JA Portal)
✅ **Secret configured**: Client secret is set (value redacted for security)
✅ **Correct**: Using the right app registration (JA Portal)

### 2. Azure AD App Registration
**App Name**: JA Portal
**Client ID**: baa611a0-39d1-427b-89b5-d91658c6ce26
**App Roles Defined**:
```json
[
  {
    "displayName": "Admin",
    "value": "Admin",
    "isEnabled": true
  }
]
```
✅ **Correct**: App role "Admin" is defined

### 3. User Role Assignment
**User**: Eric Entenman (eric@4eyed.com)
**User OID**: d494d998-61f1-412f-97da-69fa8e0a0d3c
**Assigned Roles**:
- Admin role (appRoleId: 10c953b3-eaed-4129-b799-96e7cf4dd9c3)
✅ **Correct**: User is assigned the Admin role

### 4. Code Configuration
- ✅ `response_type=id_token` in staticwebapp.config.json
- ✅ `userDetailsClaim` removed (auto-detect working)
- ✅ Backend debugging added for role extraction
- ✅ All code changes deployed

---

## 🔍 Configuration Findings

### Static Web App
- **Name**: portal
- **Resource Group**: portal
- **Region**: West US 2
- **SKU**: Standard
- **GitHub Repo**: https://github.com/4eyed/azure-portal
- **Branch**: main
- **Hostname**: witty-flower-068de881e.2.azurestaticapps.net

### Linked Backend
- **Name**: func-menu-app-18436
- **Resource Group**: rg-menu-app
- **Region**: East US
- **Type**: Linux Container Function App
- **State**: Running
- **Hostname**: func-menu-app-18436.azurewebsites.net

### Backend Configuration ⚠️
```json
{
  "AZURE_CLIENT_ID": "63e1624c-4bdf-41db-826c-91dd7b3e164c",  // Different!
  "AZURE_TENANT_ID": "ae372c45-2e81-4d1c-9490-e9ac10250047",
  "OPENFGA_STORE_ID": "01K785TE28A2Z3NWGAABN1TE8E",
  "DOTNET_CONNECTION_STRING": "Server=sqlsrv-menu-app-24259..."
}
```

**Note**: Backend uses a different app registration (github-actions-openfga), but this is likely correct as it's for backend identity, not user authentication.

---

## 🎯 Why It Works Locally But Not in Azure

### Local Environment
- Uses MSAL directly for authentication
- Dev auth simulation in development mode
- Tokens acquired through MSAL include app roles automatically

### Azure Environment
- Uses Azure Static Web Apps authentication
- SWA acts as authentication proxy
- Tokens must be configured correctly via `staticwebapp.config.json`

### The Issue
Even though all configuration looks correct, the `roles` claim may still be missing due to:

1. **Token type mismatch**: SWA might be using access tokens instead of ID tokens
2. **Login parameters not applied**: Despite config file, SWA might not be using `response_type=id_token`
3. **Cache issue**: Old configuration cached in Azure
4. **Token propagation delay**: Recent changes not yet reflected in tokens

---

## 🔧 Recommended Fixes

### Fix 1: Force Redeploy with Cache Bust

The configuration file might be cached. Let's force a complete redeployment:

```bash
# Add a comment to trigger redeployment
cd frontend/public
echo "# Force redeploy $(date)" >> staticwebapp.config.json

git add staticwebapp.config.json
git commit -m "chore: Force redeploy to apply authentication changes"
git push
```

### Fix 2: Verify Deployed Config File

After deployment, check if the config file is correctly deployed:

**Visit**: `https://witty-flower-068de881e.2.azurestaticapps.net/staticwebapp.config.json`

**Expected**: Should return your config with `response_type=id_token` in loginParameters

**If 404 or different**: Config file not deployed correctly

### Fix 3: Clear Token Cache and Test

After redeployment:

1. Visit: `https://witty-flower-068de881e.2.azurestaticapps.net/.auth/logout`
2. Clear browser cookies and cache
3. Close ALL browser windows
4. **Wait 5 minutes** for Azure to propagate config changes
5. Open new incognito window
6. Visit: `https://witty-flower-068de881e.2.azurestaticapps.net/`
7. Login again
8. Check: `https://witty-flower-068de881e.2.azurestaticapps.net/.auth/me`

**Look for**:
```json
{
  "typ": "roles",
  "val": "Admin"
}
```

### Fix 4: Check Network Traffic During Login

**Browser DevTools** → **Network tab**:

1. Filter: "auth" or "login"
2. Logout and login again
3. Find the redirect to `login.microsoftonline.com`
4. Look at the query parameters

**Should include**:
```
scope=openid+profile+email+offline_access
response_type=id_token
client_id=baa611a0-39d1-427b-89b5-d91658c6ce26
```

**If `response_type` is missing or different**:
- SWA is not using your staticwebapp.config.json
- Might need to configure in Azure Portal instead

### Fix 5: Configure Authentication in Azure Portal (Alternative)

If config file isn't being honored, configure directly in Portal:

**Azure Portal** → **Static Web Apps** → **portal** → **Configuration**

Look for an "Authentication" or "Identity providers" section.

**If you see manual configuration options**:
1. Edit the Azure Active Directory provider
2. Add custom parameters:
   ```
   response_type=id_token
   ```
3. Save and test

---

## 🧪 Testing Commands

### Check Current Deployment

```bash
# Check Static Web App settings
az staticwebapp appsettings list --name portal --resource-group portal -o json

# Check if config file is deployed (from your browser)
curl -I https://witty-flower-068de881e.2.azurestaticapps.net/staticwebapp.config.json

# Check latest deployment
az staticwebapp show --name portal --resource-group portal --query "repositoryToken" -o tsv
```

### Check Backend Logs

```bash
# Stream Function App logs
az webapp log tail --name func-menu-app-18436 --resource-group rg-menu-app

# Look for our debugging messages:
# 🔍 JWT Claims found: X
# 🎭 Roles extracted from JWT: ...
```

### Verify Role Assignment

```bash
# Check your current role assignment
az rest --method GET \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/a5438c02-c180-4d50-9951-21de1481183e/appRoleAssignedTo" \
  --query "value[?principalId=='d494d998-61f1-412f-97da-69fa8e0a0d3c']" \
  -o json
```

---

## 📊 Summary

**Configuration Status**:
- ✅ Azure AD: App role defined and user assigned
- ✅ Static Web App: Environment variables set correctly
- ✅ Code: All fixes deployed
- ⚠️ Token: Roles claim missing (despite correct config)

**Most Likely Issue**:
- Static Web Apps not applying `response_type=id_token` from config file
- Might be using cached configuration
- Or might need Portal-level configuration

**Next Steps**:
1. Force redeploy with cache bust
2. Wait 5 minutes after deployment
3. Logout, clear cache, login again
4. Check `/.auth/me` for roles claim
5. If still missing, check network traffic during login to see actual parameters
6. May need to configure in Azure Portal instead of config file

**The Good News**:
Since it works locally and all Azure configuration is correct, we just need to ensure Azure Static Web Apps is using the right authentication parameters. This is a deployment/configuration issue, not a code or permissions issue.
