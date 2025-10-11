#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting OpenFGA locally...${NC}"

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Check if OpenFGA binary exists
OPENFGA_BIN="$PROJECT_ROOT/openfga-fork/openfga"
if [ ! -f "$OPENFGA_BIN" ]; then
    echo -e "${RED}❌ ERROR: OpenFGA binary not found at $OPENFGA_BIN${NC}"
    echo -e "${YELLOW}💡 Build it with: cd openfga-fork && go build -o openfga ./cmd/openfga${NC}"
    exit 1
fi

# Get connection string from local.settings.json
CONNECTION_STRING=$(cat "$PROJECT_ROOT/backend/MenuApi/local.settings.json" | grep -o '"DOTNET_CONNECTION_STRING": *"[^"]*"' | sed 's/"DOTNET_CONNECTION_STRING": *"//' | sed 's/"$//')

if [ -z "$CONNECTION_STRING" ]; then
    echo -e "${RED}❌ ERROR: Could not find DOTNET_CONNECTION_STRING in backend/MenuApi/local.settings.json${NC}"
    exit 1
fi

# Convert .NET connection string to OpenFGA format (sqlserver://)
# Extract components and rebuild
OPENFGA_DATASTORE_URI=$(echo "$CONNECTION_STRING" | sed 's/Server=\([^;]*\);Database=\([^;]*\);User Id=\([^;]*\);Password=\([^;]*\);.*/sqlserver:\/\/\3:\4@\1?database=\2\&encrypt=true/')

echo -e "${GREEN}📋 Configuration:${NC}"
echo -e "  Database: Azure SQL (from local.settings.json)"
echo -e "  Port: 8080"
echo ""

# Check if OpenFGA is already running
if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  OpenFGA is already running on port 8080${NC}"
    echo -e "${GREEN}✅ Skipping startup${NC}"
    exit 0
fi

# Run database migrations
echo -e "${BLUE}Running database migrations...${NC}"
$OPENFGA_BIN migrate \
    --datastore-engine sqlserver \
    --datastore-uri "$OPENFGA_DATASTORE_URI" \
    2>&1 | grep -v "migration already applied" || true

# Start OpenFGA in the background
echo -e "${BLUE}Starting OpenFGA server on port 8080...${NC}"
$OPENFGA_BIN run \
    --datastore-engine sqlserver \
    --datastore-uri "$OPENFGA_DATASTORE_URI" \
    --log-format json > /tmp/openfga.log 2>&1 &

OPENFGA_PID=$!
echo $OPENFGA_PID > /tmp/openfga.pid

# Wait for OpenFGA to be ready
echo -e "${YELLOW}⏳ Waiting for OpenFGA to be ready...${NC}"
TIMEOUT=60
ELAPSED=0
while [ $ELAPSED -lt $TIMEOUT ]; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo -e "${GREEN}✅ OpenFGA is ready! (took ${ELAPSED}s)${NC}"
        break
    fi
    sleep 2
    ELAPSED=$((ELAPSED + 2))
done

if [ $ELAPSED -ge $TIMEOUT ]; then
    echo -e "${RED}❌ ERROR: OpenFGA failed to start after ${TIMEOUT}s${NC}"
    echo -e "${RED}Logs:${NC}"
    tail -20 /tmp/openfga.log
    exit 1
fi

# Initialize store and data
echo -e "${BLUE}🔧 Initializing OpenFGA store...${NC}"

# Check if store already exists
STORE_RESPONSE=$(curl -s http://localhost:8080/stores)
EXISTING_STORE_ID=$(echo $STORE_RESPONSE | grep -o '"id":"01K785TE28A2Z3NWGAABN1TE8E"' | sed 's/"id":"//' | sed 's/"//')

if [ -z "$EXISTING_STORE_ID" ]; then
    echo -e "${YELLOW}Creating new store with ID: 01K785TE28A2Z3NWGAABN1TE8E${NC}"
    # Note: OpenFGA doesn't support specifying store ID on create
    # We'll create one and note the ID
    STORE_RESPONSE=$(curl -s -X POST http://localhost:8080/stores \
        -H "Content-Type: application/json" \
        -d '{"name": "menu-app"}')
    STORE_ID=$(echo $STORE_RESPONSE | grep -o '"id":"[^"]*"' | head -1 | sed 's/"id":"//' | sed 's/"//')
    echo -e "${GREEN}✅ Created store: $STORE_ID${NC}"
    echo -e "${YELLOW}⚠️  Note: Update OPENFGA_STORE_ID in local.settings.json to: $STORE_ID${NC}"
else
    STORE_ID="01K785TE28A2Z3NWGAABN1TE8E"
    echo -e "${GREEN}✅ Using existing store: $STORE_ID${NC}"
fi

# Check if authorization model exists
EXISTING_MODELS=$(curl -s "http://localhost:8080/stores/$STORE_ID/authorization-models?page_size=1")
MODEL_COUNT=$(echo $EXISTING_MODELS | grep -o '"authorization_models":\[' | wc -l)

if echo "$EXISTING_MODELS" | grep -q '"authorization_models":\[\]'; then
    echo -e "${BLUE}📤 Uploading authorization model...${NC}"
    MODEL_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/authorization-models" \
        -H "Content-Type: application/json" \
        -d @"$PROJECT_ROOT/openfga-config/model.json")

    AUTH_MODEL_ID=$(echo $MODEL_RESPONSE | grep -o '"authorization_model_id":"[^"]*"' | sed 's/"authorization_model_id":"//' | sed 's/"//')

    if [ -n "$AUTH_MODEL_ID" ]; then
        echo -e "${GREEN}✅ Model uploaded: $AUTH_MODEL_ID${NC}"
    else
        echo -e "${RED}⚠️  Failed to upload model. Response: $MODEL_RESPONSE${NC}"
    fi
else
    echo -e "${GREEN}✅ Authorization model already exists${NC}"
    AUTH_MODEL_ID=$(echo $EXISTING_MODELS | grep -o '"id":"[^"]*"' | head -1 | sed 's/"id":"//' | sed 's/"//')
fi

# Load seed data if model exists
if [ -n "$AUTH_MODEL_ID" ]; then
    echo -e "${BLUE}📤 Loading seed data (alice, bob, charlie)...${NC}"
    TUPLES=$(cat "$PROJECT_ROOT/openfga-config/seed-data.json" | grep -v '^\s*$')

    WRITE_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/write" \
        -H "Content-Type: application/json" \
        -d "{\"writes\":$TUPLES,\"authorization_model_id\":\"$AUTH_MODEL_ID\"}")

    if echo "$WRITE_RESPONSE" | grep -q '"code"'; then
        ERROR_MSG=$(echo "$WRITE_RESPONSE" | grep -o '"message":"[^"]*"' | sed 's/"message":"//' | sed 's/"//')
        if [[ "$ERROR_MSG" == *"already exists"* ]]; then
            echo -e "${GREEN}✅ Seed data already loaded${NC}"
        else
            echo -e "${YELLOW}⚠️  Seed data response: $ERROR_MSG${NC}"
        fi
    else
        echo -e "${GREEN}✅ Seed data loaded successfully${NC}"
    fi
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ OpenFGA is ready!${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "  Health: http://localhost:8080/healthz"
echo -e "  Store ID: $STORE_ID"
echo -e "  PID: $OPENFGA_PID (saved to /tmp/openfga.pid)"
echo -e "  Logs: tail -f /tmp/openfga.log"
echo ""

# Keep the process alive and forward signals
trap "kill $OPENFGA_PID 2>/dev/null; exit 0" SIGTERM SIGINT

# Wait for the background process
wait $OPENFGA_PID
