# Security Setup Guide

This guide covers the security configuration for linking Azure Static Web Apps to Azure Functions with proper authentication.

## Overview

The application uses Azure Static Web Apps' **Linked Backend** feature, which provides:
- ✅ Functions only accessible through the Static Web App (not publicly)
- ✅ Automatic `X-MS-CLIENT-PRINCIPAL` header injection with authenticated user info
- ✅ No API keys or secrets needed in frontend code
- ✅ Built-in CORS handling
- ✅ Single security boundary

## Architecture

```
User → Azure Static Web App (Authentication) → Linked Backend → Azure Functions
                    ↓
            X-MS-CLIENT-PRINCIPAL header
            (automatically injected, cannot be spoofed)
```

## Prerequisites

- Azure Static Web App deployed
- Azure Functions app deployed
- Both resources in the same Azure subscription

## Step 1: Configure Azure Static Web App Authentication

### 1.1 Create `staticwebapp.config.json`

The file is located at `/frontend/public/staticwebapp.config.json` and includes:

- **Route protection**: All routes require authentication except login
- **Azure AD integration**: Configured with your tenant
- **API routes**: Protected and require authenticated user
- **Security headers**: CSP, X-Frame-Options, etc.

### 1.2 Update Azure AD App Registration

1. Go to **Azure Portal** > **Azure Active Directory** > **App Registrations**
2. Select your app registration (Client ID from secrets)
3. Add redirect URI: `https://your-static-web-app.azurestaticapps.net/.auth/login/aad/callback`
4. Under **Authentication** > **Implicit grant and hybrid flows**, enable:
   - ID tokens
   - Access tokens

## Step 2: Link Azure Functions Backend

### 2.1 In Azure Portal

1. Navigate to your **Azure Static Web App** resource
2. In the left menu, select **APIs**
3. Under the **Production** environment, click **Link**
4. Select your **Azure Functions app** (e.g., `func-menu-app-18436`)
5. Choose **Function App** as the backend resource type
6. Click **Link**

### 2.2 Verify Linked Backend

After linking, Azure automatically:
- Adds "Azure Static Web Apps (Linked)" identity provider to your Functions app
- Configures the Functions app to only accept requests from the Static Web App
- Sets up the `/api` routing proxy

To verify:
1. Go to your **Azure Functions app** > **Authentication**
2. You should see "Azure Static Web Apps (Linked)" as an identity provider
3. Status should be "Enabled"

## Step 3: Configure Function App Settings

Ensure your Function App has these settings (in **Configuration** > **Application settings**):

```bash
# Azure AD / Power BI (keep existing values)
AZURE_CLIENT_ID=<your-service-principal-id>
AZURE_CLIENT_SECRET=<your-service-principal-secret>
AZURE_TENANT_ID=<your-tenant-id>

# Database (keep existing values)
DOTNET_CONNECTION_STRING=<your-sql-connection-string>

# OpenFGA (keep existing values)
OPENFGA_API_URL=http://localhost:8080
OPENFGA_STORE_ID=<your-store-id>
OPENFGA_DATASTORE_ENGINE=sqlserver
OPENFGA_DATASTORE_URI=<your-sql-connection-string>
```

## Step 4: Test the Configuration

### 4.1 Test Authentication Flow

1. Navigate to your Static Web App URL
2. You should be redirected to Azure AD login
3. After login, you should see the application

### 4.2 Test API Calls

Open browser DevTools and check:

1. **Network tab**: API calls should go to `/api/*` (relative paths)
2. **Request headers**: Should NOT see explicit user authentication in URL
3. **Response**: Should receive authenticated data

### 4.3 Test Security

Try accessing the Functions app directly:
```bash
curl https://func-menu-app-18436.azurewebsites.net/api/menu-structure
```

Expected: Should be blocked or require authentication (not publicly accessible)

## Local Development

For local development, the application maintains backward compatibility:

### Environment Variables

Create `/frontend/.env`:
```bash
# Azure AD
VITE_AZURE_CLIENT_ID=<your-client-id>
VITE_AZURE_TENANT_ID=<your-tenant-id>
VITE_AZURE_REDIRECT_URI=http://localhost:5173

# API - Full URL for local Functions
VITE_API_URL=http://localhost:7071/api
```

### How It Works

