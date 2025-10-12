# Hot Reload for API Development

## Overview

When developing the backend API, you have two options for seeing your changes:

### Option 1: Manual Restart (Default)
```bash
npm run dev:native
```
- When you change a C# file, **stop** the process (Ctrl+C) and **restart**
- Simple and reliable
- No extra dependencies

### Option 2: Automatic Hot Reload (Recommended for Active Development)
```bash
npm run dev:watch
```
- Automatically detects changes to `.cs` files
- Rebuilds and restarts the backend automatically
- Uses `nodemon` to watch for file changes
- Saves time during active development

## How It Works

The `dev:watch` command uses **nodemon** to monitor C# files:

1. **Initial startup**: Builds and starts the Azure Functions backend
2. **File change detected**: When you save a `.cs` file
3. **Auto-rebuild**: Stops the backend, rebuilds, and restarts
4. **Ready**: Backend is ready with your changes (usually 5-10 seconds)

## When to Use Each Mode

**Use `npm run dev:native` when:**
- First time setup / testing
- Making infrequent changes
- You prefer manual control

**Use `npm run dev:watch` when:**
- Actively developing API features
- Making frequent changes to C# files
- You want instant feedback

## Technical Details

### Scripts Involved

1. **[scripts/start-backend.sh](scripts/start-backend.sh)**
   - Waits for OpenFGA to be ready
   - Starts Azure Functions with `func start --port 7071`
   - Used by both modes

2. **[scripts/watch-backend.sh](scripts/watch-backend.sh)**
   - Wraps `start-backend.sh` with nodemon
   - Watches `backend/MenuApi/**/*.cs` files
   - Restarts backend on changes (2.5s delay to avoid multiple restarts)

### NPM Scripts

```json
{
  "dev:native": "concurrently ... \"npm run backend\" ...",
  "dev:watch": "concurrently ... \"npm run backend:watch\" ...",
  "backend": "bash scripts/start-backend.sh",
  "backend:watch": "bash scripts/watch-backend.sh"
}
```

## Troubleshooting

**"nodemon not found" error:**
```bash
npm install  # Installs nodemon as dev dependency
```

**Backend keeps restarting:**
- Check that you're not editing files that trigger constant changes
- Adjust the delay in [scripts/watch-backend.sh](scripts/watch-backend.sh) (default: 2500ms)

**Changes not being detected:**
- Ensure you're editing `.cs` files in `backend/MenuApi/`
- Check that nodemon is watching the correct directory
- Verify file changes are being saved

## Alternative: Manual `func start`

If you prefer even more control, you can run the backend directly:

```bash
# Terminal 1: Start OpenFGA
npm run openfga

# Terminal 2: Start backend manually
cd backend/MenuApi
func start --port 7071

# Terminal 3: Start frontend
cd frontend
npm run dev
```

Then rebuild manually when needed:
```bash
# In backend/MenuApi directory
dotnet build
# func start will auto-reload
```

## Notes

- Hot reload works for **C# code changes only**
- Changes to `local.settings.json` require manual restart
- Frontend has its own hot reload via Vite (always enabled)
- Container mode doesn't support hot reload (by design)
