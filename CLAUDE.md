# Menu Access Control Demo

A full-stack Azure application demonstrating fine-grained authorization using OpenFGA with a custom SQL Server backend.

## Architecture

```
┌─────────────────┐      ┌──────────────────────┐      ┌─────────────────┐
│  React Frontend │─────▶│  Azure Functions API │─────▶│  OpenFGA Server │
│  (Static Web)   │      │  (.NET 8)            │      │  (Custom Build) │
└─────────────────┘      └──────────────────────┘      └─────────────────┘
                                                                │
                                                                ▼
                                                        ┌─────────────────┐
                                                        │  Azure SQL DB   │
                                                        │  (Serverless)   │
                                                        └─────────────────┘
```

## Components

### Frontend ([frontend/](frontend/))
- **Tech**: Vite + React 18
- **Hosting**: Azure Static Web Apps
- **Features**: User selection, dynamic menu rendering based on permissions
- **Deployment**: GitHub Actions → Azure Static Web Apps

### Backend API ([backend/MenuApi/](backend/MenuApi/))
- **Tech**: .NET 8 + Azure Functions (Isolated Worker)
- **Hosting**: Azure Functions (Custom Container)
- **Features**:
  - `/api/menu?user=<name>` - Returns menu items user can access
  - OpenFGA integration for authorization checks
  - Hardcoded menu items with dynamic permission filtering

### Authorization Server
- **Tech**: OpenFGA (custom fork with SQL Server driver)
- **Hosting**: Same container as Functions API (sidecar pattern)
- **Storage**: Azure SQL Database
- **Features**:
  - Relationship-based access control (ReBAC)
  - Role-based permissions (admin, editor, viewer)
  - Runs migrations and initializes model/data on startup

## Authorization Model

**Type Definitions**:
- `user` - Individual users (alice, bob, charlie)
- `role` - Permission groups (admin, editor, viewer)
- `menu_item` - UI elements (dashboard, users, settings, reports)

**Relations**:
- `role#assignee` - Users assigned to a role
- `menu_item#viewer_role` - Roles that can view a menu item
- `menu_item#viewer` - Computed: users who can view (direct or via role)

**Example**:
```
user:bob ──assignee──▶ role:viewer ──viewer_role──▶ menu_item:dashboard
                            │
                            └─▶ Inherits viewer permission via role
```

## Deployment

### Infrastructure
- **Frontend**: Azure Static Web App (free tier)
- **Backend**: Azure Functions Premium Plan (custom container)
- **Database**: Azure SQL Database Serverless (Basic)
- **Registry**: Azure Container Registry

### CI/CD Pipelines

**Frontend** ([.github/workflows/azure-static-web-apps-*.yml](.github/workflows/))
```
Trigger: frontend/** changes
Build: npm install && npm run build
Deploy: Azure/static-web-apps-deploy
```

**Backend** ([.github/workflows/azure-backend-deploy.yml](.github/workflows/azure-backend-deploy.yml))
```
Trigger: backend/**, openfga-fork/**, Dockerfile.combined changes
Steps:
  1. Build OpenFGA binary (Go 1.24)
  2. Build container (OpenFGA + .NET 8 API)
  3. Push to Azure Container Registry
  4. Deploy to Azure Functions
  5. Configure app settings
  6. Run smoke tests
```

## Container Structure ([Dockerfile.combined](Dockerfile.combined))

```dockerfile
FROM golang AS openfga-builder
# Copy pre-built binary from CI

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
# Install OpenFGA binary
# Copy .NET application
# Copy OpenFGA config (model.json, seed-data.json)
# Set entrypoint: start.sh
```

**Startup Flow** ([backend/MenuApi/start.sh](backend/MenuApi/start.sh)):
1. Run OpenFGA migrations on SQL Server
2. Start OpenFGA server (port 8080)
3. Wait for health check (up to 3 min)
4. Create/find OpenFGA store
5. Upload authorization model
6. Load seed data (user-role-menu relationships)
7. Start Azure Functions host (port 80)

## Environment Variables

**Azure Function App Settings**:
```bash
WEBSITES_PORT=80                          # Container exposes port 80
WEBSITES_ENABLE_APP_SERVICE_STORAGE=false # Use container filesystem

OPENFGA_API_URL=http://localhost:8080     # Internal communication
OPENFGA_STORE_ID=01K785TE28A2Z3NWGAABN1TE8E
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_DATASTORE_URI=sqlserver://user:pass@host:1433?database=...

OPENFGA_LOG_FORMAT=json
DEPLOYMENT_SHA=<git-sha>
DEPLOYMENT_TIME=<timestamp>
```

## Local Development

### Quick Start (Recommended)

**One command to start everything:**

```bash
npm run dev
```

This starts:
- OpenFGA server (port 8080)
- Azure Functions API (port 7071)
- React frontend (port 5173)

**First time setup:**

