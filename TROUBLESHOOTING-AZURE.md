# Troubleshooting Azure Functions Container Issues

This guide helps diagnose and fix common issues when running the combined OpenFGA + .NET Functions container in Azure.

## Quick Diagnostic Checklist

### 1. Check Container Logs via Kudu

Visit your Function App's Kudu console:
```
https://<function-app-name>.scm.azurewebsites.net
```

Navigate to: **Advanced Tools** → **Go** → **Current Docker Logs**

Download and review:
- `<timestamp>_default_docker.log` - Container startup logs
- `<timestamp>_default_out.log` - stdout from your application

### 2. Check Application Settings

Required environment variables (verify in Azure Portal → Configuration):

| Setting | Required Value | Purpose |
|---------|---------------|---------|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | Identifies .NET isolated worker model |
| `AzureWebJobsStorage` | `<storage-connection-string>` | Functions runtime internal storage |
| `WEBSITES_PORT` | `80` | Port the container listens on |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `false` | Disable file share mounting for custom containers |
| `WEBSITES_HEALTHCHECK_MAXPINGFAILURES` | `10` | Allow longer startup time (10 retries) |
| `OPENFGA_API_URL` | `http://localhost:8080` | OpenFGA endpoint for .NET app |
| `OPENFGA_DATASTORE_ENGINE` | `sqlserver` or `memory` | OpenFGA storage backend |
| `OPENFGA_DATASTORE_URI` | SQL connection string | Required if using SQL Server |
| `OPENFGA_STORE_ID` | Store GUID or empty | OpenFGA store identifier |

### 3. Common Failure Patterns

#### Symptom: Container stops immediately after startup

**Possible Causes:**
1. ❌ Missing `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
2. ❌ Missing or invalid `AzureWebJobsStorage`
3. ❌ SQL Server connection string issues (for OpenFGA)
4. ❌ OpenFGA migrations failed

**How to Diagnose:**
```bash
# Check Function App logs
az webapp log tail --name <function-app-name> --resource-group <rg>

# Check if container is running
az functionapp show \
  --name <function-app-name> \
  --resource-group <rg> \
  --query state
```

**Fix:**
1. Verify all required environment variables are set (see table above)
2. Check SQL Server firewall allows Azure services (if using SQL)
3. Review container logs in Kudu for specific error messages

#### Symptom: Container times out during startup (HTTP 503)

**Possible Causes:**
1. ⏱️ Startup takes longer than 230 seconds (Azure's health check timeout)
2. ⏱️ OpenFGA migrations are slow on SQL Server
3. ⏱️ SQL Server connection latency

**How to Diagnose:**
- Look for logs showing: "Waiting for OpenFGA... (120s / 120s)" → timeout
- Check if you see: "✅ OpenFGA is ready! (took XXXs)" → how long it took
- Review OpenFGA logs for SQL connection errors

**Fix:**
1. Increase health check tolerance: `WEBSITES_HEALTHCHECK_MAXPINGFAILURES=10`
2. Optimize SQL Server connection (use connection pooling)
3. Consider using in-memory OpenFGA for dev/test environments
4. Check SQL Server DTU/CPU usage - may need higher tier

#### Symptom: Functions runtime not found / No functions detected

**Possible Causes:**
1. ❌ Missing `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
2. ❌ DLL files not copied to `/home/site/wwwroot`
3. ❌ Incorrect `AzureWebJobsScriptRoot` path

**How to Diagnose:**
- Container logs show: "No job functions found"
- Container logs show: "Worker runtime not detected"

**Fix:**
```bash
# Verify environment variable is set
az functionapp config appsettings list \
  --name <function-app-name> \
  --resource-group <rg> \
  --query "[?name=='FUNCTIONS_WORKER_RUNTIME'].value" -o tsv

# Should output: dotnet-isolated

# If not set, add it:
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <rg> \
  --settings "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated"
```

#### Symptom: OpenFGA process dies during startup

**Possible Causes:**
1. ❌ Invalid SQL Server connection string
2. ❌ SQL Server firewall blocking connection
3. ❌ Insufficient permissions for OpenFGA SQL user
4. ❌ Database migrations already partially applied (conflict)

**How to Diagnose:**
- Container logs show: "❌ ERROR: OpenFGA process died unexpectedly"
- OpenFGA logs show SQL connection errors

**Fix:**
1. Verify connection string format:
   ```
   # Managed Identity (recommended)
   sqlserver://<server>.database.windows.net?database=<db>&encrypt=true&fedauth=ActiveDirectoryMSI

   # Password-based
   sqlserver://<user>:<pass>@<server>.database.windows.net?database=<db>&encrypt=true
   ```

2. Check SQL Server firewall:
   ```bash
   az sql server firewall-rule create \
     --resource-group <rg> \
     --server <server-name> \
     --name AllowAzureServices \
     --start-ip-address 0.0.0.0 \
     --end-ip-address 0.0.0.0
   ```

3. Grant permissions to managed identity (see [AZURE-MANAGED-IDENTITY-SETUP.md](AZURE-MANAGED-IDENTITY-SETUP.md))

#### Symptom: Container works locally but fails in Azure

**Possible Causes:**
1. 🔐 Different authentication methods (local uses Azure CLI, Azure uses managed identity)
2. 🌐 Firewall/network differences
3. ⚙️ Missing environment variables in Azure
4. 💾 Storage account not configured

