#!/bin/bash

# Monitor Azure Function App Startup
# Usage: ./monitor-azure-startup.sh

FUNCTION_APP="func-menu-app-18436"
RESOURCE_GROUP="rg-menu-app"
FUNCTION_URL="https://func-menu-app-18436.azurewebsites.net"

echo "=============================================="
echo "Monitoring Azure Function App Startup"
echo "=============================================="
echo "Function App: $FUNCTION_APP"
echo "URL: $FUNCTION_URL"
echo ""

# Check if container is running
echo "📊 Function App Status:"
az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query '{state:state,availabilityState:availabilityState}' \
  -o table
echo ""

# Test health endpoint with retries
echo "🏥 Testing Health Endpoint (will retry for up to 3 minutes)..."
TIMEOUT=180
INTERVAL=10
ELAPSED=0

while [ $ELAPSED -lt $TIMEOUT ]; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$FUNCTION_URL/api/health" 2>/dev/null || echo "000")

    if [ "$HTTP_CODE" = "200" ]; then
        echo "✅ SUCCESS! Container is healthy (took ${ELAPSED}s)"
        echo ""
        echo "Response:"
        curl -s "$FUNCTION_URL/api/health" | jq '.' 2>/dev/null || curl -s "$FUNCTION_URL/api/health"
        echo ""
        break
    elif [ "$HTTP_CODE" != "000" ]; then
        echo "⏳ Got HTTP $HTTP_CODE, waiting for 200... (${ELAPSED}s / ${TIMEOUT}s)"
    else
        echo "⏳ Container still starting up... (${ELAPSED}s / ${TIMEOUT}s)"
    fi

    sleep $INTERVAL
    ELAPSED=$((ELAPSED + $INTERVAL))
done

if [ "$HTTP_CODE" != "200" ]; then
    echo ""
    echo "❌ Container did not respond with 200 after ${TIMEOUT}s"
    echo "Last HTTP code: $HTTP_CODE"
    echo ""
    echo "🔍 To view container logs:"
    echo "   Visit: https://$FUNCTION_APP.scm.azurewebsites.net"
    echo "   Or run: az webapp log tail --name $FUNCTION_APP --resource-group $RESOURCE_GROUP"
    exit 1
fi

echo ""
echo "=============================================="
echo "✅ Container Started Successfully!"
echo "=============================================="
echo ""
echo "📋 Test Endpoints:"
echo "  Health: $FUNCTION_URL/api/health"
echo "  Admin Check: $FUNCTION_URL/api/admin/check"
echo "  Menu Structure: $FUNCTION_URL/api/menu-structure"
echo ""
echo "🔍 View Logs:"
echo "  Kudu: https://$FUNCTION_APP.scm.azurewebsites.net"
echo "  Stream logs: az webapp log tail --name $FUNCTION_APP --resource-group $RESOURCE_GROUP"
echo ""
