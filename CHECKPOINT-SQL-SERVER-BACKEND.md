# OpenFGA SQL Server Backend - Implementation Checkpoint

**Date:** October 10, 2025
**Status:** Phase 1 Complete - OpenFGA SQL Server Backend Implemented
**Next:** Phase 2 - Database Migrations & .NET Integration

---

## 🎉 What We've Accomplished

### ✅ Phase 1: OpenFGA SQL Server Backend (COMPLETE)

1. **Cloned and Set Up OpenFGA**
   - Cloned OpenFGA repository to `openfga-fork/`
   - Installed Go 1.25.2
   - Added SQL Server driver dependency: `github.com/microsoft/go-mssqldb v1.9.3`

2. **Created SQL Server Storage Backend**
   - Created `openfga-fork/pkg/storage/sqlserver/` package
   - Files created:
     - `doc.go` - Package documentation
     - `sqlserver.go` - Main implementation (~800 lines, adapted from MySQL)

3. **Key Adaptations Made**
   - **Package & Imports**: Changed from `mysql` to `sqlserver`, updated imports
   - **Connection Handling**: Uses `msdsn.Parse()` for SQL Server connection strings
   - **Error Handling**: Updated to handle SQL Server error codes (2627, 2601 for duplicates)
   - **SQL Dialect**: Changed to use `"sqlserver"` dialect with proper placeholder format
   - **Tracer**: Updated OpenTelemetry tracer to `"openfga/pkg/storage/sqlserver"`

4. **Registered SQL Server Driver**
   - Modified `openfga-fork/cmd/run/run.go`:
     - Added import: `"github.com/openfga/openfga/pkg/storage/sqlserver"`
     - Added case statement for `"sqlserver"` engine

5. **Built Custom OpenFGA Binary**
   - Successfully compiled custom OpenFGA binary with SQL Server support
   - Binary location: `openfga-fork/openfga`
   - Verified with: `./openfga version` ✅

6. **Provisioned Azure SQL Database**
   - Created provisioning script: `provision-azure-sql.sh`
   - Provisioned FREE tier Azure SQL Database:
     - Server: `sqlsrv-menu-app-24259.database.windows.net`
     - Database: `db-menu-app`
     - Region: West US 2
     - Cost: **$0/month** (FREE tier - 100k vCore seconds/month)
   - Credentials saved to: `.env.azure-sql`

7. **Started SQL Server Migrations**
   - Created directory: `openfga-fork/assets/migrations/sqlserver/`
   - Created: `001_initialize_schema.sql` (T-SQL version)
   - Converted MySQL schema to T-SQL:
     - `TIMESTAMP` → `DATETIME2`
     - `BLOB` → `VARBINARY(MAX)`
     - `INTEGER` → `INT`
     - Added explicit `CONSTRAINT` names for primary keys

---

## 📁 File Structure

```
/Volumes/External/dev/openfga/
├── openfga-fork/                      # OpenFGA with SQL Server support
│   ├── pkg/storage/sqlserver/         # SQL Server storage backend
│   │   ├── doc.go
│   │   └── sqlserver.go
│   ├── cmd/run/run.go                 # Modified to register sqlserver
│   ├── assets/migrations/sqlserver/   # SQL Server migrations
│   │   └── 001_initialize_schema.sql
│   └── openfga                        # Custom built binary
├── backend/MenuApi/                   # .NET Azure Functions API
│   ├── MenuApi.csproj
│   ├── Program.cs
│   ├── MenuFunction.cs
│   └── start.sh
├── Dockerfile.combined                # Needs update for custom OpenFGA
├── provision-azure-sql.sh             # Azure SQL provisioning script
├── .env.azure-sql                     # Azure SQL credentials (⚠️ KEEP SECURE!)
└── CHECKPOINT-SQL-SERVER-BACKEND.md   # This file
```

---

## 🔑 Azure SQL Database Credentials

**⚠️ IMPORTANT: Keep these secure! Add .env.azure-sql to .gitignore**

```bash
Server: sqlsrv-menu-app-24259.database.windows.net
Database: db-menu-app
Admin User: sqladmin
Admin Password: P@ssw0rd1760128283!
```

