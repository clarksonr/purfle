# Purfle — CLAUDE.md
*Auto-loaded by Claude Code at session start. Read this before doing anything else.*
*Updated at end of each session. Treat stale status as a bug.*

---

## What Purfle Is

Purfle is a **multi-agent AIVM desktop app**. It runs persistently on Windows
and Mac. The user installs agents — each defined by a signed manifest — and the
AIVM runs them on a schedule, sandboxed, unattended.

Example agents:
- `email-monitor` — polls Gmail every 15 minutes, summarizes new mail to a file
- `pr-watcher` — checks GitHub every 30 minutes for new pull requests
- `report-builder` — runs at 07:00, reads agent outputs, writes a morning report

The user sees one card per agent in the UI. Agents run in the background.
The AIVM enforces what each agent is allowed to do.

---

## Mental Model — Read This First

The AIVM is a C# class inside a .NET MAUI desktop app. It:
1. Loads signed agent manifests from disk
2. Starts each agent on its own thread on a schedule
3. Enforces the manifest's declared capabilities and permissions
4. Provides LLM inference via adapters (Anthropic first)
5. Exposes tools to the LLM via MCP
6. Writes agent output to a sandboxed local path
7. The LLM never touches the system directly — the AIVM executes on its behalf

**The AIVM guards the hen house.** The LLM proposes; the AIVM decides and acts.

### What an Agent Package IS
```
my-agent.purfle/
├── agent.manifest.json     ← signed, declares everything
├── lib/
│   └── MyAgent.dll         ← .NET assembly, loaded into isolated AssemblyLoadContext
├── prompts/
│   └── system.md           ← instruction file
└── assets/                 ← optional embedded resources
```

### What an Agent Package is NOT
| Wrong mental model | Correct mental model |
|---|---|
| Agent package = MCP server | MCP is a tool protocol used *by* the AIVM |
| Agent contains the LLM | Runtime provides inference via adapter |
| Agent holds API keys | Runtime holds credentials (Windows Credential Manager / Mac Keychain) |
| Agent calls tools directly | AIVM validates capability, then calls tool |

**MCP is plumbing inside the AIVM. It is not the packaging model.**

---

## Architecture

```
┌─────────────────────────────────────────────┐
│      .NET MAUI DESKTOP APP                  │
│      (MSIX on Windows / .pkg on macOS)      │
│  ┌──────────────────────────────────────┐   │
│  │              AIVM (C#)               │   │
│  │  Scheduler                           │   │
│  │  ├── AgentRunner: email-monitor      │   │
│  │  ├── AgentRunner: pr-watcher         │   │
│  │  └── AgentRunner: report-builder     │   │
│  └──────────────────────────────────────┘   │
│  UI: one card per agent                     │
└──────────────┬──────────────────────────────┘
               │ talks to
┌──────────────▼──────────────────────────────┐
│           AZURE (live services)              │
│  Key Registry   — Functions (consumption)   │
│  Marketplace    — App Service (F1 free)     │
│  IdentityHub.Web — App Service (F1 free)    │
└─────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  purfle demo  — starts these locally        │
│  mcp-file-server :8100                      │
│  mcp-gmail       :8102                      │
│  mcp-github      :8111                      │
└─────────────────────────────────────────────┘
```

---

## Locked Decisions — Do Not Revisit

- **Host:** .NET MAUI — Windows + Mac from one codebase
- **AIVM:** C# class inside the MAUI app — no separate process or daemon
- **Agents:** One thread each, isolated AssemblyLoadContext, sandboxed by manifest
- **Trigger model:** Scheduler built into AIVM — cron or interval in manifest
- **Output:** Local file under `<app-data>/aivm/output/<agent-id>/`
- **Identity:** JWS with ES256 (ECDSA). DID migration path later.
- **Algorithm:** ES256 — locked.
- **Capability model:** capabilities = enforcement list; permissions = per-capability config.
  No permissions entry without a matching capability. Capability without permissions is fine.
