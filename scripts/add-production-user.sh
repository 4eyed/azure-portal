#!/bin/bash

# Add production user to OpenFGA admin role
# This script calls the Azure Function endpoint to add the permission

FUNCTION_APP_URL="https://func-menu-app-18436.azurewebsites.net"
USER_ID="d494d998-61f1-412f-97da-69fa8e0a0d3c"
ROLE="admin"

echo "🔐 Adding user to OpenFGA"
echo "User ID: ${USER_ID}"
echo "Role: ${ROLE}"
echo ""

# Call the Azure Function endpoint
curl -X POST "${FUNCTION_APP_URL}/api/admin/add-user-permission" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"role\": \"${ROLE}\"
  }"

echo ""
echo ""
echo "✅ Done! If successful, refresh your browser to see the menu items."
