import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { msalInstance } from '../auth/msalInstance';
import { sqlRequest } from '../auth/config';
import { devAuthIsEnabled, getDevAuthHeader } from '../auth/devAuthStore';

const API_URL = import.meta.env.VITE_API_URL || '/api';

function buildUrl(path: string): string {
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path;
  }
  return `${API_URL}${path}`;
}

function mergeHeaders(existing?: HeadersInit, defaults?: Record<string, string>): HeadersInit {
  const headers = new Headers(existing || {});
  if (defaults) {
    Object.entries(defaults).forEach(([key, value]) => {
      if (!headers.has(key)) {
        headers.set(key, value);
      }
    });
  }
  return headers;
}

async function request(path: string, init?: RequestInit): Promise<Response> {
  const url = buildUrl(path);
  const headers = mergeHeaders(init?.headers, {});

  if (devAuthIsEnabled()) {
    const devPrincipal = getDevAuthHeader();
    if (devPrincipal) {
      headers.set('X-MS-CLIENT-PRINCIPAL', devPrincipal);
    }
  }

  await attachSqlAccessToken(headers);

  const requestInit: RequestInit = {
    ...init,
    headers,
    credentials: init?.credentials ?? 'include',
  };

  return fetch(url, requestInit);
}

async function attachSqlAccessToken(headers: Headers): Promise<void> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) {
    console.warn('No authenticated MSAL account available; skipping SQL token header.');
    return;
  }

  const request = { ...sqlRequest, account: accounts[0] };

  try {
    const response = await msalInstance.acquireTokenSilent(request);
    headers.set('X-SQL-Token', response.accessToken);
    console.debug('Attached delegated SQL token to request headers', {
      length: response.accessToken.length,
      preview: createPreview(response.accessToken),
    });
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      console.warn('SQL token requires interactive consent; prompting user via popup.');
      try {
        const response = await msalInstance.acquireTokenPopup(request);
        headers.set('X-SQL-Token', response.accessToken);
        console.debug('Attached delegated SQL token after popup consent', {
          length: response.accessToken.length,
          preview: createPreview(response.accessToken),
        });
      } catch (popupError) {
        console.error('User cancelled or popup acquisition failed; proceeding without SQL token.', popupError);
      }
    } else {
      console.error('Failed to acquire SQL delegated token silently; falling back to backend identity.', error);
    }
  }
}

function createPreview(token: string): string {
  const previewLength = 12;
  return token.length <= previewLength ? token : `${token.slice(0, previewLength)}…`;
}

export function apiGet(path: string, init?: RequestInit): Promise<Response> {
  return request(path, { ...init, method: 'GET' });
}

export function apiPost(path: string, body: unknown, init?: RequestInit): Promise<Response> {
  const headers = mergeHeaders(init?.headers, { 'Content-Type': 'application/json' });
  return request(path, {
    ...init,
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });
}

export function apiPut(path: string, body: unknown, init?: RequestInit): Promise<Response> {
  const headers = mergeHeaders(init?.headers, { 'Content-Type': 'application/json' });
  return request(path, {
    ...init,
    method: 'PUT',
    headers,
    body: JSON.stringify(body),
  });
}

export function apiDelete(path: string, init?: RequestInit): Promise<Response> {
  return request(path, { ...init, method: 'DELETE' });
}
