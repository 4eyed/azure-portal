#!/bin/bash
# Test different SQL authentication methods
# Usage: ./test-sql-auth.sh

set -e

echo "=========================================="
echo "SQL Authentication Method Tester"
echo "=========================================="
echo "This script tests various SQL authentication methods"
echo "to help diagnose connectivity issues in Azure"
echo ""

# Get connection string from environment
CONN_STR="${DOTNET_CONNECTION_STRING:-${OPENFGA_DATASTORE_URI}}"

if [ -z "$CONN_STR" ]; then
    echo "❌ ERROR: No connection string found"
    echo "   Set DOTNET_CONNECTION_STRING or OPENFGA_DATASTORE_URI"
    exit 1
fi

# Extract connection details (assuming sqlserver:// format)
extract_value() {
    local uri="$1"
    local param="$2"
    echo "$uri" | sed -n "s/.*${param}=\([^;]*\).*/\1/p"
}

# Parse sqlserver:// URI format
if [[ "$CONN_STR" == sqlserver://* ]]; then
    echo "Detected sqlserver:// URI format"

    # Extract server and database
    SERVER=$(echo "$CONN_STR" | sed -n 's|sqlserver://\([^/]*\)/.*|\1|p')
    DATABASE=$(echo "$CONN_STR" | sed -n 's|sqlserver://[^/]*/\([^?]*\).*|\1|p')

    echo "  Server: $SERVER"
    echo "  Database: $DATABASE"

    # Extract query parameters
    QUERY_STRING=$(echo "$CONN_STR" | sed -n 's|.*?\(.*\)|\1|p')

    # Check authentication method
    if [[ "$QUERY_STRING" == *"fedauth=ActiveDirectoryMSI"* ]]; then
        echo "  Auth: Managed Identity (MSI)"
    elif [[ "$QUERY_STRING" == *"fedauth=ActiveDirectoryManagedIdentity"* ]]; then
        echo "  Auth: Managed Identity"
    elif [[ "$QUERY_STRING" == *"fedauth=ActiveDirectoryDefault"* ]]; then
        echo "  Auth: Active Directory Default"
    else
        echo "  Auth: Unknown/Default"
    fi

    echo ""
    echo "Full connection string (first 100 chars):"
    echo "  ${CONN_STR:0:100}..."
else
    echo "Using ADO.NET connection string format"
    echo "  Connection string (first 100 chars):"
    echo "  ${CONN_STR:0:100}..."
fi

echo ""
echo "=========================================="
echo "Testing OpenFGA Database Connection"
echo "=========================================="
echo ""

# Test 1: Try OpenFGA migrate (validates connectivity)
echo "Method 1: OpenFGA Migrate Test"
echo "----------------------------------------"
echo "Running: openfga migrate --datastore-engine sqlserver --datastore-uri <uri>"
echo ""

if timeout 30 openfga migrate \
    --datastore-engine sqlserver \
    --datastore-uri "$CONN_STR" 2>&1 | tee /tmp/auth-test-migrate.log; then
    echo ""
    echo "✅ SUCCESS: Migration command completed"
else
    EXIT_CODE=$?
    echo ""
    echo "❌ FAILED: Migration command failed (exit code: $EXIT_CODE)"

    # Check for common error patterns
    if grep -qi "login failed\|authentication failed" /tmp/auth-test-migrate.log; then
        echo ""
        echo "🔍 DIAGNOSIS: Authentication Error Detected"
        echo "   This indicates the connection reached SQL Server but authentication failed"
        echo ""
        echo "   Possible causes:"
        echo "   1. Managed Identity not added as SQL user"
        echo "   2. Incorrect password (if using SQL auth)"
        echo "   3. Azure AD authentication not enabled on SQL Server"
        echo ""
        echo "   Fix for Managed Identity:"
        echo "   - Connect to SQL Server with admin account"
        echo "   - Run: CREATE USER [<app-service-name>] FROM EXTERNAL PROVIDER;"
        echo "   - Run: ALTER ROLE db_owner ADD MEMBER [<app-service-name>];"

    elif grep -qi "network\|timeout\|connection refused" /tmp/auth-test-migrate.log; then
        echo ""
        echo "🔍 DIAGNOSIS: Network Connectivity Error"
        echo "   The connection could not reach SQL Server"
        echo ""
        echo "   Possible causes:"
        echo "   1. Firewall rules blocking Azure services"
        echo "   2. Private endpoint or VNET configuration issue"
        echo "   3. SQL Server not accessible from this network"
        echo ""
        echo "   Fix:"
        echo "   - Check Azure SQL firewall rules"
        echo "   - Ensure 'Allow Azure services' is enabled"
        echo "   - Verify VNET configuration if using private endpoint"

    elif grep -qi "invalid\|malformed\|parse" /tmp/auth-test-migrate.log; then
        echo ""
        echo "🔍 DIAGNOSIS: Connection String Format Error"
        echo "   The connection string format is invalid"
        echo ""
        echo "   Expected format for Managed Identity:"
        echo "   sqlserver://SERVER.database.windows.net/DATABASE?fedauth=ActiveDirectoryMSI"
        echo ""
        echo "   Expected format for SQL Auth:"
        echo "   sqlserver://USER:PASSWORD@SERVER.database.windows.net/DATABASE"
    else
        echo ""
        echo "🔍 Unexpected error - showing last 20 lines of log:"
        tail -20 /tmp/auth-test-migrate.log
    fi
fi

echo ""
echo "=========================================="
echo "Recommendations"
echo "=========================================="
echo ""
echo "Based on your configuration:"
echo ""

if [[ "$CONN_STR" == *"fedauth=ActiveDirectory"* ]]; then
    echo "✓ Using Managed Identity authentication"
    echo ""
    echo "Checklist:"
    echo "  [ ] Function App has Managed Identity enabled"
    echo "  [ ] Managed Identity added as SQL user (CREATE USER ... FROM EXTERNAL PROVIDER)"
    echo "  [ ] Managed Identity has db_owner role or appropriate permissions"
    echo "  [ ] SQL Server firewall allows Azure services"
    echo ""
    echo "To test Managed Identity setup:"
    echo "  1. Go to Azure Portal → Function App → Identity → System assigned"
    echo "  2. Note the Object (principal) ID"
    echo "  3. Connect to SQL Server and run:"
    echo "     CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;"
    echo "     ALTER ROLE db_owner ADD MEMBER [<function-app-name>];"
elif [[ "$CONN_STR" == *"@"* ]]; then
    echo "✓ Using username/password authentication"
    echo ""
    echo "Checklist:"
    echo "  [ ] Username and password are correct"
    echo "  [ ] SQL Server allows SQL authentication"
    echo "  [ ] User has appropriate database permissions"
    echo "  [ ] Password doesn't contain special characters that need escaping"
else
    echo "⚠️  Authentication method unclear from connection string"
    echo ""
    echo "For Managed Identity, use:"
    echo "  sqlserver://server.database.windows.net/database?fedauth=ActiveDirectoryMSI"
    echo ""
    echo "For SQL Auth, use:"
    echo "  sqlserver://username:password@server.database.windows.net/database"
fi

echo ""
echo "=========================================="
echo "Next Steps"
echo "=========================================="
echo ""
echo "1. Check detailed logs:"
echo "   cat /tmp/auth-test-migrate.log"
echo ""
echo "2. Test API connectivity:"
echo "   curl http://localhost:\${WEBSITES_PORT:-80}/api/debug/sql-test"
echo ""
echo "3. Check configuration:"
echo "   curl http://localhost:\${WEBSITES_PORT:-80}/api/debug/config"
echo ""
echo "4. View OpenFGA logs:"
echo "   tail -100 /var/log/openfga.log"
