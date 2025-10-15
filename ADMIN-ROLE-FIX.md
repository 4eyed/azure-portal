# Admin Role Recognition Fix

## Problem Summary

Users assigned the "Admin" app role in Azure AD were not being recognized as admins in the application, resulting in:
- `isAdmin: false` from `/api/auth/check-admin` endpoint
- "Admin Mode" toggle not appearing in the sidebar
- Unable to create menu groups/items (0 menu groups found)

## Root Cause

**Azure Static Web Apps was not including the `roles` claim in the JWT token** sent to the linked backend.

### Technical Details

1. **SWA Authentication Flow**:
   - Azure Static Web Apps handles authentication via `/.auth/login/aad`
   - SWA sends `X-MS-AUTH-TOKEN` header with JWT Bearer token to linked backend
   - JWT token contains user claims (oid, name, email, etc.)

2. **Missing Configuration**:
   - Default login parameters: `"scope=openid profile email offline_access"`
   - **Missing**: `response_type=id_token` parameter
   - Without this, Azure AD does NOT include app role assignments in the JWT

3. **Backend Expectations**:
   - [ClaimsPrincipalParser.cs:102-109](backend/MenuApi/Services/ClaimsPrincipalParser.cs#L102-L109) parses JWT and looks for `roles` claim
   - [HttpRequestExtensions.cs:47](backend/MenuApi/Extensions/HttpRequestExtensions.cs#L47) checks for "admin" role via `HasRole()`
   - [CheckAdmin.cs:51](backend/MenuApi/Functions/CheckAdmin.cs#L51) combines app role check with OpenFGA check

## Fix Applied

### 1. Updated Azure Static Web Apps Configuration

**File**: [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json#L28-L32)

**Before**:
```json
"loginParameters": [
  "scope=openid profile email offline_access",
  "prompt=consent"
]
```

**After**:
```json
"loginParameters": [
  "scope=openid profile email offline_access",
  "response_type=id_token",
  "prompt=consent"
]
```

**Why this works**:
- `response_type=id_token` tells Azure AD to include app role assignments in the ID token
- Roles appear as `"roles": ["Admin"]` claim in JWT payload
- Backend can now extract and validate admin role

### 2. Added Debugging to ClaimsPrincipalParser

**File**: [backend/MenuApi/Services/ClaimsPrincipalParser.cs](backend/MenuApi/Services/ClaimsPrincipalParser.cs#L96-L120)

Added console logging to track:
- Number of JWT claims found
- Available claim types
- Roles extracted from JWT (or "NONE" if empty)

**Sample output**:
```
🔍 JWT Claims found: 15
🔍 Available claim types: oid, name, email, roles, sub, iss, aud, exp, iat
🎭 Roles extracted from JWT: Admin
```

### 3. Enhanced DebugAuth Endpoint

**File**: [backend/MenuApi/Functions/DebugAuth.cs](backend/MenuApi/Functions/DebugAuth.cs#L105-L115)

Added `X-MS-AUTH-TOKEN` header inspection to authentication diagnostics.

## Deployment Steps

### Option A: Deploy via GitHub Actions (Recommended)

1. **Commit and push changes**:
   ```bash
   git add frontend/public/staticwebapp.config.json
   git add backend/MenuApi/Services/ClaimsPrincipalParser.cs
   git add backend/MenuApi/Functions/DebugAuth.cs
   git commit -m "fix: Add response_type=id_token to include roles claim in JWT token"
   git push
   ```

2. **Wait for deployments**:
   - Frontend deployment (~2 minutes)
   - Backend deployment (~8-10 minutes)

3. **Force token refresh**:
   - Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/logout`
   - Clear browser cookies/cache
   - Visit: `https://YOUR-SITE.azurestaticapps.net/`
   - Login again

4. **Verify admin role**:
   - Open browser console (F12)
   - Look for: `🎭 Admin Status Check` logs
   - Expected: `Is Admin: true`

### Option B: Manual Frontend Deployment

If you need to deploy only the frontend immediately:

```bash
cd frontend
npm run build

# Deploy to Azure Static Web Apps
az staticwebapp upload \
  --resource-group YOUR_RESOURCE_GROUP \
  --name YOUR_STATIC_WEB_APP_NAME \
  --app-location ./dist
```

## Verification Steps

### 1. Check JWT Token Contains Roles

**Visit**: `https://YOUR-SITE.azurestaticapps.net/api/debug/auth`

**Look for**:
```
✅ X-MS-AUTH-TOKEN: Present
   Length: 1200+ characters
```

**Expected in response JSON**:
```json
{
  "roles": ["Admin"]
}
```

### 2. Check Admin Status API

**Visit**: `https://YOUR-SITE.azurestaticapps.net/api/auth/check-admin`

**Expected response**:
```json
{
  "isAdmin": true,
  "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c"
}
```

### 3. Check Application Logs

**Azure Portal** → **Function App** → **Log Stream**

**Look for**:
```
🔍 JWT Claims found: 15
🔍 Available claim types: oid, name, email, roles, ...
🎭 Roles extracted from JWT: Admin
```

### 4. Check UI

**Browser console** (F12 → Console):
```
✅ SWA authenticated user: d494d998-61f1-412f-97da-69fa8e0a0d3c
🔐 Admin Status Check
Is Admin: true
User ID (OID): d494d998-61f1-412f-97da-69fa8e0a0d3c
✅ You are an admin!
```

**Sidebar**:
- "Admin Mode" toggle should be visible at the bottom

## Troubleshooting

### Still showing "Is Admin: false" after deployment

**Possible causes**:

1. **Token not refreshed**:
   - Solution: Logout via `/.auth/logout`, clear cookies, login again

2. **App role not assigned in Azure AD**:
   - Go to: Azure Portal → Enterprise Applications → Your App → Users and groups
   - Verify your user has "Admin" role assigned
   - Wait 5-10 minutes for role assignment to propagate

3. **App role not defined**:
   - Go to: Azure Portal → App Registrations → Your App → App roles
   - Verify "Admin" role exists with value "Admin"

4. **Static Web App not using updated config**:
   - Redeploy frontend manually
   - Check deployment logs in GitHub Actions

### No roles claim in JWT token

**Debug steps**:

1. Visit `/api/debug/auth` endpoint
2. Check if `X-MS-AUTH-TOKEN` header is present
3. Check console logs for JWT claim types
4. If `roles` is not listed, verify:
   - `response_type=id_token` is in `staticwebapp.config.json`
   - Frontend was redeployed after config change
   - User logged out and logged back in

### "⚠️ No JWT claims parsed from X-MS-AUTH-TOKEN header"

This means the backend is not receiving the JWT token from SWA.

**Possible causes**:
- Running locally without proper SWA simulation
- Linked backend configuration issue
- Backend not deployed correctly

**Solution**:
- Check Azure Portal → Static Web App → APIs → Linked backend
- Verify backend function app is linked
- Check backend deployment logs

## Next Steps After Fix

Once admin role is recognized:

1. **Toggle Admin Mode** in sidebar
2. **Create menu groups**:
   - Click "Add Menu Group"
   - Example: "Dashboard", "Reports", "Settings"
3. **Create menu items**:
   - Select menu group
   - Add items with type (PowerBI, ExternalApp, etc.)
4. **Assign permissions**:
   - Use `/api/admin/permissions` endpoint
   - Or use OpenFGA CLI to create tuples

## Related Files

- [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json) - SWA authentication config
- [backend/MenuApi/Services/ClaimsPrincipalParser.cs](backend/MenuApi/Services/ClaimsPrincipalParser.cs) - JWT parsing logic
- [backend/MenuApi/Extensions/HttpRequestExtensions.cs](backend/MenuApi/Extensions/HttpRequestExtensions.cs) - Admin check helpers
- [backend/MenuApi/Functions/CheckAdmin.cs](backend/MenuApi/Functions/CheckAdmin.cs) - Admin status endpoint
- [backend/MenuApi/Functions/DebugAuth.cs](backend/MenuApi/Functions/DebugAuth.cs) - Authentication diagnostics

## References

- [Azure Static Web Apps Authentication](https://learn.microsoft.com/en-us/azure/static-web-apps/authentication-authorization)
- [Azure AD App Roles](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-add-app-roles-in-azure-ad-apps)
- [OpenID Connect response_type parameter](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest)
