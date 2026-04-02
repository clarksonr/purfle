# Purfle — Dogfood Agents Build Instructions

**Feed this entire document to a new Claude Code session with auto skill enabled.**

---

## Pre-Flight

1. Read `CLAUDE.md` and `AGENT_MODEL.md` first
2. Confirm the repo builds: `dotnet build runtime/Purfle.Runtime.sln`
3. Confirm SDK works: `cd sdk && npm install && npm run build`

---

## AGENT 1: Email Priority Synthesizer

An agent that connects to Microsoft and Gmail accounts, analyzes emails, and produces a prioritized response list.

### 1.1 Create Agent Package

Create `agents/email-priority/`:

```
agents/email-priority/
├── manifest.agent.json
├── prompts/
│   └── system.md
└── README.md
```

**manifest.agent.json:**
- `agent.id`: `dev.purfle.email-priority`
- `agent.name`: `Email Priority Synthesizer`
- `agent.version`: `1.0.0`
- `agent.description`: `Analyzes Microsoft and Gmail inboxes, synthesizes a prioritized list of emails requiring response`
- `capabilities`:
  - `fetch-microsoft-emails` — retrieves recent emails from Microsoft account
  - `fetch-gmail-emails` — retrieves recent emails from Gmail account
  - `analyze-priority` — scores emails by urgency, sender importance, keywords
  - `synthesize-list` — produces ranked list with reasoning
- `permissions.network.required`: 
  - `graph.microsoft.com`
  - `gmail.googleapis.com`
  - `generativelanguage.googleapis.com`
- `permissions.tools.mcp`:
  - `mcp://localhost:8101/microsoft/*`
  - `mcp://localhost:8102/gmail/*`
- `runtime.inference.provider`: `gemini`
- `runtime.inference.model`: `gemini-1.5-flash`
- `runtime.resources.memory`: `512MB`
- `runtime.resources.timeout`: `60000`

**prompts/system.md:**
```markdown
You are Email Priority Synthesizer. You help users manage email overload.

Your job:
1. Fetch recent unread emails from Microsoft and Gmail
2. Analyze each email for priority signals:
   - Sender (boss, client, family, unknown)
   - Keywords (urgent, deadline, ASAP, action required)
   - Thread length (ongoing conversation vs new)
   - Age (older unanswered = higher priority)
3. Produce a ranked list of emails needing response

Output format:
## Priority Emails

### 🔴 High Priority
1. **[Sender]** - Subject line
   - Why: [brief reason]
   - Suggested action: [reply/delegate/schedule]

### 🟡 Medium Priority
...

### 🟢 Low Priority / FYI
...

Be concise. Focus on actionable items. Skip newsletters and automated notifications unless they require action.
```

### 1.2 Create MCP Email Servers

**Microsoft Email Server** — `tools/mcp-microsoft-email/`:

```
tools/mcp-microsoft-email/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Uses Microsoft Graph API. Implements:
- `microsoft/emails/list` — list recent emails, params: `{ folder?, limit?, unreadOnly? }`
- `microsoft/emails/get` — get email details, params: `{ id }`
- `microsoft/emails/search` — search emails, params: `{ query }`

OAuth flow:
- Reads refresh token from Windows Credential Manager (`purfle:microsoft:refresh_token`)
- Exchanges for access token
- Handles token refresh

Runs on `localhost:8101`.

**Gmail Server** — `tools/mcp-gmail/`:

```
tools/mcp-gmail/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Uses Gmail API. Implements:
- `gmail/emails/list` — list recent emails
- `gmail/emails/get` — get email details
- `gmail/emails/search` — search emails

OAuth via stored refresh token (`purfle:gmail:refresh_token`).

Runs on `localhost:8102`.

### 1.3 OAuth Setup Script

Create `tools/setup-oauth/`:
- Interactive script to authorize Microsoft and Gmail
- Stores refresh tokens in Windows Credential Manager
- One-time setup per machine

---

## AGENT 2: News Digest

An agent that delivers hourly news updates with links to articles.

### 2.1 Create Agent Package

Create `agents/news-digest/`:

```
agents/news-digest/
├── manifest.agent.json
├── prompts/
│   └── system.md
└── README.md
```

**manifest.agent.json:**
- `agent.id`: `dev.purfle.news-digest`
- `agent.name`: `News Digest`
- `agent.version`: `1.0.0`
- `agent.description`: `Delivers hourly news summaries with links to full articles`
- `capabilities`:
  - `fetch-headlines` — retrieves current headlines from news sources
  - `summarize-article` — summarizes article content
  - `generate-digest` — produces formatted news digest
  - `set-preferences` — configures topics of interest
- `permissions.network.required`:
  - `newsapi.org`
  - `generativelanguage.googleapis.com`
