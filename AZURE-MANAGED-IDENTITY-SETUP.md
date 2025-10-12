# Azure Managed Identity Setup for SQL Server

This guide explains how to configure Azure Managed Identity authentication for both the .NET API and OpenFGA to connect to Azure SQL Server without passwords.

## Overview

Azure Managed Identity provides a secure way to connect to Azure SQL Server without storing credentials in connection strings. Both the .NET backend and OpenFGA fork now support managed identity authentication.

## Benefits

- No passwords in connection strings or secrets
- Automatic token rotation and credential management
- Works seamlessly in both local dev (with Azure CLI) and production (with managed identity)
- Centralized access management through Azure AD

## Architecture

```
┌─────────────────────────────┐
│  Azure Function App         │
│  (System-Assigned MI)       │
│                             │
│  ┌───────────────────────┐  │
│  │ .NET API              │  │─┐
│  │ - EF Core             │  │ │
│  │ - Microsoft.Data.     │  │ │
│  │   SqlClient v5.2+     │  │ │
│  └───────────────────────┘  │ │
│                             │ │  Azure AD Token
│  ┌───────────────────────┐  │ │  (auto-renewed)
│  │ OpenFGA               │  │ │
│  │ - go-mssqldb/azuread  │  │─┤
│  │ - azidentity v1.13    │  │ │
│  └───────────────────────┘  │ │
└─────────────────────────────┘ │
                                │
                                ▼
                    ┌────────────────────────┐
                    │  Azure SQL Database    │
                    │                        │
                    │  - SQL User from MI    │
                    │  - Role Assignments    │
                    └────────────────────────┘
```

## Prerequisites

- Azure CLI installed (for local development)
- Azure Function App with system-assigned managed identity enabled
- Azure SQL Database
- Sufficient permissions to manage SQL users and roles

## Step 1: Enable Managed Identity on Function App

### Using Azure Portal

1. Navigate to your Function App
2. Go to **Settings** > **Identity**
3. Under **System assigned**, toggle **Status** to **On**
4. Click **Save**
5. Copy the **Object (principal) ID** - you'll need this

### Using Azure CLI

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name <function-app-name> \
  --resource-group <resource-group>

# Get the principal ID
az functionapp identity show \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --query principalId -o tsv
```

## Step 2: Configure Azure SQL Server

### Set Azure AD Admin

Your Azure SQL Server needs an Azure AD admin to create SQL users from managed identities.

```bash
# Set yourself or a group as Azure AD admin
az sql server ad-admin create \
  --resource-group <resource-group> \
  --server-name <sql-server-name> \
  --display-name <your-email> \
  --object-id <your-azure-ad-object-id>
```

### Create SQL User from Managed Identity

Connect to your Azure SQL Database using Azure AD authentication (via Azure Data Studio, SSMS, or sqlcmd) and run:

```sql
-- Create user for the Function App's managed identity
CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions
-- For .NET API (EF Core migrations + app usage)
ALTER ROLE db_datareader ADD MEMBER [<function-app-name>];
ALTER ROLE db_datawriter ADD MEMBER [<function-app-name>];
ALTER ROLE db_ddladmin ADD MEMBER [<function-app-name>];

-- For OpenFGA (requires schema management)
-- The above roles are sufficient, but you could also grant:
-- GRANT CONTROL ON DATABASE::<database-name> TO [<function-app-name>];
```

**Note**: Replace `<function-app-name>` with your actual Function App name (not the object ID).

## Step 3: Update Connection Strings

### Connection String Formats

#### For .NET (DOTNET_CONNECTION_STRING)

**Managed Identity (Production & Local Dev):**
```bash
Server=tcp:<server-name>.database.windows.net,1433;Database=<database-name>;Authentication=Active Directory Default;Encrypt=True;
```

**Legacy Password-Based (Backwards Compatible):**
```bash
Server=tcp:<server-name>.database.windows.net,1433;Database=<database-name>;User ID=<username>;Password=<password>;Encrypt=True;
```

#### For OpenFGA (OPENFGA_DATASTORE_URI)

**Managed Identity (Production & Local Dev):**
```bash
sqlserver://<server-name>.database.windows.net?database=<database-name>&encrypt=true&fedauth=ActiveDirectoryMSI
```

Alternative formats:
- `fedauth=ActiveDirectoryManagedIdentity` (same as ActiveDirectoryMSI)
- `fedauth=ActiveDirectoryDefault` (tries multiple auth methods)

**Legacy Password-Based (Backwards Compatible):**
```bash
sqlserver://<username>:<password>@<server-name>.database.windows.net?database=<database-name>&encrypt=true
```

### Azure Function App Settings

Update your Function App configuration:

```bash
# Update .NET connection string
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "DOTNET_CONNECTION_STRING=Server=tcp:<server>.database.windows.net,1433;Database=<db>;Authentication=Active Directory Default;Encrypt=True;"

# Update OpenFGA connection string
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "OPENFGA_DATASTORE_URI=sqlserver://<server>.database.windows.net?database=<db>&encrypt=true&fedauth=ActiveDirectoryMSI"
```

## Step 4: Local Development Setup

For local development, the code uses `Active Directory Default` which tries authentication methods in this order:

1. **Environment Variables** (service principal for CI/CD)
2. **Managed Identity** (for Azure-hosted services)
3. **Azure CLI** (for local development)
4. **Visual Studio / VS Code** (Azure Account extension)

### Using Azure CLI (Recommended for Local Dev)

```bash
# Login with your Azure account
az login

