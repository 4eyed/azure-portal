# Deployment Verification Checklist

## Current Status
✅ Fix pushed to GitHub (commit `4ee615c`)
⏳ Waiting for deployment (~10 minutes)

---

## Pre-Deployment: Azure AD Configuration Verification

Before the deployment completes, let's verify your Azure AD setup is correct:

### 1. Verify App Role Definition

**Azure Portal** → **App Registrations** → **Your App** → **App roles**

**Required app role**:
```json
{
  "displayName": "Admin",
  "description": "Administrators can manage all menu items and users",
  "value": "Admin",
  "allowedMemberTypes": ["User"],
  "isEnabled": true
}
```

**Check**:
- [ ] App role named "Admin" exists
- [ ] Value is exactly "Admin" (case-sensitive)
- [ ] Enabled = true

### 2. Verify App Role Assignment

**Azure Portal** → **Enterprise Applications** → **Your App** → **Users and groups**

**Required assignment**:
- [ ] Your user (d494d998-61f1-412f-97da-69fa8e0a0d3c) is listed
- [ ] Role assigned is "Admin"
- [ ] Status is "Active"

**If not assigned**:
1. Click "Add user/group"
2. Select your user
3. Select "Admin" role
4. Click "Assign"
5. **Wait 5-10 minutes** for role assignment to propagate

### 3. Verify Token Configuration (App Registration)

**Azure Portal** → **App Registrations** → **Your App** → **Token configuration**

**Optional claims to add** (if not present):
- [ ] ID token: `email`
- [ ] ID token: `preferred_username`
- [ ] ID token: `groups` or app roles are included by default

**Note**: App roles are automatically included in ID tokens when `response_type=id_token` is used (our fix adds this).

---

## Post-Deployment: Verification Steps

### Step 1: Wait for Deployment to Complete

**GitHub Actions**: https://github.com/4eyed/azure-portal/actions

**Expected workflows**:
- ✅ Frontend deployment (Azure Static Web Apps) - ~2-3 minutes
- ✅ Backend deployment (Azure Functions) - ~8-10 minutes

**Wait until BOTH show green checkmarks** ✅

### Step 2: Force Token Refresh

**CRITICAL**: You must get a new JWT token with the updated configuration.

1. **Logout**:
   ```
   https://YOUR-SITE.azurestaticapps.net/.auth/logout
   ```

2. **Clear browser data**:
   - Open DevTools (F12)
   - Application → Storage → Clear site data
   - Or: Settings → Privacy → Clear browsing data (Cookies and cached files)

3. **Close ALL browser tabs** for your site

4. **Open new incognito/private window**

5. **Login again**:
   ```
   https://YOUR-SITE.azurestaticapps.net/
   ```

### Step 3: Check Console Logs for New Debugging

Open browser console (F12) and look for NEW debugging messages:

**Expected logs** (from our fix):
```
🔍 JWT Claims found: 15
🔍 Available claim types: oid, name, email, roles, sub, iss, aud, exp, iat
🎭 Roles extracted from JWT: Admin
```