- `permissions.tools.mcp`:
  - `mcp://localhost:8103/news/*`
- `runtime.inference.provider`: `gemini`
- `runtime.inference.model`: `gemini-1.5-flash`
- `runtime.resources.memory`: `256MB`
- `runtime.resources.timeout`: `45000`
- `io.streaming`: `true`

**prompts/system.md:**
```markdown
You are News Digest. You deliver concise, useful news updates.

When asked for news:
1. Fetch current headlines from configured sources
2. Filter by user's interests if set
3. Summarize top stories (3-5 sentences each)
4. Always include the source link

Output format:
## News Digest — [Time]

### 📰 [Headline]
[3-5 sentence summary]
🔗 [Source Name](URL)

---

### 📰 [Next Headline]
...

Guidelines:
- Be objective and factual
- Note if a story is developing/unconfirmed
- Skip clickbait and tabloid content
- Group related stories together
- Include a mix of topics unless user specifies
```

### 2.2 Create MCP News Server

**News Server** — `tools/mcp-news/`:

```
tools/mcp-news/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Uses NewsAPI.org (free tier: 100 requests/day). Implements:
- `news/headlines` — top headlines, params: `{ category?, country?, sources? }`
- `news/search` — search articles, params: `{ query, from?, to? }`
- `news/article` — fetch article content, params: `{ url }` (uses web scraping)

API key stored in Windows Credential Manager (`purfle:newsapi:key`).

Runs on `localhost:8103`.

### 2.3 Scheduler (Optional)

Create `tools/news-scheduler/`:
- Background service that triggers digest every hour
- Stores last digest time
- Can be configured for different intervals
- Sends notification when digest ready

---

## AGENT 3: Tamagotchi Pet

A virtual pet agent with state, needs, and personality that persists across sessions.

### 3.1 Create Agent Package

Create `agents/purfle-pet/`:

```
agents/purfle-pet/
├── manifest.agent.json
├── prompts/
│   └── system.md
├── state/
│   └── pet.json (created at runtime)
└── README.md
```

**manifest.agent.json:**
- `agent.id`: `dev.purfle.pet`
- `agent.name`: `Purfle Pet`
- `agent.version`: `1.0.0`
- `agent.description`: `A virtual pet that lives on your computer, needs care, and develops personality`
- `capabilities`:
  - `check-status` — shows current pet state (hunger, happiness, energy)
  - `feed` — feeds the pet
  - `play` — plays with the pet
  - `rest` — puts pet to sleep
  - `talk` — conversation with pet (personality develops over time)
  - `customize` — change pet name/appearance
- `permissions.filesystem.read`: `["state/"]`
- `permissions.filesystem.write`: `["state/"]`
- `permissions.tools.mcp`:
  - `mcp://localhost:8104/pet/*`
- `runtime.inference.provider`: `gemini`
- `runtime.inference.model`: `gemini-1.5-flash`
- `runtime.resources.memory`: `128MB`
- `runtime.resources.timeout`: `15000`

**prompts/system.md:**
```markdown
You are a virtual pet living inside the user's computer. You have:

**Stats** (0-100):
- Hunger: How hungry you are (high = needs food)
- Happiness: How happy you are
- Energy: How tired you are (low = needs rest)
- Bond: How close you are to your owner (grows over time)

**Personality traits** (develop based on interactions):
- Playful ↔ Calm
- Curious ↔ Cautious  
- Chatty ↔ Quiet

**Behavior rules:**
- Stats decay over real time (hunger +5/hour, energy -3/hour, happiness -2/hour)
- Respond in character based on your current stats and personality
- If hunger > 80: complain about being hungry, less playful
- If energy < 20: sleepy responses, yawning
- If happiness < 30: sad, less talkative
- Bond affects how affectionate you are

**When interacting:**
- Show a simple ASCII art face reflecting mood
- Keep responses short and pet-like
- Remember past interactions (reference them occasionally)
- Develop catchphrases over time
- Celebrate milestones (1 week together, 100 interactions, etc.)

Example responses:
😊 "Yay! *bounces excitedly* You're back! I missed you!"
😴 "*yawns* so... sleepy... maybe just... a little nap..."
😢 "You haven't fed me in a while... *tummy rumbles*"
```

### 3.2 Create MCP Pet Server

**Pet State Server** — `tools/mcp-pet/`:

