import { useState } from 'react';

export function useAdminMode() {
  const [isAdminMode, setIsAdminMode] = useState(false);

  const toggleAdminMode = () => {
    setIsAdminMode(prev => !prev);
  };

  return {
    isAdminMode,
    toggleAdminMode,
  };
}
