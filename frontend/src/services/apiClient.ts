import { PublicClientApplication } from '@azure/msal-browser';

const API_URL = import.meta.env.VITE_API_URL || '/api';

/**
 * Get SQL Database access token for the authenticated user
 */
async function getSqlToken(msalInstance: PublicClientApplication): Promise<string | null> {
  const accounts = msalInstance.getAllAccounts();

  if (accounts.length === 0) {
    return null;
  }

  try {
    // Request token for SQL Database scope
    const response = await msalInstance.acquireTokenSilent({
      scopes: ['https://database.windows.net//.default'],
      account: accounts[0],
    });

    console.log('✅ SQL Database token acquired');
    return response.accessToken;
  } catch (error) {
    console.warn('⚠️ Failed to get SQL token silently, trying popup:', error);

    // Try interactive popup as fallback
    try {
      const response = await msalInstance.acquireTokenPopup({
        scopes: ['https://database.windows.net//.default'],
        account: accounts[0],
      });

      console.log('✅ SQL Database token acquired via popup');
      return response.accessToken;
    } catch (popupError) {
      console.error('❌ Failed to get SQL token:', popupError);
      return null;
    }
  }
}

/**
 * Get authentication headers with Bearer token for API calls
 */
export async function getAuthHeaders(msalInstance: PublicClientApplication): Promise<HeadersInit> {
  const accounts = msalInstance.getAllAccounts();

  if (accounts.length === 0) {
    console.warn('No authenticated accounts found');
    return {
      'Content-Type': 'application/json',
    };
  }

  try {
    // Get idToken for backend authentication
    const authResponse = await msalInstance.acquireTokenSilent({
      scopes: ['openid', 'profile', 'email'],
      account: accounts[0],
    });

    // Get SQL-scoped token for database access
    const sqlToken = await getSqlToken(msalInstance);

    console.group('✅ Tokens acquired for API calls');
    console.log('Account:', authResponse.account?.name);
    console.log('OID:', authResponse.account?.localAccountId);
    console.log('idToken: Using for backend auth (contains roles claim)');
    console.log('SQL Token:', sqlToken ? '✅ Acquired' : '❌ Not available');
    console.groupEnd();

    const headers: HeadersInit = {
      'Authorization': `Bearer ${authResponse.idToken}`,
      'Content-Type': 'application/json',
    };

    // Add SQL token if available
    if (sqlToken) {
      headers['X-SQL-Token'] = sqlToken;
    }

    return headers;
  } catch (error) {
    console.error('❌ Failed to get token:', error);

    // Try interactive popup as fallback
    try {
      const authResponse = await msalInstance.acquireTokenPopup({
        scopes: ['openid', 'profile', 'email'],
        account: accounts[0],
      });

      const sqlToken = await getSqlToken(msalInstance);

      console.log('✅ Tokens acquired via popup');

      const headers: HeadersInit = {
        'Authorization': `Bearer ${authResponse.idToken}`,
        'Content-Type': 'application/json',
      };

      if (sqlToken) {
        headers['X-SQL-Token'] = sqlToken;
      }

      return headers;
    } catch (popupError) {
      console.error('❌ Failed to get token via popup:', popupError);
      return {
        'Content-Type': 'application/json',
      };
    }
  }
}

/**
 * Authenticated GET request
 */
export async function apiGet(msalInstance: PublicClientApplication, path: string): Promise<Response> {
  const headers = await getAuthHeaders(msalInstance);
  return fetch(`${API_URL}${path}`, {
    method: 'GET',
    headers,
    credentials: 'include',
  });
}

/**
 * Authenticated POST request
 */
export async function apiPost(
  msalInstance: PublicClientApplication,
  path: string,
  body: any
): Promise<Response> {
  const headers = await getAuthHeaders(msalInstance);
  return fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: JSON.stringify(body),
  });
}

/**
 * Authenticated PUT request
 */
export async function apiPut(
  msalInstance: PublicClientApplication,
  path: string,
  body: any
): Promise<Response> {
  const headers = await getAuthHeaders(msalInstance);
  return fetch(`${API_URL}${path}`, {
    method: 'PUT',
    headers,
    credentials: 'include',
    body: JSON.stringify(body),
  });
}

/**
 * Authenticated DELETE request
 */
export async function apiDelete(
  msalInstance: PublicClientApplication,
  path: string
): Promise<Response> {
  const headers = await getAuthHeaders(msalInstance);
  return fetch(`${API_URL}${path}`, {
    method: 'DELETE',
    headers,
    credentials: 'include',
  });
}
