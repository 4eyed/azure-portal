# JA Portal - Implementation Guide

## Overview

A full-stack enterprise portal application with hierarchical menu management, Power BI report embedding, and fine-grained authorization using OpenFGA.

## Architecture

```
Frontend (React + MSAL + Power BI SDK)
    ↓
Azure Functions API (.NET 8)
    ↓
├── Azure SQL Database (Menu Structure)
├── OpenFGA (Authorization)
└── Power BI Service (Reports)
```

## Features Implemented

### ✅ Phase 1: Authentication
- Azure AD authentication with MSAL React
- Token management for Power BI API access
- User profile display

### ✅ Phase 2: Backend Data Model
- **MenuGroup** - Hierarchical menu organization
- **MenuItem** - Menu items with type support
- **MenuItemType** - PowerBI, ExternalApp, AppComponent, RemoteModule, EmbedHTML
- **PowerBIConfig** - Power BI embedding configuration

### ✅ Phase 3: API Endpoints
- `GET /api/menu-structure` - Hierarchical menu with permissions
- `POST /api/menu-items` - Create menu item (admin only)
- `PUT /api/menu-items/{id}` - Update menu item (admin only)
- `DELETE /api/menu-items/{id}` - Delete menu item (admin only)
- `GET /api/powerbi/workspaces` - List Power BI workspaces
- `GET /api/powerbi/reports` - List reports in workspace
- `POST /api/powerbi/embed-token` - Generate embed token

### ✅ Phase 4: Frontend Components
- **Sidebar** - Navigation with collapsible menu groups
- **Header** - Breadcrumbs and user actions
- **MenuGroup** - Collapsible menu groups with admin controls
- **MenuItem** - Individual menu items with routing
- **PowerBIEmbed** - Power BI report embedding
- **ConfigModal** - Admin configuration for Power BI reports
- **Admin Components** - Type selector, menu item form, admin toggle

### ✅ Phase 5: Authorization
- OpenFGA integration for menu permissions
- Admin role checks for CRUD operations
- Per-user menu filtering

## Environment Variables

### Frontend (.env)

```bash
# Azure AD (Required)
VITE_AZURE_CLIENT_ID=<your-app-registration-client-id>
VITE_AZURE_TENANT_ID=<your-tenant-id>
VITE_AZURE_REDIRECT_URI=http://localhost:5173

# API (Required)
VITE_API_URL=http://localhost:7071/api

# Power BI (Required for embedding)
VITE_POWERBI_WORKSPACE_ID=<workspace-guid>
VITE_POWERBI_REPORT_ID=<report-guid>
VITE_POWERBI_EMBED_URL=https://app.powerbi.com/reportEmbed
```

### Backend (local.settings.json or Azure App Settings)

```bash
# Database
DOTNET_CONNECTION_STRING=Server=...;Database=...;User Id=...;Password=...

# OpenFGA
OPENFGA_API_URL=http://localhost:8080
OPENFGA_STORE_ID=<store-id>
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_DATASTORE_URI=sqlserver://...

# Power BI Service Principal
AZURE_CLIENT_ID=<service-principal-client-id>
AZURE_CLIENT_SECRET=<service-principal-secret>
AZURE_TENANT_ID=<tenant-id>
```

## Setup Instructions

### 1. Azure AD App Registration

```bash
# Create app registration
az ad app create \
  --display-name "JA Portal" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "http://localhost:5173"

# Note the Application (client) ID and Tenant ID
```

Configure API permissions:
- Microsoft Graph: `User.Read`
- Power BI Service: `Report.Read.All`

### 2. Power BI Service Principal

```bash
# Create service principal
az ad sp create-for-rbac --name "PowerBI-Portal-SP"

# Enable Power BI service principal in tenant settings
# Power BI Admin Portal > Tenant settings > Developer settings
# Enable "Service principals can use Power BI APIs"
```

Add service principal to Power BI workspace:
1. Open workspace in Power BI Service
2. Access > Add member
3. Search for service principal
4. Assign "Member" or "Admin" role

### 3. Database Setup

```bash
cd backend/MenuApi

# Create migration
dotnet ef migrations add InitialCreate

# Update database
DOTNET_CONNECTION_STRING="..." dotnet ef database update
```

