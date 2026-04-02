# Dogfood Agents — Setup & Credentials

## Prerequisites

### API Key
Set `ANTHROPIC_API_KEY` in your environment. All three agents use the Anthropic engine.

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
```

On Windows, use the system environment variables dialog or:
```powershell
[System.Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-...", "User")
```

### MCP Mock Servers

Start the mock servers before running agents:

```bash
# Terminal 1 — Gmail mock (port 8102)
cd tools/mcp-gmail && npm start

# Terminal 2 — GitHub mock (port 8111)
cd tools/mcp-github && npm start
```

## Agents

### email-monitor
- **Schedule:** Every 15 minutes
- **MCP Server:** mcp-gmail on localhost:8102
- **Output:** `%LOCALAPPDATA%/aivm/output/b2e4f6a8-1234-4abc-9def-111111111111/`
- **Credentials:** ANTHROPIC_API_KEY
- **What it does:** Calls the Gmail mock to list unread emails, reads each one, writes a summary to `output/email-summary.md`

### pr-watcher
- **Schedule:** Every 30 minutes
- **MCP Server:** mcp-github on localhost:8111
- **Output:** `%LOCALAPPDATA%/aivm/output/c3f5a7b9-2345-4bcd-aef0-222222222222/`
- **Credentials:** ANTHROPIC_API_KEY
- **What it does:** Calls the GitHub mock to list open PRs, writes a summary to `output/pr-summary.md`

### report-builder
- **Schedule:** Cron `0 7 * * *` (07:00 daily)
- **MCP Server:** None (reads files only)
- **Output:** `%LOCALAPPDATA%/aivm/output/d4a6b8c0-3456-4cde-bf01-333333333333/`
- **Credentials:** ANTHROPIC_API_KEY
- **What it does:** Reads email-monitor and pr-watcher output files, writes a consolidated morning report

## Manual Trigger

To manually trigger an agent outside its schedule, use the Runtime Host:

```bash
cd runtime/src/Purfle.Runtime.Host
PURFLE_MANIFEST=../../../agents/email-monitor/agent.manifest.json dotnet run
```

Or install agents to the local store and use the MAUI app's "Run Now" button.

## Installing to Local Store

Copy agent directories to `%LOCALAPPDATA%/aivm/agents/`:

```bash
cp -r agents/email-monitor "$LOCALAPPDATA/aivm/agents/"
cp -r agents/pr-watcher "$LOCALAPPDATA/aivm/agents/"
cp -r agents/report-builder "$LOCALAPPDATA/aivm/agents/"
```

The MAUI app scheduler will pick them up on next launch.
