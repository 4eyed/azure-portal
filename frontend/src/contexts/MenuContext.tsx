import { createContext, useContext, useState, useCallback, useEffect, ReactNode } from 'react';
import { useAuth } from '../auth/useAuth';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

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

  const reloadMenu = useCallback(async () => {
    if (!user) return;

    setLoading(true);
    setError(null);

    try {
      const username = user.username || 'alice';
      const response = await fetch(`${API_URL}/menu-structure?user=${username}`);

      if (!response.ok) {
        throw new Error(`Failed to load menu: ${response.statusText}`);
      }

      const data = await response.json();
      setMenuGroups(data.menuGroups || []);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load menu';
      setError(message);
      console.error('Error loading menu structure:', err);
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Load menu on initial mount when user is available
  useEffect(() => {
    if (user) {
      reloadMenu();
    }
  }, [user, reloadMenu]);

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