- **Credentials:** Owned by runtime (Windows Credential Manager / Mac Keychain). Agent never sees tokens.
- **MCP role:** Tool protocol only. AIVM wires tools at load time.
- **Cross-agent output sharing:** Deferred.
- **`io` block:** Optional, no enforcement in phase 1.
- **No over-engineering:** No abstractions for hypothetical requirements.
- **Deep link:** `purfle://` registered Windows + macOS (CFBundleURLTypes).
  `purfle://install?id={id}` → ConsentPage. `purfle://install?url={url}` → download → ConsentPage.
- **Admin auth:** PURFLE_ADMIN_TOKEN env var — bearer token for /admin routes.
- **Azure:** Consumption plan (Functions) + F1 free tier (App Service). Budget-conscious.
- **Demo model:** Azure hosts real APIs. `purfle demo` starts local MCP servers only.
- **Branding:** Purfle. No rename. No copy change.
- **Distribution:** Windows MSIX (code signing cert) + macOS .pkg/.dmg (Apple Developer).
- **Apple Team ID:** PLACEHOLDER_APPLE_TEAM_ID — replace before macOS CI build.
- **Bundle identifier:** PLACEHOLDER_BUNDLE_ID — likely com.clarksonr.purfle.
- **License:** MIT

---

## Manifest Structure — Canonical Reference

```json
{
  "purfle": "0.1",
  "id": "<uuid>",
  "name": "<display name>",
  "version": "<semver>",
  "description": "<string>",
  "identity": {
    "author": "<string>",
    "email": "<string>",
    "key_id": "<string>",
    "algorithm": "ES256",
    "issued_at": "<ISO 8601>",
    "expires_at": "<ISO 8601>",
    "signature": "<JWS compact serialization — omit at authoring time>"
  },
  "schedule": {
    "trigger": "interval | cron | startup",
    "interval_minutes": 15,
    "cron": "0 7 * * *"
  },
  "capabilities": ["llm.chat", "network.outbound", "env.read"],
  "permissions": {
    "network.outbound": { "hosts": ["api.anthropic.com"] },
    "env.read":         { "vars": ["ANTHROPIC_API_KEY"] },
    "fs.read":          { "paths": ["./data"] },
    "fs.write":         { "paths": ["./output"] }
  },
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic",
    "model": "claude-sonnet-4-20250514",
    "max_tokens": 1000
  },
  "lifecycle": {
    "on_load":   "<dotnet type string — optional>",
    "on_unload": "<dotnet type string — optional>",
    "on_error":  "terminate | log | ignore"
  },
  "tools": [
    { "name": "<string>", "server": "<mcp server url>", "description": "<string>" }
  ],
  "io": {}
}
```

### Capability strings
| Capability | Permission config | Meaning |
|---|---|---|
| `llm.chat` | none | Conversational LLM inference |
| `llm.completion` | none | Single-turn LLM completion |
| `network.outbound` | `hosts: string[]` | Outbound HTTP to listed hosts |
| `env.read` | `vars: string[]` | Read listed env vars |
| `fs.read` | `paths: string[]` | Read from listed paths |
| `fs.write` | `paths: string[]` | Write to listed paths |
| `mcp.tool` | none | Invoke MCP tools declared in `tools` |

---

## Stack

