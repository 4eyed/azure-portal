#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}Setting up OpenFGA...${NC}"

# Wait for OpenFGA to be ready
echo "Waiting for OpenFGA to be ready..."
until curl -s http://localhost:8080/healthz > /dev/null 2>&1; do
    sleep 2
done

echo -e "${GREEN}OpenFGA is ready!${NC}"

# Create store
echo -e "\n${BLUE}Creating OpenFGA store...${NC}"
STORE_RESPONSE=$(curl -s -X POST http://localhost:8080/stores \
  -H "Content-Type: application/json" \
  -d '{"name": "menu-app"}')

STORE_ID=$(echo $STORE_RESPONSE | grep -o '"id":"[^"]*' | sed 's/"id":"//')
echo -e "Store ID: ${GREEN}$STORE_ID${NC}"

# Save store ID to .env file
echo "OPENFGA_STORE_ID=$STORE_ID" > .env
echo "Store ID saved to .env file"

# Upload authorization model
echo -e "\n${BLUE}Uploading authorization model...${NC}"
MODEL_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/authorization-models" \
  -H "Content-Type: application/json" \
  -d @openfga-config/model.json)

MODEL_ID=$(echo $MODEL_RESPONSE | grep -o '"authorization_model_id":"[^"]*' | sed 's/"authorization_model_id":"//')
echo -e "Model ID: ${GREEN}$MODEL_ID${NC}"

# Load seed data
echo -e "\n${BLUE}Loading seed data...${NC}"
TUPLES=$(cat openfga-config/seed-data.json | jq -c '.tuples')

curl -s -X POST "http://localhost:8080/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" \
  -d "{\"writes\":{\"tuple_keys\":$TUPLES}}" > /dev/null

echo -e "${GREEN}Seed data loaded successfully!${NC}"

# Verify data
echo -e "\n${BLUE}Verifying setup...${NC}"
echo "Checking if alice can view dashboard..."
CHECK_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/check" \
  -H "Content-Type: application/json" \
  -d '{
    "tuple_key": {
      "user": "user:alice",
      "relation": "viewer",
      "object": "menu_item:dashboard"
    }
  }')

ALLOWED=$(echo $CHECK_RESPONSE | grep -o '"allowed":[^,}]*' | sed 's/"allowed"://')

if [ "$ALLOWED" == "true" ]; then
    echo -e "${GREEN}✓ Verification successful! Alice can view dashboard.${NC}"
else
    echo -e "${RED}✗ Verification failed. Please check the setup.${NC}"
fi

echo -e "\n${GREEN}OpenFGA setup complete!${NC}"
echo -e "\nYou can now:"
echo -e "  1. Access OpenFGA API at: http://localhost:8080"
echo -e "  2. Access OpenFGA Playground at: http://localhost:3000"
echo -e "  3. Use Store ID: ${GREEN}$STORE_ID${NC}"
