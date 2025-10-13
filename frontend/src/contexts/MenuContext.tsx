import { createContext, useContext, useState, useCallback, useEffect, useRef, ReactNode } from 'react';
import { useAuth } from '../auth/useAuth';
import { apiGet } from '../services/apiClient';

// Use relative /api path for production (linked backend) or full URL for local dev
const API_URL = import.meta.env.VITE_API_URL || '/api';

// Debug logging
console.group('🍔 Menu Context Configuration');
console.log('API URL:', API_URL);
console.log('Mode:', import.meta.env.MODE);
console.groupEnd();

interface MenuItem {
  id: number;
  name: string;
  icon: string;
  url: string;
  description: string;
  type: string;
}

interface MenuGroupData {
  id: number;
  name: string;
  icon: string;
  items: MenuItem[];
  displayOrder?: number;
  isVisible?: boolean;
}

interface MenuContextValue {
  menuGroups: MenuGroupData[];
  loading: boolean;
  error: string | null;
  reloadMenu: () => Promise<void>;
}

const MenuContext = createContext<MenuContextValue | undefined>(undefined);

export function MenuProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [menuGroups, setMenuGroups] = useState<MenuGroupData[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fetchInProgressRef = useRef(false);
  const hasLoadedRef = useRef(false); // Track if we've already loaded once

  const reloadMenu = useCallback(async () => {
    if (!user) return;

    // Prevent duplicate calls if already loading (React StrictMode protection)
    if (fetchInProgressRef.current) {
      console.log('Menu fetch already in progress, skipping duplicate call');
      return;
    }

    fetchInProgressRef.current = true;
    setLoading(true);
    setError(null);

    try {
      const username = user.username || 'alice';
      // In production, X-MS-CLIENT-PRINCIPAL header is automatically injected
      // In local dev, pass user as query param
      const endpoint = import.meta.env.DEV
        ? `/menu-structure?user=${username}`
        : `/menu-structure`;

      console.log('Fetching menu structure from:', endpoint);

      // Static Web Apps automatically forwards identity headers; apiGet preserves SWA cookies
      const response = await apiGet(endpoint);

      // Don't retry on authentication errors - they won't resolve automatically
      if (response.status === 401) {
        console.error('❌ Authentication failed (401). Backend is not receiving user authentication.');
        throw new Error('Authentication failed - please check backend configuration');
      }

      if (!response.ok) {
        throw new Error(`Failed to load menu: ${response.statusText}`);
      }

      const data = await response.json();
      console.log('Menu structure loaded:', data.menuGroups?.length, 'groups');
      setMenuGroups(data.menuGroups || []);
      hasLoadedRef.current = true; // Mark as successfully loaded
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load menu';
      setError(message);
      console.error('Error loading menu structure:', err);
      hasLoadedRef.current = true; // Mark as attempted (don't retry errors)
    } finally {
      setLoading(false);
      fetchInProgressRef.current = false;
    }
  }, [user]);

  // Load menu on initial mount when user is available - ONLY ONCE
  useEffect(() => {
    if (user && !hasLoadedRef.current && !error) {
      reloadMenu();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user]); // Only depend on user, not reloadMenu (which is stable via useCallback)

  return (
    <MenuContext.Provider value={{ menuGroups, loading, error, reloadMenu }}>
      {children}
    </MenuContext.Provider>
  );
}

export function useMenu() {
  const context = useContext(MenuContext);
  if (context === undefined) {
    throw new Error('useMenu must be used within a MenuProvider');
  }
  return context;
}
