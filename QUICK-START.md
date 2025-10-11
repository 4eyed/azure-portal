# Quick Start: Local Development

## TL;DR

```bash
# Install dependencies (first time only)
npm install
cd frontend && npm install && cd ..

# Start everything (OpenFGA + Backend + Frontend)
npm run dev

# Open browser
open http://localhost:5173
```

## What You Get

- **Frontend**: http://localhost:5173 - React UI for selecting users and viewing menus
- **Backend API**: http://localhost:7071/api - Azure Functions with OpenFGA integration
- **OpenFGA**: http://localhost:8080 - Authorization server

## Test It

```bash
# Alice (admin) - sees all menus
curl http://localhost:7071/api/menu?user=alice

# Bob (viewer) - sees only dashboard
curl http://localhost:7071/api/menu?user=bob

# Charlie (editor) - sees dashboard and reports
curl http://localhost:7071/api/menu?user=charlie
```

## Stop Everything

Press `Ctrl+C` or:

```bash
npm run stop
```

## Troubleshooting

If ports are already in use:

```bash
npm run stop  # Kills processes on 8080, 7071, 5173
npm run dev   # Start fresh
```

See [LOCAL-DEV-GUIDE.md](LOCAL-DEV-GUIDE.md) for detailed documentation.
