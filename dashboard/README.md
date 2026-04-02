# Purfle Dashboard

Lightweight agent management dashboard. ASP.NET Core Minimal API + static HTML/JS with SignalR for real-time updates.

## Run

```bash
cd dashboard/src/Purfle.Dashboard.Api
dotnet run
```

Then open http://localhost:5000 (or the URL shown in console output).

## Features

- Agent grid with status indicators (running/stopped/error)
- Click any card to view detail panel with logs
- Start/Stop buttons with real-time status via SignalR
- Dark theme, responsive layout
- In-memory mock data (10 agents matching the Purfle agent catalog)

## API

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/agents | List all agents with status |
| GET | /api/agents/{id} | Single agent detail |
| GET | /api/agents/{id}/logs | Agent log entries |
| POST | /api/agents/{id}/start | Start an agent |
| POST | /api/agents/{id}/stop | Stop an agent |
| Hub | /hubs/agents | SignalR hub for real-time updates |
