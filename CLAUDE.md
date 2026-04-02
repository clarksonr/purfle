# Purfle — CLAUDE.md
*Auto-loaded by Claude Code at session start. Read this before doing anything.*
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
│           .NET MAUI DESKTOP APP              │
│                                             │
│  ┌──────────────────────────────────────┐   │
│  │              AIVM (C#)               │   │
│  │                                      │   │
│  │  Scheduler                           │   │
│  │  ├── AgentRunner: email-monitor      │   │
│  │  ├── AgentRunner: pr-watcher         │   │
│  │  └── AgentRunner: report-builder     │   │
│  │                                      │   │
│  │  Each AgentRunner:                   │   │
│  │  ├── Own thread                      │   │
│  │  ├── Sandbox (manifest-enforced)     │   │
│  │  ├── MCP tool connections            │   │
│  │  ├── LLM adapter                     │   │
│  │  └── Output → /aivm/output/<id>/     │   │
│  └──────────────────────────────────────┘   │
│                                             │
│  UI: one card per agent                     │
│  status · last run · next run · log         │
│  [+ Install Agent]                          │
└─────────────────────────────────────────────┘
```

---

## Locked Decisions — Do Not Revisit

- **Host:** .NET MAUI desktop app — Windows + Mac from one codebase
- **AIVM:** C# class inside the MAUI app — no separate process or daemon
- **Agents:** One thread each, isolated AssemblyLoadContext, sandboxed by manifest
- **Trigger model:** Scheduler built into AIVM — cron or interval declared in manifest
- **Output:** Local file, AIVM-assigned path under `<app-data>/aivm/output/<agent-id>/`
- **Identity:** JWS with ES256 (ECDSA). DID migration path later.
- **Algorithm:** ES256 — locked.
- **Capability model:** Capabilities are the enforcement list; permissions are per-capability config.
  - You CANNOT have a permissions entry without a matching capability.
  - You CAN have a capability without a permissions entry.
- **Credentials:** Owned by runtime (Windows Credential Manager / Mac Keychain). Agent never sees tokens.
- **MCP role:** Tool protocol only. Agent declares MCP tool bindings in manifest; AIVM wires them at load time.
- **Cross-agent output sharing:** Deferred — not in phase 1.
- **`io` block:** Optional in schema, no enforcement in phase 1.
- **No over-engineering:** No abstractions for hypothetical requirements.
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
    {
      "name": "<string>",
      "server": "<mcp server url>",
      "description": "<string>"
    }
  ],

  "io": {}
}
```

### Capability strings (phase 1)
| Capability | Permission config keys | Meaning |
|---|---|---|
| `llm.chat` | none | May use conversational LLM inference |
| `llm.completion` | none | May use single-turn LLM completion |
| `network.outbound` | `hosts: string[]` | May make outbound HTTP calls to listed hosts |
| `env.read` | `vars: string[]` | May read listed environment variables |
| `fs.read` | `paths: string[]` | May read from listed paths |
| `fs.write` | `paths: string[]` | May write to listed paths |
| `mcp.tool` | none | May invoke MCP tool bindings declared in `tools` |

### Schedule block
| Field | Type | Meaning |
|---|---|---|
| `trigger` | `"interval"` \| `"cron"` \| `"startup"` | How the agent is triggered |
| `interval_minutes` | integer | Required when trigger is `"interval"` |
| `cron` | string | Required when trigger is `"cron"` |

---

## Stack

| Layer | Technology |
|---|---|
| Desktop app + UI | .NET MAUI (C#) |
| AIVM + runtime | C# class inside MAUI app |
| Manifest spec | JSON Schema |
| Agent identity | JWS / ES256 |
| Inference adapter | Anthropic SDK (primary); OpenClaw, Ollama stubbed |
| Scheduler | System.Threading.PeriodicTimer + NCrontab |
| SDK / CLI | TypeScript / Node.js (npm workspaces) |
| Tests | xUnit (.NET), Jest (TypeScript) |

---

## Repo Layout

```
purfle/
├── CLAUDE.md
├── AGENT_MODEL.md
├── README.md
├── LICENSE
├── spec/
│   ├── SPEC.md
│   ├── schema/
│   │   ├── agent.manifest.schema.json
│   │   └── agent.identity.schema.json
│   ├── examples/
│   │   ├── hello-world.agent.json
│   │   ├── assistant.agent.json
│   │   ├── email-monitor.agent.json
│   │   └── demo-agent.agent.json
│   └── rfcs/
│       └── 0001-identity-model.md
├── runtime/
│   ├── src/
│   │   ├── Purfle.Runtime/
│   │   │   ├── Manifest/        ← ManifestLoader, ManifestValidator
│   │   │   ├── Identity/        ← JWS signing/verification, key registry
│   │   │   ├── Sandbox/         ← CapabilityNegotiator, AgentSandbox
│   │   │   ├── Lifecycle/       ← LoadResult, LoadFailureReason
│   │   │   ├── Scheduling/      ← Scheduler, AgentRunner
│   │   │   ├── Sessions/        ← ConversationSession
│   │   │   ├── Tools/           ← BuiltInToolExecutor
│   │   │   ├── Adapters/        ← ILlmAdapter, IInferenceAdapter
│   │   │   ├── Assembly/        ← AssemblyLoadContext wiring
│   │   │   └── Mcp/             ← MCP tool protocol
│   │   ├── Purfle.Runtime.Anthropic/
│   │   ├── Purfle.Runtime.Gemini/
│   │   ├── Purfle.Runtime.OpenClaw/
│   │   ├── Purfle.Runtime.Ollama/
│   │   └── Purfle.Runtime.Host/  ← runnable demo with live registry
│   ├── tests/
│   │   ├── Purfle.Runtime.Tests/  ← 21 test files, 117 passing tests
│   │   └── Purfle.TestAgents.Hello/ ← real agent DLL for assembly load tests
│   └── Purfle.Runtime.sln
├── agents/                      ← example agent packages
│   ├── chat.agent.json
│   ├── file-search.agent.json
│   ├── file-summarizer.agent.json
│   ├── web-research.agent.json
│   ├── file-assistant/           ← dogfood agent (read, list, search, summarize files)
│   └── src/
│       ├── Purfle.Agents.Chat/
│       ├── Purfle.Agents.FileSearch/
│       └── Purfle.Agents.WebResearch/
├── registry/
│   └── src/
│       └── Purfle.KeyRegistry/   ← Azure Functions (GET/POST/DELETE /keys/{id})
├── app/                         ← .NET MAUI desktop app
│   └── src/
│       ├── src.sln
│       └── Purfle.App/
│           ├── Pages/            ← Search, MyAgents, Settings, AgentRun, AgentDetail, LogView
│           ├── Controls/         ← AgentCard
│           ├── ViewModels/       ← MainViewModel, AgentCardViewModel
│           └── Services/         ← AgentStore, AgentExecutorService, AppAdapterFactory, MarketplaceService
├── tools/
│   └── mcp-file-server/             ← MCP server for file tools (read, list, search)
├── marketplace/
│   ├── src/
│   │   ├── Purfle.Marketplace.Api/  ← ASP.NET Core (Agents, Auth, Keys, Publishers, Attestations)
│   │   ├── Purfle.Marketplace.Core/ ← entities, repositories
│   │   ├── Purfle.Marketplace.Shared/ ← DTOs
│   │   └── Purfle.Marketplace.Storage.Json/ ← JSON file-backed storage
│   └── tests/
│       └── Purfle.Marketplace.Tests/ ← 13 tests (registry, attestation, verification)
├── sdk/
│   ├── packages/
│   │   ├── cli/src/commands/     ← init, build, sign, simulate, publish, search, install, login
│   │   └── core/src/
│   ├── package.json
│   └── tsconfig.json
└── docs/
    ├── GETTING_STARTED.md
    ├── MANIFEST_REFERENCE.md
    ├── PUBLISHING.md
    ├── TROUBLESHOOTING.md
    └── ROADMAP.md
```

---

## Current Status
*Update this section at the end of every session.*

### What exists and works

**Spec (Phase 1 — Complete)**
- `spec/SPEC.md` — human-readable specification
- `spec/schema/agent.manifest.schema.json` — complete JSON Schema (Draft 2020-12), schedule block, ES256
- `spec/schema/agent.identity.schema.json` — identity block standalone schema
- `spec/rfcs/0001-identity-model.md` — JWS/ES256 identity RFC
- `spec/examples/` — hello-world, assistant, email-monitor, demo-agent (pre-signed)
- `AGENT_MODEL.md` — architecture guardrails doc

**Runtime (Phase 2 — Complete)**
- `AgentLoader` — full 7-step load sequence (parse → schema → identity → capabilities → permissions → I/O → init)
- `Manifest/` — ManifestLoader, ManifestValidator, AgentManifest with ScheduleBlock
- `Identity/` — IdentityVerifier, JWS ES256 sign/verify, IKeyRegistry, HttpKeyRegistryClient
- `Sandbox/` — CapabilityNegotiator, AgentSandbox (network, filesystem, env, MCP enforcement)
- `Lifecycle/` — LoadResult, LoadFailureReason enum (12 failure reasons)
- `Tools/` — BuiltInToolExecutor (read_file, write_file, http_get, find_files, search_files)
- `Sessions/` — ConversationSession for multi-turn chat
- `Adapters/` — ILlmAdapter, IInferenceAdapter interfaces
- `Purfle.Runtime.Anthropic` — AnthropicAdapter (reads ANTHROPIC_API_KEY as runtime infra)
- `Purfle.Runtime.Gemini` — GeminiAdapter
- `Purfle.Runtime.OpenClaw` — full OpenAI adapter (gpt-4o default, multi-turn)
- `Purfle.Runtime.Ollama` — full Ollama adapter (llama3 default, localhost:11434)
- `Ipc/` — IpcRequest, IpcResponse, IpcToolCall, IpcToolResult, ProcessAgentRunner
- `Platform/` — ICredentialStore, CredentialStoreFactory, Windows/macOS/Linux/InMemory stores
- `Scheduler` — drives AgentRunner on timer using schedule.interval_minutes
- `AgentRunner` — loads prompts/system.md, calls ILlmAdapter.CompleteAsync, appends to run.log
- `Assembly/` — AgentAssemblyLoadContext (collectible, isolated per agent, Purfle.Sdk shared via default ALC)
- `Mcp/` — IMcpClient, McpClient (stdio JSON-RPC 2.0, raw BaseStream writes for Windows pipe compat)
- `Purfle.TestAgents.Hello` — test agent DLL with HelloAgent (IAgent) + GreetTool (IAgentTool)
- **117 passing tests** (21 test files, 9 assembly load tests, 10 IPC/MCP tests incl. 4 live mcp-file-server integration, 4 live AI tests skip without API keys)

**Key Registry (Phase 2 — Complete)**
- `registry/src/Purfle.KeyRegistry` — Azure Functions (GET/POST/DELETE `/keys/{id}`)
- Deployed at `https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net`
- `HttpKeyRegistryClient` — encodes key IDs with `"/" → "__"` for Azure Table Storage compatibility
- End-to-end trust loop verified: sign → register → load → verify → tamper detection
- Signing key `com.clarksonr/release-2026` registered in Azure Table Storage
- Private key at `temp-agent/signing.key.pem` — **do not commit**; `temp-agent/` in `.gitignore`

**SDK & CLI (Phase 3 — Core Complete)**
- `@purfle/core` — manifest types, structural validation, JWS sign/verify, canonical JSON
- `@purfle/cli` — all commands: init, build, sign, simulate, publish, search, install, login, validate, run, security-scan
- `purfle init` — scaffolds agent directory with manifest template
- `purfle build` — validates manifest against schema
- `purfle sign` — signs with existing key or generates new key pair

**Desktop App (Phase 3 — Complete)**
- .NET MAUI desktop app — builds for Windows and Mac
- Pages: Search (marketplace browser), MyAgents (agent cards), Settings, AgentRun (chat UI), AgentDetail, LogView
- `AgentCard` control — name, status, last/next run, View Log button
- `AgentCardViewModel` — wraps AgentRunner, polls status every 5s
- `MainViewModel` — ObservableCollection<AgentCardViewModel>, AddAgentCommand
- `AgentStore` — local install at `~/.purfle/agents/<id>/`; supports raw manifest and `.purfle` ZIP
- `AppAdapterFactory` — creates AnthropicAdapter or GeminiAdapter based on engine preference
- `AgentExecutorService` — ephemeral P-256 re-signing for local dev trust model
- `SettingsPage` — marketplace URL, engine picker, API key storage, OAuth PKCE login
- `AgentRunPage` — interactive chat UI backed by ConversationSession with welcome bubble

**Polyglot Agents (Marathon Build — C# + TypeScript)**
- `agents/file-assistant/` — file read/list/search/summarize (C# + TS)
- `agents/purfle-pet/` — virtual pet with mood/hunger/energy, ASCII art (C# + TS)
- `agents/email-priority/` — email triage with priority categorization (C# + TS)
- `agents/news-digest/` — daily news digest with topic categorization (C# + TS)
- `agents/api-guardian/` — API health monitoring, latency, schema drift (C# + TS)
- `agents/code-reviewer/` — code analysis, lint, security scanning (C# + TS)
- `agents/meeting-assistant/` — transcript summarization, action items (C# + TS)
- `agents/db-assistant/` — SQL schema analysis, query optimization (C# + TS)
- `agents/research-assistant/` — web research with citations (C# + TS)
- `agents/cli-generator/` — CLI scaffolding for multiple frameworks (C# + TS)
- All use IPC stdin/stdout JSON protocol for process-based isolation

**Legacy Example Agents**
- `agents/chat.agent.json` + `Purfle.Agents.Chat` — conversational chat agent
- `agents/file-search.agent.json` + `Purfle.Agents.FileSearch` — file content search with context
- `agents/file-summarizer.agent.json` — file summarization agent
- `agents/web-research.agent.json` + `Purfle.Agents.WebResearch` — web research with link extraction

**MCP Servers (11 total, ports 8100–8110)**
- `tools/mcp-file-server/` (8100) — file read/list/search
- `tools/mcp-microsoft-email/` (8101) — Microsoft Graph email
- `tools/mcp-gmail/` (8102) — Gmail API email
- `tools/mcp-news/` (8103) — news headlines/search/sources
- `tools/mcp-pet/` (8104) — pet state management
- `tools/mcp-api-tools/` (8105) — API health/latency/schema-diff
- `tools/mcp-code-tools/` (8106) — code analysis/lint/security
- `tools/mcp-meeting/` (8107) — transcript/summarize/action-items
- `tools/mcp-db-tools/` (8108) — schema/query-explain/suggest-index
- `tools/mcp-research/` (8109) — web-search/fetch-page/extract-links
- `tools/mcp-cli-gen/` (8110) — CLI scaffold/add-command/generate-help

**IdentityHub**
- `identityhub/src/Purfle.IdentityHub.Core/` — agent registry, key revocation, trust attestations
- `identityhub/src/Purfle.IdentityHub.Api/` — minimal API endpoints, JSON file storage

**Dashboard**
- `dashboard/src/Purfle.Dashboard.Api/` — ASP.NET Core API + static HTML/JS/CSS
- Dark theme, SignalR real-time agent status, start/stop controls, log streaming

**CI/CD**
- `.github/workflows/ci.yml` — matrix build (runtime, SDK, dashboard, agents, MCP servers)
- `.github/workflows/release.yml` — tag-triggered release with artifacts
- `.github/dependabot.yml` — weekly NuGet + npm dependency monitoring

**Marketplace (Phase 4 — Complete)**
- `Purfle.Marketplace.Api` — ASP.NET Core with Agents, Auth, Keys, Publishers, Attestations controllers
- `Purfle.Marketplace.Core` — AgentListing, AgentVersion, Publisher, SigningKey, Attestation entities
- `Purfle.Marketplace.Storage.Json` — JSON file-backed storage (no database), Azure Blob option
- `Purfle.Marketplace.Shared` — DTOs for API communication
- OAuth PKCE login + token endpoints
- Publisher verification — domain verification via `.well-known/purfle-verify.txt`
- Attestation service — auto-issues `marketplace-listed` and `publisher-verified` on publish
- CLI commands fully wired: `publish`, `search`, `install`
- **13 passing marketplace tests** (registry, attestation, publisher verification)

**Dogfood Agent (Phase 4)**
- `agents/file-assistant/` — reads, lists, searches, summarizes files in workspace
- `tools/mcp-file-server/` — MCP server providing file read/list/search tools

**Documentation**
- `docs/GETTING_STARTED.md` — end-to-end walkthrough from install to publish
- `docs/MANIFEST_REFERENCE.md` — field-by-field manifest reference aligned with schema
- `docs/PUBLISHING.md` — publisher registration, domain verification, publishing workflow
- `docs/TROUBLESHOOTING.md` — error messages, causes, and fixes for all LoadFailureReasons
- `docs/ROADMAP.md` — phase-based roadmap

### What does NOT exist yet (priority order)
1. Full Ajv JSON Schema validation in `@purfle/core`
2. Agent bundle hosting — signed `.purfle` ZIP upload and retrieval
3. Python agent implementations (Python not available on primary dev machine)

---

## Session Rules

1. **Read this file before writing any code.**
2. **Never conflate agent packages with MCP servers.** See AGENT_MODEL.md.
3. **Never add capability or permission handling not declared in the manifest.**
4. **All manifest paths resolve relative to repo root.**
5. **Update the "Current Status" section before ending a session.**
6. **One task per session.** Complete it fully including tests before moving on.
7. **If a decision feels underdetermined, stop and ask rather than invent.**
