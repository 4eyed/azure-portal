import type { AccountInfo } from '@azure/msal-browser';
import { useSyncExternalStore } from 'react';

interface DevAuthState {
  readonly header: string | null;
  readonly userId: string | null;
  readonly displayName: string | null;
  readonly roles: string[];
  readonly baseRoles: string[];
  readonly isOverrideActive: boolean;
}

type Listener = () => void;

const DEV_STORAGE_KEY = 'dev-auth-roles';
const devAuthEnabled = import.meta.env.DEV && import.meta.env.VITE_ENABLE_DEV_PRINCIPAL !== 'false';

let currentAccount: AccountInfo | null = null;
let rolesOverride: string[] | null = null;
let state: DevAuthState = {
  header: null,
  userId: null,
  displayName: null,
  roles: [],
  baseRoles: [],
  isOverrideActive: false,
};

const listeners = new Set<Listener>();

function toBase64(payload: string): string {
  if (typeof globalThis.btoa === 'function') {
    return globalThis.btoa(payload);
  }

  if (typeof globalThis.Buffer !== 'undefined') {
    return globalThis.Buffer.from(payload, 'utf-8').toString('base64');
  }

  throw new Error('No base64 encoder available in this environment.');
}

interface ComputedPrincipal {
  header: string;
  userId: string | null;
  displayName: string | null;
  roles: string[];
  baseRoles: string[];
}

function computePrincipal(account: AccountInfo | null, overrideRoles?: string[] | null): ComputedPrincipal | null {
  if (!account) {
    return null;
  }

  const claims = (account.idTokenClaims ?? {}) as Record<string, unknown>;
  const oid = (claims['oid'] as string)
    ?? (claims['http://schemas.microsoft.com/identity/claims/objectidentifier'] as string)
    ?? account.homeAccountId
    ?? account.localAccountId
    ?? null;

  const displayName = (claims['name'] as string)
    ?? (claims['preferred_username'] as string)
    ?? account.username
    ?? null;

  const baseRoles = Array.isArray(claims['roles'])
    ? (claims['roles'] as string[]).filter((role) => typeof role === 'string')
    : [];

  const roles = overrideRoles ?? baseRoles;

  const claimsList = Object.entries(claims)
    .filter(([, value]) => value !== undefined && value !== null)
    .map(([key, value]) => ({
      typ: key,
      val: Array.isArray(value) ? value.join(',') : String(value),
    }));

  // Ensure role claims exist for each role
  const roleClaims = roles
    .filter((role) => role && role.length > 0)
    .flatMap((role) => ([
      { typ: 'roles', val: role },
      { typ: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role', val: role },
    ]));

  const principal = {
    identityProvider: 'aad',
    userId: oid,
    userDetails: displayName ?? oid ?? account.username,
    userRoles: roles,
    claims: mergeClaims(claimsList, roleClaims),
  };

  return {
    header: toBase64(JSON.stringify(principal)),
    userId: principal.userId ?? null,
    displayName: principal.userDetails ?? null,
    roles: roles,
    baseRoles,
  };
}

function mergeClaims(
  existing: Array<{ typ: string; val: string }>,
  additions: Array<{ typ: string; val: string }>,
): Array<{ typ: string; val: string }> {
  const merged = [...existing];

  additions.forEach((addition) => {
    const alreadyPresent = merged.some((claim) => claim.typ === addition.typ && claim.val === addition.val);
    if (!alreadyPresent) {
      merged.push(addition);
    }
  });

  return merged;
}

function notify(): void {
  listeners.forEach((listener) => listener());
}

function recomputeState(): void {
  if (!devAuthEnabled) {
    state = {
      header: null,
      userId: null,
      displayName: null,
      roles: [],
      baseRoles: [],
      isOverrideActive: false,
    };
    notify();
    return;
  }

  const principal = computePrincipal(currentAccount, rolesOverride);

  state = {
    header: principal?.header ?? null,
    userId: principal?.userId ?? null,
    displayName: principal?.displayName ?? null,
    roles: principal?.roles ?? [],
    baseRoles: principal?.baseRoles ?? [],
    isOverrideActive: Array.isArray(rolesOverride),
  };

  notify();
}

export function initializeDevAuthFromStorage(): void {
  if (!devAuthEnabled || typeof window === 'undefined') {
    return;
  }

  const stored = window.localStorage.getItem(DEV_STORAGE_KEY);
  if (stored) {
    try {
      const parsed = JSON.parse(stored);
      if (Array.isArray(parsed) && parsed.every((item) => typeof item === 'string')) {
        rolesOverride = parsed as string[];
      }
    } catch {
      rolesOverride = null;
    }
  }

  recomputeState();
}

export function updateDevAuthAccount(account: AccountInfo | null): void {
  if (!devAuthEnabled) {
    return;
  }

  currentAccount = account;
  recomputeState();
}

export function setDevAuthRolesOverride(roles: string[]): void {
  if (!devAuthEnabled) {
    return;
  }

  rolesOverride = roles;

  if (typeof window !== 'undefined') {
    window.localStorage.setItem(DEV_STORAGE_KEY, JSON.stringify(roles));
  }

  recomputeState();
}

export function clearDevAuthOverride(): void {
  if (!devAuthEnabled) {
    return;
  }

  rolesOverride = null;

  if (typeof window !== 'undefined') {
    window.localStorage.removeItem(DEV_STORAGE_KEY);
  }

  recomputeState();
}

export function getDevAuthHeader(): string | null {
  return state.header;
}

export function devAuthIsEnabled(): boolean {
  return devAuthEnabled;
}

export function subscribeDevAuth(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getDevAuthState(): DevAuthState {
  return state;
}

export function useDevAuthState(): DevAuthState {
  return useSyncExternalStore(subscribeDevAuth, getDevAuthState, getDevAuthState);
}
