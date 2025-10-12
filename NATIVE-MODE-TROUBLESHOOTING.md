# Native Mode Troubleshooting - Complete Investigation Log

**Date**: October 11, 2025
**Status**: ✅ RESOLVED - Azure Functions Runtime Upgrade Fixed Issues

## Problem Statement (Historical)

Previously, `npm run dev:native` failed to start the Azure Functions backend due to Azure Functions Core Tools compatibility issues. These issues have been resolved with the Azure Functions runtime upgrade.

## Root Cause Analysis

### Why Container Works

The container uses a **fundamentally different approach**:

1. **Dockerfile.combined** does:
   ```dockerfile
   # Stage 2: Build .NET application
   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-build
   WORKDIR /src
   COPY backend/MenuApi/MenuApi.csproj .
   RUN dotnet restore
   COPY backend/MenuApi/ .
   RUN dotnet build -c Release -o /app/build
   RUN dotnet publish -c Release -o /app/publish

   # Stage 3: Copy published output
   WORKDIR /home/site/wwwroot
   COPY --from=dotnet-build /app/publish .
   ```

2. **start.sh** executes:
   ```bash
   cd /home/site/wwwroot
   exec /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost
   ```

3. **Key point**: Runs the **Azure Functions host binary directly** on **published DLLs**, not `func start`

### The WorkerExtensions.csproj Issue

**Discovery:**
- Azure Functions SDK auto-generates `/obj/Debug/net8.0/WorkerExtensions/WorkerExtensions.csproj` during build
- This is **NORMAL** behavior for Azure Functions isolated worker model
- When `func start` runs with default settings, it:
  1. Scans for .csproj files
  2. Finds TWO: `MenuApi.csproj` AND `WorkerExtensions.csproj`
  3. Errors with "found more than one .csproj"

**Files found:**
```
/Volumes/External/dev/openfga/backend/MenuApi/MenuApi.csproj (the real one)
/Volumes/External/dev/openfga/backend/MenuApi/obj/Debug/net8.0/WorkerExtensions/WorkerExtensions.csproj (auto-generated)
```

## All Attempted Solutions (NONE WORKED)

### Attempt #1: Clean obj/bin Folders
**Script**: `scripts/start-backend.sh` (original version)
```bash
dotnet clean --verbosity quiet || true
rm -rf obj bin
func start --port 7071 --csharp
```

**Why it failed:**
- Removes the built output
- `func start` tries to rebuild
- Build recreates `WorkerExtensions.csproj`
- Same error occurs

**Result**: ❌ FAILED

---

### Attempt #2: Use --no-build Flag
**Script**: `scripts/start-backend.sh` (updated version)
```bash
dotnet build -c Debug --verbosity quiet
func start --port 7071 --no-build
```

**Why it failed:**
- Build succeeded, created `functions.metadata` with 11 functions
- But `func start --no-build` needs to run from bin folder
- Running from project root: "No job functions found"

**Result**: ❌ FAILED - Wrong working directory

---

### Attempt #3: Run from bin/Debug/net8.0 Folder
**Script**: `scripts/start-backend.sh` (final version)
```bash
dotnet build -c Debug --verbosity quiet
BUILD_OUTPUT="$PROJECT_ROOT/backend/MenuApi/bin/Debug/net8.0"
cd "$BUILD_OUTPUT"
func start --port 7071 --no-build
```

**Why it failed:**
- `func start` launched successfully
- Read `functions.metadata` (11 functions detected)
- Started worker runtime
- But then: "No job functions found" error
- Verbose logs showed it loaded extensions but couldn't find function methods

**Possible reasons:**
1. `func start` may need to run from project root with .csproj present
2. Missing metadata in DLLs or incorrect worker configuration
3. Mismatch between `functions.metadata` and actual DLL contents
4. `func start` may not be designed to run from bin folder directly

**Result**: ❌ FAILED - Functions not discovered

---

### Attempt #4: Use dotnet publish Instead
**Not tested fully, but theory:**
```bash
dotnet publish -c Debug -o bin/publish
cd bin/publish
func start --port 7071 --no-build
```

**Why this might also fail:**
- Same "no functions found" issue likely
- `func start` appears to need project context, not just DLLs

---

## Key Differences: Container vs Native

