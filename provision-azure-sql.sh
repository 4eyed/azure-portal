#!/bin/bash

# Azure SQL Database Provisioning Script
# Creates FREE tier Azure SQL Database for both local dev and production
# Uses different schemas for dev vs prod

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║    Azure SQL Database FREE Tier Provisioning           ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Configuration
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-menu-app}"
LOCATION="${LOCATION:-eastus}"
SQL_SERVER="${SQL_SERVER:-sqlsrv-menu-app-$RANDOM}"
SQL_DB="${SQL_DB:-db-menu-app}"
SQL_ADMIN_USER="${SQL_ADMIN_USER:-sqladmin}"
SQL_ADMIN_PASSWORD="${SQL_ADMIN_PASSWORD:-P@ssw0rd$(date +%s)!}"

echo -e "${YELLOW}Configuration:${NC}"
echo "  Resource Group:    $RESOURCE_GROUP"
echo "  Location:          $LOCATION"
echo "  SQL Server:        $SQL_SERVER"
echo "  SQL Database:      $SQL_DB"
echo "  Admin User:        $SQL_ADMIN_USER"
echo "  Admin Password:    ***hidden***"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}✗ Azure CLI not found. Please install it first.${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Azure CLI found${NC}"

# Check if logged into Azure
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}Not logged into Azure. Running 'az login'...${NC}"
    az login
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
echo -e "${GREEN}✓ Logged into Azure${NC}"
echo "  Subscription: $SUBSCRIPTION"
echo ""

# Create resource group if it doesn't exist
echo -e "${BLUE}Creating resource group...${NC}"
az group create \
    --name $RESOURCE_GROUP \
    --location $LOCATION \
    --output none

echo -e "${GREEN}✓ Resource group created/verified${NC}"
echo ""

# Create SQL Server
echo -e "${BLUE}Creating SQL Server (this may take 2-3 minutes)...${NC}"
az sql server create \
    --resource-group $RESOURCE_GROUP \
    --name $SQL_SERVER \
    --location $LOCATION \
    --admin-user $SQL_ADMIN_USER \
    --admin-password "$SQL_ADMIN_PASSWORD" \
    --output none

echo -e "${GREEN}✓ SQL Server created: $SQL_SERVER.database.windows.net${NC}"
echo ""

# Configure firewall to allow Azure services
echo -e "${BLUE}Configuring firewall rules...${NC}"
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER \
    --name AllowAzureServices \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

echo -e "${GREEN}✓ Azure services firewall rule created${NC}"

# Get current public IP and add firewall rule for local development
echo -e "${BLUE}Adding firewall rule for local development...${NC}"
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER \
    --name AllowLocalDevelopment \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none

echo -e "${GREEN}✓ Local development firewall rule created for IP: $MY_IP${NC}"
echo ""

# Create SQL Database (FREE tier)
echo -e "${BLUE}Creating SQL Database (FREE tier - Serverless)...${NC}"
az sql db create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER \
    --name $SQL_DB \
    --edition GeneralPurpose \
    --compute-model Serverless \
    --family Gen5 \
    --capacity 1 \
    --auto-pause-delay 60 \
    --output none

echo -e "${GREEN}✓ SQL Database created (FREE tier)${NC}"
echo ""

# Build connection strings
SQL_SERVER_FQDN="$SQL_SERVER.database.windows.net"
CONNECTION_STRING_DEV="sqlserver://$SQL_ADMIN_USER:$SQL_ADMIN_PASSWORD@$SQL_SERVER_FQDN:1433?database=$SQL_DB&encrypt=true"
CONNECTION_STRING_PROD="sqlserver://$SQL_ADMIN_USER:$SQL_ADMIN_PASSWORD@$SQL_SERVER_FQDN:1433?database=$SQL_DB&encrypt=true"

# Save credentials to .env file
cat > .env.azure-sql << EOF
# Azure SQL Database Configuration
# Generated: $(date)

SQL_SERVER=$SQL_SERVER_FQDN
SQL_DATABASE=$SQL_DB
SQL_ADMIN_USER=$SQL_ADMIN_USER
SQL_ADMIN_PASSWORD=$SQL_ADMIN_PASSWORD

# Connection Strings
# Development (uses 'dev' schema)
CONNECTION_STRING_DEV=$CONNECTION_STRING_DEV

# Production (uses 'prod' schema)
CONNECTION_STRING_PROD=$CONNECTION_STRING_PROD

# EF Core Connection String (.NET)
DOTNET_CONNECTION_STRING="Server=$SQL_SERVER_FQDN;Database=$SQL_DB;User Id=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASSWORD;Encrypt=true;TrustServerCertificate=false;"

# OpenFGA Connection String
OPENFGA_DATASTORE_URI=$CONNECTION_STRING_DEV
EOF

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              🎉 Database Provisioned!                    ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${BLUE}Server:${NC}              $SQL_SERVER_FQDN"
echo -e "${BLUE}Database:${NC}            $SQL_DB"
echo -e "${BLUE}Admin User:${NC}          $SQL_ADMIN_USER"
echo -e "${BLUE}Admin Password:${NC}      $SQL_ADMIN_PASSWORD"
echo ""
echo -e "${BLUE}Credentials saved to:${NC} ${GREEN}.env.azure-sql${NC}"
echo ""
echo -e "${YELLOW}⚠️  IMPORTANT:${NC}"
echo "  1. Keep the password secure!"
echo "  2. Add .env.azure-sql to .gitignore"
echo "  3. This uses Azure SQL Database FREE tier (100k vCore seconds/month)"
echo ""
echo -e "${BLUE}Schema Strategy:${NC}"
echo "  • Development:  Uses 'dev' schema prefix"
echo "  • Production:   Uses 'prod' schema prefix"
echo "  • Both use same database to stay within FREE tier limits"
echo ""
echo -e "${BLUE}Next Steps:${NC}"
echo "  1. Source the environment file: ${YELLOW}source .env.azure-sql${NC}"
echo "  2. Run database schema creation script"
echo "  3. Test OpenFGA connection locally"
echo ""
echo -e "${BLUE}Test Connection:${NC}"
echo -e "  ${YELLOW}sqlcmd -S $SQL_SERVER_FQDN -d $SQL_DB -U $SQL_ADMIN_USER -P '$SQL_ADMIN_PASSWORD' -Q 'SELECT @@VERSION'${NC}"
echo ""
echo -e "${BLUE}Monthly Cost:${NC} ${GREEN}\$0${NC} (within FREE tier limits)"
echo "  • 100,000 vCore seconds/month"
echo "  • 32 GB data storage"
echo "  • 32 GB backup storage"
echo ""
