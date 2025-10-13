# Deployment Notes - Authentication & SQL Connection Fixes

## Summary of Changes

This deployment fixes two critical issues:

1. **401 Unauthorized Error:** Users couldn't authenticate - backend received NULL user IDs
2. **SQL Connection Error:** "Cannot set AccessToken if 'Authentication' is specified" in Azure

## Root Causes Identified

### Issue 1: Missing Static Web App Authentication

**Problem:**
- Frontend used MSAL for authentication (browser-side)
- Backend expected `X-MS-CLIENT-PRINCIPAL` header from Azure Static Web Apps
- SWA authentication was **not configured** - no header was being injected
- Result: Backend couldn't identify users → 401 Unauthorized

**Solution:**
- Restored SWA authentication configuration in `staticwebapp.config.json`
- Added Azure AD identity provider settings
- SWA now handles authentication and injects `X-MS-CLIENT-PRINCIPAL` header automatically

### Issue 2: SqlTokenInterceptor Conflict in Production

**Problem:**
- Connection string had `Authentication=ActiveDirectoryDefault` (for Managed Identity)
- `SqlTokenInterceptor` was trying to set `AccessToken` property on same connection
- SQL Server doesn't allow both → connection error

**Solution:**
- Made `SqlTokenInterceptor` conditional: **only active in local development**
- In Azure: Uses Managed Identity via connection string (no interceptor)
- In Local Dev: Uses interceptor with user SQL tokens (for audit logs)

---

## Architecture Changes

### Before (Broken)

```
Frontend (MSAL) → API Request → Function App
                   ❌ No X-MS-CLIENT-PRINCIPAL header
                   ❌ Backend gets userId = NULL
                   ❌ Returns 401 Unauthorized

Function App → SQL Connection
             ❌ Both Authentication= and AccessToken set
             ❌ SQL Server rejects connection
```

### After (Fixed)

```
Frontend → SWA EasyAuth → API Request → Function App
           ✅ Adds X-MS-CLIENT-PRINCIPAL header
           ✅ Backend extracts user OID
           ✅ Returns user info

Function App → SQL Connection
           ✅ Local Dev: Uses SqlTokenInterceptor (user tokens)
           ✅ Azure: Uses Managed Identity (app identity)
           ✅ Connections succeed
```

---

## Files Modified

### Frontend (1 file)
- `frontend/public/staticwebapp.config.json` - Added SWA authentication configuration

### Backend (1 file)
- `backend/MenuApi/Configuration/ServiceCollectionExtensions.cs` - Conditional SqlTokenInterceptor

### Documentation (2 new files)
- `AZURE-PORTAL-SETUP.md` - Step-by-step Azure configuration guide
- `DEPLOYMENT-NOTES.md` - This file (deployment summary)

---

## Required Azure Configuration

**CRITICAL:** Code changes alone are not sufficient. You must configure Azure resources:

### 1. Static Web App Settings (Required)

```bash
az staticwebapp appsettings set \
  --name witty-flower-068de881e \
  --setting-names \
    AZURE_CLIENT_ID="baa611a0-39d1-427b-89b5-d91658c6ce26" \
    AZURE_CLIENT_SECRET="<create-and-paste-secret>"
```

**Why:** Enables SWA to authenticate users with Azure AD

### 2. Function App Managed Identity (Required)

```bash
az functionapp identity assign \
  --name func-menu-app-18436 \
  --resource-group <resource-group-name>
```

**Why:** Allows Function App to connect to Azure SQL without passwords

### 3. SQL Database Permissions (Required)

```sql
CREATE USER [func-menu-app-18436] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [func-menu-app-18436];
ALTER ROLE db_datawriter ADD MEMBER [func-menu-app-18436];
```

**Why:** Grants Function App managed identity access to database

---

## Deployment Steps

### Step 1: Deploy Code Changes

```bash
# Code is automatically deployed via GitHub Actions
# Watch deployment progress in GitHub Actions tab
```

### Step 2: Configure Azure Resources

Follow the detailed guide in `AZURE-PORTAL-SETUP.md`:

1. Create client secret for App Registration
2. Configure Static Web App settings (client ID + secret)
3. Enable Function App system-assigned managed identity
4. Grant SQL permissions to managed identity
5. Verify SWA is linked to Function App

**⏰ Time Required:** ~15-20 minutes

### Step 3: Verify Deployment

