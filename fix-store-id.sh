#!/bin/bash

# Fix OPENFGA_STORE_ID by querying SQL Server and updating Function App settings

set -e

echo "=========================================="
echo "Fix OPENFGA_STORE_ID Configuration"
echo "=========================================="
echo ""

# Load SQL credentials
if [ -f .env.azure-sql ]; then
    source .env.azure-sql
else
    echo "❌ .env.azure-sql not found!"
    echo "Run: ./provision-azure-sql.sh first"
    exit 1
fi

# Parse connection string to get components
SQL_SERVER=$(echo $OPENFGA_DATASTORE_URI | sed -n 's/.*@\([^:]*\):.*/\1/p')
SQL_DB=$(echo $OPENFGA_DATASTORE_URI | sed -n 's/.*database=\([^&]*\).*/\1/p')

echo "SQL Server: $SQL_SERVER"
echo "Database: $SQL_DB"
echo "User: $SQL_ADMIN_USER"
echo ""

# Query for store ID
echo "Querying OpenFGA stores..."

# Create SQL query file
cat > /tmp/query-store.sql <<'EOF'
SELECT TOP 1 id, name, created_at
FROM stores
WHERE name = 'menu-app'
ORDER BY created_at DESC;
EOF

# Check if sqlcmd is available
if command -v sqlcmd &> /dev/null; then
    echo "Using sqlcmd to query database..."

    STORE_ID=$(sqlcmd -S "$SQL_SERVER" \
        -d "$SQL_DB" \
        -U "$SQL_ADMIN_USER" \
        -P "$SQL_ADMIN_PASSWORD" \
        -i /tmp/query-store.sql \
        -h -1 \
        -W \
        | grep -v '^$' \
        | head -1 \
        | awk '{print $1}')

    if [ -z "$STORE_ID" ]; then
        echo "❌ No store found with name 'menu-app'"
        echo ""
        echo "This means the container never successfully initialized OpenFGA."
        echo "Possible causes:"
        echo "  1. SQL connection failed"
        echo "  2. Migrations failed"
        echo "  3. Store creation failed"
        echo ""
        echo "Check container logs with: az webapp log download ..."
        exit 1
    fi

    echo "✅ Found store ID: $STORE_ID"
    echo ""

    # Update Function App settings
    echo "Updating Function App settings..."
    az functionapp config appsettings set \
        --name func-menu-app-18436 \
        --resource-group rg-menu-app \
        --settings "OPENFGA_STORE_ID=$STORE_ID" \
        --output none

    echo "✅ OPENFGA_STORE_ID set to: $STORE_ID"
    echo ""

    # Restart Function App
    echo "Restarting Function App..."
    az functionapp restart \
        --name func-menu-app-18436 \
        --resource-group rg-menu-app \
        --output none

    echo "✅ Function App restarted"
    echo ""
    echo "=========================================="
    echo "Fix Applied!"
    echo "=========================================="
    echo ""
    echo "Wait 1-2 minutes for the container to start, then test:"
    echo "  curl 'https://func-menu-app-18436.azurewebsites.net/api/menu?user=alice'"
    echo ""

else
    echo "❌ sqlcmd not installed"
    echo ""
    echo "Install options:"
    echo "  macOS: brew install sqlcmd"
    echo "  Linux: apt-get install mssql-tools"
    echo ""
    echo "Or manually query the database and set OPENFGA_STORE_ID:"
    echo "  1. Connect to: $SQL_SERVER"
    echo "  2. Query: SELECT id FROM stores WHERE name='menu-app'"
    echo "  3. Run: az functionapp config appsettings set \\"
    echo "            --name func-menu-app-18436 \\"
    echo "            --resource-group rg-menu-app \\"
    echo "            --settings 'OPENFGA_STORE_ID=<store-id-from-query>'"
    echo ""
    exit 1
fi