| Aspect | Container (WORKS) | Native Mode (FAILS) |
|--------|-------------------|---------------------|
| **Startup Tool** | `/azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost` | `func start` CLI |
| **Working Directory** | `/home/site/wwwroot` (published output) | Project root or bin folder |
| **Build Output** | `dotnet publish` (optimized) | `dotnet build` (with symbols) |
| **Configuration** | `local.settings.json` via environment | `local.settings.json` loaded by func |
| **.csproj Scanning** | No scanning - direct host execution | `func start` scans for projects |
| **Function Discovery** | `functions.metadata` + DLL reflection | `func start` custom discovery |

## File Structure Verification

**After `dotnet build`, bin/Debug/net8.0 contains:**
```
✅ MenuApi.dll (122,880 bytes)
✅ functions.metadata (11 functions defined)
✅ host.json
✅ local.settings.json
✅ worker.config.json
✅ extensions.json
✅ All dependency DLLs (Azure.*, Microsoft.*, etc.)
✅ .azurefunctions/ folder with extensions
```

**functions.metadata shows:**
- CreateMenuGroup
- CreateMenuItem
- DeleteMenuGroup
- DeleteMenuItem
- GenerateEmbedToken
- GetMenuStructure
- GetPowerBIReports
- GetPowerBIWorkspaces
- HealthCheck
- UpdateMenuGroup
- UpdateMenuItem

All 11 functions are properly defined with HTTP triggers.

## Environment Details

```
.NET SDK: 8.0.401
Azure Functions Core Tools: 4.0.5348
Go: 1.25.2
Node: >= 18.0.0
Platform: macOS (Darwin 25.0.0)
```

## Conclusion & Recommendation

**✅ RESOLVED (October 11, 2025 - Final Update):**

### What Fixed It

**Azure Functions Runtime Upgrade** resolved all native mode issues:
- Upgraded to latest Azure Functions Core Tools
- All 11 functions now start successfully
- No more ".csproj scanning" issues
- No more "No job functions found" errors
- Native mode now works perfectly on Apple Silicon (arm64)

### Confirmed Working Output
```bash
[backend] Functions:
[backend]       CreateMenuGroup: [POST] http://localhost:7071/api/menu-groups
[backend]       CreateMenuItem: [POST] http://localhost:7071/api/menu-items
[backend]       DeleteMenuGroup: [DELETE] http://localhost:7071/api/menu-groups/{id:int}
[backend]       DeleteMenuItem: [DELETE] http://localhost:7071/api/menu-items/{id:int}
[backend]       GenerateEmbedToken: [POST] http://localhost:7071/api/powerbi/embed-token
[backend]       GetMenuStructure: [GET] http://localhost:7071/api/menu-structure
[backend]       GetPowerBIReports: [GET] http://localhost:7071/api/powerbi/reports
[backend]       GetPowerBIWorkspaces: [GET] http://localhost:7071/api/powerbi/workspaces
[backend]       HealthCheck: [GET] http://localhost:7071/api/health
[backend]       UpdateMenuGroup: [PUT] http://localhost:7071/api/menu-groups/{id:int}
[backend]       UpdateMenuItem: [PUT] http://localhost:7071/api/menu-items/{id:int}
```

### Current Recommendations

**Both modes now work equally well:**

✅ **Container Mode** (Production-like environment)
```bash
npm run dev  # OpenFGA + API in container
```

✅ **Native Mode** (Faster for development)
```bash
npm run dev:native  # OpenFGA + API natively
```

**Choose based on your needs:**
- **Container mode**: Matches production exactly, slower first build
- **Native mode**: Faster iteration, easier debugging, now fully working

## What Now Works

**Both modes are fully functional:**

```bash
# Native mode (recommended for active development)
npm run dev:native

# Container mode (recommended for production testing)
npm run dev
```

**Native Mode Benefits:**
- ✅ Faster startup after dependencies installed
- ✅ Easier debugging with breakpoints
- ✅ **Hot reload available** - Use `npm run dev:watch` for automatic rebuild when C# files change
- ✅ All 11 functions working
- ✅ Works on Apple Silicon (arm64)

**Container Mode Benefits:**
- ✅ Matches production environment exactly
- ✅ Both OpenFGA + API in one container
- ✅ No local tooling dependencies
- ✅ Consistent across all platforms

## References

- [Dockerfile.combined](Dockerfile.combined) - Container build
- [backend/MenuApi/start.sh](backend/MenuApi/start.sh) - Container startup
- [scripts/start-backend.sh](scripts/start-backend.sh) - Native mode startup
- [CLAUDE.md](CLAUDE.md) - Project documentation

---

## Historical Note

This document was kept for historical reference. The native mode issues were fully resolved with the Azure Functions runtime upgrade. All workarounds described in the earlier sections are no longer necessary.
