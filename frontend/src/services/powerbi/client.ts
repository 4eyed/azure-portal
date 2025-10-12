// Use relative /api path for production (linked backend) or full URL for local dev
const API_URL = import.meta.env.VITE_API_URL || '/api';

// Debug logging
console.group('⚡ Power BI API Client Configuration');
console.log('API URL:', API_URL);
console.log('Mode:', import.meta.env.MODE);
console.groupEnd();

export const powerBIClient = {
  baseUrl: API_URL,

  async getWorkspaces(accessToken: string) {
    const response = await fetch(`${this.baseUrl}/powerbi/workspaces`, {
      headers: {
        'Authorization': `Bearer ${accessToken}`,
      },
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch workspaces: ${response.statusText}`);
    }

    return response.json();
  },

  async getReports(workspaceId: string, accessToken: string) {
    const response = await fetch(
      `${this.baseUrl}/powerbi/reports?workspaceId=${workspaceId}`,
      {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to fetch reports: ${response.statusText}`);
    }

    return response.json();
  },

  async generateEmbedToken(workspaceId: string, reportId: string, accessToken: string) {
    const response = await fetch(`${this.baseUrl}/powerbi/embed-token`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${accessToken}`,
      },
      credentials: 'include',
      body: JSON.stringify({ workspaceId, reportId }),
    });

    if (!response.ok) {
      throw new Error(`Failed to generate embed token: ${response.statusText}`);
    }

    return response.json();
  },
};
