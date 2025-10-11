#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}🛑 Stopping all local development services...${NC}"
echo ""

# Function to kill process on a port
kill_port() {
    local port=$1
    local name=$2

    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
        echo -e "${YELLOW}Stopping $name on port $port...${NC}"
        lsof -ti:$port | xargs kill -9 2>/dev/null || true
        echo -e "${GREEN}✅ $name stopped${NC}"
    else
        echo -e "${GREEN}✓ $name not running (port $port free)${NC}"
    fi
}

# Stop OpenFGA (port 8080)
kill_port 8080 "OpenFGA"

# Stop Azure Functions (port 7071)
kill_port 7071 "Azure Functions"

# Stop Vite dev server (port 5173)
kill_port 5173 "Frontend (Vite)"

# Clean up PID file if it exists
if [ -f /tmp/openfga.pid ]; then
    PID=$(cat /tmp/openfga.pid)
    if ps -p $PID > /dev/null 2>&1; then
        echo -e "${YELLOW}Stopping OpenFGA process (PID: $PID)...${NC}"
        kill -9 $PID 2>/dev/null || true
    fi
    rm /tmp/openfga.pid
fi

# Clean up log file
if [ -f /tmp/openfga.log ]; then
    rm /tmp/openfga.log
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ All services stopped${NC}"
echo -e "${GREEN}========================================${NC}"
