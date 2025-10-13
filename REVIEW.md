# Architecture Audit

## ✅ Compliant
- **Managed identity to Azure SQL:** The Functions container reads the `DOTNET_CONNECTION_STRING` setting unchanged, logs a sanitized view, and configures EF Core directly without per-request interceptors. Managed identity credentials are exposed through `DefaultAzureCredential` for reuse across the app.【F:backend/MenuApi/Configuration/ServiceCollectionExtensions.cs†L24-L69】
- **API trusts Static Web Apps auth:** Request extensions only parse `x-ms-client-principal`, and functions such as `CheckAdmin` no longer perform custom JWT validation or expect browser tokens. Static Web Apps routes now require authentication for `/api/*`, aligning the frontend with EasyAuth semantics.【F:backend/MenuApi/Extensions/HttpRequestExtensions.cs†L16-L46】【F:backend/MenuApi/Functions/CheckAdmin.cs†L1-L70】【F:frontend/public/staticwebapp.config.json†L1-L20】
- **SQL diagnostics target managed identity:** Connectivity tests report managed identity state instead of X-SQL-Token fallbacks, and the debug endpoint acquires tokens via `TokenCredential` to verify MSI plumbing. The CLI helper script now connects with managed identity rather than embedded SQL credentials.【F:backend/MenuApi/Functions/TestSqlConnection.cs†L1-L131】【F:backend/MenuApi/Functions/SqlDebug.cs†L1-L165】【F:verify-menu-items.csx†L1-L62】
- **Power BI integration uses application credentials:** The backend service acquires Power BI access tokens through `DefaultAzureCredential`, while the frontend simply calls the linked API without forwarding user bearer tokens. Embed configuration consumes backend-issued embed tokens.【F:backend/MenuApi/Services/PowerBIService.cs†L1-L94】【F:backend/MenuApi/Functions/GetPowerBIWorkspaces.cs†L1-L63】【F:frontend/src/services/powerbi/client.ts†L1-L43】【F:frontend/src/components/PowerBI/PowerBIEmbed.tsx†L1-L94】

## ⚠️ Follow-up
- **Provision Azure roles:** Confirm the Function App’s managed identity (or chosen service principal) holds `db_datareader`/`db_datawriter` in Azure SQL and appropriate Power BI workspace permissions, since code now relies entirely on application credentials.
- **App settings parity:** Ensure the container App Settings include `DOTNET_CONNECTION_STRING` with `Authentication=Active Directory Default`, `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`, and `WEBSITES_PORT=8080` (or matching container configuration) to match the expectations logged by the diagnostics endpoints.

## 🛠️ Recommended fixes
- Rotate any existing SQL user passwords stored outside of source control and remove the legacy MSAL token acquisition scripts from CI/CD pipelines if present.
- Update runbooks to use the managed identity–based `verify-menu-items.csx` script or the `/debug/sql-test` endpoint for operational checks, replacing legacy guidance that referenced browser-provided SQL tokens.