**How to Diagnose:**
- Compare local environment variables vs Azure app settings
- Test local with same connection strings as Azure
- Check if managed identity is enabled on Function App

**Fix:**
1. Enable managed identity:
   ```bash
   az functionapp identity assign \
     --name <function-app-name> \
     --resource-group <rg>
   ```

2. Use same connection string format locally and in Azure
3. Test container locally with Azure-like settings:
   ```bash
   podman run -p 80:80 \
     -e FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
     -e AzureWebJobsStorage="<storage-connection>" \
     -e OPENFGA_DATASTORE_URI="<sql-connection>" \
     menu-app-combined:latest
   ```

## Performance Optimization

### Reduce Startup Time

Current startup sequence (target: < 120 seconds total):
1. OpenFGA migrations: ~20-40s
2. OpenFGA server start: ~10-30s
3. Store initialization: ~5-10s
4. Auth model upload: ~2-5s
5. Seed data load: ~2-5s
6. Functions host start: ~10-20s

**Optimizations:**
- ✅ Use managed identity (faster than password auth)
- ✅ Run migrations once, not on every startup
- ✅ Cache authorization models (don't re-upload if exists)
- ✅ Use higher SQL Server tier (reduces latency)
- ⚠️ Consider Azure SQL elastic pool for multiple environments

### Monitor Container Health

Add Application Insights monitoring:
```bash
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <rg> \
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=<key>"
```

View live metrics:
```bash
# Stream logs in real-time
az webapp log tail \
  --name <function-app-name> \
  --resource-group <rg>
```

## Container Health Check Details

The Dockerfile includes a health check:
```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=150s --retries=5 \
  CMD curl -f http://localhost:80/api/health || curl -f http://localhost:8080/healthz || exit 1
```

**Parameters:**
- `--start-period=150s` - Give container 2.5 minutes before health checks count as failures
- `--interval=30s` - Check every 30 seconds after start period
- `--retries=5` - Allow 5 failed checks before marking unhealthy
- Falls back to OpenFGA health if Functions not ready yet

## Debugging Commands

### View container status
```bash
az functionapp show \
  --name <function-app-name> \
  --resource-group <rg> \
  --query '{state:state,outboundIpAddresses:outboundIpAddresses}'
```

### View all app settings
```bash
az functionapp config appsettings list \
  --name <function-app-name> \
  --resource-group <rg> \
  --output table
```

### Test health endpoint manually
```bash
curl -v https://<function-app-name>.azurewebsites.net/api/health
```

### Force container restart
```bash
az functionapp restart \
  --name <function-app-name> \
  --resource-group <rg>
```

### Pull latest container image
```bash
az functionapp config container set \
  --name <function-app-name> \
  --resource-group <rg> \
  --docker-custom-image-name <acr>.azurecr.io/menu-app-combined:latest
```

### SSH into container (if enabled)
```bash
# Enable SSH first
az webapp create-remote-connection \
  --name <function-app-name> \
  --resource-group <rg>
```

## GitHub Secrets Required

Ensure these secrets are configured in your GitHub repository:

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `AZURE_CREDENTIALS` | Service principal JSON | `{"clientId":"...","clientSecret":"..."}` |
| `AZURE_FUNCTIONAPP_NAME` | Function App name | `func-menu-app-prod` |
| `AZURE_RESOURCE_GROUP` | Resource group | `rg-menu-app` |
| `ACR_NAME` | Container registry | `acrmenuapp` |
| `ACR_USERNAME` | Registry username | `acrmenuapp` |
| `ACR_PASSWORD` | Registry password | (from ACR credentials) |
| `AZURE_WEBJOBS_STORAGE` | Storage connection string | `DefaultEndpointsProtocol=https;...` |
| `OPENFGA_STORE_ID` | OpenFGA store GUID | `01HXXX...` (or empty for auto-create) |
| `OPENFGA_DATASTORE_URI` | SQL connection for OpenFGA | `sqlserver://...` |
| `DOTNET_CONNECTION_STRING` | SQL connection for .NET | `Server=tcp:...` |

## Getting Help

### Enable Diagnostic Logging
1. Go to Azure Portal → Function App → Monitoring → App Service logs
2. Enable:
   - Application Logging (Filesystem): Verbose
   - Detailed Error Messages: On
   - Failed Request Tracing: On
3. Save and restart

### Review Startup Timeline
Look for these log entries to understand where startup fails:
```
✅ Checkmarks indicate successful steps:
- ✅ OpenFGA migrations complete
- ✅ OpenFGA is ready! (took XXs)
- ✅ Authorization model uploaded
- ✅ Seed data loaded
- 🚀 Executing Azure Functions host

❌ X marks indicate failures - check logs above these
```

### Contact Support
If issues persist after trying these steps:
1. Download Docker logs from Kudu
2. Check Application Insights for exceptions
3. Verify all prerequisites from [AZURE-MANAGED-IDENTITY-SETUP.md](AZURE-MANAGED-IDENTITY-SETUP.md)
4. Open GitHub issue with logs and error messages

## Additional Resources

- [Azure Functions Container Troubleshooting](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-custom-container)
- [App Service Container Logs](https://learn.microsoft.com/en-us/azure/app-service/troubleshoot-diagnostic-logs)
- [OpenFGA Documentation](https://openfga.dev/docs)
- [Managed Identity Setup Guide](AZURE-MANAGED-IDENTITY-SETUP.md)
