import { apiGet, apiPost, apiPut, apiDelete } from '../apiClient';

// Debug logging
console.group('📡 Menu API Client Configuration');
console.log('Mode:', import.meta.env.MODE);
console.log('Is Dev:', import.meta.env.DEV);
console.log('Will use query params:', import.meta.env.DEV ? 'Yes (local dev)' : 'No (production, uses X-MS-CLIENT-PRINCIPAL header)');
console.groupEnd();

// Helper to add query params only in dev mode
function buildPath(path: string, userId?: string): string {
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
  async fetchMenuStructure(userId: string) {
    const response = await apiGet(buildPath('/menu-structure', userId));
    if (!response.ok) {
      throw new Error(`Failed to fetch menu: ${response.statusText}`);
    }
    return response.json();
  },

  async createMenuItem(data: MenuItemData, userId: string) {
    const response = await apiPost(buildPath('/menu-items', userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuItem(id: number, data: MenuItemData, userId: string) {
    const response = await apiPut(buildPath(`/menu-items/${id}`, userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async deleteMenuItem(id: number, userId: string) {
    const response = await apiDelete(buildPath(`/menu-items/${id}`, userId));

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
    const response = await apiPost(buildPath('/menu-groups', userId), data);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuGroup(id: number, data: MenuGroupData, userId: string) {
    const response = await apiPut(buildPath(`/menu-groups/${id}`, userId), data);

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
