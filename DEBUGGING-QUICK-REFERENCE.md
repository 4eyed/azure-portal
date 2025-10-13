# Azure Container Debugging - Quick Reference Card

**Last Updated**: October 13, 2025

## 🚀 Quick Start - 3 Essential Commands

```bash
# 1. Check overall health
curl https://<function-app>.azurewebsites.net/api/health?verbose=true

# 2. Test SQL connectivity (6 different methods)
curl https://<function-app>.azurewebsites.net/api/debug/sql-test

# 3. View configuration
curl https://<function-app>.azurewebsites.net/api/debug/config
```

## 📋 Pre-Flight Checks (Container Startup)

The startup script now includes automatic diagnostics:

1. ✅ **OpenFGA Binary** - Verifies binary exists and is executable
2. ✅ **SQL Server Network** - Tests DNS resolution and TCP port 1433
3. ✅ **OpenFGA Database** - Validates database connectivity before starting

Check startup logs at:
```
https://<function-app-name>.scm.azurewebsites.net
→ Logs → Docker logs
```

## 🔍 Debugging Endpoints

| Endpoint | Purpose | When to Use |
|----------|---------|-------------|
| `/api/health` | Basic health | Container health probe |
| `/api/health?verbose=true` | Detailed health | Check all components |
| `/api/debug/sql-test` | SQL connectivity | Database connection issues |
| `/api/debug/config` | Configuration view | Verify environment variables |

## 🛠️ Manual Tests (Inside Container)

### Access Container Console
```bash
# Option 1: Kudu (web-based)
https://<function-app-name>.scm.azurewebsites.net
→ Debug console → CMD

# Option 2: Azure CLI
az webapp ssh --name <function-app-name> --resource-group <resource-group>
```

### Run Test Scripts
```bash
# All tests
/usr/local/bin/debug-scripts/test-connections.sh

# Specific tests
/usr/local/bin/debug-scripts/test-connections.sh sql      # SQL only
/usr/local/bin/debug-scripts/test-connections.sh openfga  # OpenFGA only
/usr/local/bin/debug-scripts/test-connections.sh api      # API only

# SQL authentication diagnosis
/usr/local/bin/debug-scripts/test-sql-auth.sh
```

### Check Logs
```bash
# OpenFGA logs (last 100 lines)
tail -100 /var/log/openfga.log

# Search for errors
grep -i "error\|failed" /var/log/openfga.log

# Real-time logs
tail -f /var/log/openfga.log
```

### Check Processes
```bash
# Check if OpenFGA is running
pgrep -f openfga || echo "NOT RUNNING"

# Check if Functions host is running
pgrep -f "Microsoft.Azure.WebJobs" || echo "NOT RUNNING"

# View all processes
ps aux | grep -E "openfga|WebJobs"
```

### Network Tests
```bash
# Test SQL Server DNS
host <sql-server>.database.windows.net

# Test SQL Server TCP (port 1433)
timeout 5 bash -c "cat < /dev/null > /dev/tcp/<sql-server>.database.windows.net/1433" && echo "OK" || echo "FAILED"

# Test OpenFGA endpoint
curl http://localhost:8080/healthz

# Test API endpoint
curl http://localhost:80/api/health
```

## 🔧 Common Issues - Quick Fixes

### Issue: Container keeps restarting
```bash
# Check if required variables are set
az functionapp config appsettings list \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --query "[?name=='FUNCTIONS_WORKER_RUNTIME' || name=='AzureWebJobsStorage'].{Name:name, Value:value}"

# Set missing variables
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings \
    "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
    "AzureWebJobsStorage=<connection-string>"
```

### Issue: "OpenFGA process died"
```bash
# View OpenFGA logs
tail -100 /var/log/openfga.log

# Common cause: Managed Identity not configured
# Fix: Enable MI and add to SQL
az functionapp identity assign \
  --name <function-app-name> \
  --resource-group <resource-group>

# Get the principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --query principalId -o tsv)

echo "Principal ID: $PRINCIPAL_ID"

# Then connect to SQL and run:
# CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;
# ALTER ROLE db_owner ADD MEMBER [<function-app-name>];
```