```bash
# Install dependencies
npm install
cd frontend && npm install && cd ..

# Start full stack
npm run dev

# Open browser
open http://localhost:5173
```

See [QUICK-START.md](QUICK-START.md) for TL;DR or [LOCAL-DEV-GUIDE.md](LOCAL-DEV-GUIDE.md) for detailed documentation.

### Individual Services

```bash
# Just frontend
npm run frontend

# Just backend (requires OpenFGA)
npm run backend

# Just OpenFGA
npm run openfga

# Stop everything
npm run stop
```

### Prerequisites
- .NET 8 SDK
- Azure Functions Core Tools v4 (`npm install -g azure-functions-core-tools@4`)
- Node.js 18+
- OpenFGA binary (pre-built at `openfga-fork/openfga`)

### Container (Full Stack)
```bash
docker build -f Dockerfile.combined -t menu-app .
docker run -p 80:80 \
  -e OPENFGA_DATASTORE_URI="sqlserver://..." \
  -e OPENFGA_STORE_ID="..." \
  menu-app
```

## GitHub Secrets Required

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal JSON for Azure login |
| `AZURE_FUNCTIONAPP_NAME` | Function App name |
| `AZURE_RESOURCE_GROUP` | Azure resource group |
| `ACR_NAME` | Container registry name |
| `ACR_USERNAME` | Registry username |
| `ACR_PASSWORD` | Registry password |
| `OPENFGA_STORE_ID` | OpenFGA store identifier |
| `SQL_CONNECTION_STRING` | Azure SQL connection (OpenFGA format) |

## Testing

**User Permissions**:
- **alice** (admin): Dashboard, Users, Settings, Reports
- **bob** (viewer): Dashboard only
- **charlie** (editor): Dashboard, Reports

**Smoke Test**:
```bash
curl "https://func-menu-app-18436.azurewebsites.net/api/menu?user=bob"
# Expected: {"menuItems":[{"id":1,"name":"Dashboard",...}]}
```

## Key Files

- [CLAUDE.md](CLAUDE.md) - This file
- [STRUCTURE.txt](STRUCTURE.txt) - Detailed file listing
- [LOCAL-SETUP-SUMMARY.txt](LOCAL-SETUP-SUMMARY.txt) - Local dev setup
- [openfga-config/model.json](openfga-config/model.json) - Authorization schema
- [openfga-config/seed-data.json](openfga-config/seed-data.json) - Sample data
- [backend/MenuApi/Program.cs](backend/MenuApi/Program.cs) - DI configuration
- [backend/MenuApi/MenuFunction.cs](backend/MenuApi/MenuFunction.cs) - API endpoints

## OpenFGA Custom Fork

**Location**: [openfga-fork/](openfga-fork/)

**SQL Server Compatibility Fixes**:
- **Row Constructor IN**: SQL Server doesn't support `(a,b,c) IN ((1,2,3))` syntax. Replaced with OR conditions in [write.go](openfga-fork/pkg/storage/sqlserver/write.go)
- **VARBINARY Casting**: Fixed binary data storage with `CAST(@pN AS VARBINARY(MAX))` for authorization models and assertions
- **Dialect-Aware SQL**: Added `NowExpr()` (SYSDATETIME vs NOW) and `LockSuffix()` (WITH UPDLOCK vs FOR UPDATE)
- **MERGE Upserts**: Replaced MySQL's `ON DUPLICATE KEY UPDATE` with SQL Server `MERGE` statements

**Key Files**:
- [pkg/storage/sqlserver/write.go](openfga-fork/pkg/storage/sqlserver/write.go) - SQL Server-specific Write implementation
- [pkg/storage/sqlserver/sqlserver.go](openfga-fork/pkg/storage/sqlserver/sqlserver.go) - WriteAssertions, WriteAuthorizationModel overrides
- [pkg/storage/sqlcommon/sqlcommon.go](openfga-fork/pkg/storage/sqlcommon/sqlcommon.go) - Dialect-aware DBInfo methods

**Build**:
```bash
cd openfga-fork
go build -o openfga ./cmd/openfga
```

The binary is built in CI and copied into the Docker image.

## Troubleshooting

**Check logs**:
```bash
az webapp log tail --name func-menu-app-18436 --resource-group rg-menu-app
```

**Download logs**:
```bash
az webapp log download --name func-menu-app-18436 --resource-group rg-menu-app
```

**Test OpenFGA directly**:
```bash
# Inside container or when running locally
curl http://localhost:8080/healthz
curl http://localhost:8080/stores
```

**Common Issues**:
- **"No menu items available"**: Authorization model not uploaded or tuples not written
- **Timeout errors**: OpenFGA taking >3 min to start (increase timeout in start.sh)
- **SQL connection errors**: Check firewall rules allow Azure services
- **Container won't start**: Check logs for missing environment variables

## License

MIT
