#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting Azure Functions backend...${NC}"

# Check if .NET 8 SDK is available
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ ERROR: .NET SDK not found${NC}"
    echo -e "${YELLOW}💡 Install from: https://dotnet.microsoft.com/download${NC}"
    exit 1
fi

# Check if Azure Functions Core Tools is available
if ! command -v func &> /dev/null; then
    echo -e "${RED}❌ ERROR: Azure Functions Core Tools not found${NC}"
    echo -e "${YELLOW}💡 Install with: npm install -g azure-functions-core-tools@4${NC}"
    exit 1
fi

# Wait for OpenFGA to be ready (with timeout)
echo -e "${YELLOW}⏳ Waiting for OpenFGA on port 8080...${NC}"
TIMEOUT=30
ELAPSED=0
while [ $ELAPSED -lt $TIMEOUT ]; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo -e "${GREEN}✅ OpenFGA is ready!${NC}"
        break
    fi
    sleep 2
    ELAPSED=$((ELAPSED + 2))
done

if [ $ELAPSED -ge $TIMEOUT ]; then
    echo -e "${RED}❌ ERROR: OpenFGA not responding on port 8080${NC}"
    echo -e "${YELLOW}💡 Run 'npm run openfga' in a separate terminal first${NC}"
    exit 1
fi

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Load environment variables from local.settings.json
cd "$PROJECT_ROOT/backend/MenuApi"

if [ ! -f "local.settings.json" ]; then
    echo -e "${RED}❌ ERROR: local.settings.json not found in backend/MenuApi${NC}"
    exit 1
fi

echo -e "${GREEN}📋 Configuration:${NC}"
echo -e "  OPENFGA_API_URL: http://localhost:8080"
echo -e "  OPENFGA_STORE_ID: $(cat local.settings.json | grep -o '"OPENFGA_STORE_ID": *"[^"]*"' | sed 's/.*: *"//' | sed 's/".*//')"
echo -e "  Port: 7071"
echo ""

# Check if port 7071 is already in use
if lsof -Pi :7071 -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Port 7071 is already in use${NC}"
    echo -e "${YELLOW}Killing existing process...${NC}"
    lsof -ti:7071 | xargs kill -9 2>/dev/null || true
    sleep 2
fi

echo -e "${BLUE}Starting Azure Functions host on port 7071...${NC}"
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ Backend starting...${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "  API: http://localhost:7071/api"
echo -e "  Health: curl http://localhost:7071/api/health"
echo -e "  Test: curl 'http://localhost:7071/api/menu-structure?user=alice'"
echo ""

# Start Azure Functions - it will auto-build on startup
# The updated runtime handles everything correctly now
func start --port 7071
