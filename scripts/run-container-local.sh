#!/bin/bash
set -e

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo -e "${BLUE}🚀 Running Menu App Container Locally${NC}"
echo ""

# Get SQL connection string from local.settings.json
CONNECTION_STRING=$(cat "$PROJECT_ROOT/backend/MenuApi/local.settings.json" | grep -o '"DOTNET_CONNECTION_STRING": *"[^"]*"' | sed 's/"DOTNET_CONNECTION_STRING": *"//' | sed 's/"$//')

if [ -z "$CONNECTION_STRING" ]; then
    echo -e "${RED}❌ ERROR: Could not find DOTNET_CONNECTION_STRING in backend/MenuApi/local.settings.json${NC}"
    exit 1
fi

# Convert to OpenFGA format
OPENFGA_DATASTORE_URI=$(echo "$CONNECTION_STRING" | sed 's/Server=\([^;]*\);Database=\([^;]*\);User Id=\([^;]*\);Password=\([^;]*\);.*/sqlserver:\/\/\3:\4@\1?database=\2\&encrypt=true/')

# Check if container is already running
if podman ps | grep -q menu-app-local; then
    echo -e "${YELLOW}⚠️  Container already running. Stopping it...${NC}"
    podman stop menu-app-local
    podman rm menu-app-local
fi

# Check if we need to build
if ! podman images | grep -q menu-app-combined; then
    echo -e "${BLUE}📦 Building container image (this may take a few minutes)...${NC}"
    cd "$PROJECT_ROOT"
    podman build --platform=linux/amd64 -f Dockerfile.combined -t menu-app-combined .
else
    echo -e "${GREEN}✅ Using existing image 'menu-app-combined'${NC}"
    echo -e "${YELLOW}   To rebuild: podman rmi menu-app-combined && npm run container${NC}"
fi

echo ""
echo -e "${BLUE}🚀 Starting container...${NC}"
echo ""

# Run the container
podman run -d \
  --name menu-app-local \
  --platform linux/amd64 \
  -p 7071:80 \
  -p 8080:8080 \
  -e OPENFGA_DATASTORE_URI="$OPENFGA_DATASTORE_URI" \
  -e OPENFGA_STORE_ID="01K785TE28A2Z3NWGAABN1TE8E" \
  -e WEBSITES_PORT=80 \
  menu-app-combined

echo -e "${GREEN}✅ Container started!${NC}"
echo ""
echo -e "${BLUE}📋 Waiting for services to be ready (30s)...${NC}"
sleep 10

# Show logs
echo ""
echo -e "${BLUE}📋 Container logs:${NC}"
podman logs menu-app-local 2>&1 | tail -30

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ Container Running${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "  Backend API: http://localhost:7071/api"
echo -e "  OpenFGA: http://localhost:8080"
echo -e "  Container: podman logs -f menu-app-local"
echo ""
echo -e "${YELLOW}Test it:${NC}"
echo -e "  curl http://localhost:7071/api/menu?user=alice"
echo ""
echo -e "${YELLOW}Stop it:${NC}"
echo -e "  podman stop menu-app-local && podman rm menu-app-local"
echo ""