```bash
# Test authentication
curl "https://witty-flower-068de881e.2.azurestaticapps.net/api/auth/check-admin"

# Expected: Redirects to Azure AD login (if not already logged in)
# After login: Returns { "isAdmin": true/false, "userId": "<oid>" }

# Test SQL connection
curl "https://witty-flower-068de881e.2.azurestaticapps.net/api/test-sql"

# Expected: { "success": true, "database": "db-menu-app", ... }
```

**Check backend logs:**
```bash
az functionapp logs tail --name func-menu-app-18436 --resource-group <rg>

# ✅ Look for: "Extracted userId: <oid>" (NOT NULL)
# ✅ Look for: "Using Managed Identity authentication"
# ❌ Should NOT see: "Cannot set AccessToken"
```

---

## Expected Behavior After Fix

### User Authentication Flow

1. User visits: `https://witty-flower-068de881e.2.azurestaticapps.net`
2. Not logged in? → SWA redirects to Azure AD login
3. User logs in with Azure AD credentials
4. SWA issues auth cookie (`AppServiceAuthSession`)
5. All API requests include this cookie
6. SWA intercepts requests, adds `X-MS-CLIENT-PRINCIPAL` header
7. Backend extracts user OID from header
8. ✅ **Requests succeed with user identity**

### SQL Connection Flow

**In Azure (Production):**
1. Function starts → detects `WEBSITE_SITE_NAME` environment variable
2. Skips `SqlTokenInterceptor` registration
3. Uses connection string with `Authentication=ActiveDirectoryDefault`
4. SQL connection uses Function App managed identity
5. ✅ **Connection succeeds**

**In Local Dev:**
1. Function starts → no `WEBSITE_SITE_NAME` variable
2. Registers `SqlTokenInterceptor`
3. Frontend sends `X-SQL-Token` header (user SQL token from MSAL)
4. Interceptor sets `SqlConnection.AccessToken` from header
5. ✅ **Connection succeeds with user identity** (enables audit logs)

---

## Testing Checklist

After deployment and configuration:

### Authentication Tests

- [ ] Visit app URL → redirects to Azure AD login
- [ ] Login → redirects back to app successfully
- [ ] Browser has `AppServiceAuthSession` cookie (check DevTools)
- [ ] `/api/auth/check-admin` returns user OID (not NULL)
- [ ] `/.auth/me` endpoint shows user information
- [ ] Backend logs show "Extracted userId: <oid>"

### SQL Connection Tests

- [ ] `/api/test-sql` returns `"success": true`
- [ ] Backend logs show "Using Managed Identity authentication"
- [ ] No "Cannot set AccessToken" errors in logs
- [ ] Managed identity enabled in Function App (Azure Portal → Identity)
- [ ] SQL user created for managed identity (run SQL query)

### Functional Tests

- [ ] Admin toggle appears for authorized users
- [ ] Menu structure loads correctly
- [ ] Can create/edit/delete menu items in admin mode
- [ ] Power BI reports embed successfully
- [ ] OpenFGA authorization checks work

---

## Rollback Plan

If critical issues occur after deployment:

### Quick Rollback (Last Resort)

```bash
# Option 1: Revert to previous commit
git revert HEAD
git push origin main

# Option 2: Redeploy specific working commit
git checkout d363e66  # Last known working commit
git push origin main --force
```

### Targeted Rollback

**If authentication breaks:**
- Remove `auth` section from `staticwebapp.config.json`
- Change `/api/*` route to `allowedRoles: ["anonymous"]`
- Redeploy

**If SQL connections break:**
- Revert `ServiceCollectionExtensions.cs` to add interceptor unconditionally
- Remove `Authentication=` from connection string in GitHub Secrets
- Redeploy backend

---

## Known Limitations

### User SQL Tokens in Production

**Current:** Production uses managed identity (app-level SQL connection)
**Limitation:** Cannot track which user made which SQL query in audit logs
**Workaround:** Application-level logging already tracks user actions

**Future Enhancement:** Implement On-Behalf-Of (OBO) flow to use user SQL tokens in production
- Requires additional Azure AD configuration
- Adds complexity to token management
- Enables true user-level SQL audit trails

### Power BI Row-Level Security (RLS)

**Current:** Power BI embed tokens use app identity (all users see same data)
**Limitation:** Cannot filter Power BI data by user automatically
**Workaround:** Use Power BI RLS with effective user identity in embed token

