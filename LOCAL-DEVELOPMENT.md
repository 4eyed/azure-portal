# Local Development Guide

This guide explains how to run the Azure Functions API locally on your machine (not in Docker) for development.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- Docker (for running OpenFGA)
- `jq` (for JSON manipulation in scripts)

## Installation

### Azure Functions Core Tools

**macOS:**
```bash
brew tap azure/functions
brew install azure-functions-core-tools@4
```

**Windows:**
```bash
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

**Linux:**
```bash
wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install azure-functions-core-tools-4
```

### jq (for Makefile commands)

**macOS:**
```bash
brew install jq
```

**Windows:**
```bash
choco install jq
```

**Linux:**
```bash
sudo apt-get install jq
```

## Quick Start

### Option 1: Using Makefile (Recommended)

```bash
# Start OpenFGA, setup, and run functions locally (all in one)
make quickstart-local
```

### Option 2: Manual Steps

```bash
# 1. Start only OpenFGA in Docker
make start-openfga

# 2. Setup OpenFGA (create store, load model & data)
make setup

# 3. Run Azure Functions locally
make dev
```

The API will start on `http://localhost:7071`

## Development Workflow

### Starting Your Development Session

```bash
# Start OpenFGA (only needed once)
make start-openfga

# Wait ~10 seconds, then setup OpenFGA (only needed once)
make setup

# Start the Azure Function
make dev
```

### Making Code Changes

The Azure Functions Core Tools supports hot reload for some changes:

1. **Edit C# code** in [backend/MenuApi/MenuFunction.cs](backend/MenuApi/MenuFunction.cs)
2. **Save the file**
3. The function will automatically recompile (for most changes)
4. Test your changes immediately

For major changes (like adding new packages), you may need to restart:
```bash
# Press Ctrl+C to stop, then:
make dev
```

### Testing Your Changes

```bash
# In another terminal, test the endpoints:
make test

# Or manually:
curl "http://localhost:7071/api/menu?user=alice"
curl "http://localhost:7071/api/menu?user=bob"
curl "http://localhost:7071/api/menu?user=charlie"
```

### Opening the Frontend

```bash
make open
# Or manually open: frontend/index.html
```

## Project Structure for Development

```
backend/MenuApi/
├── MenuApi.csproj           # Project file
├── Program.cs               # Startup & dependency injection
├── MenuFunction.cs          # HTTP endpoints (edit this!)
├── host.json                # Function host configuration
├── local.settings.json      # Local environment variables
└── bin/                     # Build output (generated)
```

## Configuration

### local.settings.json

This file contains your local development settings:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "OPENFGA_API_URL": "http://localhost:8080",
    "OPENFGA_STORE_ID": "<auto-filled-by-setup>"
  }
}
```

The `OPENFGA_STORE_ID` is automatically populated when you run `make setup`.

### Manually Update Store ID

If needed, you can manually update the store ID:

```bash
make update-store-id
```

## Debugging

### Visual Studio Code

1. **Install C# Extension**
   - Install the "C# Dev Kit" extension

2. **Open the project**
   ```bash
   code backend/MenuApi
   ```

3. **Set breakpoints** in [MenuFunction.cs](backend/MenuApi/MenuFunction.cs)

4. **Start debugging**
   - Press `F5`
   - Or use the "Run and Debug" panel

VS Code will automatically detect the Azure Functions project and start the Functions host.

### Visual Studio

1. **Open the project**
   - Open `backend/MenuApi/MenuApi.csproj` in Visual Studio

2. **Set as startup project**

3. **Set breakpoints** in `MenuFunction.cs`

4. **Press F5** to start debugging

### Command Line Debugging

```bash
cd backend/MenuApi

# Run with detailed logging
func start --verbose

