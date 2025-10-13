import { useState, useEffect } from 'react';
import { getDevAuthState, devAuthIsEnabled, subscribeDevAuth } from './devAuthStore';

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
    // Local development: Subscribe to devAuthStore changes
    if (import.meta.env.DEV && devAuthIsEnabled()) {
      console.log('🔐 useSwaAuth: Using devAuthStore for local development');

      // Initial check
      const devState = getDevAuthState();
      if (devState.userId) {
        setIsAuthenticated(true);
        setUserInfo({
          userId: devState.userId,
          userDetails: devState.displayName || devState.userId,
          userRoles: devState.roles
        });
        console.log('✅ Local dev authenticated:', devState.userId);
        setLoading(false);
      } else {
        console.log('⏳ Local dev: Waiting for MSAL authentication...');
        setIsAuthenticated(false);
        setUserInfo(null);
        // Keep loading true until MSAL authenticates
      }

      // Subscribe to devAuthStore changes
      const unsubscribe = subscribeDevAuth(() => {
        const updatedState = getDevAuthState();
        console.log('🔄 devAuthStore updated:', updatedState.userId ? '✅ Authenticated' : '❌ Not authenticated');

        if (updatedState.userId) {
          setIsAuthenticated(true);
          setUserInfo({
            userId: updatedState.userId,
            userDetails: updatedState.displayName || updatedState.userId,
            userRoles: updatedState.roles
          });
          console.log('✅ Local dev authenticated via subscription:', updatedState.userId);
          setLoading(false);
        } else {
          setIsAuthenticated(false);
          setUserInfo(null);
          setLoading(false);
        }
      });

      return unsubscribe;
    }

    // Production: Check SWA authentication via /.auth/me
    const checkAuth = async () => {
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