- **Production**: `VITE_API_URL` is undefined → defaults to `/api` (linked backend)
- **Local Dev**: `VITE_API_URL` is set → uses full URL to local Functions

The code checks `import.meta.env.DEV`:
- `true`: Passes user as query parameter (`?user=alice`)
- `false`: Uses `X-MS-CLIENT-PRINCIPAL` header automatically injected by Azure

## Security Features Implemented

### Backend Security

1. **ClaimsPrincipalParser Service** ([backend/MenuApi/Services/ClaimsPrincipalParser.cs](backend/MenuApi/Services/ClaimsPrincipalParser.cs))
   - Parses `X-MS-CLIENT-PRINCIPAL` header
   - Extracts user ID and roles
   - Cannot be spoofed (header only set by Azure infrastructure)

2. **HttpRequest Extensions** ([backend/MenuApi/Extensions/HttpRequestExtensions.cs](backend/MenuApi/Extensions/HttpRequestExtensions.cs))
   - `GetAuthenticatedUserId()`: Gets user from header (production) or query param (dev)
   - `IsAdmin()`: Checks for admin role
   - Consistent authentication across all Functions

3. **Function Updates** (All 11 Functions)
   - Extract user from `X-MS-CLIENT-PRINCIPAL` header
   - Return 401 Unauthorized if not authenticated
   - Return 403 Forbidden if user lacks required role (admin functions)
   - Fallback to query param for local development

### Frontend Security

1. **Relative API Paths** ([frontend/src/services/](frontend/src/services/))
   - Production: Uses `/api/*` (proxied through Static Web App)
   - Local Dev: Uses `http://localhost:7071/api` (direct to Functions)
   - `credentials: 'include'` for cookie-based auth

2. **Route Protection** ([frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json))
   - All routes require authentication
   - Unauthenticated users redirected to Azure AD login
   - Security headers (CSP, X-Frame-Options, etc.)

3. **No Secrets in Code**
   - No API keys in JavaScript
   - Authentication handled by Azure infrastructure
   - User identity from secure headers

## Troubleshooting

### Issue: Functions still publicly accessible

**Solution**:
1. Verify the link in Static Web App > APIs
2. Check Functions app > Authentication > "Azure Static Web Apps (Linked)" is enabled
3. Wait 5-10 minutes for configuration to propagate

### Issue: 401 Unauthorized errors in production

**Solution**:
1. Check `staticwebapp.config.json` is in `/frontend/public/`
2. Verify Azure AD redirect URI includes `.auth/login/aad/callback`
3. Clear browser cache and cookies
4. Check browser DevTools for authentication errors

### Issue: CORS errors

**Solution**:
- Linked backend handles CORS automatically
- Verify `api_location: ""` in GitHub workflow
- Ensure API calls use relative paths (`/api/*`) not full URLs

### Issue: Local development not working

**Solution**:
1. Verify `.env` has `VITE_API_URL=http://localhost:7071/api`
2. Start Functions locally: `npm run dev:native`
3. Check Functions are listening on port 7071
4. Verify fallback query param logic in Functions code

## Security Best Practices

1. **Never** commit secrets to Git
2. **Always** use environment variables for configuration
3. **Review** the `X-MS-CLIENT-PRINCIPAL` header in Functions (it's Base64-encoded JSON)
4. **Monitor** Azure Function logs for unauthorized access attempts
5. **Update** Azure AD app registration scopes as needed
6. **Rotate** service principal secrets regularly (Azure Key Vault recommended)

## Additional Resources

- [Azure Static Web Apps Authentication](https://learn.microsoft.com/en-us/azure/static-web-apps/authentication-authorization)
- [Linked Backend Documentation](https://learn.microsoft.com/en-us/azure/static-web-apps/functions-bring-your-own)
- [X-MS-CLIENT-PRINCIPAL Header](https://learn.microsoft.com/en-us/azure/static-web-apps/user-information)
- [Azure Functions Security](https://learn.microsoft.com/en-us/azure/azure-functions/security-concepts)

## Summary

This setup provides **enterprise-grade security** by:
- Eliminating public Functions endpoints
- Using Azure infrastructure for authentication (no DIY)
- Automatically injecting verified user identity
- Providing defense-in-depth (multiple security layers)
- Supporting local development without compromising production security
