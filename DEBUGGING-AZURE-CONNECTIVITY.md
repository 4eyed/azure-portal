# Debugging Azure Container Connectivity Issues

**Created**: October 13, 2025
**Purpose**: Comprehensive guide for diagnosing SQL connectivity and container issues in Azure

## Quick Diagnosis - 5 Minute Checklist

### 1. Check Container Logs
```bash
# View container logs in Kudu
https://<function-app-name>.scm.azurewebsites.net
# Navigate to: Logs → Docker logs
```

**Look for these SUCCESS indicators:**
```
✅ OpenFGA is ready! (took XXs)
✅ Authorization model uploaded successfully
🚀 Executing Azure Functions host at ...
```

**Red flags to watch for:**
```
❌ ERROR: OpenFGA process died unexpectedly
❌ Migration failed with errors
⚠️ Cannot establish TCP connection to port 1433
⚠️ No SQL token in SqlTokenContext
```

### 2. Test Health Endpoint
```bash
# Basic health check
curl https://<function-app-name>.azurewebsites.net/api/health

# Verbose diagnostics
curl https://<function-app-name>.azurewebsites.net/api/health?verbose=true
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-10-13T...",
  "checks": {
    "api": { "status": "healthy" },
    "database": {
      "status": "healthy",
      "authenticationMethod": "Active Directory Default",
      "menuGroups": 0
    },
    "openfga": { "status": "healthy" },
    "configuration": { "status": "healthy" }
  }
}
```

### 3. Run SQL Connectivity Tests
```bash
# Test all 6 connection methods
curl https://<function-app-name>.azurewebsites.net/api/debug/sql-test
```

This will test:
1. ✅ Managed Identity (Azure production)
2. ✅ Username/Password (fallback)
3. ✅ User SQL Token (local dev style)
4. ✅ Direct SQL query
5. ✅ EF Core DbContext
6. ✅ OpenFGA connectivity

### 4. Check Configuration
```bash
# View sanitized configuration
curl https://<function-app-name>.azurewebsites.net/api/debug/config
```

---

## Debugging Endpoints Reference

### `/api/health` - Basic Health Check
**Usage**: Container health monitoring
**Query params**: `?verbose=true` for detailed checks
**Auth**: Anonymous

**Use when**:
- Setting up Azure health probe
- Quick status check
- Verifying all components are running

### `/api/debug/sql-test` - SQL Connectivity Test
**Usage**: Diagnose database connection issues
**Auth**: Anonymous (consider restricting in production)

**Use when**:
- "Cannot connect to database" errors
- Managed Identity authentication issues
- Firewall/network connectivity problems

**What it tests**:
- Managed Identity auth
- Username/password auth
- User token auth (like local dev)
- Direct SQL queries
- EF Core DbContext operations
- OpenFGA database connectivity

### `/api/debug/config` - Configuration Diagnostics
**Usage**: View environment configuration
**Auth**: Anonymous (consider restricting in production)

**Use when**:
- Verifying environment variables are set
- Checking connection string format
- Validating OpenFGA configuration
- Confirming Azure Functions settings

**What it shows**:
- Environment detection (Azure vs Local)
- Connection string analysis (sanitized)
- OpenFGA configuration
- Azure AD settings
- Process information
- Missing configuration warnings

---

## Manual Debugging Inside Container

### Connect to Container Console

**Option 1: Kudu Console**
```
https://<function-app-name>.scm.azurewebsites.net
→ Debug console → CMD or PowerShell
```

**Option 2: Azure CLI**
```bash
az webapp ssh --name <function-app-name> --resource-group <resource-group>
```

### Run Manual Tests

All debugging scripts are located in `/usr/local/bin/debug-scripts/`

#### Test 1: Full Connectivity Test Suite
```bash
cd /usr/local/bin/debug-scripts
./test-connections.sh
```

**Available test filters:**
```bash
./test-connections.sh all      # Run all tests (default)
./test-connections.sh openfga  # Test OpenFGA only
./test-connections.sh sql      # Test SQL connectivity only
./test-connections.sh api      # Test .NET API only
./test-connections.sh config   # Check configuration only
```

