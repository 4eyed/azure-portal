# Local Development Guide

Get the entire stack (OpenFGA + API + Frontend) running locally with one command.

## Prerequisites

Before running locally, ensure you have:

1. **Node.js 18+** - For the frontend
   ```bash
   node --version  # Should be 18.x or higher
   ```

2. **.NET 8 SDK** - For the Azure Functions backend
   ```bash
   dotnet --version  # Should be 8.x
   ```

3. **Azure Functions Core Tools v4** - For running Functions locally
   ```bash
   func --version  # Should be 4.x
   npm install -g azure-functions-core-tools@4
   ```

4. **OpenFGA Binary** - Already built at `openfga-fork/openfga`
   ```bash
   # Verify it exists
   ls -lh openfga-fork/openfga  # Should show ~86MB file

   # If missing, build it:
   cd openfga-fork
   go build -o openfga ./cmd/openfga
   cd ..
   ```

5. **Azure SQL Connection** - Database connection string in `backend/MenuApi/local.settings.json`
   - Should already be configured from your setup
   - Points to: `sqlsrv-menu-app-24259.database.windows.net`

## Quick Start

### 1. Install Dependencies

First time only:

```bash
# Install root dependencies (concurrently)
npm install

# Install frontend dependencies
cd frontend && npm install && cd ..
```

Or use the helper:

```bash
npm run install-all
```

### 2. Start Everything

```bash
npm run dev
```

This single command starts:
- **OpenFGA** on port 8080 (authorization server)
- **Backend API** on port 7071 (Azure Functions)
- **Frontend** on port 5173 (React + Vite)

You'll see colored output like:

```
[openfga] 🚀 Starting OpenFGA locally...
[openfga] ✅ OpenFGA is ready! (took 4s)
[backend] 🚀 Starting Azure Functions backend...
[backend] ✅ Backend starting...
[frontend] VITE v7.1.7  ready in 523 ms
[frontend] ➜  Local:   http://localhost:5173/
```

### 3. Test It

**Frontend (in browser):**
```
http://localhost:5173
```

Select a user (alice, bob, charlie) and see their authorized menu items.

**Backend API (curl):**
```bash
# Alice (admin) - sees all menu items
curl http://localhost:7071/api/menu?user=alice

# Bob (viewer) - sees only dashboard
curl http://localhost:7071/api/menu?user=bob

# Charlie (editor) - sees dashboard and reports
curl http://localhost:7071/api/menu?user=charlie
```

**OpenFGA (health check):**
```bash
curl http://localhost:8080/healthz
```

### 4. Stop Everything

Press `Ctrl+C` in the terminal running `npm run dev`, or:

```bash
npm run stop
```

This kills all processes on ports 8080, 7071, and 5173.

## Individual Services

You can also run services independently:

```bash
# Just the frontend
npm run frontend

# Just the backend (requires OpenFGA running)
npm run backend

# Just OpenFGA
npm run openfga
```

## What Happens Behind the Scenes

### OpenFGA Startup (`scripts/start-openfga.sh`)

1. Checks if OpenFGA binary exists
2. Extracts SQL connection string from `local.settings.json`
3. Runs database migrations
4. Starts OpenFGA server on port 8080
5. Creates/finds store with ID `01K785TE28A2Z3NWGAABN1TE8E`
6. Uploads authorization model from `openfga-config/model.json`
7. Loads seed data (alice, bob, charlie permissions) from `openfga-config/seed-data.json`

### Backend Startup (`scripts/start-backend.sh`)

1. Checks for .NET SDK and Azure Functions Core Tools
2. Waits for OpenFGA to be healthy on port 8080
3. Changes to `backend/MenuApi` directory
4. Loads config from `local.settings.json`:
   - `OPENFGA_API_URL=http://localhost:8080`
   - `OPENFGA_STORE_ID=01K785TE28A2Z3NWGAABN1TE8E`
5. Starts Azure Functions host on port 7071

### Frontend Startup

