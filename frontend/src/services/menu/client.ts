const API_URL = import.meta.env.VITE_API_URL;

if (!API_URL) {
  throw new Error('VITE_API_URL environment variable is required');
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
    const response = await fetch(`${this.baseUrl}/menu-items?user=${encodeURIComponent(userId)}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuItem(id: number, data: MenuItemData, userId: string) {
    const response = await fetch(`${this.baseUrl}/menu-items/${id}?user=${encodeURIComponent(userId)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to update menu item: ${response.statusText}`);
    }

    return response.json();
  },

  async deleteMenuItem(id: number, userId: string) {
    const response = await fetch(`${this.baseUrl}/menu-items/${id}?user=${encodeURIComponent(userId)}`, {
      method: 'DELETE',
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
    const response = await fetch(`${this.baseUrl}/menu-groups?user=${encodeURIComponent(userId)}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(error.error || `Failed to create menu group: ${response.statusText}`);
    }

    return response.json();
  },

  async updateMenuGroup(id: number, data: MenuGroupData, userId: string) {
    const response = await fetch(`${this.baseUrl}/menu-groups/${id}?user=${encodeURIComponent(userId)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
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
