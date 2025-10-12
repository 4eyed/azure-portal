# Running the Application

## Quick Start

The application can be run in two modes:

### Option 1: Container Mode (Recommended for Production Testing)
Runs the entire stack (OpenFGA + .NET API) in a single container:

```bash
npm run dev
```

This will:
- Build the Docker/Podman container (if not already built)
- Start OpenFGA + Azure Functions in the container (ports 80, 8080)
- Start the React frontend in development mode (port 5173)

**Access:**
- Frontend: http://localhost:5173
- Backend API: http://localhost:7071/api
- OpenFGA: http://localhost:8080

---

### Option 2: Native Mode (For Development)
Runs each component separately on your local machine:

```bash
npm run dev:native
```

This will start 3 processes concurrently:
1. **OpenFGA** - Authorization server (port 8080)
2. **Backend API** - Azure Functions (.NET 8) (port 7071)
3. **Frontend** - React app (Vite) (port 5173)

**Access:**
- Frontend: http://localhost:5173
- Backend API: http://localhost:7071/api
- OpenFGA: http://localhost:8080

---

## Prerequisites

### For Container Mode
- **Podman** or **Docker**
- **Node.js 18+** (for frontend)

### For Native Mode
All of the above, plus:
- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Azure Functions Core Tools v4** - `npm install -g azure-functions-core-tools@4`
- **Go 1.24+** - For building OpenFGA binary (one-time)

---

## First Time Setup

### 1. Install Dependencies

```bash
# Install root dependencies (concurrently)
npm install

# Install frontend dependencies
cd frontend && npm install && cd ..
```

### 2. Build OpenFGA Binary (Native Mode Only)

```bash
cd openfga-fork
go build -o openfga ./cmd/openfga
cd ..
```

### 3. Configure Environment

Copy the example environment file:

```bash
cp frontend/.env.example frontend/.env
```

Edit `frontend/.env` with your Azure AD credentials (see [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md)).

Edit `backend/MenuApi/local.settings.json` with:
- Database connection string
- OpenFGA Store ID
- Azure AD credentials (for Power BI)

---

## Development Workflow

### Starting the App

**Container mode:**
```bash
npm run dev
```

**Native mode:**
```bash
npm run dev:native
```

### Stopping the App

Press `Ctrl+C` in the terminal, or:

```bash
npm run stop
```

This will kill all running processes (OpenFGA, backend, frontend).

---

## Individual Components

You can also run components individually:

### Start OpenFGA Only
```bash
npm run openfga
```

### Start Backend API Only
```bash
npm run backend
```
**Note:** OpenFGA must be running first.

### Start Frontend Only
```bash
npm run frontend
```
**Note:** Backend must be running first.

### Run Container Only
```bash
npm run container
```

---

## Testing the API

### Health Check
```bash
curl http://localhost:7071/api/health
```

**Expected response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-10-11T...",
  "version": "1.0.0"
}
```

### Get Menu Structure (for user "alice")
```bash
curl 'http://localhost:7071/api/menu-structure?user=alice'
```

**Expected response:**
```json
{
  "menuGroups": [
    {
      "id": 1,
      "name": "CLIENT PRODUCT",
      "icon": "📦",
      "items": [
        {
          "id": 1,
          "name": "Dashboard",
          "icon": "📊",
          "url": "/dashboard",
          "description": "View your dashboard",
          "type": "AppComponent"
        },
        ...
      ]
    }
  ]
}
```

### Test Authorization

Different users have different permissions:

```bash
# Alice (admin) - sees everything
curl 'http://localhost:7071/api/menu-structure?user=alice'

# Bob (viewer) - sees only Dashboard
curl 'http://localhost:7071/api/menu-structure?user=bob'

# Charlie (editor) - sees Dashboard + Reports
curl 'http://localhost:7071/api/menu-structure?user=charlie'
```

---

## Ports Reference

| Service | Container Mode | Native Mode |
|---------|----------------|-------------|
| Frontend | 5173 | 5173 |
| Backend API | 7071 | 7071 |
| OpenFGA | 8080 | 8080 |

---

## Troubleshooting

### "Port already in use"
The scripts automatically kill processes on ports 7071 and 8080 before starting. If you see this error, manually kill the process:

```bash
# Kill process on port 7071 (Backend)
lsof -ti:7071 | xargs kill -9

