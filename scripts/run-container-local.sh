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

# Check if Podman is working, restart if needed
echo -e "${BLUE}🔍 Checking Podman status...${NC}"
if ! podman ps > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Podman not responding, attempting recovery...${NC}"

    # Kill stale processes
    echo -e "${YELLOW}   Killing stale processes...${NC}"
    pkill -9 gvproxy 2>/dev/null || true
    pkill -9 vfkit 2>/dev/null || true
    pkill -9 qemu-system 2>/dev/null || true
    sleep 2

    # Try to stop machine gracefully first
    echo -e "${YELLOW}   Stopping Podman machine...${NC}"
    podman machine stop 2>/dev/null || true
    sleep 3

    # Start machine
    echo -e "${YELLOW}   Starting Podman machine...${NC}"
    if ! podman machine start; then
        echo -e "${RED}❌ ERROR: Podman machine failed to start${NC}"
        echo ""
        echo -e "${YELLOW}Attempting full Podman reset...${NC}"
        podman machine rm -f podman-machine-default 2>/dev/null || true
        podman machine init --cpus 4 --memory 4096 --disk-size 100
        podman machine start
    fi

    # Wait for Podman to be fully ready
    echo -e "${YELLOW}   Waiting for Podman to be ready...${NC}"
    MAX_RETRIES=15
    RETRY_COUNT=0
    while ! podman ps > /dev/null 2>&1; do
        RETRY_COUNT=$((RETRY_COUNT + 1))
        if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
            echo -e "${RED}❌ ERROR: Podman failed to become ready after $MAX_RETRIES attempts${NC}"
            echo ""
            echo -e "${YELLOW}Manual troubleshooting steps:${NC}"
            echo -e "  1. podman machine stop"
            echo -e "  2. killall -9 gvproxy vfkit qemu-system-aarch64 2>/dev/null"
            echo -e "  3. podman machine start"
            echo -e "  4. podman ps"
            echo ""
            exit 1
        fi
        echo -e "${YELLOW}   Attempt $RETRY_COUNT/$MAX_RETRIES...${NC}"
        sleep 2
    done
fi
echo -e "${GREEN}✅ Podman is ready${NC}"
echo ""

# Get SQL connection string from local.settings.json
CONNECTION_STRING=$(cat "$PROJECT_ROOT/backend/MenuApi/local.settings.json" | grep -o '"DOTNET_CONNECTION_STRING": *"[^"]*"' | sed 's/"DOTNET_CONNECTION_STRING": *"//' | sed 's/"$//')

if [ -z "$CONNECTION_STRING" ]; then
    echo -e "${RED}❌ ERROR: Could not find DOTNET_CONNECTION_STRING in backend/MenuApi/local.settings.json${NC}"
    exit 1
fi

# Convert to OpenFGA format
OPENFGA_DATASTORE_URI=$(echo "$CONNECTION_STRING" | sed 's/Server=\([^;]*\);Database=\([^;]*\);User Id=\([^;]*\);Password=\([^;]*\);.*/sqlserver:\/\/\3:\4@\1?database=\2\&encrypt=true/')

# Check if ports are in use (from native mode or previous runs)
echo -e "${BLUE}🔍 Checking for port conflicts...${NC}"
if lsof -Pi :7071 -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Port 7071 in use, freeing it...${NC}"
    lsof -ti:7071 | xargs kill -9 2>/dev/null || true
    sleep 1
fi

if lsof -Pi :8080 -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Port 8080 in use, freeing it...${NC}"
    lsof -ti:8080 | xargs kill -9 2>/dev/null || true
    sleep 1
fi

# Clean up any existing containers (both old names)
echo -e "${BLUE}🧹 Cleaning up old containers...${NC}"
for container in menu-app-local menu-app; do
    if podman ps -a | grep -q $container; then
        echo -e "${YELLOW}  Removing container: $container${NC}"
        podman stop $container 2>/dev/null || true
        podman rm $container 2>/dev/null || true
    fi
done

# Always remove old image and build fresh to ensure latest code
echo -e "${BLUE}🔄 Removing old image to ensure fresh build...${NC}"
if podman images | grep -q "menu-app-combined"; then
    podman rmi menu-app-combined 2>/dev/null || true
fi

# Build fresh image with latest code (using emulation for Apple Silicon)
echo -e "${BLUE}📦 Building fresh container image with latest code...${NC}"
echo -e "${YELLOW}   This ensures you're always running the most recent changes${NC}"
echo -e "${YELLOW}   Using linux/amd64 emulation (slower on Apple Silicon)${NC}"
echo ""
cd "$PROJECT_ROOT"
podman build --platform linux/amd64 -f Dockerfile.combined.local -t menu-app-combined .

echo ""
echo -e "${BLUE}🚀 Starting container...${NC}"
echo ""

# Run the container (using port 7071 to avoid needing root for port 80)
# Using --platform linux/amd64 for emulation on Apple Silicon
podman run -d \
  --platform linux/amd64 \
  --name menu-app-local \
  -p 7071:80 \
  -p 8080:8080 \
  -e DOTNET_CONNECTION_STRING="$CONNECTION_STRING" \
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
echo -e "  Health Check: http://localhost:7071/api/health"
echo -e "  OpenFGA: http://localhost:8080"
echo -e "  Container Logs: podman logs -f menu-app-local"
echo ""
echo -e "${YELLOW}💡 Note: Using port 7071 (not 80) to avoid needing root${NC}"
echo ""
echo -e "${YELLOW}Test it:${NC}"
echo -e "  curl 'http://localhost:7071/api/menu-structure?user=alice'"
echo -e "  curl http://localhost:7071/api/health"
echo ""
echo -e "${YELLOW}Stop it:${NC}"
echo -e "  npm run stop"
echo -e "  # or: podman stop menu-app-local && podman rm menu-app-local"
echo ""
