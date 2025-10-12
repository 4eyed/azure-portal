# JA Portal - Enterprise Portal with Power BI Integration

A full-stack Azure application with hierarchical menu management, Power BI report embedding, and fine-grained authorization using OpenFGA.

## Architecture

```
┌────────────────────────────────────────────────┐
│  React Frontend (Vite + React 19)              │
│  - Azure AD Authentication (MSAL React)        │
│  - Hierarchical Menu Navigation                │
│  - Power BI Embed (powerbi-client-react)       │
│  - Material-UI Components                      │
│  - Admin Mode (Menu CRUD)                      │
└────────────────────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────┐
│  Azure Functions API (.NET 8)                  │
│  - Menu Structure API (hierarchical)           │
│  - Menu CRUD API (admin only)                  │
│  - Power BI Service (workspaces, reports)      │
│  - OpenFGA Authorization Checks                │
└────────────────────────────────────────────────┘
           │                 │                 │
           ▼                 ▼                 ▼
    ┌──────────┐     ┌──────────┐     ┌──────────────┐
    │ Azure SQL│     │ OpenFGA  │     │  Power BI    │
    │ Database │     │  Server  │     │  Service     │
    └──────────┘     └──────────┘     └──────────────┘
```

## Features

### User Mode
- **Hierarchical Navigation** - Sidebar with collapsible menu groups
- **Azure AD Authentication** - MSAL React integration
- **Power BI Embedding** - Interactive reports with filters
- **Permission-Based Access** - Menu items filtered by OpenFGA
- **Breadcrumb Navigation** - Dynamic page hierarchy
- **User Profile Display** - Avatar and account info

### Admin Mode
- **Menu Management** - Create, edit, delete menu items
- **Type Selector** - PowerBI, ExternalApp, AppComponent, RemoteModule, EmbedHTML
- **Power BI Configuration** - Workspace/report selector with live data
- **Embedding Settings** - Auto-refresh, zoom, filter panel controls
- **Inline Editing** - Edit/visibility controls on menu items
- **Admin Toggle** - Switch between user and admin modes

## Components

### Frontend ([frontend/](frontend/))
- **Tech**: Vite + React 19, TypeScript, MSAL React 3.x, Power BI Client React 2.x, Material-UI 7.x
- **Structure**: Small focused components (< 200 lines each)
- **Pages**: Dashboard, PowerBIReport
- **Components**: Layout (Sidebar, Header), Navigation (MenuGroup, MenuItem), PowerBI (Embed, ConfigModal), Admin (TypeSelector, MenuItemForm)

### Backend API ([backend/MenuApi/](backend/MenuApi/))
- **Tech**: .NET 8 + Azure Functions (Isolated Worker)
- **Endpoints**:
  - `GET /api/menu-structure` - Hierarchical menu with permissions
  - `POST /api/menu-items` - Create menu item (admin only)
  - `PUT /api/menu-items/{id}` - Update menu item (admin only)
  - `DELETE /api/menu-items/{id}` - Delete menu item (admin only)
  - `GET /api/powerbi/workspaces` - List Power BI workspaces
  - `GET /api/powerbi/reports` - List reports in workspace
  - `POST /api/powerbi/embed-token` - Generate embed token

### Database Schema
- **MenuGroup** - Hierarchical menu organization (self-referencing)
- **MenuItem** - Menu items with type, order, visibility
- **PowerBIConfig** - Embedding configuration (workspace, report, settings)
- **Authorization via OpenFGA** - Relationship-based access control

## Authorization Model

**Type Definitions**:
- `user` - Azure Entra ID users (identified by Object ID / OID)
- `role` - Permission groups (admin, editor, viewer)
- `menu_item` - UI elements (dashboard, reports, settings, etc.)

**Relations**:
- `role#assignee` - Users assigned to a role (e.g., `user:{oid}` → `role:admin`)
- `menu_item#viewer_role` - Roles that can view a menu item
- `menu_item#viewer` - Computed: users who can view (direct or via role)

**Admin Behavior**:
- Users assigned to `role:admin` bypass all menu permission checks
- Admins see ALL menu items regardless of explicit assignments
- Admins can create/edit/delete menu items and assign users to them

**Example**:
```
user:{oid-123} ──assignee──▶ role:admin ──▶ Sees ALL menus (bypass checks)
user:{oid-456} ──assignee──▶ role:viewer ──viewer_role──▶ menu_item:dashboard
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

**Frontend (.env)**:
```bash
VITE_AZURE_CLIENT_ID=<app-registration-client-id>
VITE_AZURE_TENANT_ID=<tenant-id>
VITE_AZURE_REDIRECT_URI=http://localhost:5173
VITE_API_URL=http://localhost:7071/api
VITE_POWERBI_WORKSPACE_ID=<workspace-guid>
VITE_POWERBI_REPORT_ID=<report-guid>
VITE_POWERBI_EMBED_URL=https://app.powerbi.com/reportEmbed
```

**Backend (Azure Function App Settings)**:
```bash
# Azure AD / Power BI
AZURE_CLIENT_ID=<service-principal-id>
AZURE_CLIENT_SECRET=<service-principal-secret>
AZURE_TENANT_ID=<tenant-id>