#### Test 2: SQL Authentication Diagnosis
```bash
cd /usr/local/bin/debug-scripts
./test-sql-auth.sh
```

This script:
- Analyzes connection string format
- Tests OpenFGA database connectivity
- Diagnoses common authentication errors
- Provides specific recommendations

#### Test 3: Check OpenFGA Logs
```bash
# View recent logs
tail -100 /var/log/openfga.log

# Search for errors
grep -i "error\|failed" /var/log/openfga.log

# Watch logs in real-time
tail -f /var/log/openfga.log
```

#### Test 4: Check Process Status
```bash
# Check if OpenFGA is running
pgrep -f openfga

# Check if Functions host is running
pgrep -f "Microsoft.Azure.WebJobs"

# View process details
ps aux | grep -E "openfga|WebJobs"
```

#### Test 5: Network Connectivity
```bash
# Test DNS resolution
host <sql-server>.database.windows.net

# Test TCP connectivity (port 1433)
timeout 5 bash -c "cat < /dev/null > /dev/tcp/<sql-server>.database.windows.net/1433"

# Test OpenFGA endpoint
curl http://localhost:8080/healthz

# Test API endpoint
curl http://localhost:80/api/health
```

---

## Common Issues and Solutions

### Issue 1: "OpenFGA process died unexpectedly"

**Symptoms:**
```
❌ ERROR: OpenFGA process died unexpectedly
```

**Diagnosis:**
```bash
# Check OpenFGA logs
tail -100 /var/log/openfga.log

# Look for authentication errors
grep -i "authentication\|login failed" /var/log/openfga.log
```

**Common Causes:**

1. **Managed Identity not configured**
   - Function App doesn't have System-assigned identity enabled
   - Solution: Enable in Azure Portal → Function App → Identity

2. **Managed Identity not added to SQL**
   - Identity not registered as SQL user
   - Solution:
     ```sql
     CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;
     ALTER ROLE db_owner ADD MEMBER [<function-app-name>];
     ```

3. **Firewall blocking connection**
   - Azure services not allowed
   - Solution: Enable "Allow Azure services" in SQL Server firewall

4. **Invalid connection string**
   - Check format in `/api/debug/config`
   - For Managed Identity, use:
     ```
     sqlserver://server.database.windows.net/database?fedauth=ActiveDirectoryMSI
     ```

### Issue 2: "Cannot establish TCP connection to port 1433"

**Symptoms:**
```
⚠️ Cannot establish TCP connection to port 1433
```

**Diagnosis:**
```bash
# Test DNS resolution
host <sql-server>.database.windows.net

# Test TCP connectivity
timeout 5 bash -c "cat < /dev/null > /dev/tcp/<sql-server>.database.windows.net/1433"
```

**Solutions:**

1. **Check SQL Server firewall rules**
   ```bash
   az sql server firewall-rule list \
     --server <server-name> \
     --resource-group <resource-group>
   ```

2. **Ensure "Allow Azure services" is enabled**
   ```bash
   az sql server firewall-rule create \
     --server <server-name> \
     --resource-group <resource-group> \
     --name AllowAzureServices \
     --start-ip-address 0.0.0.0 \
     --end-ip-address 0.0.0.0
   ```

3. **Check VNET/Private Endpoint configuration**
   - If using private endpoint, ensure Function App is in same VNET
   - Or use VNET integration

### Issue 3: ".NET API can connect but OpenFGA cannot"

**Symptoms:**
- `/api/health?verbose=true` shows database as healthy
- But OpenFGA logs show connection errors

**Diagnosis:**
```bash
# Test OpenFGA connection separately
cd /usr/local/bin/debug-scripts
./test-sql-auth.sh
```

**Common Causes:**

1. **Different connection string formats**
   - OpenFGA uses Go driver (different format)
   - .NET uses Microsoft.Data.SqlClient
   - Solution: Check both `OPENFGA_DATASTORE_URI` and `DOTNET_CONNECTION_STRING`

2. **Missing fedauth parameter**
   - OpenFGA requires explicit `fedauth=` for Managed Identity
   - Solution: Use `sqlserver://...?fedauth=ActiveDirectoryMSI`