**Connection Strings:**
```bash
# OpenFGA
OPENFGA_DATASTORE_URI="sqlserver://sqladmin:P@ssw0rd1760128283!@sqlsrv-menu-app-24259.database.windows.net:1433?database=db-menu-app&encrypt=true"

# .NET EF Core
DOTNET_CONNECTION_STRING="Server=sqlsrv-menu-app-24259.database.windows.net;Database=db-menu-app;User Id=sqladmin;Password=P@ssw0rd1760128283!;Encrypt=true;TrustServerCertificate=false;"
```

---

## 🎯 Phase 2: Remaining Work

### 1. Complete Database Migrations (Est: 1 hour)

**Task:** Convert remaining MySQL migrations to T-SQL

```bash
cd openfga-fork/assets/migrations/sqlserver/

# Need to create these migrations:
# 002_add_authorization_model_version.sql
# 003_add_reverse_lookup_index.sql
# 004_add_authorization_model_serialized_protobuf.sql
# 005_add_conditions_to_tuples.sql
# 006_extend_object_id.sql (if needed)
# 007_collate_object_id.sql (may not be needed for SQL Server)
```

**Key T-SQL Conversion Rules:**
- `ALTER TABLE ADD COLUMN` → `ALTER TABLE ADD`
- `DROP COLUMN` works same
- `CREATE INDEX` works same, just verify syntax
- `BLOB` → `VARBINARY(MAX)`

### 2. Test OpenFGA with Azure SQL (Est: 30 min)

**Test locally:**
```bash
cd openfga-fork

# Run OpenFGA with SQL Server
source ../.env.azure-sql
./openfga run \
  --datastore-engine sqlserver \
  --datastore-uri "$OPENFGA_DATASTORE_URI"
```

**Verify:**
- OpenFGA starts successfully
- Creates tables in Azure SQL
- Can create a store
- Can write/read tuples

### 3. Update Dockerfile.combined (Est: 1 hour)

**Current Dockerfile downloads OpenFGA binary. Need to:**

```dockerfile
# Add Go build stage at the beginning
FROM golang:1.25-alpine AS openfga-builder
WORKDIR /build
# Copy our fork
COPY openfga-fork/ .
# Build custom OpenFGA with SQL Server support
RUN go build -o openfga ./cmd/openfga

# Then in final stage, copy our custom binary:
COPY --from=openfga-builder /build/openfga /usr/local/bin/openfga
```

**Update location:** `Dockerfile.combined`

### 4. Update start.sh (Est: 30 min)

**Changes needed:**
```bash
# Use sqlserver engine instead of memory
openfga run \
  --datastore-engine sqlserver \
  --datastore-uri "$OPENFGA_DATASTORE_URI" \
  --log-format json > /var/log/openfga.log 2>&1 &
```

**Remove initialization logic** - OpenFGA will auto-migrate on startup

**Update location:** `backend/MenuApi/start.sh`

### 5. Add EF Core to .NET API (Est: 2 hours)

**A. Update MenuApi.csproj:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
```

**B. Create Models:**
```csharp
// backend/MenuApi/Models/MenuItem.cs
public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Url { get; set; }
    public string? Description { get; set; }
}
```

**C. Create DbContext:**
```csharp
// backend/MenuApi/Data/ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<MenuItem> MenuItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed data
        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem { Id = 1, Name = "Dashboard", Icon = "📊", Url = "/dashboard" },
            new MenuItem { Id = 2, Name = "Users", Icon = "👥", Url = "/users" },
            new MenuItem { Id = 3, Name = "Settings", Icon = "⚙️", Url = "/settings" },
            new MenuItem { Id = 4, Name = "Reports", Icon = "📈", Url = "/reports" }
        );
    }
}
```

**D. Update Program.cs:**
```csharp
.ConfigureServices(services =>
{
    // Add DbContext
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING")));

    // Existing OpenFGA client registration
    services.AddSingleton<OpenFgaClient>(...);
})
```

**E. Update MenuFunction.cs:**
```csharp
private readonly ApplicationDbContext _dbContext;

public MenuFunction(
    ILoggerFactory loggerFactory,
    OpenFgaClient fgaClient,
    ApplicationDbContext dbContext)
{
    _logger = loggerFactory.CreateLogger<MenuFunction>();
    _fgaClient = fgaClient;
    _dbContext = dbContext;
}

// In GetMenu:
var menuItems = await _dbContext.MenuItems.ToListAsync();
// Then check permissions for each...
```

### 6. Create Application Database Schema (Est: 30 min)

**Create:** `database/app-schema.sql`
```sql
-- Application tables (separate from OpenFGA tables)
CREATE TABLE MenuItems (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name VARCHAR(100) NOT NULL,
    Icon VARCHAR(50),
    Url VARCHAR(200) NOT NULL,
    Description VARCHAR(500)
);

