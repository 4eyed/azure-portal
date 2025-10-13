import { useState, useEffect } from 'react';
import { apiGet } from '../services/apiClient';

export function useAdminMode() {
  const [isAdminMode, setIsAdminMode] = useState(false);
  const [canBeAdmin, setCanBeAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [userId, setUserId] = useState<string | null>(null);

  useEffect(() => {
    // Check if user has admin permissions on mount
    const checkAdminStatus = async () => {
      try {
        const response = await apiGet('/auth/check-admin');

        if (response.ok) {
          const data = await response.json();
          setCanBeAdmin(data.isAdmin === true);
          setUserId(data.userId || null);

          console.group('🔐 Admin Status Check');
          console.log('Is Admin:', data.isAdmin);
          console.log('User ID (OID):', data.userId);

          if (data.isAdmin) {
            console.log('✅ You have admin access from Entra App Role!');
            console.log('Source: Azure AD App Registration → App Roles → "Admin" role');
          } else {
            console.warn('⚠️ You are NOT an admin.');
            console.log('To become admin, you need to be assigned the "Admin" app role:');
            console.log('1. Go to Azure Portal → App Registrations → Your App → App Roles');
            console.log('2. Go to Enterprise Applications → Your App → Users and groups');
            console.log('3. Add assignment → Select your user → Select "Admin" role');
            console.log('4. Wait a few minutes for changes to propagate');
            console.log('5. Logout and login again to get new token with roles');
          }
          console.groupEnd();
        } else {
          setCanBeAdmin(false);
          console.warn('Admin check failed:', response.statusText);
        }
      } catch (error) {
        console.error('Error checking admin status:', error);
        setCanBeAdmin(false);
      } finally {
        setLoading(false);
      }
    };

    checkAdminStatus();
  }, []);

  const toggleAdminMode = () => {
    if (canBeAdmin) {
      setIsAdminMode(prev => !prev);
    }
  };

  return {
    isAdminMode,
    canBeAdmin,
    loading,
    userId,
    toggleAdminMode,
  };
}
