#!/bin/bash

# Assign admin role to user in OpenFGA
# This bypasses the need for roles claim in JWT token

USER_OID="d494d998-61f1-412f-97da-69fa8e0a0d3c"
API_URL="https://witty-flower-068de881e.2.azurestaticapps.net/api"

echo "Assigning Admin role to user $USER_OID in OpenFGA..."

curl -X POST "$API_URL/admin/permissions" \
  -H "Content-Type: application/json" \
  -d "{
    \"user\": \"user:$USER_OID\",
    \"relation\": \"assignee\",
    \"object\": \"role:admin\"
  }"

echo ""
echo "Done! User should now have admin access."
echo "Refresh the page and check if Admin Mode toggle appears."
