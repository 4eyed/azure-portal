#!/bin/bash

# Azure Functions Deployment Script
# Deploys your custom container to Azure App Service Plan (B1)
# Cost: ~$20/month for prototype

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║    Azure Functions Container Deployment                ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""

# ============================================================================
# CONFIGURATION - Edit these values
# ============================================================================

# Resource names (must be globally unique for some resources)
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-menu-app}"
LOCATION="${LOCATION:-eastus}"
ACR_NAME="${ACR_NAME:-acrmenuapp$RANDOM}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-stmenuapp$RANDOM}"
APP_SERVICE_PLAN="${APP_SERVICE_PLAN:-plan-menu-app}"
FUNCTION_APP="${FUNCTION_APP:-func-menu-app-$RANDOM}"

# Container image
IMAGE_NAME="menu-app-combined"
IMAGE_TAG="latest"

echo -e "${YELLOW}Configuration:${NC}"
echo "  Resource Group:    $RESOURCE_GROUP"
echo "  Location:          $LOCATION"
echo "  ACR Name:          $ACR_NAME"
echo "  Storage Account:   $STORAGE_ACCOUNT"
echo "  App Service Plan:  $APP_SERVICE_PLAN"
echo "  Function App:      $FUNCTION_APP"
echo ""

# ============================================================================
# Prerequisites Check
# ============================================================================

echo -e "${BLUE}Checking prerequisites...${NC}"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}✗ Azure CLI not found. Please install it first.${NC}"
    echo "  Visit: https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi
echo -e "${GREEN}✓ Azure CLI found${NC}"

# Check if Podman is installed
if ! command -v podman &> /dev/null; then
    echo -e "${RED}✗ Podman not found. Please install it first.${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Podman found${NC}"

# Check if logged into Azure
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}Not logged into Azure. Running 'az login'...${NC}"
    az login
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
echo -e "${GREEN}✓ Logged into Azure${NC}"
echo "  Subscription: $SUBSCRIPTION"
echo ""

# ============================================================================
# Step 1: Create Resource Group
# ============================================================================

echo -e "${BLUE}Step 1: Creating resource group...${NC}"
az group create \
    --name $RESOURCE_GROUP \
    --location $LOCATION \
    --output none

echo -e "${GREEN}✓ Resource group created${NC}"
echo ""

# ============================================================================
# Step 2: Create Azure Container Registry
# ============================================================================

echo -e "${BLUE}Step 2: Creating Azure Container Registry...${NC}"
az acr create \
    --resource-group $RESOURCE_GROUP \
    --name $ACR_NAME \
    --sku Basic \
    --admin-enabled true \
    --output none

echo -e "${GREEN}✓ ACR created: $ACR_NAME.azurecr.io${NC}"
echo ""

# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

echo -e "${GREEN}✓ ACR credentials retrieved${NC}"
echo ""

# ============================================================================
# Step 3: Build and Push Container Image
# ============================================================================

echo -e "${BLUE}Step 3: Building and pushing container image...${NC}"

# Login to ACR with Podman
echo "$ACR_PASSWORD" | podman login $ACR_NAME.azurecr.io -u $ACR_USERNAME --password-stdin

echo -e "${YELLOW}Building image (this may take 5-10 minutes)...${NC}"
podman build \
    --platform=linux/amd64 \
    -f Dockerfile.combined \
    -t $IMAGE_NAME:$IMAGE_TAG \
    .

# Tag for ACR
podman tag $IMAGE_NAME:$IMAGE_TAG $ACR_NAME.azurecr.io/$IMAGE_NAME:$IMAGE_TAG

echo -e "${YELLOW}Pushing image to ACR...${NC}"
podman push $ACR_NAME.azurecr.io/$IMAGE_NAME:$IMAGE_TAG

echo -e "${GREEN}✓ Image pushed to ACR${NC}"
echo ""

# ============================================================================
# Step 4: Create Storage Account
# ============================================================================

echo -e "${BLUE}Step 4: Creating storage account...${NC}"
az storage account create \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku Standard_LRS \
    --output none

echo -e "${GREEN}✓ Storage account created${NC}"
echo ""

# ============================================================================
# Step 5: Create App Service Plan (B1)
# ============================================================================

echo -e "${BLUE}Step 5: Creating App Service Plan (B1 - ~\$13/month)...${NC}"
az appservice plan create \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --is-linux \
    --sku B1 \
    --output none

echo -e "${GREEN}✓ App Service Plan created (B1 tier)${NC}"
echo ""

