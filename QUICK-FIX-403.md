# Quick Fix: 403 Forbidden - Still Occurring

## Immediate Solutions to Try

### Option 1: Remove userDetailsClaim Entirely (Simplest)

Azure Static Web Apps can auto-detect the user identifier. Let's let it do that instead of specifying a claim.

**Update**: [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json)

**Remove the userDetailsClaim line**:

```json
"login": {
  "loginParameters": [
    "scope=openid profile email offline_access",
    "response_type=id_token",
    "prompt=consent"
  ]
}
```

**No userDetailsClaim specified** - SWA will auto-detect from available claims.

### Option 2: Use 'name' Claim

The 'name' claim is always present in Azure AD tokens:

```json
"userDetailsClaim": "name"
```

### Option 3: Use 'oid' Claim

Azure AD Object ID is always present:

```json
"userDetailsClaim": "oid"
```

### Option 4: Use 'preferred_username' Claim

User Principal Name:

```json
"userDetailsClaim": "preferred_username"
```

## Recommended: Try Option 1 First

Let me update the config to remove `userDetailsClaim` entirely and let SWA auto-detect.

This is the most reliable approach because:
- SWA will try multiple claims in order (email, preferred_username, name, sub)
- No dependency on optional claims being configured
- Works with default Azure AD token configuration

## Why You're Still Seeing 403

**Most likely causes**:

1. **Deployment not complete yet**
   - Frontend deployment takes 2-3 minutes
   - Check: https://github.com/4eyed/azure-portal/actions
   - Look for green checkmark on latest workflow

2. **Browser cache**
   - Old config still cached
   - Try: Hard refresh (Ctrl+Shift+R or Cmd+Shift+R)
   - Or: New incognito window

3. **Email claim not in token**
   - Azure AD needs to be configured to include email in ID token
   - Requires adding optional claim in App Registration
   - Or use a different claim (see options above)

4. **Need to logout first**
   - Old session still active
   - Must logout: `/.auth/logout`
   - Clear cookies
   - Then login again

## Step-by-Step Fix

### Step 1: Update Config (Remove userDetailsClaim)

I'll update the config to remove the userDetailsClaim requirement.

### Step 2: Deploy

```bash
git add frontend/public/staticwebapp.config.json
git commit -m "fix: Remove userDetailsClaim to allow SWA auto-detection"
git push
```

### Step 3: Wait for Deployment

- Check: https://github.com/4eyed/azure-portal/actions
- Wait for green checkmark (2-3 minutes)

### Step 4: Force Logout and Clear Cache

```bash
# Visit this URL to logout
https://YOUR-SITE.azurestaticapps.net/.auth/logout

# Then clear browser cache:
# - Chrome: Ctrl+Shift+Delete → Clear cookies and cached files
# - Or use incognito window
```

### Step 5: Login Again

Visit: `https://YOUR-SITE.azurestaticapps.net/`

**Expected**: No 403 error, successful login

## Verification

After successful login, check:

**1. User info endpoint**:
```
https://YOUR-SITE.azurestaticapps.net/.auth/me
```

Should return:
```json
{
  "clientPrincipal": {
    "userId": "...",  // Should be populated
    "userDetails": "...",  // Should be populated
    "identityProvider": "aad",
    "userRoles": ["authenticated"]
  }
}
```

**If userId is null or empty** → 403 will occur

**2. Check what claims are available**:

Look at the `claims` array in the response above. This tells us what claims are in your token.

Common claims:
- `oid` - Azure AD Object ID (always present)
- `name` - Display name (usually present)
- `preferred_username` - UPN (usually present)
- `email` - Email address (may need optional claim configuration)

## If Still Getting 403

**Try these in order**:

1. **Remove userDetailsClaim** (auto-detect)
2. **Use "name"** claim
3. **Use "oid"** claim
4. **Use "preferred_username"** claim
5. **Add email optional claim** in Azure AD

## Alternative: Check Azure AD App Registration

If none of the above work, we may need to add optional claims:

**Azure Portal** → **App Registrations** → **Your App** → **Token configuration**

**Add these optional claims for ID token**:
- [x] email
- [x] preferred_username
- [x] name

**Steps**:
1. Click "Add optional claim"
2. Select "ID" token type
3. Check all three: email, preferred_username, name
4. Click "Add"
5. Accept Microsoft Graph permissions if prompted
6. Wait 5-10 minutes for changes to propagate
7. Logout and login again

## Quick Test Without Deployment

You can also test locally to see what claims are available:

1. Login to Azure Portal
2. Go to Azure Active Directory → Users → Your user
3. Check what fields are populated:
   - Display name (maps to "name" claim)
   - User principal name (maps to "preferred_username" claim)
   - Mail (maps to "email" claim)

If any of these are empty, that claim won't be in the token.