-- Seed data
INSERT INTO MenuItems (Name, Icon, Url, Description) VALUES
('Dashboard', '📊', '/dashboard', 'View your dashboard'),
('Users', '👥', '/users', 'Manage users'),
('Settings', '⚙️', '/settings', 'Application settings'),
('Reports', '📈', '/reports', 'View and generate reports');
```

### 7. Test Complete Stack Locally (Est: 1 hour)

```bash
# 1. Source environment
source .env.azure-sql

# 2. Build container with custom OpenFGA
./start-podman.sh

# 3. Test API
curl "http://localhost:7071/api/menu?user=alice"

# 4. Verify database
# Connect to Azure SQL and check:
# - OpenFGA tables (store, tuple, authorization_model, etc.)
# - App tables (MenuItems)
```

### 8. Update Azure Deployment (Est: 1 hour)

**Update:** `deploy-to-azure.sh`

**Changes:**
- Remove PostgreSQL provisioning (already have Azure SQL)
- Update environment variables to use SQL connection strings
- Push container with custom OpenFGA binary

**Key Environment Variables:**
```bash
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_DATASTORE_URI="sqlserver://..."
DOTNET_CONNECTION_STRING="Server=...;Database=...;"
```

### 9. Update Documentation (Est: 30 min)

- Update README.md with Azure SQL setup
- Update DEPLOY-AZURE-FUNCTIONS.md
- Document SQL Server backend implementation

---

## 🚀 Quick Resume Commands

**To continue where we left off:**

```bash
cd /Volumes/External/dev/openfga

# 1. Load Azure SQL credentials
source .env.azure-sql

# 2. Test custom OpenFGA binary
cd openfga-fork
./openfga run --datastore-engine sqlserver --datastore-uri "$OPENFGA_DATASTORE_URI"

# 3. Complete remaining migrations (see Phase 2, Task 1)

# 4. Update Dockerfile.combined (see Phase 2, Task 3)

# 5. Add EF Core to .NET API (see Phase 2, Task 5)
```

---

## 📊 Estimated Time to Completion

| Task | Est. Time | Complexity |
|------|-----------|------------|
| Complete DB Migrations | 1 hour | Low |
| Test OpenFGA with Azure SQL | 30 min | Low |
| Update Dockerfile | 1 hour | Medium |
| Update start.sh | 30 min | Low |
| Add EF Core to .NET | 2 hours | Medium |
| Create App Schema | 30 min | Low |
| Test Complete Stack | 1 hour | Medium |
| Update Deployment | 1 hour | Medium |
| Update Documentation | 30 min | Low |
| **TOTAL** | **8.5 hours** | |

---

## 🎓 Key Learnings

1. **OpenFGA is well-architected** - Adding SQL Server support was straightforward because of clean abstractions
2. **MySQL → SQL Server conversion** is mostly syntax changes (TIMESTAMP → DATETIME2, BLOB → VARBINARY)
3. **Azure SQL FREE tier is generous** - 100k vCore seconds/month is enough for prototypes
4. **Single database approach** - Using one Azure SQL DB for both OpenFGA and app data stays within free tier

---

## 📝 Notes & Tips

- **Keep `.env.azure-sql` secure** - contains database password
- **SQL Server uses 1-based indexing** for some operations (unlike PostgreSQL's 0-based)
- **Azure SQL requires encryption** - always use `encrypt=true` in connection strings
- **Firewall rules** - your IP is already added, but may need updates if IP changes
- **Free tier limits** - Monitor usage in Azure Portal (100k vCore seconds = ~28 hours of continuous use/month)

---

## 🐛 Known Issues & Workarounds

1. **East US region doesn't allow new SQL servers** - Used West US 2 instead
2. **Resource provider registration required** - Ran `az provider register --namespace Microsoft.Sql`
3. **Podman background processes** - May need to kill old containers before rebuilding

---

## 📚 Reference Links

- [OpenFGA GitHub](https://github.com/openfga/openfga)
- [Microsoft go-mssqldb Driver](https://github.com/microsoft/go-mssqldb)
- [Azure SQL Database Free Tier](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)
- [Entity Framework Core with SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)

---

**Ready to continue?** Start with Phase 2, Task 1 (Complete Database Migrations)
