#!/bin/bash
# Create Azure Static Web App connected to GitHub
# Run this script manually in your terminal to handle GitHub authentication interactively

set -e

echo "🌐 Creating Azure Static Web App..."
echo ""
echo "⚠️  You will need to authenticate with GitHub"
echo "   Follow the prompts to authorize Azure"
echo ""

az staticwebapp create \
  --name stapp-menu-app \
  --resource-group rg-menu-app \
  --source https://github.com/4eyed/azure-portal.git \
  --location eastus2 \
  --branch main \
  --app-location "/frontend" \
  --output-location "." \
  --login-with-github

echo ""
echo "✅ Static Web App created!"
echo ""
echo "Getting deployment URL..."
URL=$(az staticwebapp show \
  --name stapp-menu-app \
  --resource-group rg-menu-app \
  --query "defaultHostname" \
  -o tsv)

echo ""
echo "🎉 Your Static Web App is deployed at:"
echo "   https://$URL"
echo ""
echo "GitHub Actions workflow has been automatically added to your repo!"
echo "Check: https://github.com/4eyed/azure-portal/actions"
echo ""
