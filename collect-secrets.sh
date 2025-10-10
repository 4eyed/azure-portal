#!/bin/bash
# collect-secrets.sh - Collect all GitHub Actions secret values

set -e

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-menu-app}"

echo "================================================"
echo "GitHub Actions Secrets Collection"
echo "================================================"
echo ""

# Check if logged into Azure
if ! az account show &> /dev/null; then
    echo "❌ Not logged into Azure. Run 'az login' first."
    exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

echo "📋 Current Azure Context:"
echo "   Subscription: $SUBSCRIPTION_NAME"
echo "   Subscription ID: $SUBSCRIPTION_ID"
echo "   Resource Group: $RESOURCE_GROUP"
echo ""

# Check if resource group exists
if ! az group show --name $RESOURCE_GROUP &> /dev/null; then
    echo "❌ Resource group '$RESOURCE_GROUP' not found!"
    echo "   Update RESOURCE_GROUP variable or create it first."
    exit 1
fi

echo "================================================"
echo "Required GitHub Secrets"
echo "================================================"
echo ""

# 1. Azure Credentials
echo "1️⃣  AZURE_CREDENTIALS"
echo "   Description: Service Principal for Azure CLI authentication"
echo "   ---"
echo "   Run this command and copy the ENTIRE JSON output:"
echo ""
echo "   az ad sp create-for-rbac \\"
echo "     --name github-actions-openfga \\"
echo "     --role contributor \\"
echo "     --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP \\"
echo "     --sdk-auth"
echo ""
echo "   ⚠️  You'll need to run this command manually (requires admin permissions)"
echo ""
read -p "Press Enter to continue..."
echo ""

# 2. ACR Name
echo "2️⃣  ACR_NAME"
echo "   Description: Azure Container Registry name"
ACR_NAME=$(az acr list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -z "$ACR_NAME" ]; then
    echo "   ❌ No ACR found in resource group!"
    echo "   Create one first with: az acr create --name <name> --resource-group $RESOURCE_GROUP --sku Basic"
else
    echo "   ✅ Value: $ACR_NAME"
fi
echo ""

# 3. ACR Username
echo "3️⃣  ACR_USERNAME"
echo "   Description: ACR admin username"
if [ -n "$ACR_NAME" ]; then
    ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv 2>/dev/null || echo "")
    if [ -n "$ACR_USERNAME" ]; then
        echo "   ✅ Value: $ACR_USERNAME"
    else
        echo "   ❌ Could not retrieve ACR credentials"
    fi
else
    echo "   ⏭️  Skipped (no ACR found)"
fi
echo ""

# 4. ACR Password
echo "4️⃣  ACR_PASSWORD"
echo "   Description: ACR admin password"
if [ -n "$ACR_NAME" ]; then
    ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv 2>/dev/null || echo "")
    if [ -n "$ACR_PASSWORD" ]; then
        echo "   ✅ Value: $ACR_PASSWORD"
    else
        echo "   ❌ Could not retrieve ACR password"
    fi
else
    echo "   ⏭️  Skipped (no ACR found)"
fi
echo ""

# 5. Function App Name
echo "5️⃣  AZURE_FUNCTIONAPP_NAME"
echo "   Description: Azure Function App name"
FUNC_NAME=$(az functionapp list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -z "$FUNC_NAME" ]; then
    echo "   ❌ No Function App found in resource group!"
    echo "   Create one first or run: ./deploy-to-azure.sh"
else
    echo "   ✅ Value: $FUNC_NAME"
fi
echo ""

# 6. Resource Group
echo "6️⃣  AZURE_RESOURCE_GROUP"
echo "   Description: Azure Resource Group name"
echo "   ✅ Value: $RESOURCE_GROUP"
echo ""

# 7. SQL Connection String
echo "7️⃣  SQL_CONNECTION_STRING"
echo "   Description: Azure SQL connection string for OpenFGA"
echo "   ---"

# Try to load from .env.azure-sql
if [ -f .env.azure-sql ]; then
    source .env.azure-sql
    if [ -n "$OPENFGA_DATASTORE_URI" ]; then
        echo "   ✅ Value: $OPENFGA_DATASTORE_URI"
        echo "   (Loaded from .env.azure-sql)"
    else
        echo "   ❌ OPENFGA_DATASTORE_URI not found in .env.azure-sql"
    fi
else
    echo "   ❌ .env.azure-sql file not found!"
    echo "   Run: ./provision-azure-sql.sh"
fi
echo ""

# 8. Static Web App Token (already configured)
echo "8️⃣  AZURE_STATIC_WEB_APPS_API_TOKEN_WITTY_FLOWER_068DE881E"
echo "   Description: Static Web App deployment token"
echo "   ✅ Already configured (used by existing workflow)"
echo ""

echo "================================================"
echo "Summary"
echo "================================================"
echo ""

# Create a summary
MISSING=0
FOUND=0

[ -n "$ACR_NAME" ] && ((FOUND++)) || ((MISSING++))
[ -n "$ACR_USERNAME" ] && ((FOUND++)) || ((MISSING++))
[ -n "$ACR_PASSWORD" ] && ((FOUND++)) || ((MISSING++))
[ -n "$FUNC_NAME" ] && ((FOUND++)) || ((MISSING++))
[ -n "$OPENFGA_DATASTORE_URI" ] && ((FOUND++)) || ((MISSING++))

echo "✅ Found: $FOUND/5 secrets"
if [ $MISSING -gt 0 ]; then
    echo "❌ Missing: $MISSING/5 secrets"
    echo ""
    echo "⚠️  You need to create/configure the missing resources first!"
fi

echo ""
echo "================================================"
echo "Next Steps"
echo "================================================"
echo ""
echo "1. Create Azure Service Principal (if not done):"
echo "   - Run the az ad sp command shown above"
echo "   - Copy the entire JSON output"
echo ""
echo "2. Go to GitHub repository → Settings → Secrets → Actions"
echo ""
echo "3. Click 'New repository secret' and add each secret:"
echo "   - Name: AZURE_CREDENTIALS"
echo "     Value: <entire JSON from service principal>"
echo ""
if [ -n "$ACR_NAME" ]; then
echo "   - Name: ACR_NAME"
echo "     Value: $ACR_NAME"
echo ""
fi
if [ -n "$ACR_USERNAME" ]; then
echo "   - Name: ACR_USERNAME"
echo "     Value: $ACR_USERNAME"
echo ""
fi
if [ -n "$ACR_PASSWORD" ]; then
echo "   - Name: ACR_PASSWORD"
echo "     Value: $ACR_PASSWORD"
echo ""
fi
if [ -n "$FUNC_NAME" ]; then
echo "   - Name: AZURE_FUNCTIONAPP_NAME"
echo "     Value: $FUNC_NAME"
echo ""
fi
echo "   - Name: AZURE_RESOURCE_GROUP"
echo "     Value: $RESOURCE_GROUP"
echo ""
if [ -n "$OPENFGA_DATASTORE_URI" ]; then
echo "   - Name: SQL_CONNECTION_STRING"
echo "     Value: $OPENFGA_DATASTORE_URI"
echo ""
fi
echo "4. Test the workflow:"
echo "   - Go to Actions tab → Azure Backend Deploy → Run workflow"
echo ""
echo "================================================"