### Issue: "Cannot connect to SQL Server"
```bash
# Test connectivity
curl https://<function-app-name>.azurewebsites.net/api/debug/sql-test

# Check firewall rules
az sql server firewall-rule list \
  --server <server-name> \
  --resource-group <resource-group>

# Allow Azure services
az sql server firewall-rule create \
  --server <server-name> \
  --resource-group <resource-group> \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Issue: Slow startup
```bash
# Skip migrations after first successful deployment
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "SKIP_MIGRATIONS=true"

# Increase health check tolerance
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "WEBSITES_HEALTHCHECK_MAXPINGFAILURES=10"
```

## 📊 What Success Looks Like

### Startup Logs (Good)
```
============================================
Pre-Flight Connectivity Checks
============================================

CHECK 1: OpenFGA Binary
✅ OpenFGA binary found: /usr/local/bin/openfga

CHECK 2: SQL Server Network Connectivity
SQL Server: myserver.database.windows.net
✅ DNS resolution successful
✅ TCP port 1433 is reachable

CHECK 3: OpenFGA Database Connectivity Test
✅ OpenFGA migration completed successfully (45s)
   Database connectivity confirmed

============================================
✅ OpenFGA is ready! (took 67s)
✅ Authorization model uploaded successfully
🚀 Executing Azure Functions host at ...
   Total startup time so far: 89s
```

### Health Check (Good)
```json
{
  "status": "healthy",
  "checks": {
    "database": {
      "status": "healthy",
      "authenticationMethod": "Active Directory Default",
      "responseTime": "234ms"
    },
    "openfga": {
      "status": "healthy",
      "responseTime": "12ms"
    }
  }
}
```

### SQL Test (Good)
```
========================================
TEST 1: Managed Identity Connection
========================================
✅ SUCCESS - Connected in 456ms
   Server Version: 12.00.0000
   Current User: <function-app-name>@External
   Current DB: menu-app-db
```

## 🔑 Required Environment Variables

### Minimal Configuration (Managed Identity)
```bash
# Critical - container won't start without these
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
AzureWebJobsStorage=<storage-account-connection-string>

# Database (.NET API)
DOTNET_CONNECTION_STRING="Server=<server>.database.windows.net;Database=<db>;Authentication=Active Directory Default;Encrypt=True;"

# OpenFGA
OPENFGA_DATASTORE_URI="sqlserver://<server>.database.windows.net:1433/<db>?fedauth=ActiveDirectoryMSI&encrypt=true"
OPENFGA_STORE_ID=<store-id>
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_API_URL=http://localhost:8080
```

## 📞 Escalation Path

1. **Try quick fixes above** (< 5 min)
2. **Run full diagnostics** (5-10 min)
   ```bash
   curl https://<app>.azurewebsites.net/api/debug/sql-test > sql-test.txt
   curl https://<app>.azurewebsites.net/api/debug/config > config.txt
   ```
3. **Check logs** (5 min)
   - Kudu → Docker logs
   - Application Insights
   - `/var/log/openfga.log`
4. **Review [DEBUGGING-AZURE-CONNECTIVITY.md](DEBUGGING-AZURE-CONNECTIVITY.md)** (detailed guide)
5. **Open GitHub issue** with diagnostics

## 🎯 Performance Targets

| Metric | Target | Red Flag |
|--------|--------|----------|
| Container startup | < 120s | > 180s |
| OpenFGA startup | < 90s | > 120s |
| First health check response | < 2 min | > 3 min |
| SQL query response | < 500ms | > 2s |
| OpenFGA health check | < 100ms | > 1s |

## 🔗 Related Docs

- [DEBUGGING-AZURE-CONNECTIVITY.md](DEBUGGING-AZURE-CONNECTIVITY.md) - Full guide
- [AZURE-CONTAINER-FIXES.md](AZURE-CONTAINER-FIXES.md) - Previous fixes
- [TROUBLESHOOTING-AZURE.md](TROUBLESHOOTING-AZURE.md) - General troubleshooting

---

**Pro Tip**: Bookmark this page! These commands solve 90% of Azure container issues.