# Kill process on port 8080 (OpenFGA)
lsof -ti:8080 | xargs kill -9
```

### "OpenFGA not responding"
Make sure OpenFGA starts before the backend:

```bash
# Terminal 1: Start OpenFGA
npm run openfga

# Wait for "✅ OpenFGA is ready!"
# Then in Terminal 2:
npm run backend
```

### "Cannot find WorkerExtensions.csproj"
This is normal! WorkerExtensions.csproj is auto-generated during the .NET build process. The `start-backend.sh` script handles this correctly by:
1. Running `dotnet build` (which generates WorkerExtensions)
2. Running `func start --no-build` (which doesn't try to rebuild)

### "OPENFGA_STORE_ID not set"
Check your `backend/MenuApi/local.settings.json`:

```json
{
  "Values": {
    "OPENFGA_STORE_ID": "01K785TE28A2Z3NWGAABN1TE8E",
    ...
  }
}
```

If missing, start OpenFGA once and it will display the Store ID to copy.

---

## Scripts Reference

| Command | Description |
|---------|-------------|
| `npm run dev` | Start container + frontend |
| `npm run dev:native` | Start OpenFGA + backend + frontend (native) |
| `npm run container` | Start container only |
| `npm run openfga` | Start OpenFGA server only |
| `npm run backend` | Start backend API only |
| `npm run frontend` | Start frontend only |
| `npm run stop` | Stop all running processes |
| `npm run install-all` | Install all dependencies |

---

## Environment Variables

### Backend (`backend/MenuApi/local.settings.json`)
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "OPENFGA_API_URL": "http://localhost:8080",
    "OPENFGA_STORE_ID": "01K785TE28A2Z3NWGAABN1TE8E",
    "DOTNET_CONNECTION_STRING": "Server=...;Database=...;User Id=...;Password=...;",
    "AZURE_CLIENT_ID": "your-azure-ad-client-id",
    "AZURE_CLIENT_SECRET": "your-azure-ad-client-secret",
    "AZURE_TENANT_ID": "your-azure-ad-tenant-id"
  }
}
```

### Frontend (`frontend/.env`)
```bash
VITE_AZURE_CLIENT_ID=your-azure-ad-client-id
VITE_AZURE_TENANT_ID=your-azure-ad-tenant-id
VITE_AZURE_REDIRECT_URI=http://localhost:5173
VITE_API_URL=http://localhost:7071/api
VITE_POWERBI_WORKSPACE_ID=your-powerbi-workspace-id
VITE_POWERBI_REPORT_ID=your-powerbi-report-id
VITE_POWERBI_EMBED_URL=https://app.powerbi.com/reportEmbed
```

---

## Production Deployment

The application is designed to run in a custom Azure container:

1. **Build the container:**
   ```bash
   podman build -f Dockerfile.combined -t menu-app-combined .
   ```

2. **Push to Azure Container Registry:**
   ```bash
   az acr login --name <your-acr-name>
   podman tag menu-app-combined <your-acr-name>.azurecr.io/menu-app-combined
   podman push <your-acr-name>.azurecr.io/menu-app-combined
   ```

3. **Deploy to Azure Functions:**
   - GitHub Actions handles this automatically on push to `main`
   - See `.github/workflows/azure-backend-deploy.yml`

---

## Documentation

- [CLAUDE.md](CLAUDE.md) - Project overview and architecture
- [MIGRATION-SUMMARY.md](MIGRATION-SUMMARY.md) - Recent .NET migration details
- [AZURE-AD-SETUP.md](AZURE-AD-SETUP.md) - Azure AD configuration
- [PORTAL-README.md](PORTAL-README.md) - Detailed setup guide

---

## Getting Help

If you encounter issues:
1. Check the logs in the terminal
2. Verify all prerequisites are installed
3. Check port conflicts
4. Review environment variables
5. Consult the documentation files above
