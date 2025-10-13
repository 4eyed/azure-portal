#!/bin/bash
set -e

echo "=============================================="
echo "Container Startup - $(date)"
echo "=============================================="
echo ""

echo "📋 Environment Variables:"
echo "  OPENFGA_API_URL: ${OPENFGA_API_URL:-not set}"
echo "  OPENFGA_STORE_ID: ${OPENFGA_STORE_ID:-not set}"
echo "  OPENFGA_DATASTORE_ENGINE: ${OPENFGA_DATASTORE_ENGINE:-not set}"
echo "  OPENFGA_DATASTORE_URI: ${OPENFGA_DATASTORE_URI:0:50}... (truncated)"
echo "  WEBSITES_PORT: ${WEBSITES_PORT:-not set}"
echo ""

echo "🔍 ================================================"
echo "🔍 DETAILED CONNECTION STRING ANALYSIS"
echo "🔍 ================================================"
echo ""

# Analyze DOTNET_CONNECTION_STRING
echo "📊 DOTNET_CONNECTION_STRING Analysis:"
if [ -n "$DOTNET_CONNECTION_STRING" ]; then
    echo "  ✅ Variable EXISTS"
    echo "  📏 Length: ${#DOTNET_CONNECTION_STRING} characters"

    # Check for Authentication parameter
    if echo "$DOTNET_CONNECTION_STRING" | grep -qi "Authentication="; then
        AUTH_VALUE=$(echo "$DOTNET_CONNECTION_STRING" | grep -oiE "Authentication=[^;]+" | head -1)
        echo "  ✅ Contains 'Authentication': YES"
        echo "     Value: ${AUTH_VALUE}"
    else
        echo "  ❌ Contains 'Authentication': NO"
        echo "     ⚠️  WARNING: Managed Identity requires 'Authentication=Active Directory Default'"
    fi

    # Extract key properties
    if echo "$DOTNET_CONNECTION_STRING" | grep -qi "Server="; then
        SERVER=$(echo "$DOTNET_CONNECTION_STRING" | grep -oiE "Server=[^;]+" | head -1 | cut -d= -f2)
        echo "  🖥️  Server: ${SERVER}"
    fi

    if echo "$DOTNET_CONNECTION_STRING" | grep -qi "Database="; then
        DATABASE=$(echo "$DOTNET_CONNECTION_STRING" | grep -oiE "Database=[^;]+" | head -1 | cut -d= -f2)
        echo "  🗄️  Database: ${DATABASE}"
    fi

    # Check for password (should NOT be present with Managed Identity)
    if echo "$DOTNET_CONNECTION_STRING" | grep -qiE "(Password=|Pwd=)"; then
        echo "  🔑 Has Password: YES (SQL Auth mode)"
    else
        echo "  🔐 Has Password: NO (Passwordless mode)"
    fi

    echo "  📋 Full value (sanitized):"
    # Sanitize: hide passwords if present
    SANITIZED=$(echo "$DOTNET_CONNECTION_STRING" | sed -E 's/(Password|Pwd)=[^;]+/\1=***REDACTED***/gi')
    echo "     ${SANITIZED}"
else
    echo "  ❌ Variable NOT SET"
    echo "     ⚠️  ERROR: DOTNET_CONNECTION_STRING is required!"
fi
echo ""

# Analyze OPENFGA_DATASTORE_URI
echo "📊 OPENFGA_DATASTORE_URI Analysis:"
if [ -n "$OPENFGA_DATASTORE_URI" ]; then
    echo "  ✅ Variable EXISTS"
    echo "  📏 Length: ${#OPENFGA_DATASTORE_URI} characters"

    # Check for fedauth parameter
    if echo "$OPENFGA_DATASTORE_URI" | grep -qi "fedauth="; then
        FEDAUTH_VALUE=$(echo "$OPENFGA_DATASTORE_URI" | grep -oiE "fedauth=[^&;]+" | head -1)
        echo "  ✅ Contains 'fedauth': YES"
        echo "     Value: ${FEDAUTH_VALUE}"
    else
        echo "  ❌ Contains 'fedauth': NO"
        echo "     ⚠️  WARNING: Managed Identity requires 'fedauth=ActiveDirectoryMSI'"
    fi

    # Extract server
    if echo "$OPENFGA_DATASTORE_URI" | grep -qE "sqlserver://"; then
        SERVER=$(echo "$OPENFGA_DATASTORE_URI" | sed -nE 's|.*sqlserver://([^:/]+).*|\1|p')
        echo "  🖥️  Server: ${SERVER}"
    fi

    # Extract database
    if echo "$OPENFGA_DATASTORE_URI" | grep -qE "database="; then
        DATABASE=$(echo "$OPENFGA_DATASTORE_URI" | grep -oiE "database=[^&;]+" | head -1 | cut -d= -f2)
        echo "  🗄️  Database: ${DATABASE}"
    fi

    # Check for password in URI
    if echo "$OPENFGA_DATASTORE_URI" | grep -qE ":[^:@]+@"; then
        echo "  🔑 Has Password: YES (SQL Auth mode)"
    else
        echo "  🔐 Has Password: NO (Passwordless mode)"
    fi

    echo "  📋 Full value (sanitized):"
    # Sanitize: hide username:password if present
    SANITIZED=$(echo "$OPENFGA_DATASTORE_URI" | sed -E 's|://[^:]+:[^@]+@|://***:***@|g')
    echo "     ${SANITIZED}"
