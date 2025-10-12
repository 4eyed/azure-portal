# Frontend Debugging Guide

## Environment Variable Debugging Features

The frontend now includes comprehensive debugging tools to help diagnose configuration issues.

## Features Added

### 1. Visual Environment Debugger (Bug Icon)

**Location**: Bottom-right corner of the app

**What it shows**:
- ✅ Green: Variable is set correctly
- ⚠️ Yellow: Optional variable not set
- ❌ Red: Required variable missing

**How to use**:
1. Look for the bug icon 🐛 in the bottom-right corner
2. Click to expand and see all environment variables
3. Values are masked for security (e.g., `baa611a0...ce26`)
4. Check the status chips to see what's missing

**When it appears**:
- Always visible in **development mode** (`npm run dev`)
- In **production**, only appears if there are **errors** (missing required variables)
- Click the bug icon to expand/collapse

### 2. Console Logging

Open browser DevTools (F12) and check the Console tab. You'll see:

#### Azure AD Configuration (🔐)
```
🔐 Azure AD Configuration
  Build Mode: development
  Is Dev: true
  Client ID: baa611a0...ce26
  Tenant ID: ae372c45...0047
  Redirect URI: http://localhost:5173
  Authority: https://login.microsoftonline.com/ae372c45...
```

**If variables are missing**, you'll see:
```
❌ Missing required Azure AD environment variables!
Expected environment variables:
  - VITE_AZURE_CLIENT_ID: ❌ Missing
  - VITE_AZURE_TENANT_ID: ✅ Set
  - VITE_AZURE_REDIRECT_URI: ✅ Set
Check:
  - Local dev: frontend/.env file
  - Production: Azure Portal → Static Web App → Environment variables
```

#### API Client Configuration (📡 / ⚡ / 🍔)
```
📡 Menu API Client Configuration
  API URL: http://localhost:7071/api
  Mode: development
  Is Dev: true
  Will use query params: Yes (local dev)

⚡ Power BI API Client Configuration
  API URL: http://localhost:7071/api
  Mode: development

🍔 Menu Context Configuration
  API URL: http://localhost:7071/api
  Mode: development
```

### 3. Error Messages with Actionable Steps

If the app fails to start due to missing environment variables, you'll see a clear error with next steps:

```javascript
Error: Missing required Azure AD environment variables.
Check your .env file or Azure Portal configuration.
```

The console will show exactly which variables are missing and where to configure them.

## Debugging Scenarios

### Scenario 1: Local Development Not Working

**Symptoms**:
- App shows "Missing required Azure AD environment variables" error
- Red error indicators in the debug panel

**Solution**:
1. Open browser DevTools (F12) → Console
2. Check the 🔐 Azure AD Configuration section
3. Identify which variables show ❌ Missing
4. Check your `frontend/.env` file has all required variables:
   ```bash
   VITE_AZURE_CLIENT_ID=baa611a0-39d1-427b-89b5-d91658c6ce26
   VITE_AZURE_TENANT_ID=ae372c45-2e81-4d1c-9490-e9ac10250047
   VITE_AZURE_REDIRECT_URI=http://localhost:5173
   VITE_API_URL=http://localhost:7071/api
   ```
5. Restart dev server: `npm run dev`

### Scenario 2: Production Build Failing

**Symptoms**:
- GitHub Actions build fails with "Missing required Azure AD environment variables"
- Build logs show error during Vite build step

**Solution**:
1. Go to **Azure Portal** → Your Static Web App
2. **Settings** → **Environment variables** → **Production**
3. Verify these 3 variables exist:
   - `VITE_AZURE_CLIENT_ID`
   - `VITE_AZURE_TENANT_ID`
   - `VITE_AZURE_REDIRECT_URI`
4. If missing, add them (see [CONFIGURATION-CLEANUP.md](../CONFIGURATION-CLEANUP.md))
5. Trigger new deployment (commit or manual workflow run)

### Scenario 3: API Calls Failing

**Symptoms**:
- App loads but shows "Failed to load menu" or API errors
- Network tab shows 404 or CORS errors

**Solution**:
1. Open browser DevTools (F12) → Console
2. Check the 📡 Menu API Client Configuration
3. Verify `API URL` is correct:
   - **Local dev**: Should be `http://localhost:7071/api`
   - **Production**: Should be `/api` (relative path)
4. Check the Network tab:
   - Local dev: Calls should go to `http://localhost:7071/api/*`
   - Production: Calls should go to `/api/*` (same origin)
5. Verify backend is running (local) or linked (production)

### Scenario 4: Wrong Environment Being Used

**Symptoms**:
- App is using production URLs in development (or vice versa)
- Query parameters not being added in local dev

**Solution**:
1. Open browser DevTools (F12) → Console
2. Check `Build Mode` and `Is Dev` values:
   - Local: `Mode: development`, `Is Dev: true`
   - Production: `Mode: production`, `Is Dev: false`
3. Click the debug panel (🐛) in bottom-right
4. Check the "Build" section shows correct mode
5. If wrong mode:
   - Local: Run `npm run dev` (not `npm run build`)
   - Production: Verify build command in GitHub workflow

### Scenario 5: Environment Variables Not Updating

**Symptoms**:
- Changed `.env` file but app still shows old values
- Added variables to Azure Portal but build still fails

**Solution**:

**Local Development**:
1. Stop dev server (Ctrl+C)
2. Edit `frontend/.env`
3. Restart dev server: `npm run dev`
4. Hard refresh browser (Ctrl+Shift+R or Cmd+Shift+R)
5. Check console logs to verify new values

**Production**:
1. Update Azure Portal environment variables
2. Wait 2-3 minutes for Azure to propagate settings
3. Trigger new deployment:
   ```bash
   git commit --allow-empty -m "Trigger rebuild"
   git push
   ```
4. Wait for GitHub Actions to complete
5. Hard refresh browser on deployed site

## Understanding the Debug Output

### Status Indicators

| Icon | Meaning | Action Required |
|------|---------|-----------------|
| ✅ Green | Variable is set and valid | None |
| ⚠️ Yellow | Optional variable not set | Only needed for specific features |
| ❌ Red | Required variable missing | Must be configured |

### Environment Modes

| Mode | DEV | PROD | Where |
|------|-----|------|-------|
| `development` | ✅ true | ❌ false | Local dev (`npm run dev`) |
| `production` | ❌ false | ✅ true | Deployed to Azure |

### API URL Patterns

| Environment | VITE_API_URL | Actual URL Used | Auth Method |
|-------------|--------------|-----------------|-------------|
| Local Dev | `http://localhost:7071/api` | `http://localhost:7071/api` | Query param (`?user=alice`) |
| Production | `undefined` or `/api` | `/api` (relative) | X-MS-CLIENT-PRINCIPAL header |

## Disabling Debug Features

### For Production Deployment

The debug panel automatically hides in production **unless there are errors**. No action needed.

### For Local Development

To hide the debug panel in local dev:

**Option 1: Conditional in App.tsx**
```typescript
// Only show in dev if ?debug=true in URL
{(import.meta.env.DEV && new URLSearchParams(window.location.search).has('debug')) && <EnvDebugger />}
```

**Option 2: Remove component**
```typescript
// Comment out in App.tsx
// <EnvDebugger />
```

### To Remove Console Logging

**Option 1: Use production build locally**
```bash
npm run build
npm run preview
```

**Option 2: Comment out console statements**
Edit the files and comment out `console.group` sections:
- `src/auth/config.ts`
- `src/services/menu/client.ts`
- `src/services/powerbi/client.ts`
- `src/contexts/MenuContext.tsx`

## Quick Reference: Environment Variable Checklist

### Required Variables (All Environments)

- [ ] `VITE_AZURE_CLIENT_ID` - Azure AD Client ID
- [ ] `VITE_AZURE_TENANT_ID` - Azure AD Tenant ID
- [ ] `VITE_AZURE_REDIRECT_URI` - OAuth redirect (environment-specific)

### Optional Variables

- [ ] `VITE_API_URL` - API endpoint (defaults to `/api` if not set)
- [ ] `VITE_POWERBI_WORKSPACE_ID` - Only for Power BI features
- [ ] `VITE_POWERBI_REPORT_ID` - Only for Power BI features
- [ ] `VITE_POWERBI_EMBED_URL` - Only for Power BI features

### Local Development Setup

1. Copy `.env.example` to `.env`
2. Fill in values (use the ones from CLAUDE.md)
3. Run `npm run dev`
4. Check browser console for 🔐 and 📡 logs
5. Click 🐛 icon to verify all variables are ✅ green

### Production Setup

1. Go to Azure Portal → Static Web App
2. Settings → Environment variables → Production
3. Add the 3 required VITE_* variables
4. Trigger deployment
5. Check deployment logs for build success
6. Visit deployed site, check console (should not see errors)

## Getting Help

If you're still having issues after checking the debug output:

1. **Collect Debug Information**:
   - Screenshot of the debug panel (🐛)
   - Copy console logs (🔐 📡 ⚡ 🍔 sections)
   - Note the environment (local dev vs production)

2. **Check Documentation**:
   - [CONFIGURATION-CLEANUP.md](../CONFIGURATION-CLEANUP.md) - Configuration guide
   - [SECURITY-SETUP.md](../SECURITY-SETUP.md) - Security setup
   - [CLAUDE.md](../CLAUDE.md) - Project overview

3. **Common Fixes**:
   - Restart dev server
   - Clear browser cache (Ctrl+Shift+Delete)
   - Check backend is running (for local dev)
   - Verify Azure Portal settings (for production)
