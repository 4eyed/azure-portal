const API_URL = import.meta.env.VITE_API_URL || '/api';

console.group('⚡ Power BI API Client Configuration');
console.log('API URL:', API_URL);
console.log('Mode:', import.meta.env.MODE);
console.groupEnd();

export const powerBIClient = {
  baseUrl: API_URL,

  async getWorkspaces() {
    const response = await fetch(`${this.baseUrl}/powerbi/workspaces`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch workspaces: ${response.statusText}`);
    }

    return response.json();
  },

  async getReports(workspaceId: string) {
    const response = await fetch(
      `${this.baseUrl}/powerbi/reports?workspaceId=${workspaceId}`,
      {
        credentials: 'include',
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to fetch reports: ${response.statusText}`);
    }

    return response.json();
  },

  async generateEmbedToken(workspaceId: string, reportId: string) {
    const response = await fetch(`${this.baseUrl}/powerbi/embed-token`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
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
