# Azure Portal Configuration Steps

## 🎯 Goal: Add `roles` claim to ID token

Your authentication is working, but the `roles` claim is missing from the token. Follow these steps to add it.

---

## Step 1: Add Roles as Optional Claim ⭐ (MOST IMPORTANT)

### Navigate to Token Configuration

1. Open **Azure Portal**: https://portal.azure.com
2. Search for: **"App registrations"**
3. Find and click your app with Client ID: **baa611a0-39d1-427b-89b5-d91658c6ce26**
4. In the left menu, click: **Token configuration**

### Add the Roles Claim

5. Click the **"+ Add optional claim"** button
6. Select token type: **ID** (not Access or SAML)
7. Scroll down and find: **roles**
8. Check the checkbox next to **roles**
9. Click **"Add"** button at the bottom

### Grant Consent (if prompted)

10. If you see a popup about Microsoft Graph permissions:
    - It will say: "This requires User.Read permission"
    - Click **"Add"** to grant consent
    - This is normal and required

### Verify

11. After adding, you should see a new row in the "Optional claims" section:
    - **Token type**: ID
    - **Claim name**: roles
    - **Additional properties**: (none)

---

## Step 2: Verify App Role Definition

### Navigate to App Roles

1. Still in **App registrations** → **Your app**
2. In the left menu, click: **App roles**

### Check for Admin Role

3. Look for an app role with:
   - **Display name**: Admin
   - **Value**: Admin (exactly, case-sensitive)
   - **Enabled**: Yes/True

### If Admin Role Doesn't Exist, Create It

4. Click **"+ Create app role"**
5. Fill in the form:
   - **Display name**: `Admin`
   - **Allowed member types**: Select **"Users/Groups"**
   - **Value**: `Admin` (EXACTLY this, case-sensitive)
   - **Description**: `Administrators can manage all menu items and users`
   - **Do you want to enable this app role?**: Check **Yes**
6. Click **"Apply"**

---

## Step 3: Assign Admin Role to Your User

### Navigate to Enterprise Applications

1. In Azure Portal, search for: **"Enterprise applications"**
2. **IMPORTANT**: Make sure "Application type" filter is set to **"All applications"**
3. Search for your app (you can search by name or Client ID: baa611a0-39d1-427b-89b5-d91658c6ce26)
4. Click on your app

### Assign the Role

5. In the left menu, click: **Users and groups**
6. Check if you see: **Eric Entenman** (eric@4eyed.com) with role **Admin**

### If Your User is Not Listed or Has No Role

7. Click **"+ Add user/group"**
8. Under **Users**, click **"None Selected"**
9. Search for and select: **Eric Entenman** (eric@4eyed.com)
10. Click **"Select"** button at the bottom
11. Under **Select a role**, click **"None Selected"**
12. Select: **Admin**
13. Click **"Select"** button at the bottom
14. Click **"Assign"** button

### Verify Assignment

15. You should now see a row in the "Users and groups" list:
    - **Name**: Eric Entenman
    - **User name**: eric@4eyed.com
    - **Assignment type**: Direct
    - **Role**: Admin

---

## Step 4: Wait and Re-authenticate

### Wait for Propagation

1. **Wait 5-10 minutes** for Azure AD to propagate the changes
2. Changes to token configuration can take a few minutes to take effect

### Force New Token

3. Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/logout`
4. Clear browser cookies and cache:
   - Chrome: Press `Ctrl+Shift+Delete` (Windows) or `Cmd+Shift+Delete` (Mac)
   - Select "Cookies and other site data" and "Cached images and files"
   - Click "Clear data"
5. **Close ALL browser tabs/windows**
6. Open a **new incognito/private window**
7. Visit: `https://YOUR-SITE.azurestaticapps.net/`
8. Login again

---

## Step 5: Verify Roles Claim is Now Present

### Check User Info

Visit: `https://YOUR-SITE.azurestaticapps.net/.auth/me`

### Look for Roles Claim

You should now see a claim with type "roles":

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

✅ **SUCCESS**: If you see the `roles` claim, you're done!

---

## Step 6: Verify Admin Access in Application

### Check Browser Console

1. Open your site in the browser
2. Press `F12` to open Developer Tools
3. Go to the **Console** tab
4. Look for these NEW debug messages:

```
🔍 JWT Claims found: 17
🔍 Available claim types: aud, iss, roles, oid, name, email, ...
🎭 Roles extracted from JWT: Admin
```

✅ **Key indicator**: "🎭 Roles extracted from JWT: Admin" (not "NONE")

### Check Admin API

Visit: `https://YOUR-SITE.azurestaticapps.net/api/auth/check-admin`

**Expected response**:
```json
{
  "isAdmin": true,
  "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c"
}
```

### Check UI

1. Look at the left sidebar
2. Scroll to the bottom
3. You should see: **"Admin Mode"** toggle switch
4. Toggle it **ON**
5. Click **"+ Add Menu Group"**
6. Create your first menu group! 🎉

---

## Troubleshooting

### Still No Roles Claim After Waiting

**Check these**:

1. **Optional claim added?**
   - App registrations → Your app → Token configuration
   - Should see "roles" listed under ID token optional claims

2. **App role defined?**
   - App registrations → Your app → App roles
   - Should see "Admin" role with value "Admin"

3. **Role assigned?**
   - Enterprise applications → Your app → Users and groups
   - Eric Entenman should have "Admin" role

4. **Waited long enough?**
   - Azure AD changes can take 5-10 minutes
   - Try waiting a bit longer

5. **Fresh token?**
   - Must logout, clear cookies, and login again
   - Old tokens won't have the new claims

### Roles Claim Present but Still Not Admin

**Check the value**:
- The roles claim value must be EXACTLY "Admin" (case-sensitive)
- Not "admin", "ADMIN", or "Administrator"
- Must match the app role value exactly

**Check backend logs**:
- Azure Portal → Function App → Log Stream
- Look for: "🎭 Roles extracted from JWT: ..."
- Should show "Admin", not "NONE"

### Alternative: Use Groups Instead of Roles

If you can't get app roles working, we can use Azure AD security groups:

1. Create a security group called "Portal Admins"
2. Add your user to the group
3. Configure app to receive `groups` claim
4. Update backend to check group membership

(This requires code changes - let me know if you need this approach)

---

## Quick Reference

**Your Configuration**:
- **Tenant ID**: ae372c45-2e81-4d1c-9490-e9ac10250047
- **Client ID**: baa611a0-39d1-427b-89b5-d91658c6ce26
- **User OID**: d494d998-61f1-412f-97da-69fa8e0a0d3c
- **User Email**: eric@4eyed.com
- **Required Role**: Admin (value: "Admin")

**Portal Links**:
- [App Registrations](https://portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/RegisteredApps)
- [Enterprise Applications](https://portal.azure.com/#view/Microsoft_AAD_IAM/StartboardApplicationsMenuBlade/~/AppAppsPreview)
- [Azure Active Directory](https://portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview)

**Documentation**:
- See [MISSING-ROLES-CLAIM.md](MISSING-ROLES-CLAIM.md) for detailed explanation
- See [ADMIN-ROLE-FIX.md](ADMIN-ROLE-FIX.md) for original fix details
- See [DEPLOYMENT-VERIFICATION-CHECKLIST.md](DEPLOYMENT-VERIFICATION-CHECKLIST.md) for testing steps
