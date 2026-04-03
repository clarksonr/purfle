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

### GitHub Token (for pr-watcher)

The pr-watcher agent uses the real GitHub API via the MCP GitHub server. You need a personal access token with `repo` scope.

**Generate a token:**
1. Go to https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Select the `repo` scope
4. Copy the token

**Configure the token (choose one):**

```bash
# Option 1: Environment variable
export GITHUB_TOKEN="ghp_..."

# Option 2: Purfle credential file
mkdir -p ~/.purfle
echo "ghp_..." > ~/.purfle/github-token
chmod 600 ~/.purfle/github-token
```

On Windows:
```powershell
[System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "ghp_...", "User")
```

### Gmail OAuth (for email-monitor)

The email-monitor agent uses the real Gmail API via the MCP Gmail server with OAuth 2.0 PKCE.

**Setup:**
1. Create OAuth credentials in Google Cloud Console (https://console.cloud.google.com/apis/credentials)
2. Configure as a Desktop application
3. Set the following environment variables:

```bash
export GMAIL_CLIENT_ID="your-client-id.apps.googleusercontent.com"
export GMAIL_CLIENT_SECRET="your-client-secret"
```

4. On first run, the MCP Gmail server will open a browser for OAuth consent
5. Tokens are cached in `~/.purfle/gmail-tokens.json` for subsequent runs

**Fallback:** If OAuth is not configured, the Gmail server falls back to mock data for development.

### MCP Servers

Start the MCP servers before running agents:

```bash
# Terminal 1 — Gmail (port 8102)
cd tools/mcp-gmail && npm start

# Terminal 2 — GitHub (port 8111)
cd tools/mcp-github && npm start
```

## Agents

### email-monitor
- **Schedule:** Every 15 minutes
- **MCP Server:** mcp-gmail on localhost:8102
- **Output:** `%LOCALAPPDATA%/aivm/output/b2e4f6a8-1234-4abc-9def-111111111111/`
- **Credentials:** ANTHROPIC_API_KEY, Gmail OAuth
- **What it does:** Calls Gmail API to list unread emails, reads each one, writes a summary to `output/email-summary.md`

### pr-watcher
- **Schedule:** Every 30 minutes
- **MCP Server:** mcp-github on localhost:8111
- **Output:** `%LOCALAPPDATA%/aivm/output/c3f5a7b9-2345-4bcd-aef0-222222222222/`
- **Credentials:** ANTHROPIC_API_KEY, GITHUB_TOKEN
- **What it does:** Calls GitHub API to list open PRs, writes a summary to `output/pr-summary.md`

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
