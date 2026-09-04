# RHINO_minion frontend

Landing page and V0 control panel for RHINO_minion.

## Run locally

```bash
npm install
npm run dev
```

Start the Python backend on `127.0.0.1:8766` before opening the frontend. Vite proxies `/health` and `/api` to it. The panel discovers connected Rhino sessions, reads the scene and can create a demo mass through the real bridge.

Free-form AI prompts are intentionally not simulated: they will be enabled when the Planner/Executor backend is implemented.

## Stack
- React
- Vite
- Lucide icons
- Plain CSS

The original electric-blue accent was replaced with light gray throughout the interface.