3. **Port not specified**
   - Some parsers require explicit `:1433`
   - Solution: Use `sqlserver://server.database.windows.net:1433/database`

### Issue 4: "Authentication failed for user"

**Symptoms:**
```
Login failed for user '<token-identified principal>'
```

**Diagnosis:**
```bash
# Check managed identity is registered
curl https://<function-app-name>.azurewebsites.net/api/debug/config
```

**Solutions:**

1. **Create SQL user for Managed Identity**
   ```sql
   -- Connect to SQL Server as admin
   CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;

   -- Grant appropriate permissions
   ALTER ROLE db_owner ADD MEMBER [<function-app-name>];

   -- Or more restrictive:
   GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [<function-app-name>];
   ```

2. **Verify Azure AD authentication is enabled**
   - Go to SQL Server → Azure Active Directory
   - Ensure an Azure AD admin is set

3. **Wait for propagation**
   - Changes can take 5-10 minutes to propagate
   - Restart Function App after making changes

### Issue 5: "Container keeps restarting"

**Symptoms:**
- Container starts then stops repeatedly
- Logs show partial startup

**Diagnosis:**
```bash
# Check startup logs
# Look for:
# - Pre-flight check failures
# - Timeout during OpenFGA startup
# - Missing environment variables
```

**Solutions:**

1. **Check timeout settings**
   - OpenFGA startup must complete in < 230 seconds
   - Increase `WEBSITES_HEALTHCHECK_MAXPINGFAILURES` to 10

2. **Skip migrations if already run**
   - Set `SKIP_MIGRATIONS=true` to save time
   - Only set this after first successful deployment

3. **Check required environment variables**
   ```bash
   FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
   AzureWebJobsStorage=<connection-string>
   DOTNET_CONNECTION_STRING=<connection-string>
   OPENFGA_DATASTORE_URI=<connection-string>
   OPENFGA_STORE_ID=<store-id>
   ```

---

## Environment Variables Reference

### Required for Azure Functions
```bash
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated          # CRITICAL - container won't start without this
AzureWebJobsStorage=<storage-connection-string>   # CRITICAL - Functions runtime requires this
WEBSITES_PORT=80                                  # Port for Functions host
```

### Required for Database
```bash
DOTNET_CONNECTION_STRING=<sql-connection-string>  # .NET API database connection

# For Managed Identity (recommended):
# Server=<server>.database.windows.net;Database=<db>;Authentication=Active Directory Default;Encrypt=True;

# For SQL Auth (fallback):
# Server=<server>.database.windows.net;Database=<db>;User ID=<user>;Password=<pwd>;Encrypt=True;
```

### Required for OpenFGA
```bash
OPENFGA_API_URL=http://localhost:8080            # OpenFGA server URL (internal)
OPENFGA_STORE_ID=<store-id>                      # OpenFGA store identifier
OPENFGA_DATASTORE_ENGINE=sqlserver               # Database engine type
OPENFGA_DATASTORE_URI=<sqlserver-uri>            # OpenFGA-format connection string

# For Managed Identity (recommended):
# sqlserver://<server>.database.windows.net:1433/<database>?fedauth=ActiveDirectoryMSI&encrypt=true

# For SQL Auth (fallback):
# sqlserver://<user>:<password>@<server>.database.windows.net:1433/<database>?encrypt=true
```

### Optional Configuration
```bash
SKIP_MIGRATIONS=true                             # Skip migrations after first run (faster startup)
WEBSITES_HEALTHCHECK_MAXPINGFAILURES=10         # Allow 10 failed health checks before restart
AZURE_CLIENT_ID=<client-id>                     # For Azure AD authentication
AZURE_TENANT_ID=<tenant-id>                     # For Azure AD authentication
```

---

## Performance Optimization

### Reduce Startup Time

1. **Skip migrations after first deployment**
   ```bash
   az functionapp config appsettings set \
     --name <function-app-name> \
     --resource-group <resource-group> \
     --settings "SKIP_MIGRATIONS=true"
   ```

2. **Use connection pooling**
   - Already enabled by default in connection strings
   - Verify: `Max Pool Size=100;Min Pool Size=0;Pooling=true;`

