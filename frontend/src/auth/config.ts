import { Configuration, LogLevel } from '@azure/msal-browser';

// Fail fast if required environment variables are missing
const clientId = import.meta.env.VITE_AZURE_CLIENT_ID;
const tenantId = import.meta.env.VITE_AZURE_TENANT_ID;
const redirectUri = import.meta.env.VITE_AZURE_REDIRECT_URI;

// Debug logging for environment variables
console.group('🔐 Azure AD Configuration');
console.log('Build Mode:', import.meta.env.MODE);
console.log('Is Dev:', import.meta.env.DEV);
console.log('Client ID:', clientId ? `${clientId.slice(0, 8)}...${clientId.slice(-4)}` : 'MISSING');
console.log('Tenant ID:', tenantId ? `${tenantId.slice(0, 8)}...${tenantId.slice(-4)}` : 'MISSING');
console.log('Redirect URI:', redirectUri || 'MISSING');
console.log('Authority:', tenantId ? `https://login.microsoftonline.com/${tenantId}` : 'MISSING');
console.groupEnd();

if (!clientId || !tenantId || !redirectUri) {
  console.error('❌ Missing required Azure AD environment variables!');
  console.error('Expected environment variables:');
  console.error('  - VITE_AZURE_CLIENT_ID:', clientId ? '✅ Set' : '❌ Missing');
  console.error('  - VITE_AZURE_TENANT_ID:', tenantId ? '✅ Set' : '❌ Missing');
  console.error('  - VITE_AZURE_REDIRECT_URI:', redirectUri ? '✅ Set' : '❌ Missing');
  console.error('Check:');
  console.error('  - Local dev: frontend/.env file');
  console.error('  - Production: Azure Portal → Static Web App → Environment variables');
  throw new Error('Missing required Azure AD environment variables. Check your .env file or Azure Portal configuration.');
}

export const msalConfig: Configuration = {
  auth: {
    clientId: clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri: redirectUri,
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        // Suppress harmless warning in React StrictMode (MSAL handles it internally)
        if (message.includes('already an instance')) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            break;
          case LogLevel.Warning:
            console.warn(message);
            break;
        }
      },
    },
  },
};

export const loginRequest = {
  scopes: ['User.Read'],
};

// Power BI scopes for workspace and report access
export const powerBIRequest = {
  scopes: [
    'https://analysis.windows.net/powerbi/api/Dataset.ReadWrite.All',
    'https://analysis.windows.net/powerbi/api/Report.ReadWrite.All',
    'https://analysis.windows.net/powerbi/api/Workspace.ReadWrite.All',
  ],
};
