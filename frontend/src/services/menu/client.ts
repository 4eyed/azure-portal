// Use relative /api path for production (linked backend) or full URL for local dev
const API_URL = import.meta.env.VITE_API_URL || '/api';

// Debug logging
console.group('📡 Menu API Client Configuration');
console.log('API URL:', API_URL);
console.log('Mode:', import.meta.env.MODE);
console.log('Is Dev:', import.meta.env.DEV);
console.log('Will use query params:', import.meta.env.DEV ? 'Yes (local dev)' : 'No (production, uses X-MS-CLIENT-PRINCIPAL header)');
console.groupEnd();

// In production with linked backend, VITE_API_URL should be undefined or '/api'
// In local dev, VITE_API_URL should be 'http://localhost:7071/api'

// Helper to add query params only in dev mode
function buildUrl(path: string, userId?: string): string {
  const base = `${API_URL}${path}`;
  // In local dev, pass userId as query param. In production, Azure Static Web Apps handles auth.
  if (import.meta.env.DEV && userId) {
    return `${base}?user=${encodeURIComponent(userId)}`;
  }
  return base;
}

export interface MenuItemData {
  id?: number;
  name: string;
  icon?: string;
  url: string;
  description?: string;
  type: string;
  menuGroupId?: number;
  displayOrder: number;
  isVisible: boolean;
  powerBIConfig?: PowerBIConfigData;
}

export interface PowerBIConfigData {
  workspaceId: string;
  reportId: string;
  embedUrl: string;
  autoRefreshInterval?: number;
  defaultZoom?: string;
  showFilterPanel: boolean;
  showFilterPanelExpanded: boolean;
}

export interface MenuGroupData {
  id?: number;
  name: string;
  icon?: string;
  parentId?: number;
  displayOrder: number;
  isVisible: boolean;
}

export const menuClient = {
  baseUrl: API_URL,

  async createMenuItem(data: MenuItemData, userId: string) {
    const response = await fetch(buildUrl('/menu-items', userId), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuItem(id: number, data: MenuItemData, userId: string) {
    const response = await fetch(buildUrl(`/menu-items/${id}`, userId), {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async deleteMenuItem(id: number, userId: string) {
    const response = await fetch(buildUrl(`/menu-items/${id}`, userId), {
      method: 'DELETE',
      credentials: 'include',
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to delete menu item: ${response.statusText}`);
    }

    return response.ok;
  },

  async toggleMenuItemVisibility(id: number, item: MenuItemData, userId: string) {
    const updated = { ...item, isVisible: !item.isVisible };
    return this.updateMenuItem(id, updated, userId);
  },

  async createMenuGroup(data: MenuGroupData, userId: string) {
    const response = await fetch(buildUrl('/menu-groups', userId), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuGroup(id: number, data: MenuGroupData, userId: string) {
    const response = await fetch(buildUrl(`/menu-groups/${id}`, userId), {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async toggleMenuGroupVisibility(id: number, group: MenuGroupData, userId: string) {
    const updated = { ...group, isVisible: !group.isVisible };
    return this.updateMenuGroup(id, updated, userId);
  },
};