3. **Optimize OpenFGA timeout**
   - Current: 120 seconds max wait
   - Typical: 60-90 seconds actual startup
   - If consistently timing out, investigate database latency

### Monitor Startup Time

```bash
# Check startup duration in logs
# Look for: "Total startup time so far: XXs"
# Should be < 120s total
```

**Breakdown:**
- Pre-flight checks: 10-20s
- OpenFGA startup: 40-70s
- Functions host startup: 10-20s
- **Total**: 60-110s typical

---

## Testing Local Changes

### Build and Test Locally

```bash
# Build container
podman build -f Dockerfile.combined -t menu-app-test .

# Run with environment variables
podman run -p 80:80 -p 8080:8080 \
  -e DOTNET_CONNECTION_STRING="Server=..." \
  -e OPENFGA_DATASTORE_URI="sqlserver://..." \
  -e OPENFGA_STORE_ID="01..." \
  menu-app-test

# Test endpoints
curl http://localhost:80/api/health?verbose=true
curl http://localhost:80/api/debug/sql-test
curl http://localhost:80/api/debug/config

# Run manual tests inside container
podman exec -it <container-id> /bin/bash
cd /usr/local/bin/debug-scripts
./test-connections.sh
```

---

## Advanced Debugging

### Enable Verbose Logging

Add to Function App settings:
```bash
AzureFunctionsJobHost__Logging__Console__IsEnabled=true
AzureFunctionsJobHost__Logging__LogLevel__Default=Information
OPENFGA_LOG_LEVEL=debug
```

### Capture Network Traffic

```bash
# Install tcpdump in container (if needed)
apt-get update && apt-get install -y tcpdump

# Capture SQL Server traffic
tcpdump -i any -n port 1433 -w /tmp/sql-traffic.pcap

# Download and analyze with Wireshark
```

### Check Managed Identity Token

```bash
# Get token from instance metadata (inside container)
curl -H "Metadata: true" \
  "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://database.windows.net/" \
  | jq '.'

# Decode JWT token
# Copy access_token and paste at https://jwt.ms
```

---

## Deployment Checklist

Before deploying to Azure:

- [ ] Managed Identity enabled on Function App
- [ ] Managed Identity added as SQL user with appropriate permissions
- [ ] SQL Server firewall allows Azure services
- [ ] All required environment variables set
- [ ] `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` configured
- [ ] `AzureWebJobsStorage` connection string configured
- [ ] Connection strings use correct format (Managed Identity vs SQL Auth)
- [ ] OpenFGA connection string matches .NET connection string (same auth method)
- [ ] Health check endpoint accessible (test after deployment)

After deployment:

- [ ] Check container logs for successful startup
- [ ] Test `/api/health?verbose=true`
- [ ] Test `/api/debug/sql-test`
- [ ] Verify no restart loops (check metrics)
- [ ] Monitor Application Insights for errors
- [ ] Set `SKIP_MIGRATIONS=true` after first successful run

---

## Getting Help

If issues persist after following this guide:

1. **Collect diagnostics**
   ```bash
   curl https://<app>.azurewebsites.net/api/health?verbose=true > health.json
   curl https://<app>.azurewebsites.net/api/debug/config > config.txt
   curl https://<app>.azurewebsites.net/api/debug/sql-test > sql-test.txt
   ```

2. **Export container logs**
   - Go to Kudu console
   - Download `/var/log/openfga.log`
   - Download Docker logs

3. **Check Application Insights**
   - Go to Azure Portal → Function App → Application Insights
   - Look for exceptions and failed requests

4. **Open GitHub issue**
   - Include all collected diagnostics (redact sensitive data)
   - Include error messages and timestamps
   - Describe steps already attempted

---

## Related Documentation

- [AZURE-CONTAINER-FIXES.md](AZURE-CONTAINER-FIXES.md) - Previous container fixes
- [AZURE-MANAGED-IDENTITY-SETUP.md](AZURE-MANAGED-IDENTITY-SETUP.md) - Managed Identity configuration
- [TROUBLESHOOTING-AZURE.md](TROUBLESHOOTING-AZURE.md) - General troubleshooting guide
- [RUNNING-THE-APP.md](RUNNING-THE-APP.md) - Local development setup
