# SQL Tester UI - Feature Summary

**Created**: October 13, 2025
**Location**: Header toolbar button

## Overview

Added a comprehensive SQL Tester dialog to the frontend UI that provides access to all the backend debugging endpoints through an intuitive interface.

## Access

Click the **🔌 SQL Tester** button in the header toolbar (top-right, next to fullscreen and info buttons).

## Features

### 4 Tabs with Different Tests

#### 1️⃣ **Quick Test**
- Fast basic connectivity test
- Shows database info, auth method, table counts
- Same as the original popup test, but with better UI
- **Endpoint**: `/api/test-sql`

**What it shows:**
- ✅ Database name and server
- ✅ Authentication method (Managed Identity vs SQL Auth)
- ✅ Current SQL user and login
- ✅ Table counts (MenuItems, MenuGroups, PowerBIConfigs)
- ✅ Environment info (Azure vs Local)
- ✅ Authentication mode (User Token vs Managed Identity)

#### 2️⃣ **Detailed Test**
- Comprehensive connectivity test
- Tests **6 different connection methods**
- Shows detailed diagnostic report
- **Endpoint**: `/api/debug/sql-test`

**What it tests:**
1. ✅ Managed Identity (Azure production default)
2. ✅ Username/Password (fallback option)
3. ✅ User SQL Token (like local dev)
4. ✅ Direct SQL query execution
5. ✅ EF Core DbContext operations
6. ✅ OpenFGA connectivity

**Perfect for:**
- Diagnosing connection failures
- Testing different auth methods
- Identifying which connection method works
- Troubleshooting Managed Identity issues

#### 3️⃣ **Health Check**
- Checks all system components
- Shows detailed status for each
- **Endpoint**: `/api/health?verbose=true`

**What it checks:**
- ✅ **API**: Status, version, environment
- ✅ **Database**: Connectivity, auth method, response time, table count
- ✅ **OpenFGA**: Health, store ID, response time
- ✅ **Configuration**: Missing settings, environment issues

**Status indicators:**
- 🟢 Healthy - All components working
- 🟡 Degraded - Some issues detected
- 🔴 Unhealthy - Critical component failed

#### 4️⃣ **Configuration**
- View sanitized configuration
- See environment variables (passwords hidden)
- Check connection string formats
- **Endpoint**: `/api/debug/config`

**What it shows:**
- 🔍 Environment detection (Azure vs Local)
- 🔍 Database connection string (sanitized)
- 🔍 OpenFGA configuration
- 🔍 Azure Functions settings
- 🔍 Process information
- 🔍 SQL Token context status

## UI/UX Features

### Beautiful Design
- Modern gradient header (purple to violet)
- Smooth animations and transitions
- Color-coded results (green = success, red = error, yellow = warning)
- Responsive layout (works on mobile)

### User-Friendly
- Tabbed interface for easy navigation
- Clear status indicators
- Formatted output (both pretty and raw)
- Console-style logs for detailed reports
- Copy-friendly format

### Smart Display
- Sanitized sensitive data (passwords hidden)
- Pretty-printed JSON where appropriate
- Console-style logs for technical details
- Expandable sections for detailed info
- Responsive grid layout for health checks

## Usage Examples

### Example 1: Quick Connection Test
1. Click **🔌 SQL Tester** button
2. Stay on "Quick Test" tab
3. Click **▶️ Run Quick Test**
4. See results in ~1 second

**Use when:** You want to quickly verify database connectivity

### Example 2: Diagnose Connection Issues
1. Click **🔌 SQL Tester** button
2. Switch to "Detailed Test" tab
3. Click **▶️ Run Detailed Test**
4. Read the report to see which methods work/fail

**Use when:** Database connection is failing and you need to know why

### Example 3: Check System Health
1. Click **🔌 SQL Tester** button
2. Switch to "Health Check" tab
3. Click **▶️ Run Health Check**
4. Review status of all components

**Use when:** You want to verify entire system is healthy

### Example 4: Debug Configuration
1. Click **🔌 SQL Tester** button
2. Switch to "Configuration" tab
3. Click **▶️ View Configuration**
4. Check environment variables and settings

**Use when:** You suspect configuration issues or missing variables

## Screenshots Reference

### Quick Test Results
```
✅ Connection Successful

Database Information
• Database: db-menu-app
• Server: sqlsrv-menu-app-24259.database.windows.net
• Auth Method: User SQL Token

SQL User
• Current User: dbo
• Login: eric@4eyed.com

Tables Found
• MenuItems: 5
• MenuGroups: 4
• PowerBI Configs: 0

Environment
• Platform: Local
• Auth Mode: User Token
• Token Length: 1234 chars
```

### Health Check (All Healthy)
```
✅ System Healthy

✅ API
• Status: healthy
• Version: 1.0.0
• Environment: Local

✅ Database
• Server: sqlsrv-menu-app-24259.database.windows.net
• Database: db-menu-app
• Auth: User SQL Token
• Menu Groups: 4
• Response Time: 234ms

✅ OpenFGA
• URL: http://localhost:8080
• Store ID: 01JASE...
• Response Time: 12ms

✅ Configuration
• Environment: Local
• No issues detected
```

### Detailed Test (Partial Output)
```
========================================
SQL Server Connectivity Test Report
Timestamp: 2025-10-13 14:32:15 UTC
========================================

Environment: Local
Site Name: local

Connection String Analysis:
  Server=sqlsrv-menu-app-24259.database.windows.net
  Database=db-menu-app
  User ID=dbuser
  Password=[SET]
  Authentication: [default - SQL auth]
  🔑 Uses username/password

========================================
TEST 1: Managed Identity Connection
========================================
⏩ SKIPPED: No Managed Identity in local environment

========================================
TEST 2: Username/Password Connection
========================================
✅ SUCCESS - Connected in 456ms
   Server Version: 12.00.0000
   Current User: dbuser
   Current DB: db-menu-app

[... more tests ...]
```

## Files Created

1. **[frontend/src/components/Debug/SqlTesterDialog.tsx](frontend/src/components/Debug/SqlTesterDialog.tsx)** - Main dialog component
2. **[frontend/src/components/Debug/SqlTesterDialog.css](frontend/src/components/Debug/SqlTesterDialog.css)** - Styling

## Files Modified

1. **[frontend/src/components/Layout/Header.tsx](frontend/src/components/Layout/Header.tsx)** - Added SQL Tester button

## Benefits

### For Developers
- 🚀 Instant access to all debugging tools
- 🚀 No need to use curl or Postman
- 🚀 Visual feedback on connection status
- 🚀 Copy-paste friendly output
- 🚀 All tests in one place

### For Troubleshooting
- 🔍 Test 6 different connection methods
- 🔍 See exactly which method works
- 🔍 Identify configuration issues
- 🔍 Verify system health quickly
- 🔍 Get actionable error messages

### For Production
- ✅ Safe read-only operations
- ✅ Passwords sanitized in output
- ✅ No data modifications
- ✅ Works in both local and Azure
- ✅ Respects authentication

## Technical Details

### State Management
- Uses React hooks (`useState`)
- Independent state for each tab
- Preserves results when switching tabs

### API Integration
- Uses existing `apiClient` service
- MSAL token authentication
- Proper error handling
- Loading states

### Responsive Design
- Works on desktop, tablet, mobile
- Scrollable content areas
- Collapsible sections
- Touch-friendly buttons

## Future Enhancements (Optional)

- [ ] Auto-refresh health check every 30s
- [ ] Export test results to JSON/text file
- [ ] Compare results over time
- [ ] Add "Run All Tests" button
- [ ] Connection history/logs
- [ ] Performance metrics chart

## Testing the UI

### Local Development
```bash
# Start the app
npm run dev

# Open browser: http://localhost:5173
# Login with your account
# Click "🔌 SQL Tester" button in header
# Try each tab
```

### Production (Azure)
```bash
# Open your Azure Static Web App URL
# Login
# Click "🔌 SQL Tester" button
# Run tests to diagnose production issues
```

## Troubleshooting

### "Cannot find module" error
- Ensure `SqlTesterDialog.tsx` and `SqlTesterDialog.css` exist in `frontend/src/components/Debug/`
- Check imports in `Header.tsx`

### Styling issues
- Clear browser cache
- Check CSS file is properly imported
- Verify classnames match between TSX and CSS

### API errors
- Check backend endpoints are running
- Verify API base URL is correct
- Check browser console for errors

## Related Documentation

- [DEBUGGING-AZURE-CONNECTIVITY.md](DEBUGGING-AZURE-CONNECTIVITY.md) - Backend debugging guide
- [DEBUGGING-QUICK-REFERENCE.md](DEBUGGING-QUICK-REFERENCE.md) - Quick reference for commands

---

**Pro Tip**: Keep this dialog open while troubleshooting to quickly re-test after making changes! 🚀
