# Purfle — CLAUDE.md
*Auto-loaded by Claude Code at session start. Read this before doing anything.*
*Updated at end of each session. Treat stale status as a bug.*

---

## Read This First — Mental Model

Purfle is an **AI Virtual Machine (AIVM)**. It is a sandboxed host process that:
1. Loads a signed agent package (manifest + .NET DLLs)
2. Enforces the manifest's declared capabilities and permissions
3. Provides LLM inference via adapters (Anthropic first, others stubbed)
4. Exposes tools to the LLM via MCP (Model Context Protocol)
5. Executes tool calls on behalf of the agent — the LLM never touches the system directly

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
| Agent holds API keys | Runtime holds credentials (Windows Credential Manager) |
| Agent calls tools directly | AIVM validates capability, then calls tool |

**MCP is plumbing inside the AIVM. It is not the packaging model.**

---

## Architecture

```
┌──────────────────────────────────────┐
│          MARKETPLACE                 │  phase 4 — monetized
├──────────────────────────────────────┤
│          SDK + TOOLING               │  phase 3 — TypeScript/Node
├──────────────────────────────────────┤
│    IDENTITY + TRUST LAYER            │  ← THE KERNEL
│  signing · audit · revocation        │
├──────────────────────────────────────┤
│       MANIFEST SPEC                  │  phase 1 — build this first
│  identity · capabilities · perms     │
├──────────────────────────────────────┤
│    RUNTIME (AIVM)                    │  phase 2 — .NET / C# / Windows
│  ManifestLoader · Sandbox · LLM      │
└──────────────────────────────────────┘
```

---

## Locked Decisions — Do Not Revisit

- **Identity:** JWS with ES256 (ECDSA). DID migration path later. Do not abstract for DID today.
- **Algorithm:** ES256 — smaller keys, faster verification than RS256. Locked.
- **Packaging:** .NET DLL assemblies in `AssemblyLoadContext`. Not MCP servers. Not plugins. DLLs.
- **Credentials:** Owned by runtime (Windows Credential Manager phase 1). Agent never sees tokens.
- **Capability model:** Model A — capabilities are the enforcement list; permissions are per-capability config.
  - You CANNOT have a permissions entry without a matching capability.
  - You CAN have a capability without a permissions entry (no config needed).
- **MCP role:** Tool protocol only. Agent declares MCP tool bindings in manifest; AIVM wires them at load time.
- **`io` block:** Present in schema as optional, no enforcement in phase 1. Deferred to marketplace phase.
- **Phase 1 target:** Windows / .NET only. No mobile, no edge, no cross-platform yet.
- **Open core:** spec + runtime + SDK open source. Marketplace monetized.
- **No over-engineering:** No abstractions for hypothetical requirements.

---

## Manifest Structure — Canonical Reference

This is the authoritative shape of a Purfle agent manifest. The JSON Schema
in `spec/schema/agent.manifest.schema.json` is the machine-readable version of this.

```json
{
  "purfle": "0.1",
  "id": "<uuid>",
  "name": "<display name>",
  "version": "<semver>",
  "description": "<string>",

  "identity": {
    "author": "<string — reverse-domain or username>",
    "email": "<string>",
    "key_id": "<string>",
    "algorithm": "ES256",
    "issued_at": "<ISO 8601>",
    "expires_at": "<ISO 8601>",
    "signature": "<JWS compact serialization — omit at authoring time>"
  },

  "capabilities": ["llm.chat", "network.outbound"],

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

### Capability strings (exhaustive for phase 1)
| Capability | Permission config keys | Meaning |
|---|---|---|
| `llm.chat` | none | May use conversational LLM inference |
| `llm.completion` | none | May use single-turn LLM completion |
| `network.outbound` | `hosts: string[]` | May make outbound HTTP calls to listed hosts |
| `env.read` | `vars: string[]` | May read listed environment variables |
| `fs.read` | `paths: string[]` | May read from listed paths |
| `fs.write` | `paths: string[]` | May write to listed paths |
| `mcp.tool` | none | May invoke MCP tool bindings declared in `tools` |

---

## Stack

| Layer | Technology |
|---|---|
| Manifest spec | JSON Schema |
| Agent identity | JWS / ES256 |
| Runtime host | .NET 8 / C# / Windows |
| Inference adapter | Anthropic SDK (primary); OpenClaw, Ollama stubbed |
| SDK / CLI | TypeScript / Node.js (npm workspaces) |
| Tests | xUnit (.NET), Jest (TypeScript) |

---

## Repo Layout

```
purfle/
├── CLAUDE.md                          ← you are here — keep this current
├── AGENT_MODEL.md                     ← architecture guardrails, read if confused about MCP
├── README.md
├── spec/
│   ├── SPEC.md
│   ├── schema/
│   │   ├── agent.manifest.schema.json
│   │   └── agent.identity.schema.json
│   ├── examples/
│   │   ├── hello-world.agent.json
│   │   └── assistant.agent.json       ← working POC manifest
│   └── rfcs/
│       └── 0001-identity-model.md
├── runtime/
│   ├── src/
│   │   ├── Purfle.Runtime/
│   │   │   ├── Manifest/
│   │   │   ├── Identity/
│   │   │   ├── Sandbox/
│   │   │   └── Lifecycle/
│   │   └── Purfle.Runtime.OpenClaw/
│   ├── tests/
│   │   └── Purfle.Runtime.Tests/
│   └── Purfle.Runtime.sln
├── sdk/
│   ├── packages/
│   │   ├── cli/src/commands/
│   │   │   └── simulate.ts            ← working
│   │   └── core/src/
│   ├── package.json
│   └── tsconfig.json
├── marketplace/
└── docs/
```

---

## Current Status
*Update this section at the end of every session.*

### What exists and works
- Monorepo scaffolded
- TypeScript CLI with `simulate` command — runs a manifest-driven agent
- Working LLM-backed terminal chat agent (`assistant.agent.json`) using Anthropic SDK
- `AGENT_MODEL.md` — architecture guardrails doc
- .NET solution scaffolded (Manifest/Identity/Sandbox/Lifecycle namespaces, xUnit project)
- `spec/schema/agent.manifest.schema.json` — formal JSON Schema (phase 1 capability strings, all constraint rules)
- `spec/schema/agent.identity.schema.json` — identity block schema
- `spec/rfcs/0001-identity-model.md` — JWS/ES256 identity RFC (accepted)
- `spec/SPEC.md` — human-readable manifest specification v0.1

### What does NOT exist yet (priority order)
1. `runtime/.../Manifest/ManifestLoader.cs` — loads + deserializes manifest
2. `runtime/.../Manifest/ManifestValidator.cs` — semantic validation
3. Integration tests in `Purfle.Runtime.Tests`
4. `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`

---

## Session Rules

1. **Read this file before writing any code.**
2. **Never conflate agent packages with MCP servers.** See AGENT_MODEL.md.
3. **Never add capability or permission handling not declared in the manifest.**
4. **All manifest paths resolve relative to repo root.**
5. **Update the "Current Status" section before ending a session.**
6. **One task per session.** Complete it fully including tests before moving on.
7. **If a decision feels underdetermined, stop and ask rather than invent.**
