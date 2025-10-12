#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🔄 Starting backend with hot reload using nodemon...${NC}"
echo -e "${YELLOW}💡 Changes to .cs files will trigger automatic rebuild and restart${NC}"
echo ""

# Check if nodemon is available
if ! command -v nodemon &> /dev/null; then
    echo -e "${RED}❌ ERROR: nodemon not found${NC}"
    echo -e "${YELLOW}💡 Install with: npm install -g nodemon${NC}"
    echo -e "${YELLOW}💡 Or run without hot reload: npm run backend${NC}"
    exit 1
fi

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Use nodemon to watch .cs files and restart the backend script
cd "$SCRIPT_DIR/.."
nodemon \
  --watch "backend/MenuApi/**/*.cs" \
  --ext "cs" \
  --exec "bash scripts/start-backend.sh" \
  --delay 2500ms \
  --signal SIGTERM