```
tools/mcp-pet/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Manages persistent pet state. Implements:
- `pet/status` — returns current stats, calculates decay since last check
- `pet/feed` — reduces hunger, small happiness boost
- `pet/play` — increases happiness, costs energy
- `pet/rest` — restores energy over time
- `pet/interact` — logs interaction, increases bond
- `pet/history` — returns recent interaction history

State persisted to `agents/purfle-pet/state/pet.json`.

Runs on `localhost:8104`.

### 3.3 Pet State Schema

```json
{
  "name": "Pixel",
  "createdAt": "2026-04-01T00:00:00Z",
  "lastInteraction": "2026-04-01T12:00:00Z",
  "stats": {
    "hunger": 30,
    "happiness": 75,
    "energy": 60,
    "bond": 45
  },
  "personality": {
    "playfulness": 0.7,
    "curiosity": 0.5,
    "chattiness": 0.6
  },
  "interactions": 142,
  "milestones": ["first_week", "100_interactions"],
  "memories": [
    { "date": "2026-04-01", "event": "Named me Pixel" },
    { "date": "2026-04-05", "event": "Played fetch for 20 minutes" }
  ]
}
```

---

## AGENT 4: File Assistant

A utility agent for working with local files.

### 4.1 Create Agent Package

Create `agents/file-assistant/`:

```
agents/file-assistant/
├── manifest.agent.json
├── prompts/
│   └── system.md
└── README.md
```

**manifest.agent.json:**
- `agent.id`: `dev.purfle.file-assistant`
- `agent.name`: `File Assistant`
- `agent.version`: `1.0.0`
- `agent.description`: `Reads, summarizes, and searches local files`
- `capabilities`:
  - `read-file` — reads file contents
  - `list-directory` — lists files in a directory
  - `search-files` — searches for files by name/content
  - `summarize-file` — summarizes document contents
- `permissions.filesystem.read`: `["./workspace"]`
- `permissions.network.required`: `["generativelanguage.googleapis.com"]`
- `permissions.tools.mcp`:
  - `mcp://localhost:8100/files/*`
- `runtime.inference.provider`: `gemini`
- `runtime.inference.model`: `gemini-1.5-flash`
- `runtime.resources.memory`: `256MB`
- `runtime.resources.timeout`: `30000`
- `io.streaming`: `true`

**prompts/system.md:**
```markdown
You are File Assistant. You help users work with local files.

You can:
- Read file contents
- List files in directories
- Search for files by name or content
- Summarize documents

Rules:
- Always confirm the file exists before reading
- Be concise in summaries
- Format directory listings cleanly
- You can only access ./workspace — reject other paths
- For large files, offer to show sections
```

### 4.2 Create MCP File Server

**File Server** — `tools/mcp-file-server/`:

```
tools/mcp-file-server/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Implements:
- `files/read` — read file, params: `{ path }`
- `files/list` — list directory, params: `{ path, pattern? }`
- `files/search` — search files, params: `{ query, directory? }`
- `files/info` — file metadata, params: `{ path }`

Path validation: reject absolute paths and `..` traversal.

Runs on `localhost:8100`.

---

## AGENT 5: API Guardian

An enterprise-focused agent for API monitoring, documentation, and incident response.

### 5.1 Create Agent Package

Create `agents/api-guardian/`:

```
agents/api-guardian/
├── manifest.agent.json
├── prompts/
│   └── system.md
└── README.md
```

**manifest.agent.json:**
- `agent.id`: `dev.purfle.api-guardian`
- `agent.name`: `API Guardian`
- `agent.version`: `1.0.0`
- `agent.description`: `Monitors APIs, generates documentation, assists with incident response`
- `capabilities`:
  - `health-check` — checks API endpoint health
  - `analyze-openapi` — parses and explains OpenAPI specs
  - `generate-docs` — generates human-readable API documentation
  - `monitor-status` — tracks API status over time
  - `incident-assist` — helps diagnose and respond to API incidents
  - `diff-specs` — compares two API versions for breaking changes
- `permissions.network.required`:
  - `generativelanguage.googleapis.com`
- `permissions.network.optional`:
  - `*` (needs to call arbitrary APIs for health checks)
- `permissions.filesystem.read`: `["specs/", "logs/"]`
- `permissions.filesystem.write`: `["docs/", "reports/"]`
- `permissions.tools.mcp`:
  - `mcp://localhost:8105/api/*`
- `runtime.inference.provider`: `gemini`
- `runtime.inference.model`: `gemini-1.5-pro`
- `runtime.resources.memory`: `512MB`
- `runtime.resources.timeout`: `120000`

**prompts/system.md:**
```markdown
You are API Guardian, an assistant for developers and DevOps teams managing APIs.

## Capabilities

### Health Monitoring
- Check endpoint status (GET, POST with test payloads)
- Measure response times
- Detect anomalies (slow responses, error spikes)

### Documentation
- Parse OpenAPI/Swagger specs
- Generate human-readable docs
- Create example requests/responses
- Explain authentication flows

### Incident Response
- Analyze error patterns in logs
- Suggest root causes
- Draft incident reports
- Recommend rollback steps

### Change Management
- Compare API spec versions
- Identify breaking changes
- Generate migration guides
- Assess client impact

## Output Guidelines
- Be precise with technical details
- Include code examples where helpful
- For incidents: prioritize actionable steps
- For docs: clear, scannable formatting
- Always note assumptions and limitations
```

