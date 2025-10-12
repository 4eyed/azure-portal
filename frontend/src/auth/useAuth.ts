import { useMsal } from '@azure/msal-react';
import { loginRequest, powerBIRequest } from './config';

export function useAuth() {
  const { instance, accounts } = useMsal();

  const login = () => {
    instance.loginRedirect(loginRequest);
  };

  const logout = () => {
    instance.logoutRedirect();
  };

  const getAccessToken = async (scopes?: string[]) => {
    if (accounts.length === 0) {
      throw new Error('No authenticated user');
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

  return {
    isAuthenticated: accounts.length > 0,
    user: accounts[0] || null,
    login,
    logout,
    getAccessToken,
    getPowerBIToken,
  };
}
