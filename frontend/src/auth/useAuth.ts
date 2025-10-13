import { useMsal } from '@azure/msal-react';
import { useSwaAuth } from './useSwaAuth';
import { loginRequest, powerBIRequest } from './config';

/**
 * Authentication hook that combines SWA auth (for authentication)
 * with MSAL (for token acquisition)
 *
 * - Authentication: Handled by Azure Static Web Apps (useSwaAuth)
 * - Token acquisition: Still uses MSAL for delegated permissions (SQL, Power BI)
 */
export function useAuth() {
  const { instance, accounts } = useMsal();
  const { isAuthenticated, userInfo, loading, logout: swaLogout } = useSwaAuth();

  const login = () => {
    // In production, SWA handles login automatically
    // In local dev, MSAL can be used
    if (import.meta.env.DEV) {
      instance.loginRedirect(loginRequest);
    } else {
      // Redirect to SWA login
      window.location.href = '/.auth/login/aad';
    }
  };

  const logout = () => {
    // Use SWA logout
    swaLogout();
  };

  const getAccessToken = async (scopes?: string[]) => {
    if (accounts.length === 0) {
      console.warn('No MSAL account available for token acquisition');
      throw new Error('No authenticated MSAL account');
    }

    const request = scopes
      ? { scopes, account: accounts[0] }
      : { ...loginRequest, account: accounts[0] };

    try {
      const response = await instance.acquireTokenSilent(request);
      console.log('Token acquired silently for scopes:', scopes);
      return response.accessToken;
    } catch (error) {
      console.warn('Silent token acquisition failed, requesting interactive consent:', error);
      // If silent token acquisition fails, try interactive login
      try {
        const response = await instance.acquireTokenPopup(request);
        console.log('Token acquired via popup for scopes:', scopes);
        return response.accessToken;
      } catch (popupError) {
        console.error('Failed to acquire token:', popupError);
        throw popupError;
      }
    }
  };

  const getPowerBIToken = async () => {
    console.log('Requesting Power BI token with scopes:', powerBIRequest.scopes);
    return getAccessToken(powerBIRequest.scopes);
  };

  // Create a user object compatible with existing code
  const user = userInfo ? {
    name: userInfo.userDetails,
    username: userInfo.userId,
    ...userInfo
  } : null;

  return {
    isAuthenticated,
    user,
    loading,
    login,
    logout,
    getAccessToken,
    getPowerBIToken,
  };
}
