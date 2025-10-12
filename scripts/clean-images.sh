#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🧹 Cleaning up Docker images...${NC}"
echo ""

# First, stop all services
echo -e "${YELLOW}Stopping all services first...${NC}"
bash "$(dirname "$0")/stop-all.sh"

echo ""
echo -e "${BLUE}Removing menu-app images...${NC}"

# Remove tagged menu-app images
REMOVED_TAGGED=false
if podman images | grep -q "menu-app-combined"; then
    echo -e "${YELLOW}  Removing tagged image: menu-app-combined${NC}"
    podman rmi menu-app-combined 2>/dev/null || true
    REMOVED_TAGGED=true
fi

if [ "$REMOVED_TAGGED" = true ]; then
    echo -e "${GREEN}✅ Tagged images removed${NC}"
else
    echo -e "${GREEN}✓ No tagged images found${NC}"
fi

# Remove dangling images (old build layers)
echo ""
echo -e "${BLUE}Removing dangling images (<none>)...${NC}"

DANGLING_COUNT=$(podman images -f "dangling=true" -q | wc -l | tr -d ' ')

if [ "$DANGLING_COUNT" -gt 0 ]; then
    echo -e "${YELLOW}  Found $DANGLING_COUNT dangling images${NC}"
    podman image prune -f
    echo -e "${GREEN}✅ Dangling images removed${NC}"
else
    echo -e "${GREEN}✓ No dangling images found${NC}"
fi

# Show disk space saved
echo ""
echo -e "${BLUE}Current image status:${NC}"
podman images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.Created}}" | head -10

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ Cleanup complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "${YELLOW}💡 Next run of 'npm run dev' will build fresh${NC}"
