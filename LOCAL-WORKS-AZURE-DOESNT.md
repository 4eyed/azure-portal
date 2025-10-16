# Working Locally but Not in Azure

## Current Situation

✅ **Local Development**: Admin role works correctly
- User is recognized as admin
- Roles claim appears in token
- Can create menu groups/items

❌ **Azure Production**: Admin role NOT working
- 403 Forbidden (now resolved)
- But roles claim likely missing from token
- User not recognized as admin

## Root Cause: Azure Static Web App Configuration

The issue is that **Azure Static Web Apps uses its own authentication configuration** that can override or be different from your `staticwebapp.config.json` file.

### Two Possible Issues

1. **Configuration in Portal vs File**: Azure SWA might have authentication configured directly in the Azure Portal that takes precedence
2. **Environment Variables Not Set**: `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` might not be configured
3. **Old Configuration Cached**: Azure might be using an older version of your config

---

## Solution 1: Check Azure Portal Configuration

### Navigate to Authentication Settings

1. **Azure Portal** → Search for **"Static Web Apps"**
2. Find and click your Static Web App
3. Left menu → **Configuration** → **Authentication**

### What to Check

Look for any **custom authentication provider** settings that might override your config file.

**Expected**:
- Authentication provider: **Azure Active Directory**
- Or: "Authentication managed by staticwebapp.config.json"

**If you see manual configuration**:
- It might have different login parameters
- It might not include `response_type=id_token`
- This would override your staticwebapp.config.json

### Option A: Use Config File (Recommended)

1. In Authentication settings, look for an option to **"Use configuration file"** or similar
2. Or: Delete any custom authentication configuration
3. Save changes
4. Wait 2-3 minutes
5. Redeploy frontend (may be needed to pick up changes)

### Option B: Configure in Portal

If Azure Portal authentication is preferred:

1. Find the authentication provider settings
2. Add these parameters:
   ```
   scope=openid profile email offline_access
   response_type=id_token
   prompt=consent
   ```

---

## Solution 2: Verify Environment Variables

### Check Application Settings

**Azure Portal** → **Static Web Apps** → **Your app** → **Configuration** → **Application settings**

**Required variables**:
- `AZURE_CLIENT_ID` = `baa611a0-39d1-427b-89b5-d91658c6ce26`
- `AZURE_CLIENT_SECRET` = (your app secret)

### If Missing - Add Them

1. Click **+ Add** (or "New application setting")
2. Add:
   - Name: `AZURE_CLIENT_ID`
   - Value: `baa611a0-39d1-427b-89b5-d91658c6ce26`
3. Click **+ Add** again
4. Add:
   - Name: `AZURE_CLIENT_SECRET`
   - Value: (get from App Registration → Certificates & secrets)
5. Click **Save** at the top

**Note**: If you don't have a client secret, you need to create one:

