# SOLUTION: Use OpenFGA for Admin Authorization

## Problem Confirmed

The `roles` claim is **NOT appearing in the Azure Static Web Apps JWT token**, despite:
- ✅ App role "Admin" defined in Azure AD
- ✅ User assigned to "Admin" role
- ✅ `response_type=id_token` in staticwebapp.config.json
- ✅ All configuration correct

**Root cause**: Azure Static Web Apps is not honoring the `staticwebapp.config.json` login parameters, or there's a platform limitation preventing app roles from being included in the token.

## Solution: Bypass Token Roles - Use OpenFGA Directly

Since OpenFGA is already configured and working, we can assign admin permissions directly in the OpenFGA database, bypassing the need for roles in the JWT token entirely.

### How It Works

**Current (broken) flow**:
1. User logs in → Azure AD issues token
2. Token should include `roles: ["Admin"]` claim
3. SWA forwards token to backend
4. Backend extracts roles from token ❌ **FAILS - no roles claim**

**New (working) flow**:
1. User logs in → Azure AD issues token
2. Token includes user ID (OID)
3. SWA forwards token to backend
4. Backend extracts user ID ✅ **WORKS**
5. Backend checks OpenFGA: "Is this user assigned to role:admin?" ✅ **WORKS**

The code **already supports this** - see line 51 in [CheckAdmin.cs](backend/MenuApi/Functions/CheckAdmin.cs#L51):
```csharp
var isAdmin = req.IsAdmin(_claimsParser) || await _authService.IsAdmin(userId);
```

It checks **both** token roles AND OpenFGA. Since token roles are empty, it will use OpenFGA.

---

## Implementation Steps

### Step 1: Assign Admin via OpenFGA API (Production)

Since you can't call the `/api/auth/assign-user-permission` endpoint (requires admin access - chicken-and-egg), we need to write directly to the OpenFGA database.

**Option A: Via Azure Function App Console**

1. **Azure Portal** → **Function App (func-menu-app-18436)** → **Console**
2. Run this curl command:

```bash
curl -X POST http://localhost:8080/stores/01K785TE28A2Z3NWGAABN1TE8E/write \
  -H "Content-Type: application/json" \
  -d '{
    "writes": {
      "tuple_keys": [
        {
          "user": "user:d494d998-61f1-412f-97da-69fa8e0a0d3c",
          "relation": "assignee",
          "object": "role:admin"
        }
      ]
    }
  }'
```

**Option B: Temporarily Allow Anonymous Access**

1. Edit [AddUserPermission.cs](backend/MenuApi/Functions/AddUserPermission.cs#L51-57)
2. Comment out the admin check temporarily:
```csharp
// Check if user is admin
// if (!req.IsAdmin(_claimsParser) && !await _authService.IsAdmin(adminUserId))
// {
//     return new CorsObjectResult(new { error = "Only admins can assign user permissions" })
//     {
//         StatusCode = StatusCodes.Status403Forbidden
//     };
// }
```
3. Deploy backend
4. Call the API:
```bash
curl -X POST https://witty-flower-068de881e.2.azurestaticapps.net/api/auth/assign-user-permission \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c",
    "relation": "assignee",
    "object": "role:admin"
  }'
```
5. Re-enable the admin check and redeploy

**Option C: Update Seed Data (Best for future)**

Add your user to the seed data so it's automatically assigned on deployment:

Edit [openfga-config/seed-data.json](openfga-config/seed-data.json) and add:
```json
{
  "user": "user:d494d998-61f1-412f-97da-69fa8e0a0d3c",
  "relation": "assignee",
  "object": "role:admin"
}
```

Then redeploy the backend.

---

## Step 2: Verify Admin Access

After assigning via OpenFGA:

1. **Refresh** `https://witty-flower-068de881e.2.azurestaticapps.net/`
2. **Check** `/api/auth/check-admin`:
```json
{
  "isAdmin": true,
  "userId": "d494d998-61f1-412f-97da-69fa8e0a0d3c",
  "debug": {
    "rolesFromToken": [],
    "hasAdminRoleInToken": false,
    "isAdminInOpenFGA": true,  ← Should be true now!
    "finalAdminStatus": true
  }
}
```

3. **UI**: "Admin Mode" toggle should appear
4. **Test**: Create menu groups and items

---

## Step 3: Clean Up (Optional)

Since we're now using OpenFGA exclusively for admin authorization, you can:

1. **Remove token role dependency** from frontend code
2. **Update documentation** to explain admin assignment via OpenFGA
3. **Remove `response_type=id_token`** from staticwebapp.config.json (not needed anymore)

---

## Advantages of OpenFGA Approach

✅ **Works immediately** - no waiting for Azure AD token configuration
✅ **More flexible** - can assign/revoke admin without touching Azure AD
✅ **Audit trail** - OpenFGA tracks all authorization changes
✅ **Scalable** - can add more fine-grained permissions (editor, viewer, etc.)
✅ **Works locally and in Azure** - same authorization logic everywhere

---

## Quick Command Reference

### Check if you're admin in OpenFGA (local)
```bash
curl -X POST http://localhost:8080/stores/01K785TE28A2Z3NWGAABN1TE8E/check \
  -H "Content-Type: application/json" \
  -d '{
    "tuple_key": {
      "user": "user:d494d998-61f1-412f-97da-69fa8e0a0d3c",
      "relation": "assignee",
      "object": "role:admin"
    }
  }'
```

### Assign admin (local)
```bash
curl -X POST http://localhost:8080/stores/01K785TE28A2Z3NWGAABN1TE8E/write \
  -H "Content-Type: application/json" \
  -d '{
    "writes": {
      "tuple_keys": [
        {
          "user": "user:d494d998-61f1-412f-97da-69fa8e0a0d3c",
          "relation": "assignee",
          "object": "role:admin"
        }
      ]
    }
  }'
```

### Remove admin (local)
```bash
curl -X POST http://localhost:8080/stores/01K785TE28A2Z3NWGAABN1TE8E/write \
  -H "Content-Type: application/json" \
  -d '{
    "deletes": {
      "tuple_keys": [
        {
          "user": "user:d494d998-61f1-412f-97da-69fa8e0a0d3c",
          "relation": "assignee",
          "object": "role:admin"
        }
      ]
    }
  }'
```

---

## Why Azure AD Roles Didn't Work

Azure Static Web Apps has known limitations with custom authentication parameters:

1. **Config file may not be fully honored** in all scenarios
2. **App roles might require Portal configuration** in addition to config file
3. **Token customization is limited** compared to App Service authentication
4. **Standard tier features** may differ from documented behavior

This is a known pain point with SWA. Using OpenFGA for authorization is actually the **recommended approach** for complex permission scenarios.

---

## Next Steps

1. **Choose Option B above** (temporarily disable admin check)
2. **Call the assign-user-permission API** to make yourself admin in OpenFGA
3. **Re-enable the admin check** and redeploy
4. **Test** - admin access should now work!
5. **Update seed data** so future deployments include your admin assignment

This will get you unblocked immediately and is actually a better long-term solution than relying on token-based roles! 🎉