# Set your subscription
az subscription set --subscription <subscription-id>

# Verify you can connect
az sql db show \
  --resource-group <resource-group> \
  --server <server-name> \
  --name <database-name>

# Test connection with sqlcmd
sqlcmd -S <server-name>.database.windows.net -d <database-name> -G -C
```

### Local Environment Variables

Create a `.env` file (or set environment variables):

```bash
# .NET connection string (same format as production)
export DOTNET_CONNECTION_STRING="Server=tcp:<server>.database.windows.net,1433;Database=<db>;Authentication=Active Directory Default;Encrypt=True;"

# OpenFGA connection string (same format as production)
export OPENFGA_DATASTORE_URI="sqlserver://<server>.database.windows.net?database=<db>&encrypt=true&fedauth=ActiveDirectoryDefault"

# Other OpenFGA settings
export OPENFGA_DATASTORE_ENGINE=sqlserver
export OPENFGA_STORE_ID=<your-store-id>
export OPENFGA_API_URL=http://localhost:8080
```

## Step 5: Testing

### Test .NET Connection

```bash
cd backend/MenuApi

# Run migrations (uses managed identity via Azure CLI)
dotnet ef database update

# Or check the connection manually
dotnet run
```

### Test OpenFGA Connection

```bash
# Start OpenFGA with managed identity connection string
cd openfga-fork
go run ./cmd/openfga run \
  --datastore-engine sqlserver \
  --datastore-uri "sqlserver://<server>.database.windows.net?database=<db>&encrypt=true&fedauth=ActiveDirectoryDefault"

# Check health endpoint
curl http://localhost:8080/healthz
```

### Test Full Application

```bash
# From project root
npm run dev

# Or with container
podman build -f Dockerfile.combined -t menu-app .
podman run -p 80:80 \
  -e OPENFGA_DATASTORE_URI="sqlserver://..." \
  -e DOTNET_CONNECTION_STRING="Server=tcp:..." \
  menu-app
```

## Troubleshooting

### Error: "Login failed for user"

**Cause**: SQL user not created or insufficient permissions

**Solution**:
1. Verify you created the SQL user: `SELECT * FROM sys.database_principals WHERE name = '<function-app-name>'`
2. Check role memberships: `SELECT * FROM sys.database_role_members`
3. Ensure you're using the Function App NAME (not Object ID) when creating the user

### Error: "AADSTS50020: User account does not exist"

**Cause**: Local development not authenticated with Azure CLI

**Solution**:
```bash
az login
az account set --subscription <subscription-id>
```

### Error: "Connection timeout" or "Cannot open server"

**Cause**: Firewall rules blocking connection

**Solution**:
```bash
# Add your current IP to SQL firewall
az sql server firewall-rule create \
  --resource-group <resource-group> \
  --server <server-name> \
  --name AllowMyIP \
  --start-ip-address <your-ip> \
  --end-ip-address <your-ip>

# For Azure services (Function App)
az sql server firewall-rule create \
  --resource-group <resource-group> \
  --server <server-name> \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Error: "Could not load type 'Microsoft.Data.SqlClient'"

**Cause**: Missing NuGet package

**Solution**:
```bash
cd backend/MenuApi
dotnet restore
dotnet build
```

### Go Error: "federated authentication library not loaded"

**Cause**: azuread package not imported

**Solution**: Verify [sqlserver.go:14](openfga-fork/pkg/storage/sqlserver/sqlserver.go#L14) imports `"github.com/microsoft/go-mssqldb/azuread"`

## Migration from Password-Based Authentication

The implementation is **backwards compatible**. Both connection string formats work:

### Gradual Migration Path

1. **Phase 1 (Current)**: Keep using password-based connection strings
2. **Phase 2 (Test)**: Switch to managed identity in dev/staging environment
3. **Phase 3 (Production)**: Update production connection strings to managed identity
4. **Phase 4 (Cleanup)**: Remove password secrets from Key Vault/configuration

### Rollback Plan

If issues occur, simply revert to password-based connection strings:

```bash
# Revert .NET connection string
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "DOTNET_CONNECTION_STRING=Server=tcp:<server>..;User ID=<user>;Password=<pass>;..."

# Revert OpenFGA connection string
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "OPENFGA_DATASTORE_URI=sqlserver://<user>:<pass>@<server>..."
```

## Security Best Practices

1. **Principle of Least Privilege**: Grant only necessary SQL roles (db_datareader, db_datawriter)
2. **Audit Access**: Enable Azure SQL auditing to track managed identity access
3. **Rotate Identities**: If compromised, disable and create new managed identity
4. **Network Security**: Use Private Link for SQL Server to avoid public internet exposure
5. **Monitor Logs**: Review Application Insights for authentication failures

## Additional Resources

- [Microsoft Docs: Managed Identity with Azure SQL](https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database)
- [Entity Framework Core with Managed Identity](https://learn.microsoft.com/en-us/azure/azure-sql/database/azure-sql-dotnet-entity-framework-core-quickstart)
- [go-mssqldb azuread package](https://pkg.go.dev/github.com/microsoft/go-mssqldb/azuread)
- [Azure SDK for Go - azidentity](https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/sdk/azidentity)

## Summary

With this setup:

✅ **No passwords** in connection strings or environment variables
✅ **Automatic token management** by Azure AD
✅ **Local development** works via Azure CLI authentication
✅ **Production** uses Function App's managed identity
✅ **Backwards compatible** with existing password-based auth
✅ **Both .NET and OpenFGA** authenticate the same way
