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

# Validate required environment variables
if [ -z "$OPENFGA_DATASTORE_URI" ]; then
    echo "❌ ERROR: OPENFGA_DATASTORE_URI environment variable is required"
    exit 1
fi

if [ -z "$OPENFGA_STORE_ID" ]; then
    echo "⚠️  WARNING: OPENFGA_STORE_ID not set! Will try to create/find store dynamically."
fi

# Run database migrations
echo "Running OpenFGA database migrations..."
openfga migrate \
    --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
    --datastore-uri "$OPENFGA_DATASTORE_URI" \
    || {
        echo "WARNING: Migration may have already been run or encountered an error"
    }

# Start OpenFGA in the background with SQL Server
echo "Starting OpenFGA on port 8080 with SQL Server backend..."
openfga run \
    --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
    --datastore-uri "$OPENFGA_DATASTORE_URI" \
    --log-format json > /var/log/openfga.log 2>&1 &
OPENFGA_PID=$!

# Wait for OpenFGA to be ready (up to 3 minutes for SQL migrations)
echo "Waiting for OpenFGA to be ready (this may take up to 3 minutes for SQL migrations)..."
OPENFGA_TIMEOUT=180  # 3 minutes
OPENFGA_INTERVAL=5   # Check every 5 seconds
ELAPSED=0

while [ $ELAPSED -lt $OPENFGA_TIMEOUT ]; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo "✅ OpenFGA is ready! (took ${ELAPSED}s)"
        break
    fi
    echo "⏳ Waiting for OpenFGA... (${ELAPSED}s / ${OPENFGA_TIMEOUT}s)"
    sleep $OPENFGA_INTERVAL
    ELAPSED=$((ELAPSED + OPENFGA_INTERVAL))
done

# Check if OpenFGA started successfully
if ! curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo "❌ ERROR: OpenFGA failed to start after ${OPENFGA_TIMEOUT}s"
    echo "OpenFGA logs:"
    cat /var/log/openfga.log
    exit 1
fi

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

    # Upload model and check response
    echo "Uploading authorization model..."
    MODEL_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/authorization-models" \
        -H "Content-Type: application/json" \
        -d @/openfga-config/model.json)

    echo "Model upload response: $MODEL_RESPONSE"

    # Extract authorization model ID from response
    AUTH_MODEL_ID=$(echo $MODEL_RESPONSE | jq -r '.authorization_model_id // empty')

    if [ -z "$AUTH_MODEL_ID" ]; then
        echo "❌ ERROR: Failed to upload authorization model"
        echo "Response was: $MODEL_RESPONSE"
        exit 1
    fi

    echo "✅ Authorization model uploaded successfully (ID: $AUTH_MODEL_ID)"

    # Load seed data if exists
    if [ -f /openfga-config/seed-data.json ]; then
        echo "Loading seed data..."
        TUPLES=$(jq -c '.tuples' /openfga-config/seed-data.json)

        WRITE_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/write" \
            -H "Content-Type: application/json" \
            -d "{\"writes\":{\"tuple_keys\":$TUPLES},\"authorization_model_id\":\"$AUTH_MODEL_ID\"}")

        echo "Seed data response: $WRITE_RESPONSE"

        # Check if write was successful
        if echo "$WRITE_RESPONSE" | jq -e '.code' > /dev/null 2>&1; then
            echo "❌ ERROR: Failed to write seed data"
            exit 1
        fi

        echo "✅ Seed data loaded successfully"
    fi
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
echo "🚀 Executing Azure Functions host..."
exec /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost
