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

# Wait for OpenFGA to be ready (reduced to 2 minutes for faster failure detection)
echo "Waiting for OpenFGA to be ready (max 2 minutes for SQL migrations)..."
OPENFGA_TIMEOUT=120  # 2 minutes (safer margin for Azure's 230s timeout)
OPENFGA_INTERVAL=5   # Check every 5 seconds
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
        echo "OpenFGA logs:"
        cat /var/log/openfga.log
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

    # Load seed data if exists and we have a valid model
    if [ -f /openfga-config/seed-data.json ]; then
        if [ -n "$AUTH_MODEL_ID" ] && [ "$AUTH_MODEL_ID" != "null" ]; then
            echo "Loading seed data..."
            TUPLES=$(jq -c '.tuples' /openfga-config/seed-data.json)

            echo "📤 Writing $(echo $TUPLES | jq '. | length') tuples to store..."

            WRITE_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/write" \
                -H "Content-Type: application/json" \
                -d "{\"writes\":{\"tuple_keys\":$TUPLES},\"authorization_model_id\":\"$AUTH_MODEL_ID\"}")

            echo "Seed data response: $WRITE_RESPONSE"

            # Check if write was successful
            if echo "$WRITE_RESPONSE" | jq -e '.code' > /dev/null 2>&1; then
                ERROR_CODE=$(echo "$WRITE_RESPONSE" | jq -r '.code')
                ERROR_MSG=$(echo "$WRITE_RESPONSE" | jq -r '.message')

                # If error is about tuples already existing, that's okay
                if [[ "$ERROR_MSG" == *"already exists"* ]] || [[ "$ERROR_MSG" == *"duplicate"* ]]; then
                    echo "⚠️  Seed data may already exist: $ERROR_MSG"
                    echo "✅ Continuing with existing data"
                else
                    echo "⚠️  WARNING: Failed to write seed data"
                    echo "Error code: $ERROR_CODE"
                    echo "Error message: $ERROR_MSG"
                    echo "Continuing anyway - manual data initialization may be required"
                fi
            else
                echo "✅ Seed data loaded successfully"
            fi
        else
            echo "⚠️  Skipping seed data load - no valid authorization model ID"
        fi
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
echo "🚀 Executing Azure Functions host at $(date '+%Y-%m-%d %H:%M:%S')..."
echo "   Total startup time so far: ${ELAPSED}s"
echo ""

# Forward Functions logs to stdout for Azure diagnostics
exec /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost
