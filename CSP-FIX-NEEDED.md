# Content Security Policy Fix Needed

## Issue in Console Logs

```
Refused to frame 'https://login.microsoftonline.com/' because it violates the following Content Security Policy directive: "frame-src 'self' https://*.powerbi.com".
```

## Root Cause

The Content Security Policy in `staticwebapp.config.json` doesn't allow framing Microsoft login pages, which MSAL uses for silent token acquisition via iframe.

## Current CSP Configuration

[frontend/public/staticwebapp.config.json:43](frontend/public/staticwebapp.config.json#L43):
```json
"Content-Security-Policy": "default-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://*.powerbi.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; connect-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net; img-src 'self' data: https:; frame-src 'self' https://*.powerbi.com;"
```

**Problem**: `frame-src` only allows `'self'` and `https://*.powerbi.com`, but MSAL needs to frame `https://login.microsoftonline.com`.

## Impact

**Low priority** - This is causing the SQL token acquisition warnings but NOT preventing authentication:

```
Failed to acquire SQL delegated token silently; falling back to backend identity.
BrowserAuthError: monitor_window_timeout
```

**Why it's low priority**:
- Main authentication (SWA) works fine
- Backend uses Managed Identity for SQL (not delegated tokens) in production
- X-SQL-Token header is only used in local development
- The error is caught and handled gracefully

## Fix (If Needed)

If you want to eliminate the console errors, update [frontend/public/staticwebapp.config.json:43](frontend/public/staticwebapp.config.json#L43):

**Current**:
```json
"frame-src 'self' https://*.powerbi.com;"
```

**Updated**:
```json
"frame-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net;"
```

**Full updated CSP**:
```json
"Content-Security-Policy": "default-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://*.powerbi.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; connect-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net; img-src 'self' data: https:; frame-src 'self' https://*.powerbi.com https://*.microsoftonline.com https://*.msauth.net;"
```

## Alternative: Disable SQL Token Acquisition in Production

Since Azure uses Managed Identity for SQL in production, we could disable SQL token acquisition entirely when running in Azure.

**Update**: [frontend/src/services/apiClient.ts:49-82](frontend/src/services/apiClient.ts#L49-L82)

Add environment check:
```typescript
async function attachSqlAccessToken(headers: Headers): Promise<void> {
  // Skip SQL token acquisition in production (Azure uses Managed Identity)
  if (import.meta.env.PROD) {
    console.debug('Production mode: skipping SQL delegated token (using Managed Identity)');
    return;
  }

  // Local development: Acquire SQL delegated token
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) {
    console.warn('No authenticated MSAL account available; skipping SQL token header.');
    return;
  }
  // ... rest of existing code
}
```

**Benefits**:
- No CSP violation in production
- Cleaner console logs
- SQL token acquisition only happens in local dev (where it's needed)

## Recommendation

**Option 1 (Simplest)**: Update CSP to allow Microsoft auth iframes
- Single line change in staticwebapp.config.json
- Fixes console errors
- No code changes needed

**Option 2 (Cleaner)**: Disable SQL token acquisition in production
- Requires code change in apiClient.ts
- More explicit separation of dev vs prod behavior
- Reduces unnecessary token acquisition attempts

**Option 3 (Do Nothing)**:
- Error is benign and handled gracefully
- Focus on admin role fix first
- Revisit later if console noise becomes problematic

## Status

- [x] Issue documented
- [ ] Fix applied (waiting for admin role fix to be verified first)
- [ ] Tested in production
- [ ] Console errors resolved

## Related Files

- [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json#L43) - CSP configuration
- [frontend/src/services/apiClient.ts](frontend/src/services/apiClient.ts#L49-L82) - SQL token acquisition
- [backend/MenuApi/Extensions/HttpRequestExtensions.cs](backend/MenuApi/Extensions/HttpRequestExtensions.cs#L63-L106) - Backend SQL token handling