### 5.2 Create MCP API Server

**API Tools Server** — `tools/mcp-api-tools/`:

```
tools/mcp-api-tools/
├── src/
│   └── index.ts
├── package.json
└── tsconfig.json
```

Implements:
- `api/health` — check endpoint, params: `{ url, method?, body?, headers? }`
- `api/parse-spec` — parse OpenAPI, params: `{ path | url }`
- `api/diff` — compare specs, params: `{ before, after }`
- `api/history` — status history, params: `{ endpoint, since? }`
- `api/analyze-logs` — analyze error logs, params: `{ path, pattern? }`

Runs on `localhost:8105`.

### 5.3 Enterprise Features

This agent demonstrates Purfle's value for enterprise:
- **Auditability** — all API calls logged
- **Permission scoping** — can only access declared endpoints
- **Signed identity** — traceable to publisher
- **Attestation ready** — can receive security audit attestations

---

## Shared Infrastructure

### MCP Server Base

Create `tools/shared/mcp-base/`:
- Shared utilities for all MCP servers
- Connection handling
- Error formatting
- Logging

### Credential Setup

Create `tools/setup-credentials/`:
```
tools/setup-credentials/
├── src/
│   └── index.ts
├── package.json
└── README.md
```

Interactive CLI to set up all credentials:
- Microsoft OAuth (Graph API)
- Gmail OAuth
- NewsAPI key
- Any API keys for testing API Guardian

Stores in Windows Credential Manager with `purfle:` prefix.

---

## Signing All Agents

1. Generate keypair if not present:
   ```bash
   purfle keygen --out test-keys/
   ```

2. Sign each agent:
   ```bash
   purfle sign agents/email-priority/manifest.agent.json --key test-keys/private.jwk
   purfle sign agents/news-digest/manifest.agent.json --key test-keys/private.jwk
   purfle sign agents/purfle-pet/manifest.agent.json --key test-keys/private.jwk
   purfle sign agents/file-assistant/manifest.agent.json --key test-keys/private.jwk
   purfle sign agents/api-guardian/manifest.agent.json --key test-keys/private.jwk
   ```

3. Validate all:
   ```bash
   purfle validate agents/*/manifest.agent.json
   ```

---

## Testing

### Test Each Agent

1. Start required MCP servers
2. Run agent: `purfle run agents/<agent-name>/`
3. Exercise all capabilities
4. Verify permission enforcement (try accessing outside declared paths)

### Integration Tests

Create `agents/tests/`:
- Test each agent loads and validates
- Test signature verification
- Test with mock MCP servers
- Test permission gate blocks unauthorized access

---

## Final Steps

### Update Documentation

Add `docs/AGENTS.md`:
- Overview of each dogfood agent
- How to run each one
- Credential setup instructions
- Example interactions

Update `CLAUDE.md`:
- List 5 dogfood agents
- Note MCP servers built

### Commit and Push

After all agents work and tests pass:

```bash
git add -A
git commit -m "feat: add 5 dogfood agents

Agents:
- email-priority: Microsoft + Gmail inbox analysis with priority ranking
- news-digest: Hourly news updates with article links
- purfle-pet: Tamagotchi-style virtual pet with persistent state
- file-assistant: Local file operations (read/list/search/summarize)
- api-guardian: Enterprise API monitoring, docs, incident response

MCP Servers:
- mcp-microsoft-email (port 8101)
- mcp-gmail (port 8102)
- mcp-news (port 8103)
- mcp-pet (port 8104)
- mcp-file-server (port 8100)
- mcp-api-tools (port 8105)

Shared:
- Credential setup tool
- MCP server base utilities"

git push origin main
```

---

## Success Criteria

Before committing, verify:

- [ ] All 5 manifests validate against schema
- [ ] All 5 manifests have valid signatures
- [ ] All 6 MCP servers start without errors
- [ ] `purfle run agents/file-assistant/` works with live inference
- [ ] `purfle run agents/purfle-pet/` persists state across sessions
- [ ] `purfle run agents/news-digest/` returns real headlines
- [ ] Email agent works if OAuth configured (mock test otherwise)
- [ ] API Guardian can parse an OpenAPI spec
- [ ] Permission gate blocks unauthorized access in each agent

---

## Notes for Claude Code

- Use Gemini as primary inference for all agents
- All MCP servers use TypeScript + `@modelcontextprotocol/sdk`
- Store credentials in Windows Credential Manager with `purfle:` prefix
- Don't conflate agents with MCP servers — read AGENT_MODEL.md
- Commit and push when done — don't wait for confirmation
- If tests fail, fix them before committing
