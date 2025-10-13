# Frontend Development Guide

The frontend is a Vite + React application that links to the Azure Static Web App (SWA) backend. This guide captures the project-
specific steps for running it locally and explains how the new development authentication bridge keeps local requests aligned with
SWA.

## Quick Start

```bash
# Install dependencies
npm install
cd frontend && npm install

# Start OpenFGA, Functions, and the frontend in separate processes
cd ..
npm run dev:native
```

The default dev script launches:

- OpenFGA on <http://localhost:8080>
- Azure Functions on <http://localhost:7071>
- Vite dev server on <http://localhost:5173>

Set `VITE_API_URL=http://localhost:7071/api` in `.env.local` so the frontend hits the local Functions host.

## Entra Sign-In & Static Web Apps Emulation

When you sign in with Entra ID locally, the new **Local Authentication Helper** banner appears. It generates a `X-MS-CLIENT-PRINCIPAL`
header from the MSAL account claims and automatically attaches it to every API request. This mirrors what Azure Static Web Apps does
in production, so the backend receives the same identity/role information.

- The helper is enabled only in `npm run dev` builds (`import.meta.env.DEV === true`).
- Toggle the Admin role in the banner to simulate SWA App Role assignments; the choice is stored in `localStorage` so your preference
  persists across refreshes.
- Use the **Reset Roles** button to revert to the roles contained in your Entra ID token.

If you need to disable the helper—for example when testing requests from the SWA CLI proxy—set `VITE_ENABLE_DEV_PRINCIPAL=false` in
`.env.local`.

## Power BI Tokens

Power BI requests still rely on MSAL for delegated tokens. The Local Authentication Helper is separate; it never forwards bearer
tokens to the API—only the static `X-MS-CLIENT-PRINCIPAL` payload expected by SWA.

## Troubleshooting

- **Banner does not appear** – Ensure you are running `npm run dev` and that `VITE_ENABLE_DEV_PRINCIPAL` is not set to `false`.
- **API returns 401** – Sign in again so MSAL refreshes the account claims, or use the Reset button to clear role overrides.
- **Testing anonymous access** – Use the banner’s close button to dismiss the helper temporarily, or disable it via
  `VITE_ENABLE_DEV_PRINCIPAL=false` and restart the dev server.
