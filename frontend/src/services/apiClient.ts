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

  const requestInit: RequestInit = {
    ...init,
    headers,
    credentials: init?.credentials ?? 'include',
  };

  return fetch(url, requestInit);
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
