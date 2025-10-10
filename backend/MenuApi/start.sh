#!/bin/bash
set -e

echo "Starting OpenFGA with SQL Server and Azure Functions..."

# Validate required environment variables
if [ -z "$OPENFGA_DATASTORE_URI" ]; then
    echo "ERROR: OPENFGA_DATASTORE_URI environment variable is required"
    exit 1
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

# Wait for OpenFGA to be ready
echo "Waiting for OpenFGA to be ready..."
for i in {1..30}; do
    if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
        echo "OpenFGA is ready!"
        break
    fi
    echo "Waiting for OpenFGA... ($i/30)"
    sleep 2
done

# Check if OpenFGA started successfully
if ! curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo "ERROR: OpenFGA failed to start"
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

    # Upload model
    echo "Uploading authorization model..."
    curl -s -X POST "http://localhost:8080/stores/$STORE_ID/authorization-models" \
        -H "Content-Type: application/json" \
        -d @/openfga-config/model.json > /dev/null

    echo "Authorization model uploaded"

    # Load seed data if exists
    if [ -f /openfga-config/seed-data.json ]; then
        echo "Loading seed data..."
        TUPLES=$(jq -c '.tuples' /openfga-config/seed-data.json)

        WRITE_RESPONSE=$(curl -s -X POST "http://localhost:8080/stores/$STORE_ID/write" \
            -H "Content-Type: application/json" \
            -d "{\"writes\":{\"tuple_keys\":$TUPLES}}")

        echo "Seed data response: $WRITE_RESPONSE"
        echo "Seed data loaded successfully"
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

# Start Azure Functions host in the foreground
echo "Starting Azure Functions host with OPENFGA_STORE_ID=$OPENFGA_STORE_ID..."
cd /home/site/wwwroot

# Start the Functions host with environment properly set
exec /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost
