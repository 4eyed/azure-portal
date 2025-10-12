# Portal Application - Implementation Plan

## Original Requirements

Lets build the portal app. The portal is an app that allows and Admin user to configure hierarchtical menu items. A menu item can be of different types but the first one is an embeded PowerBI report. The admin area should allow the admin to select Fabric workspaces and reports and embeded them in the main body of the app.

Use the latest msal react libraries for Azure Entra authentication
Use latest Power BI react sdk to embed and customize the embeded power bi report

Use the screenshots located in the screenshots folder

## Implementation Status: ✅ COMPLETE

All phases have been implemented following the core principles:
1. **Keep code files small** - All components < 200 lines
2. **Fail fast** - No fallbacks, throw errors on missing config
3. **Follow latest patterns** - Latest MSAL React, Power BI SDK, Material-UI

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Frontend (React)                        │
│  ┌────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │   MSAL     │  │  Power BI    │  │  React Router    │   │
│  │   Auth     │  │  Client SDK  │  │  Navigation      │   │
│  └────────────┘  └──────────────┘  └──────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Azure Functions API (.NET 8)                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │   Menu       │  │  Power BI    │  │   OpenFGA        │ │
│  │   CRUD API   │  │  Service     │  │   AuthZ Checks   │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
    ┌──────────────┐ ┌──────────┐  ┌──────────────┐
    │  Azure SQL   │ │ OpenFGA  │  │  Power BI    │
    │  Database    │ │  Store   │  │  Service     │
    └──────────────┘ └──────────┘  └──────────────┘
```

---

## What Was Built

### Phase 1: Authentication (✅ Complete)
**Files Created:**
- `frontend/src/auth/config.ts` - MSAL config with fail-fast validation
- `frontend/src/auth/MsalProvider.tsx` - Minimal auth wrapper
- `frontend/src/auth/useAuth.ts` - Auth hook with token management

**Features:**
- Azure AD login with popup flow
- Access token acquisition for Power BI API
- User profile display
- Automatic token refresh

---

### Phase 2: Backend Data Models (✅ Complete)
**Files Created:**
- `backend/MenuApi/Models/MenuItemType.cs` - Enum for menu types
- `backend/MenuApi/Models/MenuGroup.cs` - Hierarchical groups
- `backend/MenuApi/Models/PowerBIConfig.cs` - Power BI settings
- Updated `backend/MenuApi/Models/MenuItem.cs` - Enhanced model
- Updated `backend/MenuApi/Data/ApplicationDbContext.cs` - EF relationships

**Database Schema:**
```sql
MenuGroup
  - Id (PK)
  - Name
  - Icon
  - ParentId (FK to MenuGroup)
  - DisplayOrder
  - IsVisible

MenuItem
  - Id (PK)
  - Name
  - Icon
  - Url
  - Description
  - Type (enum)
  - MenuGroupId (FK)
  - DisplayOrder
  - IsVisible

PowerBIConfig
  - Id (PK)
  - MenuItemId (FK)
  - WorkspaceId
  - ReportId
  - EmbedUrl
  - AutoRefreshInterval
  - DefaultZoom
  - ShowFilterPanel
  - ShowFilterPanelExpanded
```

---

### Phase 3: API Endpoints (✅ Complete)
**Menu Management (Admin Only):**
- `GET /api/menu-structure` - Hierarchical menu filtered by user permissions
- `POST /api/menu-items` - Create new menu item
- `PUT /api/menu-items/{id}` - Update menu item
- `DELETE /api/menu-items/{id}` - Delete menu item

**Power BI Integration:**
- `GET /api/powerbi/workspaces` - List available workspaces
- `GET /api/powerbi/reports?workspaceId=X` - List reports in workspace
- `POST /api/powerbi/embed-token` - Generate embed token

**Files Created:**
- `backend/MenuApi/Functions/GetMenuStructure.cs` (~90 lines)
- `backend/MenuApi/Functions/CreateMenuItem.cs` (~65 lines)
- `backend/MenuApi/Functions/UpdateMenuItem.cs` (~80 lines)
- `backend/MenuApi/Functions/DeleteMenuItem.cs` (~60 lines)
- `backend/MenuApi/Functions/GetPowerBIWorkspaces.cs` (~45 lines)
- `backend/MenuApi/Functions/GetPowerBIReports.cs` (~50 lines)
- `backend/MenuApi/Functions/GenerateEmbedToken.cs` (~70 lines)
- `backend/MenuApi/Services/PowerBIService.cs` (~90 lines)

---

### Phase 4: Frontend Components (✅ Complete)

**Layout Components:**
- `components/Layout/Sidebar.tsx` - Left navigation panel (~120 lines)
- `components/Layout/Header.tsx` - Top header with breadcrumbs (~35 lines)

**Navigation Components:**
- `components/Navigation/MenuGroup.tsx` - Collapsible menu groups (~60 lines)
- `components/Navigation/MenuItem.tsx` - Individual menu items (~35 lines)

**Power BI Components:**
- `components/PowerBI/PowerBIEmbed.tsx` - Report embedding (~80 lines)
- `components/PowerBI/ConfigModal.tsx` - Admin config dialog (~150 lines)

**Admin Components:**
- `components/Admin/TypeSelector.tsx` - Menu type dropdown (~40 lines)
- `components/Admin/MenuItemForm.tsx` - Create/edit form (~110 lines)
- `components/Admin/AdminToggle.tsx` - Admin mode switch (~25 lines)

**Pages:**
- `pages/Dashboard.tsx` - Main dashboard view
- `pages/PowerBIReport.tsx` - Report viewer page

**Services:**
- `services/powerbi/client.ts` - Power BI API client (~60 lines)

**Hooks:**
- `hooks/useAdminMode.ts` - Admin mode state management

**Main App:**
- `App.tsx` - Routing and authentication wrapper (~55 lines)

---

## UI Features Implemented

### User Mode
✅ Hierarchical sidebar navigation
✅ Collapsible menu groups
✅ Active menu item highlighting
✅ Search bar (UI only, filtering not implemented)
✅ User profile display
✅ Power BI report embedding with filters
✅ Breadcrumb navigation
✅ Fullscreen toggle (UI only)

### Admin Mode
✅ Admin mode toggle switch
✅ Edit/visibility controls on menu items
✅ "New Menu Item" buttons in menu groups
✅ Menu item type selector
✅ Power BI workspace selector (fetches real workspaces)
✅ Power BI report selector (fetches real reports)
✅ Configuration modal for Power BI settings
✅ Auto-refresh interval setting
✅ Zoom by default setting
✅ Filter panel visibility settings
✅ Generated embed URL display

---

## Security & Authorization

**OpenFGA Integration:**
- User permissions checked for each menu item view
- Admin role required for menu CRUD operations
- Backend enforces all authorization (frontend hides UI only)

**Azure AD:**
- Delegated permissions for user authentication
- Service principal for Power BI API access

---

## Configuration Required

### Azure AD App Registration
1. Create app registration for frontend
2. Configure redirect URI: `http://localhost:5173`
3. Grant permissions:
   - Microsoft Graph: `User.Read`
   - Power BI Service: `Report.Read.All`

### Power BI Service Principal
1. Create service principal for backend
2. Enable in Power BI tenant settings
3. Add to workspace with Member role

### Environment Variables

**Frontend (.env):**
```bash
VITE_AZURE_CLIENT_ID=<your-client-id>
VITE_AZURE_TENANT_ID=<your-tenant-id>
VITE_AZURE_REDIRECT_URI=http://localhost:5173
VITE_API_URL=http://localhost:7071/api
VITE_POWERBI_WORKSPACE_ID=<workspace-guid>
VITE_POWERBI_REPORT_ID=<report-guid>
VITE_POWERBI_EMBED_URL=https://app.powerbi.com/reportEmbed
```

**Backend (App Settings):**
```bash
AZURE_CLIENT_ID=<service-principal-id>
AZURE_CLIENT_SECRET=<service-principal-secret>
AZURE_TENANT_ID=<tenant-id>
DOTNET_CONNECTION_STRING=<sql-connection-string>
OPENFGA_API_URL=http://localhost:8080
OPENFGA_STORE_ID=<store-id>
```

---

## Build Status

✅ Frontend build: PASSING (196KB)
✅ Backend build: PASSING
✅ All dependencies installed
✅ Database migration created

---

## Next Steps (Future Enhancements)

1. **Drag-and-drop reordering** - Menu item ordering UI
2. **Icon picker** - Visual icon selection for menu items
3. **Nested menu groups** - Multi-level hierarchy support
4. **Search functionality** - Filter menu items by name
5. **Audit logging** - Track menu changes
6. **Role management UI** - Manage OpenFGA roles/permissions
7. **Multiple report support** - Multiple Power BI configs per menu item
8. **Custom themes** - Configurable color schemes

---

## Files Summary

**Total Files Created: 42**

- Backend: 13 files (Models, Functions, Services, Migrations)
- Frontend: 27 files (Components, Pages, Services, Hooks, Styles)
- Documentation: 2 files (README, .env.example)

**Lines of Code:**
- Backend: ~1,200 lines (C#)
- Frontend: ~2,000 lines (TypeScript/TSX)
- Styles: ~600 lines (CSS)

---

## Testing Checklist

Before deployment, test:

- [ ] Azure AD authentication flow
- [ ] Menu loading for different users (alice, bob, charlie)
- [ ] Admin mode toggle
- [ ] Create new menu item
- [ ] Edit existing menu item
- [ ] Delete menu item
- [ ] Power BI workspace selection
- [ ] Power BI report selection
- [ ] Report embedding with filters
- [ ] Permission checks (admin vs. viewer)

---

## Support & Documentation

See [PORTAL-README.md](../PORTAL-README.md) for:
- Detailed setup instructions
- Troubleshooting guide
- API documentation
- Architecture diagrams
- Deployment procedures

---

**Implementation Date:** October 11, 2025
**Status:** ✅ Ready for Testing
**Framework Versions:**
- React: 19.1.1
- .NET: 8.0
- MSAL React: 3.0.20
- Power BI Client React: 2.0.0
- Material-UI: 7.3.4
