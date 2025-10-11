# Function App Troubleshooting Guide

## Current Issue: API Returning 404/Timeout

### Status
- ✅ Container is running
- ✅ Azure Functions host is responding (Kestrel server)
- ❌ `/api/menu` endpoint times out
- ❌ Other endpoints return 404

### Root Cause Analysis

**Problem:** The `OPENFGA_STORE_ID` environment variable is not set.

**Evidence:**
```bash
$ az functionapp config appsettings list ...
OPENFGA_STORE_ID:  (empty)
```

**Why This Causes Issues:**
1. `start.sh` creates/retrieves the store ID dynamically
2. `start.sh` exports `OPENFGA_STORE_ID=$STORE_ID` (line 68)
3. `start.sh` executes `exec /azure-functions-host/...` (line 108)
4. The `exec` replaces the shell process, environment may not propagate
5. .NET Functions host starts with empty `OPENFGA_STORE_ID`
6. OpenFGA SDK hangs or fails when making requests

### Quick Fix Options

#### Option 1: Manually Set OPENFGA_STORE_ID (Fastest)

1. **Get the Store ID from SQL Server:**
```bash
# Connect to Azure SQL
sqlcmd -S sqlsrv-menu-app-24259.database.windows.net \
  -d db-menu-app \
  -U sqladmin \
  -P 'P@ssw0rd1760128283!' \
  -Q "SELECT id, name FROM stores WHERE name='menu-app'"
```

2. **Set it as an environment variable:**
```bash
# Replace YOUR_STORE_ID with actual ID from above
az functionapp config appsettings set \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app \
  --settings "OPENFGA_STORE_ID=YOUR_STORE_ID"
```

3. **Restart the Function App:**
```bash
az functionapp restart \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app
```

#### Option 2: Fix start.sh to Write ENV File (Proper Fix)

The issue is that `export` in bash doesn't propagate through `exec`. We need to write the store ID to a file that the Functions host can read.

**Update start.sh:**
```bash
# After getting STORE_ID (line 68), write to a file
echo "OPENFGA_STORE_ID=$STORE_ID" >> /home/site/wwwroot/.env

# Update Program.cs to read from .env file if OPENFGA_STORE_ID not set
```

#### Option 3: Use Azure SQL to Store Configuration

Instead of dynamic store creation, use a fixed store ID stored in Azure configuration.

### Diagnostic Commands

#### Check Container Logs
```bash
# Enable logging
az webapp log config \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app \
  --docker-container-logging filesystem \
  --level verbose

# Download logs
az webapp log download \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app \
  --log-file /tmp/logs.zip

# Extract and view
unzip /tmp/logs.zip -d /tmp/logs/
cat /tmp/logs/LogFiles/*_docker.log | tail -200
```

#### Test API Endpoints
```bash
# Test with curl
curl -v "https://func-menu-app-18436.azurewebsites.net/api/menu?user=alice"

# Check HTTP status
curl -I "https://func-menu-app-18436.azurewebsites.net/api/menu?user=alice"

# Test with timeout
timeout 10 curl "https://func-menu-app-18436.azurewebsites.net/api/menu?user=alice"
```

#### Check Function App Status
```bash
# Overall status
az functionapp show \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app \
  --query "{state:state, availabilityState:availabilityState}"

# List functions
az functionapp function list \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app

# Check app settings
az functionapp config appsettings list \
  --name func-menu-app-18436 \
  --resource-group rg-menu-app \
  --query "[?starts_with(name, 'OPENFGA')].{Name:name, Value:value}"
```

### Understanding the Deployment Flow

```
1. GitHub Actions builds container
   └─ Includes: .NET API + OpenFGA binary + start.sh

2. Container pushed to ACR
   └─ Image: acrmenuapp768.azurecr.io/menu-app-combined:latest

3. Function App pulls container
   └─ Azure starts the container

4. start.sh executes (ENTRYPOINT)
   ├─ Validates OPENFGA_DATASTORE_URI exists
   ├─ Runs OpenFGA migrations (connects to SQL Server)
   ├─ Starts OpenFGA server (port 8080)
   ├─ Waits for OpenFGA health check (up to 180s)
   ├─ Creates/finds store "menu-app"
   ├─ Exports OPENFGA_STORE_ID=<dynamic-id>  ⚠️  ISSUE HERE
   ├─ Uploads authorization model
   ├─ Loads seed data
   └─ Executes Azure Functions host          ⚠️  ENV NOT INHERITED

5. Functions host starts
   ├─ Reads Program.cs configuration
   ├─ Creates OpenFgaClient with StoreId=""  ⚠️  EMPTY!
   └─ Registers HTTP triggers

6. Request comes in to /api/menu
   ├─ MenuFunction.GetMenu() executes
   ├─ Tries to call OpenFGA SDK
   └─ Hangs/fails because StoreId is empty   ⚠️  TIMEOUT
```

### Next Steps

1. **Immediate:** Use Option 1 to manually set OPENFGA_STORE_ID
2. **Short-term:** Test if API works after setting the store ID
3. **Long-term:** Implement Option 2 to fix the environment propagation issue

### Testing After Fix

Once OPENFGA_STORE_ID is set:

```bash
# Should return menu items
curl "https://func-menu-app-18436.azurewebsites.net/api/menu?user=alice"

# Expected response:
{
  "user": "alice",
  "menuItems": [
    {"Id": 1, "Name": "Dashboard", "Icon": "📊", "Url": "/dashboard"},
    {"Id": 2, "Name": "Users", "Icon": "👥", "Url": "/users"},
    ...
  ]
}
```

### Additional Resources

- **Azure Portal:** https://portal.azure.com
- **Function App:** Navigate to func-menu-app-18436 → Log stream
- **SQL Server:** Use Azure Data Studio or sqlcmd to query stores table
- **OpenFGA Docs:** https://openfga.dev/docs

---

**Last Updated:** 2025-10-11
**Issue:** OPENFGA_STORE_ID not propagating from start.sh to Functions host
