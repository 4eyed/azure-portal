# Correct Azure Portal Steps - App Roles

## ⚠️ Important: Roles Are NOT Optional Claims

App roles are **automatically included** in tokens when properly configured. You do NOT need to add them as optional claims.

**What you need**:
1. App role defined in App Registration ✓
2. User assigned to that role in Enterprise Applications ✓
3. `response_type=id_token` in login parameters ✓ (already deployed)

---

## Step 1: Verify App Role is Defined

**Navigate to App Roles**:
1. Azure Portal → **App registrations**
2. Find app with Client ID: **baa611a0-39d1-427b-89b5-d91658c6ce26**
3. Left menu → **App roles**

**Check for Admin role**:
- Display name: **Admin**
- Value: **Admin** (exactly, case-sensitive)
- Allowed member types: **Users/Groups**
- Enabled: **Yes**

### If Admin Role Doesn't Exist - Create It

1. Click **Create app role**
2. Fill in:
   - **Display name**: `Admin`
   - **Allowed member types**: Select **"Users/Groups"**
   - **Value**: `Admin` ⚠️ MUST BE EXACTLY "Admin" (case-sensitive)
   - **Description**: `Administrators can manage menu items`
   - **Enable this app role**: ✓ Check this box
3. Click **Apply**

**Screenshot reference**: Look for a grid/table with columns: Display name, Description, Allowed member types, Value, Enabled

---

## Step 2: Assign Admin Role to User (MOST IMPORTANT)

**Navigate to Enterprise Applications**:
1. Azure Portal → Search for **"Enterprise applications"**
2. **Filter**: Set "Application type" to **"All applications"**
3. **Search**: Type your Client ID: `baa611a0-39d1-427b-89b5-d91658c6ce26`
4. Click on the app in the results

**Assign the role**:
5. Left menu → **Users and groups**
6. Look for: **Eric Entenman** (eric@4eyed.com)

### If User is Not Listed OR Has No Role

7. Click **+ Add user/group** (blue button at top)
8. Under **Users**:
   - Click **"None Selected"**
   - Search: `eric@4eyed.com` or `Eric Entenman`
   - Click on your user
   - Click **"Select"** button at bottom
9. Under **Select a role**:
   - Click **"None Selected"**
   - Select: **Admin**
   - Click **"Select"** button at bottom
10. Click **"Assign"** button

**Verify assignment**:
- You should see a row with:
  - Name: Eric Entenman
  - User Principal Name: eric@4eyed.com
  - Role: Admin

---

## Step 3: Wait and Re-authenticate

### Important: Token Refresh Required

Azure AD only adds app roles to **new tokens**. Your existing token won't update automatically.

**Steps**:
1. **Wait 2-3 minutes** for assignment to propagate
2. Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/logout`
3. **Close ALL browser tabs** for your site
4. **Open new incognito/private window** (recommended)
5. Visit: `https://YOUR-SITE.azurestaticapps.net/`
6. Login again

---

## Step 4: Verify Roles Claim

**Check user info**:

Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/me`

**Look for the roles claim**:
```json
{
  "clientPrincipal": {
    "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c",
    "userDetails": "eric@4eyed.com",
    "claims": [
      {
        "typ": "roles",
        "val": "Admin"
      },
      // ... other claims ...
    ]
  }
}
```

✅ **Success**: If you see `"typ": "roles"` with `"val": "Admin"`, you're done!

❌ **Still missing**: If no roles claim appears, check:
- Did you assign the role in **Enterprise Applications** (not just App Registrations)?
- Did you wait 2-3 minutes for propagation?
- Did you logout and login again to get a new token?

---

## Step 5: Verify Admin Access

**Browser console** (F12):
```
🔍 JWT Claims found: 17
🔍 Available claim types: ..., roles, ...
🎭 Roles extracted from JWT: Admin
```

**API check**:
Visit: `https://YOUR-SITE.azurestaticapps.net/api/auth/check-admin`

Expected:
```json
{
  "isAdmin": true,
  "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c"
}
```

**UI**:
- "Admin Mode" toggle appears in sidebar
- Can click "Add Menu Group"
- Create your first menu!

---

## Common Mistakes

### ❌ Assigning role in App Registrations
App Registrations shows the role **definition**, not **assignments**.
✅ **Fix**: Assign in **Enterprise Applications** → **Users and groups**

### ❌ Using old token
The roles claim is only in **new tokens** issued after assignment.
✅ **Fix**: Logout, clear cookies, login again

### ❌ Wrong role value
If app role value is "admin" (lowercase) but backend expects "Admin".
✅ **Fix**: Role value must be exactly "Admin" (case-sensitive)

### ❌ Not waiting for propagation
Azure AD can take 1-2 minutes to propagate role assignments.
✅ **Fix**: Wait 2-3 minutes before testing

---

## Troubleshooting

### Roles claim still missing after following all steps

**Double-check Enterprise Applications assignment**:
1. Enterprise Applications → Your app → Users and groups
2. Verify Eric Entenman has "Admin" role (not "No role assigned")

**Check app role enabled**:
1. App Registrations → Your app → App roles
2. Verify "Enabled" column shows "Yes"

**Try different user**:
- Create a test user
- Assign Admin role to test user
- Login as test user
- Check if roles claim appears

**Check Azure AD logs**:
1. Azure Portal → Azure Active Directory → Sign-in logs
2. Find your recent sign-in
3. Click on it → "Additional Details" tab
4. Look for claims in the token

### Backend still shows "isAdmin: false"

**Check backend logs**:
- Azure Portal → Function App → Log stream
- Look for: "🎭 Roles extracted from JWT: ..."
- Should show "Admin", not "NONE"

**Check role name match**:
- App role value in Azure AD: "Admin"
- Backend expects: "Admin" (case-sensitive)
- These must match exactly

---

## Quick Reference

**Your Info**:
- Client ID: baa611a0-39d1-427b-89b5-d91658c6ce26
- Tenant ID: ae372c45-2e81-4d1c-9490-e9ac10250047
- User OID: d494d998-61f1-412f-97da-69fa8e0a0d3c
- User Email: eric@4eyed.com

**What to configure**:
1. ✓ App role "Admin" defined in App Registration → App roles
2. ✓ Role assigned to Eric Entenman in Enterprise Applications → Users and groups
3. ✓ Logout and login to get new token with roles claim

**How to verify success**:
- `/.auth/me` shows roles claim
- Browser console shows "🎭 Roles extracted from JWT: Admin"
- `/api/auth/check-admin` returns `isAdmin: true`
- "Admin Mode" toggle visible in UI

---

## Summary

**DO NOT** try to add roles as optional claims - it won't work that way.

**DO**:
1. Define app role in App Registrations → App roles
2. Assign role to user in Enterprise Applications → Users and groups
3. Logout and login to get new token
4. Verify roles claim in `/.auth/me`

The `response_type=id_token` we already added will automatically include app roles in the token once the role is assigned.