1. **Azure Portal** → **App registrations** → Your app
2. Left menu → **Certificates & secrets**
3. Click **+ New client secret**
4. Description: `Static Web App Authentication`
5. Expires: Choose duration (12 months recommended)
6. Click **Add**
7. **COPY THE VALUE IMMEDIATELY** (you won't be able to see it again)
8. Add this value to Azure Static Web App application settings

---

## Solution 3: Force Config Reload

### Redeploy staticwebapp.config.json

Sometimes Azure caches the old configuration file.

**Option A: Trigger new deployment**:
```bash
# Make a trivial change to trigger deployment
cd frontend
echo "# Trigger deployment" >> README.md
git add README.md
git commit -m "chore: Trigger frontend redeployment"
git push
```

**Option B: Manual deployment verification**:
1. Check GitHub Actions deployment logs
2. Verify `staticwebapp.config.json` is in the deployment
3. Check build output includes the config file

### Verify Deployed Config

After deployment, try to access:
```
https://YOUR-SITE.azurestaticapps.net/staticwebapp.config.json
```

**Expected**: Should return your config file (or 404 if protected)

---

## Solution 4: Check Azure AD App Registration Redirect URIs

### Verify Production Redirect URI

**Azure Portal** → **App registrations** → Your app → **Authentication**

**Check Redirect URIs include**:
- `https://YOUR-SITE.azurestaticapps.net/.auth/login/aad/callback`
- Or: `https://YOUR-SITE.azurestaticapps.net/*` (wildcard)

**If missing**:
1. Click **+ Add a platform**
2. Select **Web**
3. Add redirect URI: `https://YOUR-SITE.azurestaticapps.net/.auth/login/aad/callback`
4. Check: **ID tokens** (for implicit flow)
5. Click **Configure**

---

## Solution 5: Compare Local vs Azure Tokens

### Check What's Different

**Local** (working):
```bash
# Visit local /.auth/me equivalent
# Check what claims are in the token
```

**Azure** (not working):
```bash
# Visit: https://YOUR-SITE.azurestaticapps.net/.auth/me
# Compare claims
```

**Look for differences**:
- Does Azure token have fewer claims?
- Is `roles` claim present in local but not Azure?
- Are the claim types different?

### If roles claim missing in Azure only

**Possible causes**:
1. Different app registration being used
2. Different tenant being used
3. Authentication configuration in Portal overriding config file
4. Environment variables pointing to different client ID

---

## Debugging Steps

### Step 1: Check Current Azure Configuration

Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/me`

**Capture the response** and check:
- Does it have a `roles` claim?
- What claims ARE present?
- Is the `aud` (audience) correct? Should be: `baa611a0-39d1-427b-89b5-d91658c6ce26`

### Step 2: Check Backend Logs

**Azure Portal** → **Function App** (linked backend) → **Log stream**

**Look for our debugging**:
```
🔍 JWT Claims found: X
🔍 Available claim types: ...
🎭 Roles extracted from JWT: ...
```

**Compare with local logs**:
- Does Azure show fewer claims?
- Does Azure show "NONE" for roles?

### Step 3: Check Authentication Flow

**Browser DevTools** → **Network tab**:
1. Filter: "auth"
2. Logout and login again
3. Look for redirect to `login.microsoftonline.com`
4. Check query parameters in the auth request

**Should include**:
- `scope=openid profile email offline_access`
- `response_type=id_token`
- `client_id=baa611a0-39d1-427b-89b5-d91658c6ce26`

**If missing `response_type=id_token`**:
- Azure SWA authentication config is not using your staticwebapp.config.json
- Need to fix Portal configuration or environment variables

---

## Quick Diagnostic

Run these checks and report back:

### Check 1: Environment Variables
**Azure Portal** → **Static Web Apps** → **Your app** → **Configuration** → **Application settings**

❓ Do you see `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET`?

### Check 2: Authentication Configuration
**Azure Portal** → **Static Web Apps** → **Your app** → **Configuration** → **Authentication**

❓ What does it say? Is there custom configuration or does it reference the config file?

### Check 3: /.auth/me Response
Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/me`

❓ Is there a `roles` claim in the claims array?

### Check 4: Backend Logs
**Azure Portal** → **Function App** → **Log stream**

❓ What does it show for "🎭 Roles extracted from JWT"?

---

## Expected Fix

Most likely one of these will resolve it:

1. ✅ **Add environment variables** (AZURE_CLIENT_ID and AZURE_CLIENT_SECRET)
2. ✅ **Remove Portal authentication override** (let staticwebapp.config.json control auth)
3. ✅ **Redeploy frontend** to pick up latest config
4. ✅ **Add redirect URI** for your Azure site

---

## Next Steps

Please check:
1. Azure Static Web App → Configuration → Application settings
2. Azure Static Web App → Configuration → Authentication
3. Share what you see and I'll guide you to the specific fix needed

The good news is that since it works locally, the code and Azure AD setup are correct - it's just a deployment configuration issue! 🎯