else
    echo "  ❌ Variable NOT SET"
    echo "     ⚠️  ERROR: OPENFGA_DATASTORE_URI is required!"
fi
echo ""

echo "🔍 ================================================"
echo ""

# Check if connection string uses Managed Identity
if [[ "$OPENFGA_DATASTORE_URI" == *"fedauth=ActiveDirectoryMSI"* ]] || \
   [[ "$OPENFGA_DATASTORE_URI" == *"fedauth=ActiveDirectoryManagedIdentity"* ]] || \
   [[ "$OPENFGA_DATASTORE_URI" == *"fedauth=ActiveDirectoryDefault"* ]]; then
    echo "🔐 Detected Azure Managed Identity authentication"
    echo "   Authentication will use the container's managed identity"
    echo ""
elif [[ "$OPENFGA_DATASTORE_URI" == *"Password="* ]] || [[ "$OPENFGA_DATASTORE_URI" == *"pwd="* ]]; then
    echo "🔑 Using legacy password-based authentication"
    echo ""
else
    echo "⚠️  Connection string format unclear - will attempt password-less connection"
    echo ""
fi

# Validate required environment variables
if [ -z "$OPENFGA_DATASTORE_URI" ]; then
    echo "❌ ERROR: OPENFGA_DATASTORE_URI environment variable is required"
    exit 1
fi

if [ -z "$OPENFGA_STORE_ID" ]; then
    echo "⚠️  WARNING: OPENFGA_STORE_ID not set! Will try to create/find store dynamically."
fi

echo ""
echo "=============================================="
echo "Pre-Flight Connectivity Checks"
echo "=============================================="

# Function to extract server from connection string
extract_server_from_uri() {
    local uri="$1"
    # Extract server from sqlserver://server:port format
    echo "$uri" | sed -n 's|.*sqlserver://\([^:;/]*\).*|\1|p'
}

# Check 1: Verify OpenFGA binary exists and is executable
echo ""
echo "CHECK 1: OpenFGA Binary"
echo "----------------------------------------"
if command -v openfga &> /dev/null; then
    echo "✅ OpenFGA binary found: $(which openfga)"
    openfga version 2>&1 || echo "   (version command not available)"
else
    echo "❌ ERROR: OpenFGA binary not found in PATH"
    exit 1
fi

# Check 2: Extract and test SQL Server connectivity
echo ""
echo "CHECK 2: SQL Server Network Connectivity"
echo "----------------------------------------"
SQL_SERVER=$(extract_server_from_uri "$OPENFGA_DATASTORE_URI")
if [ -n "$SQL_SERVER" ]; then
    echo "SQL Server: $SQL_SERVER"

    # Try to resolve DNS
    echo "Testing DNS resolution..."
    if host "$SQL_SERVER" &> /dev/null; then
        echo "✅ DNS resolution successful"
        IP_ADDRESS=$(host "$SQL_SERVER" | grep "has address" | head -1 | awk '{print $4}')
        echo "   Resolved to: $IP_ADDRESS"
    else
        echo "⚠️  DNS resolution failed or host command not available"
    fi

    # Try to test TCP connectivity on port 1433 (SQL Server default)
    echo "Testing TCP connectivity on port 1433..."
    if timeout 5 bash -c "cat < /dev/null > /dev/tcp/$SQL_SERVER/1433" 2>/dev/null; then
        echo "✅ TCP port 1433 is reachable"
    else
        echo "⚠️  Cannot establish TCP connection to port 1433"
        echo "   This could indicate:"
        echo "   - Firewall blocking the connection"
        echo "   - SQL Server not listening on this port"
        echo "   - Network routing issues"
    fi
else
    echo "⚠️  Could not extract server name from OPENFGA_DATASTORE_URI"
    echo "   URI format: ${OPENFGA_DATASTORE_URI:0:50}..."
fi

