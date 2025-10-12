const API_URL = import.meta.env.VITE_API_URL;

if (!API_URL) {
  throw new Error('VITE_API_URL environment variable is required');
}

export const powerBIClient = {
  baseUrl: API_URL,

  async getWorkspaces(accessToken: string) {
    const response = await fetch(`${this.baseUrl}/powerbi/workspaces`, {
      headers: {
        'Authorization': `Bearer ${accessToken}`,
      },
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
      body: JSON.stringify({ workspaceId, reportId }),
    });

    if (!response.ok) {
      throw new Error(`Failed to generate embed token: ${response.statusText}`);
    }

    return response.json();
  },
};
