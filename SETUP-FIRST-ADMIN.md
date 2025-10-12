# Setting Up the First Admin User

After implementing the Admin role system, you need to manually assign yourself as the first admin user. Follow these steps:

## Step 1: Start the Application

```bash
# Start the full stack (container + frontend)
npm run dev

# OR start natively with hot reload
npm run dev:watch
```

## Step 2: Login and Extract Your Entra OID

1. Open browser to http://localhost:5173
2. Login with your Azure Entra account
3. Open browser console (F12)
4. Look for the "Admin status check" log message:
   ```
   Admin status check: { isAdmin: false, userId: "a1b2c3d4-..." }
   ```
5. Copy your `userId` (this is your Entra Object ID)

## Step 3: Assign Yourself to Admin Role

Use curl to add your OID to the admin role in OpenFGA:

```bash
# Get OpenFGA store ID
export STORE_ID=$(curl -s http://localhost:8080/stores | jq -r '.stores[0].id')
echo "Store ID: $STORE_ID"

# Get authorization model ID
export MODEL_ID=$(curl -s "http://localhost:8080/stores/$STORE_ID/authorization-models?page_size=1" | jq -r '.authorization_models[0].id')
echo "Model ID: $MODEL_ID"

# Replace YOUR_OID with the userId from Step 2
export YOUR_OID="a1b2c3d4-5678-90ab-cdef-1234567890ab"

# Add yourself to admin role
curl -X POST "http://localhost:8080/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" \
  -d "{
    \"writes\": {
      \"tuple_keys\": [{
        \"user\": \"user:$YOUR_OID\",
        \"relation\": \"assignee\",
        \"object\": \"role:admin\"
      }]
    },
    \"authorization_model_id\": \"$MODEL_ID\"
  }" | jq '.'
```

## Step 4: Verify Admin Access

1. Refresh the browser (http://localhost:5173)
2. Check console logs:
   ```
   Admin status check: { isAdmin: true, userId: "a1b2c3d4-..." }
   ```
3. You should now see the "Admin mode" toggle in the sidebar footer
4. Your user profile should show "(Admin)" badge

## Step 5: Enable Admin Mode and Create Menus

1. Toggle "Admin mode" to ON in the sidebar
2. Click "+ New Group" to create menu groups
3. Add menu items to each group
4. Assign users to menu items using the API (see below)

## Assigning Other Users to Menu Items

Once you're an admin, you can assign other users viewer permissions:

```bash
# Via API (requires authentication)
curl -X POST "http://localhost:7071/api/admin/assign-user-permission" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "other-user-oid",
    "relation": "viewer",
    "object": "menu_item:dashboard"
  }'
```

Or directly via OpenFGA:

```bash
# Assign user to role
curl -X POST "http://localhost:8080/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" \
  -d "{
    \"writes\": {
      \"tuple_keys\": [{
        \"user\": \"user:other-user-oid\",
        \"relation\": \"assignee\",
        \"object\": \"role:viewer\"
      }]
    },
    \"authorization_model_id\": \"$MODEL_ID\"
  }"

# Assign role to menu item
curl -X POST "http://localhost:8080/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" \
  -d "{
    \"writes\": {
      \"tuple_keys\": [{
        \"user\": \"role:viewer\",
        \"relation\": \"viewer_role\",
        \"object\": \"menu_item:dashboard\"
      }]
    },
    \"authorization_model_id\": \"$MODEL_ID\"
  }"
```

## Troubleshooting

### "Admin status check: { isAdmin: false }"

- Verify you added the tuple correctly: `curl http://localhost:8080/stores/$STORE_ID/read`
- Check OpenFGA logs: `docker logs <container-id>`
- Ensure your OID matches exactly (case-sensitive)

### Admin Toggle Not Showing

- Check browser console for errors
- Verify `/api/admin/check` endpoint returns `isAdmin: true`
- Hard refresh (Ctrl+Shift+R / Cmd+Shift+R)

### Can't Create Menus

- Ensure "Admin mode" toggle is ON
- Check backend logs for authorization errors
- Verify database migration ran: `dotnet ef migrations list`

## Next Steps

Once you have admin access and created some menus:
1. Create menu groups for organizing your reports
2. Add menu items (Power BI reports, external apps, etc.)
3. Assign users to specific menu items via OpenFGA
4. Test with non-admin users to verify permissions work correctly
