import { PublicClientApplication } from '@azure/msal-browser';

const API_URL = import.meta.env.VITE_API_URL || '/api';

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
    // For single-page apps calling their own backend, we use the idToken
    // The idToken contains user info and roles claims from Azure AD
    const response = await msalInstance.acquireTokenSilent({
      scopes: ['openid', 'profile', 'email'],
      account: accounts[0],
    });

    console.group('✅ Token acquired for API calls');
    console.log('Account:', response.account?.name);
    console.log('OID:', response.account?.localAccountId);
    console.log('Using idToken (contains roles claim)');
    console.groupEnd();

    // Use idToken which contains the roles claim from App Registration
    const token = response.idToken;

    return {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    };
  } catch (error) {
    console.error('❌ Failed to get token:', error);

    // Try interactive popup as fallback
    try {
      const response = await msalInstance.acquireTokenPopup({
        scopes: ['openid', 'profile', 'email'],
        account: accounts[0],
      });

      console.log('✅ Token acquired via popup');
      const token = response.idToken;

      return {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      };
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
