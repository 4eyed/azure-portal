import { PublicClientApplication } from '@azure/msal-browser';
import { apiGet, apiPost, apiPut, apiDelete } from '../apiClient';

// Debug logging
console.group('📡 Menu API Client Configuration');
console.log('Mode:', import.meta.env.MODE);
console.log('Is Dev:', import.meta.env.DEV);
console.log('Will use query params:', import.meta.env.DEV ? 'Yes (local dev)' : 'No (production, uses X-MS-CLIENT-PRINCIPAL header)');
console.groupEnd();

// Helper to add query params only in dev mode
function buildPath(path: string, userId?: string): string {
  // In local dev, pass userId as query param. In production, Azure Static Web Apps handles auth.
  if (import.meta.env.DEV && userId) {
    return `${path}?user=${encodeURIComponent(userId)}`;
  }
  return path;
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
  async createMenuItem(msalInstance: PublicClientApplication, data: MenuItemData, userId: string) {
    const response = await apiPost(msalInstance, buildPath('/menu-items', userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuItem(msalInstance: PublicClientApplication, id: number, data: MenuItemData, userId: string) {
    const response = await apiPut(msalInstance, buildPath(`/menu-items/${id}`, userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async deleteMenuItem(msalInstance: PublicClientApplication, id: number, userId: string) {
    const response = await apiDelete(msalInstance, buildPath(`/menu-items/${id}`, userId));

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to delete menu item: ${response.statusText}`);
    }

    return response.ok;
  },

  async toggleMenuItemVisibility(msalInstance: PublicClientApplication, id: number, item: MenuItemData, userId: string) {
    const updated = { ...item, isVisible: !item.isVisible };
    return this.updateMenuItem(msalInstance, id, updated, userId);
  },

  async createMenuGroup(msalInstance: PublicClientApplication, data: MenuGroupData, userId: string) {
    const response = await apiPost(msalInstance, buildPath('/menu-groups', userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuGroup(msalInstance: PublicClientApplication, id: number, data: MenuGroupData, userId: string) {
    const response = await apiPut(msalInstance, buildPath(`/menu-groups/${id}`, userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async toggleMenuGroupVisibility(msalInstance: PublicClientApplication, id: number, group: MenuGroupData, userId: string) {
    const updated = { ...group, isVisible: !group.isVisible };
    return this.updateMenuGroup(msalInstance, id, updated, userId);
  },
};
