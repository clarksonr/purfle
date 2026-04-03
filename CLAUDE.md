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
4. Provides LLM inference via the engine adapter declared in the manifest
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
- **Trigger model:** Scheduler built into AIVM — trigger type declared in manifest
- **Output:** Local file under `<app-data>/aivm/output/<agent-id>/`
- **Identity:** JWS with ES256 (ECDSA). DID migration path later.
- **Algorithm:** ES256 — locked.
- **Capability model:** capabilities = enforcement list; permissions = per-capability config.
  No permissions entry without a matching capability. Capability without permissions is fine.
- **Credentials:** Owned by runtime (Windows Credential Manager / Mac Keychain). Agent never sees tokens.
- **MCP role:** Tool protocol only. AIVM wires tools at load time.
- **Engine agnostic — locked:** The runtime selects the adapter based on `runtime.engine`
  in the manifest. No engine is hardcoded, preferred, or defaulted anywhere in the codebase,
  prompts, or documentation. Supported engines: `gemini`, `anthropic`, `openai`, `ollama`.
  Current developer preference: Gemini. Do not assume Anthropic anywhere.
- **Cross-agent output sharing:** Deferred.
- **`io` block:** Optional, no enforcement in phase 1.
- **No over-engineering:** No abstractions for hypothetical requirements.
- **Deep link:** `purfle://` registered Windows + macOS (CFBundleURLTypes).
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
    "trigger": "interval | cron | startup | window | event",
    "interval_minutes": 15,
    "cron": "0 7 * * *",
    "window": {
      "start":    "<ISO 8601 datetime or cron expression>",
      "end":      "<ISO 8601 datetime or cron expression>",
      "run_at":   "window_open | window_close | interval_within",
      "timezone": "UTC"
    },
    "event": {
      "source": "<mcp server url>",
      "topic":  "<string>"
    }
  },
  "capabilities": ["llm.chat", "network.outbound", "env.read"],
  "permissions": {
    "network.outbound": { "hosts": ["<llm-provider-api-host>"] },
    "env.read":         { "vars": ["<ENGINE>_API_KEY"] },
    "fs.read":          { "paths": ["./data"] },
    "fs.write":         { "paths": ["./output"] }
  },
  "runtime": {
    "requires": "purfle/0.1",
    "engine":   "gemini | anthropic | openai | ollama",
    "engine_fallback": ["anthropic", "openai"],
    "model":    "<model string for chosen engine>",
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
  "io": {
    "reads": ["<agent-id-uuid>"]
  }
}
```

### Engine values and their model strings
| Engine | `runtime.engine` | Example model string | API key env var |
|---|---|---|---|
| Google Gemini | `gemini` | `gemini-2.0-flash` | `GEMINI_API_KEY` |
| Anthropic | `anthropic` | `claude-sonnet-4-20250514` | `ANTHROPIC_API_KEY` |
| OpenAI | `openai` | `gpt-4o` | `OPENAI_API_KEY` |
| Ollama (local) | `ollama` | `llama3` | none (localhost:11434) |

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
| `agent.read` | none | Read output files from agents declared in io.reads |
| `hardware.*.*` | `asset_id, window_start, window_end, commands[]` | RFC 0002 hardware access — future |

### Schedule trigger types
| Trigger | Required fields | Meaning |
|---|---|---|
| `interval` | `interval_minutes` | Run every N minutes, indefinitely |
| `cron` | `cron` | Run on NCrontab expression |
| `startup` | none | Run once when AIVM starts |
| `window` | `window.start, window.end, window.run_at` | Run relative to a declared time window |
| `event` | `event.source, event.topic` | Run when an MCP server emits a named event |

**window trigger details:**
- `window_open` — fires once when the window opens
- `window_close` — fires once shortly before the window closes (default 60s lead)
- `interval_within` — fires on `interval_minutes` cadence only while inside the window;
  suppressed outside, no catch-up after window closes
- start/end: ISO 8601 datetime (one-shot) or cron expression (recurring)

**event trigger details:**
- AIVM subscribes to the MCP server's event stream at agent load time
- When the named topic fires, the agent runs immediately
- If already running: queue event (depth 1), drop further events and log
- On unload: unsubscribe and close connection cleanly

---

## Stack

| Layer | Technology |
|---|---|
| Desktop | .NET MAUI (C#) — Windows + macOS |
| AIVM | C# class inside MAUI |
| Manifest spec | JSON Schema Draft 2020-12 |
| Agent identity | JWS / ES256 |
| Inference | Engine-agnostic: Gemini, OpenAI, Ollama, Anthropic (adapter per engine) |
| Scheduler | PeriodicTimer + NCrontab + WindowTrigger + EventTrigger |
| SDK / CLI | TypeScript / Node.js |
| Tests | xUnit + Jest |
| macOS credentials | Keychain via SecureStorage |
| macOS notifications | UNUserNotificationCenter |
| Windows notifications | WinRT toast via INotificationService |
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
4. Done. Public key is registered in Azure permanently.

CI does not need this key. CI only verifies signatures (GET, unauthenticated).
Repeat only if you generate a new key pair.

---

## Azure Deployed Services

| Service | URL | Status |
|---|---|---|
| Key Registry | https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net | Live |
| Marketplace API | purfle-marketplace.azurewebsites.net | Bicep ready, not deployed |
| IdentityHub.Web | purfle-identityhub-web.azurewebsites.net | Bicep ready, not deployed |

---

## Required Secrets

| Variable | Where | Purpose |
|---|---|---|
| `GEMINI_API_KEY` | Local + GH secret | LLM inference — Gemini (current dev preference) |
| `ANTHROPIC_API_KEY` | Local + GH secret | LLM inference — Anthropic adapter |
| `OPENAI_API_KEY` | Local + GH secret | LLM inference — OpenAI adapter |
| `PURFLE_REGISTRY_API_KEY` | Local only, one-time | Register signing key |
| `PURFLE_ADMIN_TOKEN` | Azure App Service env | IdentityHub.Web admin auth |
| `AZURE_STORAGE_CONNECTION_STRING` | Azure App Service env | Backups + bundle store |
| `MARKETPLACE_API_URL` | IdentityHub.Web config | Marketplace proxy |
| `IDENTITYHUB_API_URL` | IdentityHub.Web config | IdentityHub proxy |
| `APPLE_TEAM_ID` | GH secret | macOS signing |
| `APPLE_ID` | GH secret | Notarization |
| `APPLE_APP_PASSWORD` | GH secret | Notarization app-specific password |
| `WINDOWS_CERT_PFX` | GH secret (base64) | MSIX signing |
| `WINDOWS_CERT_PASSWORD` | GH secret | MSIX signing |
| `AZURE_CREDENTIALS` | GH secret | Bicep deploy (service principal JSON) |
| `GITHUB_TOKEN` | Local env or ~/.purfle/github-token | GitHub MCP server (repo scope) |
| `GMAIL_CLIENT_ID` | Local env | Gmail OAuth client ID |
| `GMAIL_CLIENT_SECRET` | Local env | Gmail OAuth client secret |
| `PURFLE_BUNDLES_CONTAINER` | Azure App Service env | Azure bundle blob container name |

---

## Current Status
*Update this section at the end of every session.*

### What exists and works

**Spec**
- SPEC.md, JSON Schema (Draft 2020-12), identity schema
- RFC 0001 — identity model (JWS/ES256)
- RFC 0002 — hardware access model (concept/pre-proposal) `spec/rfcs/0002-hardware-access-model.md`
- RFC 0003 — cross-agent output sharing (status: Accepted) `spec/rfcs/0003-cross-agent-output-sharing.md`
- Examples: hello-world, assistant, email-monitor, demo-agent, window-agent, event-agent
- AGENT_MODEL.md

**Runtime — 169 unit tests + 11 integration tests**
- AgentLoader (7-step), ManifestLoader/Validator, IdentityVerifier, JWS ES256
- IKeyRegistry, HttpKeyRegistryClient
- CapabilityNegotiator, AgentSandbox (network/fs/env/MCP)
- LoadFailureReason (14 reasons incl. InvalidCrossAgentReference), BuiltInToolExecutor, ConversationSession
- GeminiAdapter, AnthropicAdapter, OpenClawAdapter, OllamaAdapter (backoff, token usage, ResolvedCredential)
- ProcessAgentRunner, CredentialStoreFactory (Win/Mac/Linux/InMemory)
- **Multi-provider auth** — AuthProfileStore (file + keychain), CredentialResolutionEngine (fallback cascade),
  UserProviderPreferences, ICredentialResolver, env var seeding, 22 auth tests
- **engine_fallback** — manifest field for ordered engine fallback list
- Scheduler: interval, cron, startup, window, event — overlap skip, crash isolation
- WindowTrigger: window_open, window_close, interval_within (ISO 8601 + cron windows)
- EventTrigger: IEventSource/IEventSourceFactory, queue depth 1, drop on full
- **SseEventSource** — production SSE client with exponential backoff + jitter reconnect
- **SseEventSourceFactory** — DI-ready factory for production event sources
- AgentRunner (run.jsonl + run.log), AgentAssemblyLoadContext, McpClient
- Purfle.TestAgents.Hello
- **IAgentOutputReader + AgentSandboxedOutputReader** — cross-agent output sharing via io.reads
- **agent.read capability** — declares cross-agent read access (no permissions config)
- **ITokenUsageTracker + FileTokenUsageTracker** — usage.jsonl per agent, append-only, thread-safe

**Key Registry** — deployed, trust loop verified
- Signing key: com.clarksonr/release-2026 (2026-04-02)
- Private key: temp-agent/signing.key.pem — DO NOT COMMIT
- Public key NOT YET registered in Azure — run purfle setup locally

**SDK & CLI** — 73 tests (49 core + 17 CLI + 7 operational)
- init, build, sign, simulate (--trigger window_open/event), publish, search, install, login,
  validate, run, security-scan, pack (SHA-256 sidecar), setup (GitHub token check), demo
- **purfle status** — agent roster table with trigger, last run, next run, status (color-coded)
- **purfle logs** `<agent-id>` `[--tail N]` `[--follow]` — view agent run.log with streaming
- **purfle uninstall** `<agent-id>` `[--keep-output]` `[--yes]` — remove agent and output
- **purfle update** `[<agent-id>]` — Marketplace version check, SHA-256 verify, signature verify, reinstall
- **purfle doctor** — 12-item environment checklist with color-coded output (✓/✗/⚠)
- **purfle demo** — color-coded banner, server summary table, next-steps block
- Bundle SHA-256 integrity: pack writes .sha256, install verifies hash on download

**Desktop App**
- **DashboardPage** — default landing tab with summary bar, today's digest, agent roster
- SetupWizardPage (4-step), ConsentPage, AgentDetailPage (7-tab: Overview, Permissions, Files, History, System.md, Usage, Install)
- **RunHistoryPage** — virtualized run list, All/Success/Error filter, load-more pagination (50/page)
- **RunDetailPage** — full output display, error detail section, token usage, Retry action, Open output folder
- **AgentCard live state** — pulse animation, relative timestamps, 100-char output preview,
  120-char error truncation, "No output yet" placeholder
- **Marketplace install flow** — Install tab fetches agent metadata, streams CLI output live,
  success/error banners, offline handling
- **INotificationService** + MacNotificationService + WindowsNotificationService (WinRT toast) + NullNotificationService
- **Settings page** — API key management via SecureStorage (Gemini/Anthropic/OpenAI with status dots), Ollama URL + test,
  output dir with Open in Explorer/Finder, log retention (7/14/30/90 days), notification prefs (master + 3 sub-toggles),
  About section with version/runtime/platform + Copy Diagnostic Info
- **Connected Accounts section** — multi-provider auth profiles with status dots, drag-to-reorder priority,
  add/remove API key, ConnectedAccountsViewModel, provider preference persistence
- **Token usage tab** in AgentDetailPage — per-agent usage.jsonl viewer with cost estimates
- AgentCard, LogViewPage, AgentRunPage
- purfle:// deep link (Windows + macOS)
- Entitlements.plist, CFBundleURLTypes, code signing config in csproj

**Polyglot Agents** — 10 agents (C# + TypeScript, IPC)

**MCP Servers** — 12 total (:8100–:8111)
- :8111 GitHub — real GitHub REST API (token via GITHUB_TOKEN or ~/.purfle/github-token)
- :8102 Gmail — real Gmail API with OAuth 2.0 PKCE (falls back to mock if no OAuth)

**IdentityHub**
- Core, Api, Web (public + admin + backup/restore)
- GET /health on Api and Web

**Marketplace** — 24 tests, LocalFileBundleStore + AzureBlobBundleStore, GET /health
- AzureBlobBundleStore: AZURE_STORAGE_CONNECTION_STRING + PURFLE_BUNDLES_CONTAINER
- Bundle SHA-256 hash stored on upload, returned in version metadata

**Dogfood Agents** — email-monitor, pr-watcher, report-builder, file-assistant (signed)
- **report-builder** — rich Markdown digest with tables (displays in Dashboard digest)
- report-builder now reads email-monitor and pr-watcher via IAgentOutputReader (io.reads wired)

**Infrastructure** — infra/marketplace.bicep + infra/identityhub-web.bicep

**CI/CD** — ci.yml (matrix + macOS), release.yml (MSIX + .pkg + notarization + Azure deploy)

**Docs** — GETTING_STARTED, MANIFEST_REFERENCE, PUBLISHING, TROUBLESHOOTING, ROADMAP
- CONTRIBUTING.md — contributor guide with prerequisites, build, test, branching, commit conventions
- docs/AGENT_AUTHORING.md — complete walkthrough from zero to published agent

**Integration Tests** — Purfle.IntegrationTests (11 tests)
- AgentLifecycleTest — load + run + verify output
- SandboxEnforcementTests — fs.write, env.read, network, capability negotiation
- MultiAgentIsolationTest — concurrent agents write to isolated directories
- CrossAgentReadIntegrationTest — reader reads writer output via IAgentOutputReader
- SignatureTamperingTest — tampered manifest fails verification
- TokenUsageAccumulationTest — two runs accumulate in usage.jsonl
- ExpiredManifestTest — expired identity rejected at load time

**README.md** — public-facing project README with architecture, features, quick start, agent model

### What does NOT exist yet (priority order)
1. Register signing public key — one-time local action (see Key Registration above)
2. Azure deployment — needs AZURE_CREDENTIALS secret, then tag push triggers release.yml

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
9. **Engine agnostic — non-negotiable:** Never assume, default, or hardcode any
   specific LLM engine. Always derive the engine from `runtime.engine` in the manifest.
   Never write example manifests, test fixtures, or documentation that imply Anthropic
   is the default. Current developer preference is Gemini.
10. **Git rules — non-negotiable:**
    - Run all git commands from repo root. Never use `cd` before git.
    - Never chain with `&&`. Every command is its own line.
    - Never `git add .` — always stage by explicit file path.
    - Stage then commit as two separate commands.
    - Accumulate all commits locally. `git push` once at the very end.
11. **commit.md workflow:**
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