**Future Enhancement:** Implement user-delegated Power BI tokens
- Frontend: Acquire Power BI token via MSAL
- Backend: Use OBO flow to generate embed token with user context
- Power BI RLS: Automatically filters data by user

---

## Monitoring & Observability

### Key Metrics to Watch

**Authentication:**
- 401 Unauthorized errors (should be near zero)
- Successful logins per day
- Failed login attempts

**SQL Connections:**
- Connection errors (should be zero)
- Query latency
- Active connections count

**Application:**
- API response times
- Failed API requests
- User session duration

### Log Queries (Application Insights)

```kusto
// Authentication failures
traces
| where message contains "Extracted userId: NULL"
| project timestamp, message
| order by timestamp desc

// SQL connection errors
exceptions
| where outerMessage contains "Cannot set AccessToken" or outerMessage contains "ManagedIdentityCredential"
| project timestamp, outerMessage, innerExceptions
| order by timestamp desc

// API request success rate
requests
| where timestamp > ago(1h)
| summarize Total=count(), Failed=countif(success == false) by name
| extend SuccessRate = (Total - Failed) * 100.0 / Total
| order by SuccessRate asc
```

---

## Security Considerations

### Static Web App Authentication

✅ **Pros:**
- Azure-managed authentication (no custom JWT validation needed)
- Automatic token refresh
- Built-in session management
- HTTP-only secure cookies

⚠️ **Security Notes:**
- Client secret stored in Azure (not in code)
- Session cookies are scoped to SWA domain
- Logout via `/.auth/logout` clears session

### Managed Identity for SQL

✅ **Pros:**
- No SQL passwords in code or configuration
- Automatic credential rotation by Azure
- Fine-grained RBAC permissions
- Audit logs show managed identity name

⚠️ **Security Notes:**
- Grant minimum required permissions (db_datareader, db_datawriter)
- Don't grant db_owner unless absolutely necessary
- Monitor SQL audit logs for suspicious queries

### Local Development

⚠️ **Security Notes:**
- `devAuthStore` only active in `import.meta.env.DEV` mode
- Never enable dev auth in production builds
- User SQL tokens expire after 1 hour (MSAL handles refresh)
- Tokens stored in browser localStorage (cleared on logout)

---

## Support & Troubleshooting

### If Authentication Doesn't Work

1. Check `AZURE-PORTAL-SETUP.md` Section "Troubleshooting"
2. Verify all Azure configuration steps completed
3. Wait 5-10 minutes for changes to propagate
4. Check browser console for CORS or auth errors
5. Review Function App logs for error messages

### If SQL Connections Fail

1. Verify managed identity enabled: `az functionapp identity show`
2. Check SQL user exists: `SELECT name FROM sys.database_principals`
3. Verify connection string includes `Authentication=ActiveDirectoryDefault`
4. Review Function App logs for specific error messages

### If Local Development Breaks

1. Ensure `.env` file has all required variables
2. Check `import.meta.env.DEV` is `true` in browser console
3. Verify MSAL acquires SQL token successfully
4. Check backend logs show "Registering SqlTokenInterceptor"

---

## Next Steps

After successful deployment:

1. **Assign Admin Users** → See `SETUP-FIRST-ADMIN.md`
2. **Create Menu Structure** → Use Admin Mode in app
3. **Add Power BI Reports** → Configure in menu items
4. **Monitor Application** → Set up Application Insights alerts
5. **Plan Future Enhancements:**
   - User-delegated SQL tokens (for audit trails)
   - User-delegated Power BI tokens (for RLS)
   - Additional identity providers (Google, GitHub, etc.)

---

## Questions or Issues?

If you encounter problems:

1. **Check Documentation:**
   - `AZURE-PORTAL-SETUP.md` - Configuration guide
   - `RUNNING-THE-APP.md` - Local development
   - `CLAUDE.md` - Architecture overview

2. **Review Logs:**
   - Backend: `az functionapp logs tail`
   - Frontend: Browser DevTools Console
   - Azure: Application Insights

3. **Verify Configuration:**
   - Run through deployment checklist again
   - Ensure all Azure steps completed
   - Check GitHub Secrets are up to date

---

**Deployment Date:** 2025-10-13
**Version:** Post-authentication-fix
**Status:** ✅ Ready for production