**What to check**:
- [ ] See "🔍 JWT Claims found" message (NEW - didn't exist before)
- [ ] See "roles" in the list of available claim types
- [ ] See "🎭 Roles extracted from JWT: Admin" (not "NONE")

**If you see "⚠️ No JWT claims parsed"**:
- Backend deployment may not be complete yet
- Wait a few more minutes and hard refresh (Ctrl+Shift+R)

### Step 4: Verify Admin Status API

Visit:
```
https://YOUR-SITE.azurestaticapps.net/api/auth/check-admin
```

**Expected response**:
```json
{
  "isAdmin": true,
  "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c"
}
```

**If still false**:
- Check backend logs (see Step 5)
- Verify app role assignment (see Step 6)

### Step 5: Check Backend Logs

**Azure Portal** → **Function App** → **Log stream**

**Look for our new debugging**:
```
🔍 JWT Claims found: 15
🔍 Available claim types: oid, name, email, roles, ...
🎭 Roles extracted from JWT: Admin
```

**Troubleshooting**:
- ❌ "⚠️ No JWT claims parsed" → X-MS-AUTH-TOKEN header not sent by SWA
- ❌ "Available claim types" doesn't include "roles" → Token configuration issue
- ❌ "🎭 Roles extracted from JWT: NONE" → No roles in token despite claim being present

### Step 6: Use Debug Auth Endpoint

Visit:
```
https://YOUR-SITE.azurestaticapps.net/api/debug/auth
```

**Check the response JSON for**:

1. **X-MS-AUTH-TOKEN header**:
   ```json
   {
     "report": "✅ X-MS-AUTH-TOKEN: Present\n   Length: 1200+ characters"
   }
   ```

2. **Roles in response**:
   ```json
   {
     "roles": ["Admin"]
   }
   ```

**Save this output** - it's useful for troubleshooting!

### Step 7: Check UI

**Browser console**:
```
✅ SWA authenticated user: d494d998-61f1-412f-97da-69fa8e0a0d3c
🔐 Admin Status Check
Is Admin: true
User ID (OID): d494d998-61f1-412f-97da-69fa8e0a0d3c
✅ You are an admin!
```

**Sidebar**:
- [ ] "Admin Mode" toggle visible at bottom
- [ ] Can toggle Admin Mode ON
- [ ] See admin controls (Add Menu Group, etc.)

---

## Troubleshooting Decision Tree

### Issue: Still showing "Is Admin: false" after deployment

**Step 1**: Check if new debugging appears in console
- ✅ YES: Proceed to Step 2
- ❌ NO: Backend deployment not complete or browser cache not cleared
  - Solution: Wait 5 more minutes, hard refresh, clear cache

**Step 2**: Check console for "🎭 Roles extracted from JWT: Admin"
- ✅ YES (shows "Admin"): Proceed to Step 3
- ❌ NO (shows "NONE"): Roles not in JWT token
  - Solution: Check Azure AD app role assignment (see Pre-Deployment section)

**Step 3**: Check if "roles" appears in "Available claim types"
- ✅ YES: Backend is receiving roles claim but not recognizing admin
  - Solution: Check if role value is exactly "Admin" (case-sensitive)
- ❌ NO: JWT token doesn't include roles claim
  - Solution:
    1. Verify staticwebapp.config.json was deployed (check GitHub Actions)
    2. Verify you logged out and logged back in
    3. Try incognito window

**Step 4**: Check backend logs for errors
- Look for exceptions in Function App Log Stream
- Check Application Insights for errors

### Issue: "⚠️ No JWT claims parsed from X-MS-AUTH-TOKEN header"

**Possible causes**:

1. **Running locally without SWA simulation**:
   - Use dev auth tools or query parameter fallback

2. **X-MS-AUTH-TOKEN header not sent**:
   - Check /api/debug/auth endpoint
   - If "❌ X-MS-AUTH-TOKEN: Missing", linked backend configuration issue

3. **Backend not deployed**:
   - Check GitHub Actions for backend deployment status
   - Check Azure Portal Function App deployment center

### Issue: Roles claim present but wrong value

**Check `/api/debug/auth` response**:
```json
{
  "roles": ["SomeOtherRole"]  // ❌ Not "Admin"
}
```

**Solution**:
1. Azure AD app role VALUE must be exactly "Admin" (case-sensitive)
2. Re-assign user to "Admin" role in Enterprise App
3. Logout and login to get new token

---

## Expected Timeline

| Time | Status |
|------|--------|
| T+0 | ✅ Git push completed |
| T+2 min | Frontend deployment completes |
| T+10 min | Backend deployment completes |
| T+12 min | Logout and clear cache |
| T+13 min | Login with new JWT token |
| T+14 min | ✅ Admin access working! |

---

## Success Criteria

**All of these must be true**:
- [x] GitHub Actions shows both deployments succeeded ✅
- [ ] Console logs show: "🎭 Roles extracted from JWT: Admin"
- [ ] `/api/auth/check-admin` returns `{"isAdmin": true}`
- [ ] "Admin Mode" toggle visible in sidebar
- [ ] Can create menu groups in Admin Mode
- [ ] Menu structure persists after refresh

---

## Next Steps After Success

Once admin access is working:

1. **Create Initial Menu Structure**:
   - Toggle Admin Mode
   - Add menu group: "Dashboard"
   - Add menu item: "Home Dashboard" (type: Dashboard)

2. **Test Menu Permissions**:
   - Menu should now show 1 group with 1 item
   - Test with another user (should see no menus unless assigned)

3. **Remove Debugging** (optional):
   - Console.WriteLine statements in ClaimsPrincipalParser.cs
   - Keep DebugAuth endpoint for future troubleshooting

4. **Monitor Application**:
   - Check Application Insights for any errors
   - Monitor Function App metrics

---

## Contact Info for Support

If issues persist after following this checklist:

1. **Capture diagnostics**:
   - Screenshot of browser console logs
   - Output from `/api/debug/auth`
   - Screenshot of Azure AD app role assignment

2. **Check documentation**:
   - [ADMIN-ROLE-FIX.md](ADMIN-ROLE-FIX.md)
   - [CLAUDE.md](CLAUDE.md)

3. **Review commit**:
   - Commit: `4ee615c`
   - Changes: 4 files, +283 lines
