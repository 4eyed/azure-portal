import { useState, useEffect } from 'react';
import { getDevAuthState, devAuthIsEnabled } from './devAuthStore';

interface SwaUser {
  userId: string;
  userDetails: string;
  userRoles: string[];
}

interface UseSwaAuthReturn {
  isAuthenticated: boolean;
  userInfo: SwaUser | null;
  loading: boolean;
  logout: () => void;
}

/**
 * Hook to check Azure Static Web Apps authentication status
 *
 * In production: Checks /.auth/me endpoint for SWA authentication
 * In local dev: Uses devAuthStore to simulate SWA authentication
 */
export function useSwaAuth(): UseSwaAuthReturn {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [userInfo, setUserInfo] = useState<SwaUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkAuth = async () => {
      // Local development: Use devAuthStore to simulate SWA auth
      if (import.meta.env.DEV && devAuthIsEnabled()) {
        console.log('🔐 useSwaAuth: Using devAuthStore for local development');
        const devState = getDevAuthState();

        if (devState.userId) {
          setIsAuthenticated(true);
          setUserInfo({
            userId: devState.userId,
            userDetails: devState.displayName || devState.userId,
            userRoles: devState.roles
          });
          console.log('✅ Local dev authenticated:', devState.userId);
        } else {
          setIsAuthenticated(false);
          setUserInfo(null);
          console.warn('⚠️ Local dev: No user in devAuthStore');
        }

        setLoading(false);
        return;
      }

      // Production: Check SWA authentication via /.auth/me
      try {
        console.log('🔐 useSwaAuth: Checking SWA authentication via /.auth/me');
        const response = await fetch('/.auth/me', {
          credentials: 'include' // Include authentication cookies
        });

        if (!response.ok) {
          console.warn(`⚠️ /.auth/me returned ${response.status}`);
          setIsAuthenticated(false);
          setUserInfo(null);
          setLoading(false);
          return;
        }

        const data = await response.json();
        const principal = data.clientPrincipal;

        if (principal) {
          console.log('✅ SWA authenticated user:', principal.userId);
          setIsAuthenticated(true);
          setUserInfo({
            userId: principal.userId,
            userDetails: principal.userDetails,
            userRoles: principal.userRoles || []
          });
        } else {
          console.warn('⚠️ No clientPrincipal in /.auth/me response');
          setIsAuthenticated(false);
          setUserInfo(null);
        }
      } catch (error) {
        console.error('❌ Failed to check SWA authentication:', error);
        setIsAuthenticated(false);
        setUserInfo(null);
      } finally {
        setLoading(false);
      }
    };

    checkAuth();
  }, []);

  const logout = () => {
    if (import.meta.env.DEV) {
      // Local dev: Can't really logout from SWA
      console.log('Logout not implemented for local dev');
      window.location.reload();
    } else {
      // Production: Use SWA logout
      window.location.href = '/.auth/logout';
    }
  };

  return { isAuthenticated, userInfo, loading, logout };
}