# Check 3: Test OpenFGA database connectivity with a simple test
echo ""
echo "CHECK 3: OpenFGA Database Connectivity Test"
echo "----------------------------------------"
echo "Testing if OpenFGA can connect to database..."
echo "This will attempt to run OpenFGA migrate (which validates connectivity)"
echo ""

# Run a quick migration test (this validates DB connectivity without side effects)
MIGRATE_START=$(date +%s)
if openfga migrate \
    --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
    --datastore-uri "$OPENFGA_DATASTORE_URI" 2>&1 | tee /tmp/openfga-migrate.log; then
    MIGRATE_END=$(date +%s)
    MIGRATE_DURATION=$((MIGRATE_END - MIGRATE_START))
    echo ""
    echo "✅ OpenFGA migration completed successfully (${MIGRATE_DURATION}s)"
    echo "   Database connectivity confirmed"
else
    MIGRATE_END=$(date +%s)
    MIGRATE_DURATION=$((MIGRATE_END - MIGRATE_START))
    echo ""
    echo "⚠️  Migration completed with warnings (${MIGRATE_DURATION}s)"
    echo "   Checking if tables already exist (this is OK)..."

    if grep -qi "already exists\|duplicate" /tmp/openfga-migrate.log 2>/dev/null; then
        echo "✅ Tables already exist - this is normal on subsequent startups"
    else
        echo "❌ Migration failed with errors. Last 10 lines:"
        tail -10 /tmp/openfga-migrate.log 2>/dev/null || echo "   (no log available)"
        echo ""
        echo "   Common causes:"
        echo "   1. Managed Identity not granted database permissions"
        echo "   2. Firewall rules blocking Azure service"
        echo "   3. Connection string format incorrect"
        echo "   4. SQL Server authentication configuration issue"
        echo ""
        echo "   Continuing startup, but OpenFGA may not work correctly..."
    fi
fi

echo ""
echo "=============================================="
echo "Pre-Flight Checks Complete"
echo "=============================================="
echo ""

# Run database migrations (skip by default in production to speed up startup)
if [ "${SKIP_MIGRATIONS}" = "true" ]; then
    echo "⏩ Skipping database migrations (SKIP_MIGRATIONS=true)"
    echo "   Set SKIP_MIGRATIONS=false to run migrations on next deployment"
else
    echo "Running OpenFGA database migrations..."
    openfga migrate \
        --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
        --datastore-uri "$OPENFGA_DATASTORE_URI" \
        || {
            echo "WARNING: Migration may have already been run or encountered an error"
        }
fi

# Start OpenFGA in the background with SQL Server
echo "Starting OpenFGA on port 8080 with SQL Server backend..."
openfga run \
    --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
    --datastore-uri "$OPENFGA_DATASTORE_URI" \
    --log-format json > /var/log/openfga.log 2>&1 &
OPENFGA_PID=$!

# Wait for OpenFGA to be ready (reduced to 2 minutes for faster failure detection)
echo "Waiting for OpenFGA to be ready (max 2 minutes for SQL migrations)..."
OPENFGA_TIMEOUT=120  # 2 minutes (safer margin for Azure's 230s timeout)
OPENFGA_INTERVAL=2   # Check every 2 seconds (faster detection)
ELAPSED=0

while [ $ELAPSED -lt $OPENFGA_TIMEOUT ]; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo "✅ OpenFGA is ready! (took ${ELAPSED}s at $(date '+%Y-%m-%d %H:%M:%S'))"
        break
    fi
    echo "⏳ [$(date '+%H:%M:%S')] Waiting for OpenFGA... (${ELAPSED}s / ${OPENFGA_TIMEOUT}s)"

    # Check if OpenFGA process is still running
    if ! kill -0 $OPENFGA_PID 2>/dev/null; then
        echo "❌ ERROR: OpenFGA process died unexpectedly"
        echo ""
        echo "OpenFGA logs (last 100 lines):"
        tail -100 /var/log/openfga.log
        echo ""
        echo "Common causes:"
        echo "  1. Database connection failed (check managed identity permissions)"
        echo "  2. Invalid datastore URI format"
        echo "  3. SQL Server firewall blocking connection"
        echo "  4. OpenFGA crashed during initialization"
        exit 1
    fi

    sleep $OPENFGA_INTERVAL
    ELAPSED=$((ELAPSED + OPENFGA_INTERVAL))
done

# Check if OpenFGA started successfully
if ! curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo "❌ ERROR: OpenFGA failed to start after ${OPENFGA_TIMEOUT}s"
    echo "OpenFGA logs (last 50 lines):"
    tail -50 /var/log/openfga.log
    exit 1
fi

