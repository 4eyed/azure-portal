#!/bin/bash
# Manual debugging script for testing connectivity inside the container
# Usage: ./test-connections.sh [test-name]
# Available tests: all, openfga, sql, api, config

set -e

echo "=========================================="
echo "Container Connectivity Test Suite"
echo "=========================================="
echo "Timestamp: $(date)"
echo ""

# Colors for output (if supported)
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

TEST_FILTER="${1:-all}"

# Test 1: OpenFGA Process Check
if [[ "$TEST_FILTER" == "all" || "$TEST_FILTER" == "openfga" ]]; then
    echo "=========================================="
    echo "TEST: OpenFGA Process"
    echo "=========================================="

    if pgrep -f "openfga" > /dev/null; then
        OPENFGA_PID=$(pgrep -f "openfga")
        print_success "OpenFGA process is running (PID: $OPENFGA_PID)"

        # Check memory usage
        if command -v ps &> /dev/null; then
            MEM_INFO=$(ps -p $OPENFGA_PID -o rss=,vsz= 2>/dev/null || echo "unknown")
            echo "   Memory usage: RSS=${MEM_INFO}"
        fi

        # Check how long it's been running
        if command -v ps &> /dev/null; then
            START_TIME=$(ps -p $OPENFGA_PID -o lstart= 2>/dev/null || echo "unknown")
            echo "   Started: ${START_TIME}"
        fi
    else
        print_error "OpenFGA process is NOT running"
        echo "   Check logs at: /var/log/openfga.log"
    fi
    echo ""

    # Test OpenFGA HTTP endpoint
    echo "Testing OpenFGA HTTP endpoint..."
    OPENFGA_URL="${OPENFGA_API_URL:-http://localhost:8080}"

    if curl -f -s "${OPENFGA_URL}/healthz" > /dev/null 2>&1; then
        print_success "OpenFGA health endpoint is responding"
        echo "   URL: ${OPENFGA_URL}/healthz"

        # Try to get stores
        if STORES=$(curl -s "${OPENFGA_URL}/stores" 2>&1); then
            STORE_COUNT=$(echo "$STORES" | grep -o '"id"' | wc -l)
            print_success "OpenFGA stores endpoint accessible (${STORE_COUNT} stores)"
        fi
    else
        print_error "OpenFGA health endpoint is NOT responding"
        echo "   URL: ${OPENFGA_URL}/healthz"
    fi
    echo ""
fi

# Test 2: SQL Server Connectivity
if [[ "$TEST_FILTER" == "all" || "$TEST_FILTER" == "sql" ]]; then
    echo "=========================================="
    echo "TEST: SQL Server Connectivity"
    echo "=========================================="

    # Extract server from connection string
    if [ -n "$OPENFGA_DATASTORE_URI" ]; then
        SQL_SERVER=$(echo "$OPENFGA_DATASTORE_URI" | sed -n 's|.*sqlserver://\([^:;/]*\).*|\1|p')

        if [ -n "$SQL_SERVER" ]; then
            echo "SQL Server: $SQL_SERVER"

            # DNS resolution test
            echo "Testing DNS resolution..."
            if host "$SQL_SERVER" &> /dev/null; then
                IP=$(host "$SQL_SERVER" | grep "has address" | head -1 | awk '{print $4}')
                print_success "DNS resolved to: $IP"
            else
                print_warning "DNS resolution failed or host command not available"
            fi

            # TCP connectivity test (port 1433)
            echo "Testing TCP connectivity on port 1433..."
            if timeout 5 bash -c "cat < /dev/null > /dev/tcp/$SQL_SERVER/1433" 2>/dev/null; then
                print_success "Port 1433 is reachable"
            else
                print_error "Cannot connect to port 1433"
                echo "   Possible causes:"
                echo "   - Firewall blocking connection"
                echo "   - SQL Server not listening"
                echo "   - Network routing issue"
            fi

            # Check if OpenFGA can connect
            echo ""
            echo "Testing OpenFGA database connectivity..."
            if timeout 10 openfga migrate \
                --datastore-engine ${OPENFGA_DATASTORE_ENGINE:-sqlserver} \
                --datastore-uri "$OPENFGA_DATASTORE_URI" 2>&1 | tee /tmp/test-migrate.log; then
                print_success "OpenFGA can connect to database"
            else
                if grep -qi "already exists\|duplicate" /tmp/test-migrate.log 2>/dev/null; then
                    print_success "Database already initialized (this is OK)"
                else
                    print_error "OpenFGA cannot connect to database"
                    echo ""
                    echo "Last 10 lines of migration output:"
                    tail -10 /tmp/test-migrate.log 2>/dev/null || echo "(no output)"
                fi
            fi
        else
            print_warning "Could not extract server name from OPENFGA_DATASTORE_URI"
        fi
    else
        print_error "OPENFGA_DATASTORE_URI not set"
    fi
    echo ""