### 4. OpenFGA Setup

The existing OpenFGA setup will work. No additional changes needed for basic menu authorization.

### 5. Frontend Setup

```bash
cd frontend

# Copy environment template
cp .env.example .env

# Edit .env with your values
# VITE_AZURE_CLIENT_ID=...
# VITE_AZURE_TENANT_ID=...
# etc.

# Install and run
npm install
npm run dev
```

### 6. Backend Setup

```bash
cd backend/MenuApi

# Install dependencies (already done)
dotnet restore

# Run locally
func start
```

## Usage

### User Mode
1. Sign in with Azure AD
2. Navigate menu items in sidebar
3. Click menu items to view content
4. Power BI reports will embed automatically

### Admin Mode
1. Toggle "Admin mode" in sidebar footer
2. See edit/visibility controls on menu groups and items
3. Click "+ New Menu Item" to create items
4. Select "Power BI Report" type to configure embedding
5. Choose workspace and report from dropdowns
6. Configure refresh interval, zoom, filters
7. Save to create menu item

## Code Structure

### Frontend
```
frontend/src/
├── auth/                 # Authentication
│   ├── config.ts        # MSAL configuration (fail-fast)
│   ├── MsalProvider.tsx # Auth provider wrapper
│   └── useAuth.ts       # Auth hook
├── components/
│   ├── Layout/          # Sidebar, Header
│   ├── Navigation/      # MenuGroup, MenuItem
│   ├── PowerBI/         # PowerBIEmbed, ConfigModal
│   └── Admin/           # TypeSelector, MenuItemForm, AdminToggle
├── services/
│   └── powerbi/         # Power BI API client
├── pages/               # Dashboard, PowerBIReport
├── hooks/               # useAdminMode
└── App.tsx             # Main app with routing
```

### Backend
```
backend/MenuApi/
├── Models/              # MenuItem, MenuGroup, PowerBIConfig
├── Data/                # ApplicationDbContext
├── Functions/           # API endpoints (one per file)
├── Services/            # PowerBIService
└── Program.cs          # DI registration
```

## Key Design Principles

1. **Small Files** - Each component/function < 200 lines
2. **Fail Fast** - Throw errors on missing config, no fallbacks
3. **Latest Patterns** - Current MSAL, Power BI SDK, MUI patterns
4. **Single Responsibility** - One concern per file
5. **Type Safety** - TypeScript/C# with strict typing

## Deployment

### Frontend (Azure Static Web Apps)
```bash
# GitHub Actions already configured
# Triggers on frontend/** changes
```

### Backend (Azure Functions)
```bash
# Build and push container
docker build -f Dockerfile.combined -t <acr>.azurecr.io/menu-app:latest .
docker push <acr>.azurecr.io/menu-app:latest

# Update Function App
az functionapp config container set \
  --name <function-app> \
  --resource-group <rg> \
  --docker-custom-image-name <acr>.azurecr.io/menu-app:latest
```

## Troubleshooting

### Authentication Issues
- Verify Azure AD app registration redirect URIs
- Check MSAL configuration in browser console
- Ensure API permissions are granted

### Power BI Embedding Issues
- Verify service principal has workspace access
- Check embed token expiration
- Confirm workspace/report IDs are correct

### Menu Not Loading
- Check OpenFGA authorization model is loaded
- Verify user tuples exist in OpenFGA
- Check API endpoint logs for permission checks

### Admin Mode Not Working
- Verify user has admin role in OpenFGA
- Check `role:admin` tuple exists for user
- Confirm backend admin checks are passing

## Next Steps

1. **Drag-and-drop reordering** - Add menu item reordering
2. **Nested menu groups** - Support parent/child groups
3. **Menu item icons** - Icon picker component
4. **Audit logging** - Track menu changes
5. **Role management** - UI for managing OpenFGA roles
6. **Workspace permissions** - Fine-grained Power BI permissions

## Support

- Frontend issues: Check browser console for errors
- Backend issues: Check Function App logs
- Authorization issues: Check OpenFGA store data
- Power BI issues: Verify service principal permissions
