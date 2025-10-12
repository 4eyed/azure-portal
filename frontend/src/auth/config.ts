import { Configuration, LogLevel } from '@azure/msal-browser';

// Fail fast if required environment variables are missing
const clientId = import.meta.env.VITE_AZURE_CLIENT_ID;
const tenantId = import.meta.env.VITE_AZURE_TENANT_ID;
const redirectUri = import.meta.env.VITE_AZURE_REDIRECT_URI;

if (!clientId || !tenantId || !redirectUri) {
  throw new Error('Missing required Azure AD environment variables. Check your .env file.');
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