# Run on a different port
func start --port 7072
```

## Common Development Tasks

### Adding a New Endpoint

1. **Open** [backend/MenuApi/MenuFunction.cs](backend/MenuApi/MenuFunction.cs)

2. **Add a new function:**
   ```csharp
   [Function("MyNewEndpoint")]
   public async Task<HttpResponseData> MyNewEndpoint(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "my-endpoint")] HttpRequestData req)
   {
       _logger.LogInformation("MyNewEndpoint processing request");

       var response = req.CreateResponse(HttpStatusCode.OK);
       response.Headers.Add("Content-Type", "application/json");
       response.Headers.Add("Access-Control-Allow-Origin", "*");

       await response.WriteStringAsync(JsonSerializer.Serialize(new
       {
           message = "Hello from my new endpoint!"
       }));

       return response;
   }
   ```

3. **Save and test:**
   ```bash
   curl http://localhost:7071/api/my-endpoint
   ```

### Adding a New NuGet Package

```bash
cd backend/MenuApi
dotnet add package PackageName
```

The package will be automatically restored next time you run `make dev`.

### Viewing Logs

Logs appear in the terminal where you ran `make dev`. To see more details:

```bash
cd backend/MenuApi
func start --verbose
```

### Changing the Port

Edit [backend/MenuApi/host.json](backend/MenuApi/host.json):

```json
{
  "version": "2.0",
  "extensions": {
    "http": {
      "routePrefix": "api",
      "port": 7072
    }
  }
}
```

Don't forget to update the frontend URL in [frontend/index.html](frontend/index.html).

## Testing OpenFGA Integration

### Direct OpenFGA API Calls

```bash
# Check if alice can view dashboard
curl -X POST "http://localhost:8080/stores/$(grep OPENFGA_STORE_ID .env | cut -d'=' -f2)/check" \
  -H "Content-Type: application/json" \
  -d '{
    "tuple_key": {
      "user": "user:alice",
      "relation": "viewer",
      "object": "menu_item:dashboard"
    }
  }'
```

### OpenFGA Playground

Open the visual playground:
```bash
open http://localhost:3000
```

Or visit: http://localhost:3000

## Troubleshooting

### Port Already in Use

```bash
# Find what's using port 7071
lsof -i :7071

# Kill the process
kill -9 <PID>
```

### OpenFGA Store ID Not Found

```bash
# Check if .env exists
cat .env

# If not, run setup again
make setup
```

### Functions Not Starting

```bash
# Check .NET version
dotnet --version  # Should be 8.0.x

# Check Functions Core Tools
func --version    # Should be 4.x.x

# Restore packages manually
cd backend/MenuApi
dotnet restore
dotnet build
```

### OpenFGA Connection Failed

```bash
# Check if OpenFGA is running
curl http://localhost:8080/healthz

# If not, start it
make start-openfga
```

### CORS Issues in Frontend

The API includes CORS headers for local development. If you still have issues:

1. **Check the API is running:** http://localhost:7071/api/menu?user=alice
2. **Check browser console** for specific CORS errors
3. **Try a different browser** or use incognito mode

## Performance Tips

### Faster Startup

The isolated worker model (used here) is slower to start than the in-process model, but provides better compatibility and isolation. Startup time is typically 5-10 seconds.

### Hot Reload

Most C# changes will hot-reload automatically. Changes that require restart:
- Adding/removing functions
- Changing function attributes
- Adding NuGet packages
- Changing Program.cs

## Environment Variables

All environment variables are in [backend/MenuApi/local.settings.json](backend/MenuApi/local.settings.json):

| Variable | Description | Default |
|----------|-------------|---------|
| `OPENFGA_API_URL` | OpenFGA server URL | `http://localhost:8080` |
| `OPENFGA_STORE_ID` | OpenFGA store ID | Auto-filled by setup |
| `FUNCTIONS_WORKER_RUNTIME` | Runtime identifier | `dotnet-isolated` |

## Next Steps

- Read about [Azure Functions local development](https://docs.microsoft.com/azure/azure-functions/functions-develop-local)
- Learn about [OpenFGA](https://openfga.dev/docs)
- Check out the [API implementation](backend/MenuApi/MenuFunction.cs)
- See [deployment guide](AZURE-DEPLOYMENT.md) for production deployment

## Stopping Everything

```bash
# Stop Functions: Press Ctrl+C in the terminal running func start

# Stop OpenFGA
docker-compose stop openfga

# Or stop everything
make stop
```
