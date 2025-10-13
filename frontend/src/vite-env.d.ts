/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AZURE_CLIENT_ID: string;
  readonly VITE_AZURE_TENANT_ID: string;
  readonly VITE_AZURE_REDIRECT_URI: string;
  readonly VITE_API_URL: string;
  readonly VITE_POWERBI_WORKSPACE_ID: string;
  readonly VITE_POWERBI_REPORT_ID: string;
  readonly VITE_POWERBI_EMBED_URL: string;
  readonly VITE_ENABLE_DEV_PRINCIPAL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
