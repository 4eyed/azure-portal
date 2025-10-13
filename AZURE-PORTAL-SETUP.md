# Azure Portal Configuration Guide

This document provides step-by-step instructions for configuring Azure resources after deploying the code changes.

## Prerequisites

- Azure CLI installed and authenticated: `az login`
- Access to Azure Portal with appropriate permissions
- GitHub deployment completed successfully

---

## Phase 1: Configure Static Web App Authentication

### Step 1.1: Create Client Secret for App Registration

```bash
# Navigate to Azure AD App Registration
az ad app credential reset \
  --id baa611a0-39d1-427b-89b5-d91658c6ce26 \
  --append \
  --display-name "Static Web App EasyAuth"

# Save the output - you'll need the "password" field
```

**Alternative (Azure Portal):**
1. Go to Azure Portal → Azure Active Directory → App Registrations
2. Select your app (ID: `baa611a0-39d1-427b-89b5-d91658c6ce26`)
3. Click "Certificates & secrets" → "New client secret"
4. Description: `Static Web App EasyAuth`
5. Expires: 24 months (or your preference)
6. Click "Add"
7. **IMPORTANT:** Copy the "Value" immediately (it won't show again)

### Step 1.2: Configure Static Web App Settings

```bash
# Set the environment variables in Static Web App
az staticwebapp appsettings set \
  --name witty-flower-068de881e \
  --setting-names \
    AZURE_CLIENT_ID="baa611a0-39d1-427b-89b5-d91658c6ce26" \
    AZURE_CLIENT_SECRET="<paste-client-secret-here>"
```

**Alternative (Azure Portal):**
1. Go to Azure Portal → Static Web Apps
2. Select `witty-flower-068de881e`
3. Click "Configuration" (under Settings)
4. Click "Application settings" tab
5. Add two settings:
   - Name: `AZURE_CLIENT_ID`, Value: `baa611a0-39d1-427b-89b5-d91658c6ce26`
   - Name: `AZURE_CLIENT_SECRET`, Value: `<paste-secret-here>`
6. Click "Save"

### Step 1.3: Verify Static Web App is Linked to Function App

```bash
# Check if Function App is linked
az staticwebapp backends show \
  --name witty-flower-068de881e \
  --resource-group <resource-group-name>
```

**Alternative (Azure Portal):**
1. Go to Azure Portal → Static Web Apps → `witty-flower-068de881e`
2. Click "APIs" (under Settings)
3. Under "Production" environment, check if Function App is linked
4. If not linked:
   - Click "Link"
   - Select "Function App"
   - Choose `func-menu-app-18436`
   - Click "Link"

---

## Phase 2: Enable Function App Managed Identity

### Step 2.1: Enable System-Assigned Managed Identity

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name>

# Save the output - you'll need the "principalId" (Object ID)
```

**Alternative (Azure Portal):**
1. Go to Azure Portal → Function Apps
2. Select `func-menu-app-18436`
3. Click "Identity" (under Settings)
4. Click "System assigned" tab
5. Set "Status" to "On"
6. Click "Save"
7. **Copy the "Object (principal) ID"** - you'll need it for SQL permissions

### Step 2.2: Grant SQL Database Permissions to Managed Identity

**Option A: Using Azure Data Studio or SSMS**

1. Connect to SQL Server: `sqlsrv-menu-app-24259.database.windows.net`
2. Database: `db-menu-app`
3. Authentication: Azure Active Directory (as SQL Admin)
4. Run this SQL script:

```sql
-- Create user for Function App managed identity
CREATE USER [func-menu-app-18436] FROM EXTERNAL PROVIDER;

-- Grant read/write permissions
ALTER ROLE db_datareader ADD MEMBER [func-menu-app-18436];
ALTER ROLE db_datawriter ADD MEMBER [func-menu-app-18436];

-- Verify permissions
SELECT
    dp.name AS UserName,
    dp.type_desc AS UserType,
    r.name AS RoleName
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
LEFT JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
WHERE dp.name = 'func-menu-app-18436';
```

**Option B: Using Azure CLI**

```bash
# Get SQL Admin credentials (if needed)
SQL_SERVER="sqlsrv-menu-app-24259"
DATABASE="db-menu-app"
ADMIN_USER="<sql-admin-username>"

# Connect and create user
az sql db show-connection-string \
  --client ado.net \
  --server $SQL_SERVER \
  --name $DATABASE

# Use the connection string with a SQL client to run the CREATE USER script above
```

---

## Phase 3: Verify Configuration

### Step 3.1: Test Static Web App Authentication

```bash
# Visit the app URL
open https://witty-flower-068de881e.2.azurestaticapps.net

# Expected behavior:
# 1. Redirects to Azure AD login (if not already logged in)
# 2. After login, redirects back to app
# 3. App loads successfully
```

**Check cookies in browser DevTools:**
- Open DevTools → Application → Cookies
- Should see: `AppServiceAuthSession` cookie

### Step 3.2: Test Function App Managed Identity

```bash
# Call the test endpoint
curl -X GET "https://witty-flower-068de881e.2.azurestaticapps.net/api/test-sql" \
  --cookie "AppServiceAuthSession=<your-cookie>"

# Expected response:
# {
#   "success": true,
#   "database": "db-menu-app",
#   "connectionInfo": {
#     "authMethod": "ActiveDirectoryDefault",
#     ...
#   }
# }
```

**Alternative (Browser):**
1. Login to the app
2. Open: `https://witty-flower-068de881e.2.azurestaticapps.net/api/test-sql`
3. Should show JSON response with `"success": true`

### Step 3.3: Test User Authentication Flow

```bash
# Check if user ID is extracted correctly
curl -X GET "https://witty-flower-068de881e.2.azurestaticapps.net/api/auth/check-admin" \
  --cookie "AppServiceAuthSession=<your-cookie>"

# Expected response:
# {
#   "isAdmin": true/false,
#   "userId": "<your-azure-ad-oid>"
# }
```

**Check backend logs:**
```bash
az functionapp logs tail \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name>

# Look for:
# ✅ "Extracted userId: <oid>" (NOT NULL!)
# ✅ "Using Managed Identity authentication (no interceptor)"
# ❌ Should NOT see: "Cannot set AccessToken if 'Authentication' is specified"
```

---

## Phase 4: Troubleshooting

### Issue: Static Web App doesn't redirect to login

**Symptoms:**
- Visiting app shows blank page or error
- No redirect to Azure AD login

**Solutions:**
1. Check if `staticwebapp.config.json` deployed correctly:
   ```bash
   # View deployed config
   az staticwebapp show \
     --name witty-flower-068de881e \
     --query "buildProperties"
   ```

2. Verify client secret is set in SWA settings
3. Check Azure AD app registration allows SWA redirect URI
4. Wait 5-10 minutes for configuration to propagate

### Issue: "Cannot set AccessToken if 'Authentication' is specified"

**Symptoms:**
- Backend logs show SQL connection error
- Function App can't query database

**Solutions:**
1. Verify `ServiceCollectionExtensions.cs` has conditional interceptor logic
2. Check `WEBSITE_SITE_NAME` environment variable is set (indicates Azure environment)
3. Confirm `DOTNET_CONNECTION_STRING` includes `Authentication=ActiveDirectoryDefault`
4. Verify managed identity is enabled on Function App

### Issue: "Extracted userId: NULL" in logs

**Symptoms:**
- `CheckAdmin` returns 401 Unauthorized
- Backend can't identify user

**Solutions:**
1. Verify SWA authentication is configured (check `auth` section in config)
2. Check `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` are set in SWA
3. Verify Function App is linked to SWA (APIs → Production → Linked)
4. Test `/.auth/me` endpoint to see if SWA has user info:
   ```bash
   curl "https://witty-flower-068de881e.2.azurestaticapps.net/.auth/me" \
     --cookie "AppServiceAuthSession=<your-cookie>"
   ```

### Issue: Managed Identity authentication fails

**Symptoms:**
- "No User Assigned or Delegated Managed Identity found"
- SQL connection fails in Azure

**Solutions:**
1. Verify system-assigned managed identity is enabled:
   ```bash
   az functionapp identity show \
     --name func-menu-app-18436 \
     --resource-group <resource-group-name>
   ```

2. Check SQL user was created for managed identity:
   ```sql
   SELECT name FROM sys.database_principals
   WHERE name = 'func-menu-app-18436';
   ```

3. Verify connection string has `Authentication=ActiveDirectoryDefault`

4. Check Azure SQL firewall allows Azure services:
   ```bash
   az sql server firewall-rule list \
     --server sqlsrv-menu-app-24259 \
     --resource-group <resource-group-name>
   ```

---

## Phase 5: Post-Deployment Verification Checklist

- [ ] Static Web App redirects to Azure AD login
- [ ] After login, app loads successfully
- [ ] Browser has `AppServiceAuthSession` cookie
- [ ] `/.auth/me` endpoint returns user info
- [ ] `/api/auth/check-admin` returns user ID (not NULL)
- [ ] `/api/test-sql` connects successfully
- [ ] Backend logs show "Using Managed Identity authentication"
- [ ] Backend logs show "Extracted userId: <oid>" (not NULL)
- [ ] No "Cannot set AccessToken" errors in logs
- [ ] Admin toggle appears for admin users
- [ ] Menu structure loads correctly
- [ ] Power BI reports embed successfully

---

## Helpful Commands

### Get Resource Information

```bash
# Get Static Web App details
az staticwebapp show \
  --name witty-flower-068de881e \
  --query "{name:name, defaultHostname:defaultHostname, linkedBackend:linkedBackend.backendResourceId}"

# Get Function App details
az functionapp show \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name> \
  --query "{name:name, defaultHostName:defaultHostName, identity:identity.principalId}"

# Get SQL Server details
az sql server show \
  --name sqlsrv-menu-app-24259 \
  --resource-group <resource-group-name> \
  --query "{name:name, fullyQualifiedDomainName:fullyQualifiedDomainName, administratorLogin:administratorLogin}"
```

### View Logs

```bash
# Function App logs (real-time)
az functionapp logs tail \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name>

# Application Insights logs (if configured)
az monitor app-insights query \
  --app <app-insights-name> \
  --analytics-query "traces | where timestamp > ago(1h) | order by timestamp desc | take 50"
```

### Restart Services

```bash
# Restart Function App
az functionapp restart \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name>

# Restart Static Web App (redeploy triggers restart)
az staticwebapp deploy \
  --name witty-flower-068de881e \
  --source .
```

---

## Security Notes

- **Client Secret:** Treat like a password - never commit to git
- **Managed Identity:** No secrets stored anywhere - most secure option
- **Connection String:** Should NOT contain passwords in production
- **SQL Permissions:** Grant minimum required permissions (reader/writer)
- **SWA Authentication:** Handles token refresh automatically
- **Cookies:** `AppServiceAuthSession` is HTTP-only and secure

---

## Next Steps

After completing this configuration:

1. **Assign Admin Users:** Add users to `role:admin` in OpenFGA (see `SETUP-FIRST-ADMIN.md`)
2. **Create Menu Items:** Use Admin Mode to create menu structure
3. **Configure Power BI:** Add Power BI reports to menu items
4. **Monitor Application:** Set up Application Insights alerts
5. **Review Security:** Audit access permissions and policies

---

## Support

If you encounter issues:

1. Check the troubleshooting section above
2. Review backend logs: `az functionapp logs tail`
3. Check browser DevTools console for frontend errors
4. Verify all configuration steps completed
5. Wait 5-10 minutes for Azure configuration changes to propagate

For application-specific issues, see:
- `RUNNING-THE-APP.md` - Local development setup
- `SETUP-FIRST-ADMIN.md` - Admin user configuration
- `CLAUDE.md` - Architecture overview