# Database
DOTNET_CONNECTION_STRING=<sql-connection-string>

# OpenFGA
OPENFGA_API_URL=http://localhost:8080
OPENFGA_STORE_ID=<store-id>
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_DATASTORE_URI=<sql-connection-string-for-openfga>

# Container Settings (Azure only)
WEBSITES_PORT=80
WEBSITES_ENABLE_APP_SERVICE_STORAGE=false
```

## Local Development

### Quick Start

**Option 1: Container Mode (Recommended)**
```bash
npm install        # Install concurrently
npm run dev        # Start container + frontend
```

**Option 2: Native Mode (For Development)**
```bash
npm install        # Install concurrently + nodemon
npm run dev:native # Start OpenFGA + backend + frontend
```

**Option 3: Native Mode with Hot Reload**
```bash
npm install        # Install dependencies
npm run dev:watch  # Start with automatic backend rebuild on C# file changes
```

**Access the app:**
- Frontend: http://localhost:5173
- Backend API: http://localhost:7071/api
- Health check: http://localhost:7071/api/health

### Prerequisites
- **Container mode:** Podman/Docker + Node.js 18+
- **Native mode:** Above + .NET 8 SDK + Azure Functions Core Tools v4 + Go 1.24

### Detailed Instructions
See [RUNNING-THE-APP.md](RUNNING-THE-APP.md) for:
- First-time setup
- Environment configuration
- Testing the API
- Troubleshooting
- Individual component startup

See [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md) for Azure AD configuration details.

### Setting Up Your First Admin User
After starting the app for the first time, you need to manually assign yourself as an admin.
See [SETUP-FIRST-ADMIN.md](SETUP-FIRST-ADMIN.md) for detailed steps:
1. Login with your Entra account
2. Extract your Entra Object ID from browser console
3. Add yourself to `role:admin` in OpenFGA using curl
4. Verify the "Admin mode" toggle appears in the sidebar
5. Create menu groups and items in Admin Mode

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

**Local Testing**:
1. Start the app: `npm run dev`
2. Login with your Entra account
3. Setup first admin user (see [SETUP-FIRST-ADMIN.md](SETUP-FIRST-ADMIN.md))
4. Enable Admin Mode and create test menus
5. Verify admin sees all menus without explicit assignments
6. Test non-admin user access (should see no menus until assigned)

**API Smoke Tests**:
```bash
# Check admin status (requires authentication in production)
curl "http://localhost:7071/api/admin/check"
# Expected: {"isAdmin":true,"userId":"..."}

# Get menu structure (filtered by user permissions)
curl "http://localhost:7071/api/menu-structure?user=YOUR_OID"
# Expected: {"menuGroups":[...]} (empty if no menus created yet)

# Health check
curl "http://localhost:7071/api/health"
# Expected: {"status":"healthy","timestamp":"..."}
```

**Authorization Testing**:
- Admins bypass permission checks and see ALL menu items
- Regular users only see menu items they're explicitly assigned to
- Use OpenFGA API to verify tuple relationships

## Documentation

- [CLAUDE.md](CLAUDE.md) - This file (project overview)
- [SETUP-FIRST-ADMIN.md](SETUP-FIRST-ADMIN.md) - Setting up your first admin user
- [RUNNING-THE-APP.md](RUNNING-THE-APP.md) - Local development setup
- [PORTAL-README.md](PORTAL-README.md) - Detailed setup guide
- [IMPLEMENTATION-SUMMARY.md](IMPLEMENTATION-SUMMARY.md) - Technical implementation details
- [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md) - Azure AD configuration and credentials
- [screenshots/Portal-Plan.md](screenshots/Portal-Plan.md) - Implementation plan and status
- [frontend/.env.example](frontend/.env.example) - Environment variable template

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

## Current Status

**✅ Implementation Complete**
- 52 files created (~3,800 lines of code)
- Frontend build: PASSING (196KB)
- Backend build: PASSING
- Azure AD app registration: Created
- Service principal for Power BI: Created

**Access the Application:**
- Frontend: http://localhost:5173
- Backend API: http://localhost:7071/api
- Credentials: See [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md)

## Troubleshooting

**Frontend Issues:**
```bash
# Check if MSAL config is correct
cat frontend/.env

# Check browser console for errors
# Verify redirect URI matches app registration
```

**Backend Issues:**
```bash
# Test API endpoint
curl http://localhost:7071/api/menu-structure?user=alice

# Check environment variables
# Verify database connection string
```

**Authentication Issues:**
- Check [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md) for app registration details
- Verify redirect URI is configured correctly
- Wait 5-10 minutes for app registration to propagate

**Power BI Issues:**
- Enable service principal in Power BI tenant settings
- Add service principal to workspace with Member role
- Update workspace/report IDs in frontend/.env

## License

MIT