# Show recent OpenFGA logs for debugging
echo "📋 Recent OpenFGA startup logs:"
tail -20 /var/log/openfga.log | head -10 || echo "No logs available"
echo ""

# Initialize OpenFGA if configuration exists
if [ -f /openfga-config/model.json ]; then
    echo "Initializing OpenFGA with authorization model..."

    # Check if store already exists, otherwise create it
    STORE_RESPONSE=$(curl -s http://localhost:8080/stores)
    EXISTING_STORE_ID=$(echo $STORE_RESPONSE | jq -r '.stores[]? | select(.name=="menu-app") | .id' | head -1)

    if [ -z "$EXISTING_STORE_ID" ] || [ "$EXISTING_STORE_ID" == "null" ]; then
        echo "Creating new store..."
        STORE_RESPONSE=$(curl -s -X POST http://localhost:8080/stores \
            -H "Content-Type: application/json" \
            -d '{"name": "menu-app"}')
        STORE_ID=$(echo $STORE_RESPONSE | jq -r '.id')
        echo "Created store: $STORE_ID"
    else
        STORE_ID=$EXISTING_STORE_ID
        echo "Using existing store: $STORE_ID"
    fi

    # Export for the Functions app
    export OPENFGA_STORE_ID=$STORE_ID

    # Check if authorization models already exist for this store
    echo "Checking for existing authorization models..."
    EXISTING_MODELS=$(curl -s "http://localhost:8080/stores/$STORE_ID/authorization-models?page_size=1")
    EXISTING_MODEL_ID=$(echo $EXISTING_MODELS | jq -r '.authorization_models[0]?.id // empty')

    if [ -n "$EXISTING_MODEL_ID" ] && [ "$EXISTING_MODEL_ID" != "null" ]; then
        echo "Found existing authorization model: $EXISTING_MODEL_ID"
        echo "⚠️  Using existing model instead of uploading new one"
        AUTH_MODEL_ID=$EXISTING_MODEL_ID
    else
        # Upload model and check response
        echo "Uploading new authorization model..."
        echo "📤 Model payload preview:"
        cat /openfga-config/model.json | jq -c '.' | head -c 200
        echo "..."

        MODEL_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/authorization-models" \
            -H "Content-Type: application/json" \
            -d @/openfga-config/model.json)

        echo "Model upload response: $MODEL_RESPONSE"

        # Extract authorization model ID from response
        AUTH_MODEL_ID=$(echo $MODEL_RESPONSE | jq -r '.authorization_model_id // empty')

        if [ -z "$AUTH_MODEL_ID" ]; then
            echo "⚠️  WARNING: Failed to upload authorization model"
            echo "Response was: $MODEL_RESPONSE"
            echo "Checking if model exists after failed upload..."

            # Try to get existing models again in case it was actually created
            RETRY_MODELS=$(curl -s "http://localhost:8080/stores/$STORE_ID/authorization-models?page_size=1")
            AUTH_MODEL_ID=$(echo $RETRY_MODELS | jq -r '.authorization_models[0]?.id // empty')

            if [ -n "$AUTH_MODEL_ID" ] && [ "$AUTH_MODEL_ID" != "null" ]; then
                echo "✅ Found model after retry: $AUTH_MODEL_ID"
            else
                echo "⚠️  Continuing without authorization model - Functions may not work correctly"
                echo "Manual intervention may be required to upload the model"
            fi
        else
            echo "✅ Authorization model uploaded successfully (ID: $AUTH_MODEL_ID)"
        fi
    fi

    # Seed data loading removed (legacy code)
    # Authorization relationships are now managed via the API, not seed files
else
    echo "No OpenFGA configuration found, skipping initialization"
fi

# Function to handle shutdown
shutdown() {
    echo "Shutting down..."
    kill $OPENFGA_PID 2>/dev/null || true
    exit 0
}

trap shutdown SIGTERM SIGINT

# Final environment check before starting Functions
echo ""
echo "=============================================="
echo "Starting Azure Functions Host"
echo "=============================================="
echo "  OPENFGA_API_URL: $OPENFGA_API_URL"
echo "  OPENFGA_STORE_ID: $OPENFGA_STORE_ID"
echo "  Current directory: $(pwd)"
echo "  OpenFGA PID: $OPENFGA_PID"
echo ""

cd /home/site/wwwroot

# List files to verify deployment
echo "📁 Function files:"
ls -la *.dll 2>/dev/null | head -5 || echo "No DLL files found"
echo ""

# Start the Functions host with environment properly set
echo "🚀 Executing Azure Functions host at $(date '+%Y-%m-%d %H:%M:%S')..."
echo "   Total startup time so far: ${ELAPSED}s"
echo ""

# Forward Functions logs to stdout for Azure diagnostics
exec /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost
