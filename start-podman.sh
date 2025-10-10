#!/bin/bash

set -e

echo "╔══════════════════════════════════════════════════════════╗"
echo "║      Starting Menu App with Podman                      ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Configuration
IMAGE_NAME="menu-app-combined"
CONTAINER_NAME="menu-app"
CONFIG_DIR="$(pwd)/openfga-config"

# Check if Podman is installed
if ! command -v podman &> /dev/null; then
    echo -e "${RED}✗ Podman not found. Please install Podman first.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Podman found: $(podman --version)${NC}"
echo ""

# Check if we're on macOS and need a Podman machine
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo -e "${BLUE}Checking Podman machine (required on macOS)...${NC}"

    # Check if any machine exists
    MACHINE_COUNT=$(podman machine list --format "{{.Name}}" 2>/dev/null | grep -v "^$" | wc -l | tr -d ' ')

    if [ "$MACHINE_COUNT" -eq 0 ] || [ -z "$MACHINE_COUNT" ]; then
        echo -e "${BLUE}No Podman machine found. Creating one...${NC}"
        echo -e "${BLUE}This will take a few minutes...${NC}"
        podman machine init
        if [ $? -ne 0 ]; then
            echo -e "${RED}✗ Failed to create Podman machine${NC}"
            exit 1
        fi
    fi

    # Check if machine is running
    RUNNING=$(podman machine list --format "{{.Running}}" 2>/dev/null | grep -c "true")

    if [ "$RUNNING" -eq 0 ]; then
        echo -e "${BLUE}Starting Podman machine...${NC}"
        podman machine start
        if [ $? -ne 0 ]; then
            echo -e "${RED}✗ Failed to start Podman machine${NC}"
            exit 1
        fi
        echo -e "${GREEN}✓ Podman machine started${NC}"
        sleep 5
    else
        echo -e "${GREEN}✓ Podman machine is running${NC}"
    fi
fi
echo ""

# Stop and remove existing container
if podman ps -a | grep -q $CONTAINER_NAME; then
    echo -e "${BLUE}Stopping existing container...${NC}"
    podman stop $CONTAINER_NAME 2>/dev/null || true
    podman rm $CONTAINER_NAME 2>/dev/null || true
fi

# Build the image
echo -e "${BLUE}Building container image...${NC}"
echo -e "${BLUE}Note: Building for AMD64 platform (Azure Functions doesn't support ARM yet)${NC}"
podman build --platform=linux/amd64 -f Dockerfile.combined -t $IMAGE_NAME .

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Image built successfully${NC}"
echo ""

# Run the container
echo -e "${BLUE}Starting container...${NC}"
podman run -d \
    --platform=linux/amd64 \
    --name $CONTAINER_NAME \
    -p 7071:80 \
    -p 8080:8080 \
    -v "$CONFIG_DIR:/openfga-config:ro" \
    $IMAGE_NAME

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Failed to start container${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Container started${NC}"
echo ""

# Wait for services
echo -e "${BLUE}Waiting for services to start...${NC}"
for i in {1..30}; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo -e "${GREEN}✓ OpenFGA is ready${NC}"
        break
    fi
    echo -n "."
    sleep 1
done
echo ""

for i in {1..30}; do
    if curl -s http://localhost:7071/api/menu?user=alice > /dev/null 2>&1; then
        echo -e "${GREEN}✓ API is ready${NC}"
        break
    fi
    echo -n "."
    sleep 1
done
echo ""

# Test the API
echo ""
echo -e "${BLUE}Testing API...${NC}"
RESPONSE=$(curl -s "http://localhost:7071/api/menu?user=alice")
if echo "$RESPONSE" | grep -q "menuItems"; then
    echo -e "${GREEN}✓ API is working!${NC}"
    echo ""
    echo "Sample response:"
    echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
else
    echo -e "${RED}✗ API test failed${NC}"
    echo "Showing logs:"
    podman logs $CONTAINER_NAME
fi

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║                  🎉 App is Running!                      ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "Access the application:"
echo "  📱 Frontend:  Open frontend/index.html in your browser"
echo "  🔌 API:       http://localhost:7071/api/menu?user=alice"
echo "  🔐 OpenFGA:   http://localhost:8080/healthz"
echo ""
echo "Useful commands:"
echo "  podman logs -f $CONTAINER_NAME     # View logs"
echo "  podman stop $CONTAINER_NAME        # Stop container"
echo "  podman start $CONTAINER_NAME       # Start container"
echo "  podman exec -it $CONTAINER_NAME /bin/bash  # Shell access"
echo ""
echo "Or use the Makefile:"
echo "  make -f Makefile.podman logs       # View logs"
echo "  make -f Makefile.podman test       # Test endpoints"
echo "  make -f Makefile.podman stop       # Stop container"
echo ""