fi

# Test 3: .NET API Connectivity
if [[ "$TEST_FILTER" == "all" || "$TEST_FILTER" == "api" ]]; then
    echo "=========================================="
    echo "TEST: .NET API Connectivity"
    echo "=========================================="

    API_PORT="${WEBSITES_PORT:-80}"
    echo "Testing API on port ${API_PORT}..."

    # Check if Functions host is running
    if pgrep -f "Microsoft.Azure.WebJobs" > /dev/null; then
        print_success "Azure Functions host is running"
    else
        print_warning "Azure Functions host process not detected"
    fi

    # Test health endpoint
    if curl -f -s "http://localhost:${API_PORT}/api/health" > /dev/null 2>&1; then
        print_success "API health endpoint is responding"

        # Get verbose health info
        HEALTH_RESPONSE=$(curl -s "http://localhost:${API_PORT}/api/health?verbose=true")
        if [ -n "$HEALTH_RESPONSE" ]; then
            echo ""
            echo "Verbose health check:"
            echo "$HEALTH_RESPONSE" | jq '.' 2>/dev/null || echo "$HEALTH_RESPONSE"
        fi
    else
        print_error "API health endpoint is NOT responding"
        echo "   Tried: http://localhost:${API_PORT}/api/health"
    fi
    echo ""
fi

# Test 4: Configuration Check
if [[ "$TEST_FILTER" == "all" || "$TEST_FILTER" == "config" ]]; then
    echo "=========================================="
    echo "TEST: Environment Configuration"
    echo "=========================================="

    # Check critical environment variables
    check_env_var() {
        local var_name="$1"
        local var_value="${!var_name}"

        if [ -n "$var_value" ]; then
            if [[ "$var_name" == *"PASSWORD"* ]] || [[ "$var_name" == *"SECRET"* ]] || [[ "$var_name" == *"URI"* ]]; then
                print_success "${var_name}: [SET - ${#var_value} chars]"
            else
                print_success "${var_name}: ${var_value}"
            fi
        else
            print_warning "${var_name}: [NOT SET]"
        fi
    }

    echo "Azure Functions Configuration:"
    check_env_var "FUNCTIONS_WORKER_RUNTIME"
    check_env_var "AzureWebJobsScriptRoot"
    check_env_var "WEBSITES_PORT"
    echo ""

    echo "Database Configuration:"
    check_env_var "DOTNET_CONNECTION_STRING"
    echo ""

    echo "OpenFGA Configuration:"
    check_env_var "OPENFGA_API_URL"
    check_env_var "OPENFGA_STORE_ID"
    check_env_var "OPENFGA_DATASTORE_ENGINE"
    check_env_var "OPENFGA_DATASTORE_URI"
    echo ""

    # Check if running in Azure
    if [ -n "$WEBSITE_SITE_NAME" ]; then
        print_success "Running in Azure"
        echo "   Site Name: $WEBSITE_SITE_NAME"
    else
        echo "Running locally"
    fi
    echo ""
fi

echo "=========================================="
echo "Test Suite Complete"
echo "=========================================="
echo ""
echo "Available test filters:"
echo "  ./test-connections.sh all      - Run all tests (default)"
echo "  ./test-connections.sh openfga  - Test OpenFGA only"
echo "  ./test-connections.sh sql      - Test SQL connectivity only"
echo "  ./test-connections.sh api      - Test .NET API only"
echo "  ./test-connections.sh config   - Check configuration only"
echo ""
echo "For more detailed diagnostics, try:"
echo "  curl http://localhost:\${WEBSITES_PORT:-80}/api/debug/config"
echo "  curl http://localhost:\${WEBSITES_PORT:-80}/api/debug/sql-test"
echo "  curl http://localhost:\${WEBSITES_PORT:-80}/api/health?verbose=true"