# ============================================================================
# Step 6: Create Function App with Custom Container
# ============================================================================

echo -e "${BLUE}Step 6: Creating Function App with custom container...${NC}"
az functionapp create \
    --name $FUNCTION_APP \
    --resource-group $RESOURCE_GROUP \
    --plan $APP_SERVICE_PLAN \
    --storage-account $STORAGE_ACCOUNT \
    --functions-version 4 \
    --runtime custom \
    --deployment-container-image-name $ACR_NAME.azurecr.io/$IMAGE_NAME:$IMAGE_TAG \
    --docker-registry-server-user $ACR_USERNAME \
    --docker-registry-server-password $ACR_PASSWORD \
    --output none

echo -e "${GREEN}✓ Function App created${NC}"
echo ""

# ============================================================================
# Step 7: Configure Application Settings
# ============================================================================

echo -e "${BLUE}Step 7: Configuring application settings...${NC}"

# Get storage account connection string for Azure Functions runtime
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --query connectionString \
    -o tsv)

# Note: OpenFGA store will be created on container startup
# We're setting empty OPENFGA_STORE_ID - the container will create one
az functionapp config appsettings set \
    --name $FUNCTION_APP \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "WEBSITES_ENABLE_APP_SERVICE_STORAGE=false" \
        "WEBSITES_PORT=80" \
        "WEBSITES_HEALTHCHECK_MAXPINGFAILURES=10" \
        "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
        "AzureWebJobsStorage=$STORAGE_CONNECTION_STRING" \
        "OPENFGA_API_URL=http://localhost:8080" \
        "OPENFGA_STORE_ID=" \
        "OPENFGA_DATASTORE_ENGINE=memory" \
    --output none

echo -e "${GREEN}✓ Application settings configured${NC}"
echo ""

# ============================================================================
# Step 8: Enable CORS
# ============================================================================

echo -e "${BLUE}Step 8: Enabling CORS...${NC}"
az functionapp cors add \
    --name $FUNCTION_APP \
    --resource-group $RESOURCE_GROUP \
    --allowed-origins "*" \
    --output none

echo -e "${GREEN}✓ CORS enabled${NC}"
echo ""

# ============================================================================
# Step 9: Restart Function App
# ============================================================================

echo -e "${BLUE}Step 9: Restarting Function App...${NC}"
az functionapp restart \
    --name $FUNCTION_APP \
    --resource-group $RESOURCE_GROUP \
    --output none

echo -e "${GREEN}✓ Function App restarted${NC}"
echo ""

# ============================================================================
# Get Function App URL
# ============================================================================

FUNCTION_URL=$(az functionapp show \
    --name $FUNCTION_APP \
    --resource-group $RESOURCE_GROUP \
    --query defaultHostName \
    -o tsv)

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              🎉 Deployment Complete!                     ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${BLUE}Your Function App is deployed at:${NC}"
echo -e "  ${GREEN}https://$FUNCTION_URL${NC}"
echo ""
echo -e "${BLUE}API Endpoints:${NC}"
echo -e "  Menu API: ${GREEN}https://$FUNCTION_URL/api/menu?user=alice${NC}"
echo ""
echo -e "${BLUE}Test it:${NC}"
echo -e "  ${YELLOW}curl \"https://$FUNCTION_URL/api/menu?user=alice\"${NC}"
echo ""
echo -e "${BLUE}Azure Portal:${NC}"
echo -e "  https://portal.azure.com/#resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$FUNCTION_APP"
echo ""
echo -e "${YELLOW}⚠️  Note:${NC} First request may take 30-60 seconds as the container starts."
echo ""
echo -e "${BLUE}Next Steps:${NC}"
echo "  1. Wait 1-2 minutes for container to fully start"
echo "  2. Test the API endpoint above"
echo "  3. Update your frontend/index.html with the new URL:"
echo -e "     ${YELLOW}const API_URL = 'https://$FUNCTION_URL/api';${NC}"
echo ""
echo -e "${BLUE}Monthly Cost Estimate:${NC}"
echo "  App Service Plan (B1):    ~\$13/month"
echo "  Container Registry:       ~\$5/month"
echo "  Storage Account:          ~\$0.50/month"
echo "  ${GREEN}Total: ~\$20/month${NC}"
echo ""
echo -e "${BLUE}To delete all resources:${NC}"
echo -e "  ${RED}az group delete --name $RESOURCE_GROUP --yes --no-wait${NC}"
echo ""