| Layer | Technology |
|---|---|
| Desktop | .NET MAUI (C#) — Windows + macOS |
| AIVM | C# class inside MAUI |
| Manifest spec | JSON Schema Draft 2020-12 |
| Agent identity | JWS / ES256 |
| Inference | Anthropic (primary); OpenAI, Gemini, Ollama |
| Scheduler | PeriodicTimer + NCrontab |
| SDK / CLI | TypeScript / Node.js |
| Tests | xUnit + Jest |
| macOS credentials | Keychain via SecureStorage |
| macOS notifications | UNUserNotificationCenter |
| macOS signing | Apple Developer — PLACEHOLDER_APPLE_TEAM_ID |
| Windows signing | Code signing cert (WINDOWS_CERT_PFX secret) |
| Azure | Functions consumption + App Service F1 |
| IaC | Bicep in `infra/` |

---

## Key Registration — One-Time Setup

**Context:** The Azure Key Registry Function requires an API key to POST or DELETE keys.
GET (verification) is unauthenticated. Registration is a one-time human action — not CI.

**Steps (do once, locally):**
1. Azure Portal → Key Registry Function App → App keys → copy any key value
2. Set in local terminal:
   - Windows: `$env:PURFLE_REGISTRY_API_KEY = "paste-key-here"`
   - macOS: `export PURFLE_REGISTRY_API_KEY=paste-key-here`
3. Run: `purfle setup`
4. Done. The public key is now registered in Azure permanently.

CI does not need this key. CI only verifies signatures (GET, unauthenticated).
You will only need to repeat this if you generate a new key pair.

---

## Azure Deployed Services

| Service | URL | Status |
|---|---|---|
| Key Registry | https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net | Live |
| Marketplace API | purfle-marketplace.azurewebsites.net | Bicep ready, not yet deployed |
| IdentityHub.Web | purfle-identityhub-web.azurewebsites.net | Bicep ready, not yet deployed |

---

## Required Secrets

| Variable | Where | Purpose |
|---|---|---|
| `ANTHROPIC_API_KEY` | Local + GH secret | LLM inference |
| `PURFLE_REGISTRY_API_KEY` | Local only, one-time | Register signing key (see above) |
| `PURFLE_ADMIN_TOKEN` | Azure App Service env | IdentityHub.Web admin auth |
| `AZURE_STORAGE_CONNECTION_STRING` | Azure App Service env | Backups + bundle store |
| `MARKETPLACE_API_URL` | IdentityHub.Web config | Marketplace proxy |
| `IDENTITYHUB_API_URL` | IdentityHub.Web config | IdentityHub proxy |
| `APPLE_TEAM_ID` | GH secret | macOS signing |
| `APPLE_ID` | GH secret | Notarization |
| `APPLE_APP_PASSWORD` | GH secret | Notarization app-specific password |
| `WINDOWS_CERT_PFX` | GH secret (base64) | MSIX signing |
| `WINDOWS_CERT_PASSWORD` | GH secret | MSIX signing |

---

## Current Status
*Update this section at the end of every session.*

### What exists and works

**Spec** — SPEC.md, JSON Schema, identity schema, RFC 0001, examples, AGENT_MODEL.md

**Runtime — 122 passing tests**
- AgentLoader (7-step), ManifestLoader/Validator, IdentityVerifier, JWS ES256
- IKeyRegistry, HttpKeyRegistryClient
- CapabilityNegotiator, AgentSandbox (network/fs/env/MCP)
- LoadFailureReason (13 reasons), BuiltInToolExecutor, ConversationSession
- AnthropicAdapter, GeminiAdapter, OpenClawAdapter, OllamaAdapter
  (all: backoff, token usage reporting)
- ProcessAgentRunner, CredentialStoreFactory (Win/Mac/Linux/InMemory)
- Scheduler (overlap skip, crash isolation), AgentRunner (run.jsonl + run.log)
- AgentAssemblyLoadContext, McpClient (stdio JSON-RPC 2.0)
- Purfle.TestAgents.Hello (HelloAgent + GreetTool)

**Key Registry** — deployed, trust loop verified, new key pair 2026-04-02
- Private key: temp-agent/signing.key.pem — DO NOT COMMIT
- Public key NOT YET registered — run purfle setup locally (see Key Registration above)

**SDK & CLI** — 73 core + 16 CLI tests
- @purfle/core: Ajv validation, JWS, canonical JSON
- @purfle/cli: init, build, sign, simulate, publish, search, install, login,
  validate, run, security-scan, pack, setup, demo

**Desktop App**
- SetupWizardPage (4-step), ConsentPage, AgentDetailPage (5-tab)
- AgentCard, LogViewPage, AgentRunPage, SettingsPage
- purfle:// deep link (Windows + macOS registered)
- NotificationService (Windows toast; macOS UNUserNotificationCenter)
- macOS: Entitlements (sandbox, network, keychain), code signing config in csproj

**Polyglot Agents** — 10 agents (C# + TypeScript, IPC protocol)

**MCP Servers** — 12 total (:8100–:8111); :8102 gmail and :8111 github are mocks

**IdentityHub**
- Core, Api, Web (public site + admin)
- Web public: Home, /agents, /agents/{id}, /publishers/{id}, /keys/{id}, /badge/{id}, /feed.xml
- Web admin: Dashboard, Agent moderation, Publisher mgmt, Key registry, Attestations, Backup/Restore
- BackupService: local zip + Azure Blob push/pull/list
- /health endpoints on both Api and Web

**Marketplace** — 19 tests, LocalFileBundleStore (Azure store not wired)

**Dogfood Agents** — email-monitor, pr-watcher, report-builder, file-assistant (signed)

**Marketplace** — /health endpoint added

**Infrastructure** — Bicep templates in infra/
- infra/marketplace.bicep (F1 App Service)
- infra/identityhub-web.bicep (shares F1 plan)

**CI/CD** — ci.yml (matrix + macOS build), release.yml (tag-triggered + macOS .pkg + notarization + Windows MSIX + Azure deploy), dependabot.yml

**Docs** — GETTING_STARTED, MANIFEST_REFERENCE, PUBLISHING, TROUBLESHOOTING, ROADMAP

### What does NOT exist yet (priority order)
1. Register signing public key — one-time local action, see Key Registration section above
2. Azure deployment: run Bicep + deploy step (infra ready, needs AZURE_CREDENTIALS secret)
3. Azure-backed bundle blob store (AzureBlobBundleStore)
4. Bundle SHA-256 integrity hashing
5. Real GitHub MCP server (replace mock)
6. Real Gmail MCP server (verify OAuth end-to-end)

---

## Session Rules

1. **Read this file before writing any code.**
2. **Never conflate agent packages with MCP servers.** See AGENT_MODEL.md.
3. **Never add capability or permission handling not declared in the manifest.**
4. **All manifest paths resolve relative to repo root.**
5. **Update "Current Status" before ending a session.**
6. **Complete each task fully including tests before moving to the next.**
7. **If underdetermined, stop and ask rather than invent.**
8. **Placeholders:** Never hardcode PLACEHOLDER_* values. Use the string as-is.
9. **Git rules — non-negotiable:**
   - Run all git commands from repo root. Never use `cd` before git.
   - Never chain with `&&`. Every command is its own line.
   - Never `git add .` — always stage by explicit file path.
   - Stage then commit as two separate commands.
   - Accumulate all commits locally. `git push` once at the very end.
10. **commit.md workflow:**
    - Create `commit.md` at session start using the template below.
    - Append an entry each time a logical unit of work is complete.
    - At session end: read commit.md top to bottom, execute each group.
    - After all commits: `git rm commit.md` then `git push`.
    - commit.md is never committed to the repo.

### commit.md template

```
# Session Commit Plan
Generated: <ISO timestamp>

## Group: <conventional commit message>
Type: feat|fix|chore|test|docs|refactor
Files:
- path/to/file1
- path/to/file2
Notes: <why together; note if any file also modified in a later group>

## Group: <next message>
Type: ...
Files:
- ...
Notes: ...
```

**Execution at session end:**
- For each group in order:
  - `git add path/to/file1`
  - `git add path/to/file2`
  - `git commit -m "type(scope): message"`
- If a file appears in multiple groups, include it only in the latest group.
  Note "also modified after: <earlier group>" in that earlier group's Notes.
- After all commits: `git rm commit.md`
- Then: `git push`
