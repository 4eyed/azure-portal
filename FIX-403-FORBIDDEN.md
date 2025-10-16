# Fix: 403 Forbidden - Missing Email/Handle from Login Service

## Error Message

```
403: Forbidden

We need an email address or a handle from your login service.
To use this login, please update your account with the missing info.
```

## Root Cause

Azure Static Web Apps requires a user identifier claim to establish user identity. The `userDetailsClaim` configuration was pointing to a claim that might not be present in the Azure AD token:

**Problematic config**:
```json
"userDetailsClaim": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
```

This long-form claim URI may not be present in the ID token by default.

## Fix Applied

### 1. Updated userDetailsClaim to use "email"

**File**: [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json#L34)

**Changed from**:
```json
"userDetailsClaim": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
```

**Changed to**:
```json
"userDetailsClaim": "email"
```

**Why this works**:
- `email` is a standard OpenID Connect claim
- We're already requesting it in `scope=openid profile email`
- Azure AD will include it in the ID token when `response_type=id_token` is set

### 2. Verify Azure AD App Registration Has Email Claim

**Azure Portal** → **App Registrations** → **Your App** → **Token configuration**

**Check optional claims**:
- [ ] **ID token**: `email` claim is present

**If not present, add it**:
1. Click "Add optional claim"
2. Select "ID" token type
3. Check "email"
4. Click "Add"
5. If prompted to add Microsoft Graph permissions, click "Add" (this adds `User.Read` permission)

### 3. Alternative: Use preferred_username

If the `email` claim is still not available, you can use `preferred_username` instead:

```json
"userDetailsClaim": "preferred_username"
```

This uses the user's UPN (User Principal Name) as the identifier.

### 4. Alternative: Remove userDetailsClaim Entirely

Azure Static Web Apps should auto-detect the user identifier from standard claims. Try removing the line:

```json
"login": {
  "loginParameters": [
    "scope=openid profile email offline_access",
    "response_type=id_token",
    "prompt=consent"
  ]
}
```

**No userDetailsClaim specified** - SWA will try to find a suitable claim automatically (email, preferred_username, sub, etc.)

## Deployment Steps

### Quick Fix (Recommended)

**Commit and push**:
```bash
git add frontend/public/staticwebapp.config.json
git commit -m "fix: Use 'email' claim for userDetailsClaim to resolve 403 Forbidden"
git push
```

**Wait for deployment** (~2-3 minutes for frontend)

**Test**:
1. Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/logout`
2. Clear browser cookies
3. Visit: `https://YOUR-SITE.azurestaticapps.net/`
4. Login again

## Testing the Fix

### Step 1: Check Token Configuration

**Azure Portal** → **App Registrations** → **Your App** → **Token configuration**

**Verify**:
- [x] ID token includes `email` claim
- [x] ID token includes `preferred_username` claim

### Step 2: Test Login

1. Logout: `/.auth/logout`
2. Clear cookies
3. Login again

**Expected result**: No 403 error, successful login

### Step 3: Verify User Details

Visit: `/.auth/me`

**Expected response**:
```json
{
  "clientPrincipal": {
    "userId": "your.email@domain.com",
    "userDetails": "your.email@domain.com",
    "userRoles": ["authenticated", "anonymous"],
    "identityProvider": "aad",
    "claims": [
      { "typ": "email", "val": "your.email@domain.com" },
      { "typ": "oid", "val": "d494d998-61f1-412f-97da-69fa8e0a0d3c" },
      { "typ": "roles", "val": "Admin" }
    ]
  }
}
```

**Key fields**:
- `userId`: Should be populated with email or username
- `userDetails`: Should be populated
- `claims`: Should include `email` claim

## Troubleshooting

### Still getting 403 after fix

**Possible causes**:

1. **Email claim not in token**:
   - Check Azure AD → App Registrations → Token configuration
   - Add `email` as optional claim for ID token
   - May require admin consent for Microsoft Graph `User.Read` permission

2. **User account has no email**:
   - Check user's Azure AD profile
   - Ensure email field is populated
   - Try using `preferred_username` instead

3. **Configuration not deployed**:
   - Check GitHub Actions deployment status
   - Verify `staticwebapp.config.json` in deployed site
   - Hard refresh after deployment

### Alternative Claims to Try

If `email` doesn't work, try these in order:

1. **preferred_username** (UPN):
   ```json
   "userDetailsClaim": "preferred_username"
   ```

2. **name** (display name):
   ```json
   "userDetailsClaim": "name"
   ```

3. **upn** (user principal name):
   ```json
   "userDetailsClaim": "upn"
   ```

4. **Remove entirely** (auto-detect):
   ```json
   "login": {
     "loginParameters": [
       "scope=openid profile email offline_access",
       "response_type=id_token",
       "prompt=consent"
     ]
   }
   ```

### Verify Claims in Token

You can decode the JWT token to see what claims are available:

1. Login and get the token from browser DevTools → Network → Look for redirect with `id_token` parameter
2. Copy the JWT token
3. Go to https://jwt.ms/ and paste the token
4. Check available claims

**Look for**:
- `email`
- `preferred_username`
- `name`
- `upn`
- `oid` (always present - Azure AD Object ID)

## Related Configuration

### Complete auth section

```json
"auth": {
  "identityProviders": {
    "azureActiveDirectory": {
      "registration": {
        "openIdIssuer": "https://login.microsoftonline.com/YOUR-TENANT-ID/v2.0",
        "clientIdSettingName": "AZURE_CLIENT_ID",
        "clientSecretSettingName": "AZURE_CLIENT_SECRET"
      },
      "login": {
        "loginParameters": [
          "scope=openid profile email offline_access",
          "response_type=id_token",
          "prompt=consent"
        ]
      },
      "userDetailsClaim": "email"
    }
  }
}
```

### What each part does

- **scope=openid profile email**: Request standard OIDC claims including email
- **response_type=id_token**: Request ID token (includes app roles)
- **prompt=consent**: Always show consent screen (ensures fresh token with latest roles)
- **userDetailsClaim**: Which claim to use for `userId` and `userDetails` in SWA

## Expected Behavior After Fix

1. ✅ Login succeeds without 403 error
2. ✅ `/.auth/me` shows `userId` and `userDetails` populated
3. ✅ `roles` claim includes "Admin"
4. ✅ Backend receives `X-MS-AUTH-TOKEN` with complete user info
5. ✅ Admin status check returns `isAdmin: true`

## References

- [Azure Static Web Apps Authentication Configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/authentication-authorization)
- [Azure AD Token Configuration](https://learn.microsoft.com/en-us/azure/active-directory/develop/active-directory-optional-claims)
- [OpenID Connect Standard Claims](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)
