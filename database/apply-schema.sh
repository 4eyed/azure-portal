#!/bin/bash
# Script to apply application schema to Azure SQL Database
# Requires sqlcmd to be installed

set -e

echo "Applying application database schema to Azure SQL..."

# Load environment variables
if [ -f ../.env.azure-sql ]; then
    source ../.env.azure-sql
else
    echo "ERROR: .env.azure-sql not found"
    exit 1
fi

# Check if sqlcmd is available
if ! command -v sqlcmd &> /dev/null; then
    echo "ERROR: sqlcmd is not installed"
    echo "Install with: brew install microsoft/mssql-release/mssql-tools (macOS)"
    echo "Or visit: https://learn.microsoft.com/en-us/sql/tools/sqlcmd-utility"
    exit 1
fi

# Apply schema
echo "Connecting to: $SQL_SERVER"
echo "Database: $SQL_DATABASE"

sqlcmd -S "$SQL_SERVER" -d "$SQL_DATABASE" -U "$SQL_ADMIN_USER" -P "$SQL_ADMIN_PASSWORD" -i app-schema.sql -C

echo "✅ Application schema applied successfully!"