1. Changes to `frontend` directory
2. Uses Vite dev server (port 5173)
3. Loads `VITE_API_URL=http://localhost:7071/api` from `.env.local`

## Troubleshooting

### Port Already in Use

If you see "port already in use" errors:

```bash
# Stop everything
npm run stop

# Or manually kill specific ports
lsof -ti:8080 | xargs kill -9  # OpenFGA
lsof -ti:7071 | xargs kill -9  # Backend
lsof -ti:5173 | xargs kill -9  # Frontend
```

### OpenFGA Won't Start

Check the logs:

```bash
tail -f /tmp/openfga.log
```

Common issues:
- **SQL connection timeout**: Check Azure SQL firewall allows your IP
- **Migration errors**: May already be applied, usually safe to ignore

### Backend Can't Connect to OpenFGA

Ensure OpenFGA is running:

```bash
curl http://localhost:8080/healthz
# Should return: {"status":"SERVING"}
```

Check the store ID matches:

```bash
# In backend/MenuApi/local.settings.json
"OPENFGA_STORE_ID": "01K785TE28A2Z3NWGAABN1TE8E"
```

### Frontend Shows "Failed to fetch"

1. Check backend is running:
   ```bash
   curl http://localhost:7071/api/menu?user=alice
   ```

2. Check CORS (browser console for errors)

3. Verify `.env.local` in frontend:
   ```bash
   cat frontend/.env.local
   # Should show: VITE_API_URL=http://localhost:7071/api
   ```

### "Store ID not found" Error

The OpenFGA store may need to be recreated. The script handles this automatically, but if you see persistent errors:

```bash
# Stop everything
npm run stop

# Remove OpenFGA state (forces clean initialization)
rm /tmp/openfga.* 2>/dev/null || true

# Restart
npm run dev
```

## File Structure

```
/Volumes/External/dev/openfga/
├── package.json                    # Root scripts (npm run dev)
├── scripts/
│   ├── start-openfga.sh           # OpenFGA startup
│   ├── start-backend.sh           # Backend startup
│   └── stop-all.sh                # Cleanup script
├── frontend/
│   ├── .env.local                 # VITE_API_URL=http://localhost:7071/api
│   └── package.json               # npm run dev → vite
├── backend/MenuApi/
│   ├── local.settings.json        # OPENFGA_* env vars
│   ├── Program.cs                 # OpenFGA client setup
│   └── MenuFunction.cs            # /api/menu endpoint
├── openfga-config/
│   ├── model.json                 # Authorization schema
│   └── seed-data.json             # User-role-menu tuples
└── openfga-fork/
    └── openfga                    # Pre-built binary (86MB)
```

## Environment Variables

### Backend (`backend/MenuApi/local.settings.json`)

```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "OPENFGA_API_URL": "http://localhost:8080",
    "OPENFGA_STORE_ID": "01K785TE28A2Z3NWGAABN1TE8E",
    "DOTNET_CONNECTION_STRING": "Server=...;Database=db-menu-app;..."
  }
}
```

### Frontend (`frontend/.env.local`)

```
VITE_API_URL=http://localhost:7071/api
```

## User Permissions (Seed Data)

| User    | Role   | Can View                           |
|---------|--------|-----------------------------------|
| alice   | admin  | Dashboard, Users, Settings, Reports |
| bob     | viewer | Dashboard only                     |
| charlie | editor | Dashboard, Reports                 |

## Next Steps

- **Add new menu items**: Edit `backend/MenuApi/MenuFunction.cs`
- **Add new users**: Modify `openfga-config/seed-data.json`
- **Change permissions**: Update `openfga-config/seed-data.json`
- **Modify authorization model**: Edit `openfga-config/model.json`

After changing OpenFGA config, restart with:

```bash
npm run stop
npm run dev
```

The startup scripts will re-initialize OpenFGA with the new configuration.

## Additional Resources

- [OpenFGA Documentation](https://openfga.dev/docs)
- [Azure Functions Local Development](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-local)
- [Vite Documentation](https://vitejs.dev/)
- [Project README](./README.md)
