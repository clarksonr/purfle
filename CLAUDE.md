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
│   │   └── email-monitor.agent.json
│   └── rfcs/
│       └── 0001-identity-model.md
├── runtime/
│   ├── src/
│   │   ├── Purfle.Runtime/
│   │   │   ├── Manifest/        ← ManifestLoader, ManifestValidator ✓
│   │   │   ├── Identity/        ← JWS signing/verification
│   │   │   ├── Sandbox/         ← capability enforcement
│   │   │   ├── Lifecycle/       ← agent load/unload
│   │   │   ├── Scheduling/      ← Scheduler, AgentRunner
│   │   │   └── Adapters/        ← ILlmAdapter, AnthropicAdapter
│   │   └── Purfle.Runtime.OpenClaw/
│   ├── tests/
│   │   └── Purfle.Runtime.Tests/
│   └── Purfle.Runtime.sln
├── app/                         ← .NET MAUI desktop app
│   └── src/
│       ├── src.sln
│       └── Purfle.App/
│           ├── Pages/
│           ├── Controls/
│           ├── ViewModels/
│           └── Services/
├── sdk/
│   ├── packages/
│   │   ├── cli/src/commands/
│   │   └── core/src/
│   ├── package.json
│   └── tsconfig.json
├── marketplace/
└── docs/
    ├── ARCHITECTURE.md
    └── ROADMAP.md
```

---

## Current Status
*Update this section at the end of every session.*

### What exists and works
- Repo is public under MIT license
- Monorepo fully scaffolded
- TypeScript CLI with `simulate` command — runs a single manifest-driven agent
- Working LLM-backed terminal chat agent (`assistant.agent.json`) using Anthropic SDK
- `AGENT_MODEL.md` — architecture guardrails doc
- .NET solution scaffolded (Manifest/Identity/Sandbox/Lifecycle namespaces, xUnit project)
- `spec/schema/agent.manifest.schema.json` — complete, includes schedule block, ES256
- `spec/schema/agent.identity.schema.json` — identity block standalone schema
- `spec/examples/hello-world.agent.json` and `assistant.agent.json` — valid, schema-tested
- `spec/examples/email-monitor.agent.json` — scheduled agent example (interval, 15 min)
- `spec/examples/demo-agent.agent.json` — pre-signed manifest used by `dotnet run` in `Purfle.Runtime.Host`
- `spec/SPEC.md` — human-readable specification
- `spec/rfcs/0001-identity-model.md` — JWS/ES256 identity RFC
- `runtime/.../Manifest/ManifestLoader.cs` — loads and deserializes manifests, tested
- `runtime/.../Manifest/AgentManifest.cs` — includes `ScheduleBlock` record
- `runtime/.../Manifest/EmbeddedSchemas.cs` — includes `scheduleBlock` def
- **`ILlmAdapter`** — `Purfle.Runtime.Adapters.ILlmAdapter` with `CompleteAsync(systemPrompt, userMessage)`
- **`AnthropicAdapter`** — implements `IInferenceAdapter` + `ILlmAdapter`; reads `ANTHROPIC_API_KEY` directly as runtime infrastructure (agents do not need `env.read` for it)
- **`AgentRunner`** — loads `prompts/system.md`, calls `ILlmAdapter.CompleteAsync`, appends to `run.log`
- **`Scheduler`** — drives `AgentRunner` on timer using `schedule.interval_minutes`
- **82 passing tests** (4 live AI tests skip without API keys)
- **`.NET MAUI desktop app`** — builds for Windows and Mac
  - Three tabs: Search (marketplace browser), My Agents (scheduled agent cards), Settings
  - `AgentCard` control — name, status, last/next run, View Log button
  - `AgentCardViewModel` — wraps `AgentRunner`, polls status every 5s
  - `MainViewModel` — `ObservableCollection<AgentCardViewModel>`, `AddAgentCommand`
  - `MauiProgram` — creates `Scheduler`, scans `%LOCALAPPDATA%/aivm/agents`
  - `LogViewPage` — scrollable `run.log` viewer
  - `AgentRunPage` — interactive chat UI backed by `ConversationSession`
  - `SettingsPage` — marketplace URL, engine picker, API key storage, OAuth PKCE login
  - `AgentStore` — local install at `~/.purfle/agents/<id>/`; supports raw manifest and `.purfle` ZIP
  - `AppAdapterFactory` — creates `AnthropicAdapter` or `GeminiAdapter` based on engine preference
  - `AgentExecutorService` — ephemeral P-256 re-signing for local dev trust model
- **Live Azure key registry** — `registry/src/Purfle.KeyRegistry` (three Azure Functions: GET/POST/DELETE `/keys/{id}`)
  - Deployed at `https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net`
  - `HttpKeyRegistryClient` — encodes key IDs with `"/" → "__"` for Azure Table Storage row key compatibility
  - `Purfle.Runtime.Host` wired to live registry; `dotnet run` verifies signatures against it end-to-end
  - Signing key `com.clarksonr/release-2026` registered in Azure Table Storage
  - Private key at `temp-agent/signing.key.pem` — **do not commit**

### What does NOT exist yet (priority order)
1. `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`
2. Agent assembly (`agent.dll`) loading end-to-end — `AssemblyLoadContext` wiring exists but untested with a real DLL
3. Windows Credential Manager integration for API key storage
4. Marketplace backend — scaffolded but not fully wired

---

## Session Rules

1. **Read this file before writing any code.**
2. **Never conflate agent packages with MCP servers.** See AGENT_MODEL.md.
3. **Never add capability or permission handling not declared in the manifest.**
4. **All manifest paths resolve relative to repo root.**
5. **Update the "Current Status" section before ending a session.**
6. **One task per session.** Complete it fully including tests before moving on.
7. **If a decision feels underdetermined, stop and ask rather than invent.**
