#!/bin/bash

# Add OpenFGA permission for real Azure AD user
# This grants admin role to the authenticated user

OPENFGA_URL="${OPENFGA_API_URL:-http://localhost:8080}"
STORE_ID="${OPENFGA_STORE_ID}"
USER_ID="d494d998-61f1-412f-97da-69fa8e0a0d3c"

echo "🔐 Adding OpenFGA permission for user: ${USER_ID}"
echo "OpenFGA URL: ${OPENFGA_URL}"
echo "Store ID: ${STORE_ID}"

# Add tuple: user is assigned to admin role
curl -X POST "${OPENFGA_URL}/stores/${STORE_ID}/write" \
  -H "Content-Type: application/json" \
  -d '{
    "writes": {
      "tuple_keys": [
        {
          "user": "user:'"${USER_ID}"'",
          "relation": "assignee",
          "object": "role:admin"
        }
      ]
    }
  }'

echo ""
echo ""
echo "✅ Permission added! User ${USER_ID} is now assigned to admin role."
echo ""
echo "To verify, run:"
echo "curl '${OPENFGA_URL}/stores/${STORE_ID}/check' -H 'Content-Type: application/json' -d '{\"tuple_key\":{\"user\":\"user:${USER_ID}\",\"relation\":\"viewer\",\"object\":\"menu_item:1\"}}'"
