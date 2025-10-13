import { useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import {
  clearDevAuthOverride,
  devAuthIsEnabled,
  initializeDevAuthFromStorage,
  setDevAuthRolesOverride,
  updateDevAuthAccount,
} from './devAuthStore';

const DEV_STORAGE_KEY = 'dev-auth-roles';

function readInitialRoles(): string[] | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    const stored = window.localStorage.getItem(DEV_STORAGE_KEY);
    if (!stored) {
      return null;
    }

    const parsed = JSON.parse(stored);
    if (Array.isArray(parsed) && parsed.every((value) => typeof value === 'string')) {
      return parsed as string[];
    }
  } catch {
    return null;
  }

  return null;
}

export function useDevAuthUpdater(): void {
  const { accounts } = useMsal();

  useEffect(() => {
    if (!devAuthIsEnabled()) {
      return;
    }

    initializeDevAuthFromStorage();

    // Hydrate overrides from storage on initial load
    const initialRoles = readInitialRoles();
    if (initialRoles) {
      setDevAuthRolesOverride(initialRoles);
    } else {
      clearDevAuthOverride();
    }
  }, []);

  useEffect(() => {
    if (!devAuthIsEnabled()) {
      return;
    }

    updateDevAuthAccount(accounts[0] ?? null);
  }, [accounts]);
}
