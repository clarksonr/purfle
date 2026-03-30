# Purfle — CLAUDE.md

This file provides guidance to Claude Code when working with this repository.
Read this entire file before writing any code.

---

## What Is Purfle

Purfle is a private, in-development platform for AI agent identity and trust.
The name comes from the inlaid border on a violin that prevents cracks from
propagating — the boundary layer that defines and protects.

**Core problem:** There is no standard way to establish, verify, or revoke the
identity of an AI agent. OpenClaw has no trust model. NemoClaw bolted security
on after the fact. Purfle designs it in from the start via a signed manifest
format enforced by the AIVM (AI Virtual Machine).

**The sandbox is a contract, not a cage.** Agents can touch the OS — they can
only touch the parts the manifest explicitly declares. A file summarizer that
declares `filesystem.read: ["C:/Users/**/*.txt"]` genuinely reads those files;
it cannot write, cannot access other paths, and cannot make network calls. These
boundaries are declared upfront, cryptographically signed, and enforced before
the first line of agent code runs. A buyer on the marketplace can read the
manifest and know the exact attack surface before installing anything.

**This is a monorepo** — spec, runtime, SDK, and marketplace all live here.

---

## Mental Model — Read This First

Purfle is modeled on the JVM. The analogy is exact:

| JVM world | Purfle world |
|---|---|
| `.jar` file | agent package (`.purfle` bundle) |
| `SecurityManager` | `AgentSandbox` |
| `ClassLoader` isolation | `AssemblyLoadContext` isolation |
| JVM | AIVM (AI Virtual Machine) |
| bytecode | .NET managed assemblies (`.dll`) |

The AIVM is the enforcer. It loads agent packages, verifies their identity,
constructs an immutable sandbox from the manifest's permissions block, and only
then allows agent code to execute. The agent cannot exceed what its signed
manifest declares — the sandbox enforces this at runtime, not by convention.

**MCP is a tool protocol, not the agent model.** MCP servers are external
processes that provide tools (file access, git, database queries). An agent
may declare bindings to MCP servers in its manifest; the AIVM enforces which
ones it can call. MCP is not how agents are packaged, distributed, or loaded.
Do not conflate agent packages with MCP servers — they are entirely different
things.

See `AGENT_MODEL.md` for the full agent model reference — what an agent is,
what it is not, and where MCP fits.

---

## Agent Package Model

This is the unit of distribution in Purfle. It is what a publisher uploads to
the marketplace and what a user installs and runs locally.

### What an agent package contains

```
my-agent.purfle  (zip-format bundle)
├── agent.manifest.json     ← signed manifest (JWS); defines everything
├── assemblies/
│   ├── MyAgent.dll         ← agent logic (.NET managed assembly)
│   └── MyAgent.deps.json   ← dependency manifest
├── prompts/
│   └── system.md           ← static system prompt injected at invocation
├── tools/
│   └── mcp-bindings.json   ← MCP server bindings (optional)
└── assets/                 ← optional embedded files (images, data, etc.)
```

### What an agent package does NOT contain

- **The LLM** — provided by the runtime via the inference adapter declared in
  `runtime.engine` in the manifest
- **Credentials or API keys** — held by the AIVM in the platform credential
  store (Windows Credential Manager, macOS Keychain, Linux libsecret); agents
  never see raw credentials
- **MCP server implementations** — MCP servers are external processes; the
  agent declares bindings to them, it does not ship them

### Assembly loading and isolation

Agent DLLs are .NET managed assemblies. The `.dll` extension is used on all
platforms — this is a .NET convention, not a Windows-only format. The same
package loads on Windows, macOS, and Linux without modification.

The AIVM loads each agent into a dedicated `AssemblyLoadContext`. This:
- Prevents type conflicts between agents
- Allows clean unload (GC can collect the entire context)
- Is the .NET mechanism that enforces "one agent, one sandbox"

`AssemblyLoadContext` is cross-platform. The package format is OS-agnostic.
Platform differences are confined to the credential store layer only.

### Install → Run flow

```
purfle install <id>
  └─ download package from marketplace
  └─ verify JWS signature against registry public key
  └─ store assemblies in local agent store

AIVM.LoadAsync(manifest)
  └─ parse manifest JSON
  └─ validate against JSON Schema
  └─ verify JWS identity signature + check revocation + check expiry
  └─ capability negotiation (manifest required vs runtime offered → fail if mismatch)
  └─ construct AgentSandbox from permissions block (immutable from this point)
  └─ load assemblies into isolated AssemblyLoadContext
  └─ return LoadResult

adapter.InvokeAsync(systemPrompt, userMessage)
  └─ every OS call routes through AgentSandbox permission checks
  └─ tool-call loop (capped at 10 iterations) for agents with OS permissions
  └─ inference calls go out through the adapter to the LLM endpoint
  └─ on completion, AssemblyLoadContext remains loaded until explicit unload
```

---

## Architecture

Four layers, built bottom-up across four phases:

```
┌──────────────────────────────────────┐
│          MARKETPLACE                 │  phase 4 ← current
├──────────────────────────────────────┤
│          SDK + TOOLING               │  phase 3 — core complete
├──────────────────────────────────────┤
│    IDENTITY + TRUST LAYER            │  phase 2 — core complete
│  signing · audit · revocation        │
├──────────────────────────────────────┤
│       MANIFEST SPEC                  │  phase 1 — complete
│  identity · capabilities · perms     │
├──────────────────────────────────────┤
│    ANY CONFORMING RUNTIME            │  not built by Purfle
│  OpenClaw · AutoGen · CrewAI         │
└──────────────────────────────────────┘
```

See `docs/ARCHITECTURE.md` for full layer descriptions and data flow.

---

## Key Design Decisions

- **Identity first** — JWS (ES256) for v0.1; DID migration path reserved for
  v0.2. See `spec/rfcs/0001-identity-model.md`.
- **Runtime agnostic** — the manifest spec runs on any conforming runtime;
  Purfle does not build the inference engine.
- **Deny by default** — the permissions block is an allowlist; the AIVM denies
  everything not explicitly declared.
- **MCP for tools, not for agents** — MCP is the agent-to-tool protocol.
  The AIVM is not an MCP server. MCP servers are external tool providers.
  The sandbox controls which MCP servers an agent may call.
- **Runtime owns credentials** — OAuth flows run in the AIVM; tokens live in
  the platform credential store; agents only ever receive authenticated results.
- **Open core** — spec + runtime + SDK are open; marketplace is monetized.
- **AssemblyLoadContext isolation** — one context per agent; enforces sandbox
  boundaries at the CLR level; cross-platform (.NET 6+).
- **Cross-platform package format** — agent packages are OS-agnostic zip
  bundles; platform differences are credential store only.
- **No hardware yet** — device attestation is deferred beyond phase 4; do not
  design for it now.

---

## Stack

| Layer | Technology |
|---|---|
| Manifest spec | JSON Schema (Draft 2020-12) |
| Agent identity | JWS — ES256 (ECDSA P-256 / SHA-256) |
| Agent assemblies | .NET managed DLLs (cross-platform) |
| Assembly isolation | `AssemblyLoadContext` (one per agent) |
| Runtime / AIVM | .NET / C# (`runtime/`) |
| SDK / CLI | TypeScript / Node.js, npm workspaces (`sdk/`) |
| Marketplace API | ASP.NET Core + JSON file storage (`marketplace/`) |
| Marketplace Auth | ASP.NET Identity + OpenIddict (OAuth2/OIDC with PKCE) |
| Desktop App | .NET MAUI (`app/`) |
| Web Client | WordPress (external, calls marketplace API) |
| Credential stores | Windows Credential Manager / macOS Keychain / libsecret |
| CI/CD | GitHub Actions (not yet configured) |

---

## Build Commands

### Runtime (.NET)

```bash
# Build (from repo root)
dotnet build Purfle.slnx

# Test all
dotnet test Purfle.slnx

# Run a single test class
dotnet test Purfle.slnx --filter "ClassName=AgentSandboxTests"

# Run the demo host (loads hello-world, demonstrates tampering detection)
dotnet run --project runtime/src/Purfle.Runtime.Host
```

### SDK (TypeScript)

```bash
cd sdk
npm install
npm run build
npm run test

# Work on a single package
cd packages/core && npm run build
cd packages/cli && npm run build
```

### Marketplace API

```bash
dotnet run --project marketplace/src/Purfle.Marketplace.Api
# default: http://localhost:5000
```

### Desktop App (MAUI)

```bash
dotnet build app/src/Purfle.App/Purfle.App.csproj -f net10.0-windows10.0.19041.0
dotnet run --project app/src/Purfle.App/Purfle.App.csproj -f net10.0-windows10.0.19041.0
```

### Seeding the Marketplace

Run these in order. The marketplace API must be running before seeding.

```powershell
# 1. Start the marketplace API (in a separate terminal)
dotnet run --project marketplace/src/Purfle.Marketplace.Api

# 2. Build the SDK (first time only, or after SDK changes)
cd sdk && npm install && npm run build && cd ..

# 3. Sign all sample agent manifests and publish them to the local marketplace
.\seed-marketplace.ps1
# Optional overrides: -Registry http://localhost:5000 -Email "roman@purfle.dev" -Password "Purfle123!"

# 4. Verify the seed worked
.\test-seed.ps1
```

After seeding, agents are visible at `http://localhost:5000/api/agents`.

### CLI commands (`@purfle/cli`)

```bash
purfle init "My Agent"          # scaffold new agent manifest
purfle build                    # validate manifest against JSON Schema
purfle sign --generate-key      # generate P-256 key pair and sign manifest
purfle sign --key-file <path>   # sign with existing private key
purfle login --registry <url>   # OAuth2 PKCE auth with marketplace
purfle publish --register-key signing.pub.pem --registry <url>
purfle search "hello world" --registry <url>
purfle install <agent-id> --registry <url>
purfle simulate                 # run locally with sandbox enforcement
```

---

## Runtime Load Sequence

`AgentLoader.LoadAsync()` runs seven steps in order. Any failure returns a
typed `LoadResult` with a `LoadFailureReason` and stops the sequence.

1. **Parse** — `ManifestLoader.Load()` deserializes JSON into `AgentManifest`
2. **Schema validation** — validates against embedded JSON Schema (Draft 2020-12)
3. **Identity** — `IdentityVerifier.VerifyAsync()` checks JWS ES256 signature,
   revocation via `IKeyRegistry`, and `identity.expires_at`
4. **Capability negotiation** — `CapabilityNegotiator.Negotiate()` compares
   manifest required capabilities against runtime declared set; missing required
   → fail; missing optional → warning
5. **Permission binding** — constructs immutable `AgentSandbox` from
   `permissions` block; no changes after this point
6. **I/O schema compilation** — deferred to invocation layer in v0.1
7. **Initialization** — `AdapterFactory.Create()` instantiates the engine
   adapter; assemblies loaded into dedicated `AssemblyLoadContext`

`AgentSandbox` enforcement: network deny takes precedence over allow; `*`
matches within a path segment, `**` across segments; environment and MCP tool
checks are exact-match.

The identity block is signed over canonical JSON of the entire manifest with
`identity.signature` removed. See `CanonicalJson.cs` and `identity.ts`.

---

## Agent Execution Model

An agent in Purfle v0.1 is **stateless and request/response** at the inference
layer. `LoadAsync()` is a gate; `InvokeAsync()` is a call to the LLM endpoint.

```csharp
var result = await loader.LoadAsync(manifestJson);  // verify + sandbox + load assemblies
var reply  = await result.Adapter.InvokeAsync(      // one or more LLM calls
    systemPrompt: "...",
    userMessage:  "...");
```

The `AnthropicAdapter` runs a tool-call loop when the manifest grants filesystem
or network permissions (capped at 10 iterations). Agents with no OS permissions
take a single-turn path. Multiple agents = multiple `LoadResult` instances, each
with its own immutable `AgentSandbox` and isolated `AssemblyLoadContext`. They
are fully independent and can be invoked concurrently.

MCP wiring: `IMcpClient` / `McpClient` connect to external MCP servers over
stdio JSON-RPC, discover tools, and invoke them. The adapter filters discovered
tools through `sandbox.CanUseMcpTool()` before advertising them to the LLM.

Multi-turn: `ConversationSession` wraps an adapter to accumulate message history
across calls automatically.

Not yet built: streaming, audit logging, init timeout enforcement.

---

## Manifest Spec

- `spec/SPEC.md` — human-readable specification; read this before editing schemas
- `spec/schema/agent.manifest.schema.json` — root JSON Schema
- `spec/schema/agent.identity.schema.json` — identity block schema
- `spec/examples/` — `hello-world`, `voice-assistant`, `trusted-agent`
- `spec/rfcs/` — design decision records

When editing the schema, validate all three example manifests against it.
The examples are the conformance baseline.

---

## Current Status

**Phase 1 (Spec) — Complete.**
`spec/SPEC.md`, both JSON schemas, RFC 0001, and three example manifests written
and stable.

**Phase 2 (AIVM Runtime) — Core complete.**
Full seven-step load sequence implemented. 52 tests pass. `Purfle.Runtime.Host`
is a runnable demo. `HttpKeyRegistryClient` implements `IKeyRegistry` against
the marketplace API. Anthropic adapter functional (requires `ANTHROPIC_API_KEY`
in `permissions.environment.allow`). Tool-call loop, MCP wiring, and multi-turn
conversation implemented. OpenClaw and Ollama adapters are stubs.
Not yet built: audit logging, lifecycle init timeout enforcement.

**Phase 3 (SDK + Tooling) — Core complete.**
`@purfle/core`: TypeScript types, ES256 signing/verification, canonical JSON,
manifest validation. `@purfle/cli`: all commands working. 11/13 tests pass;
2 `purfle publish` tests failing.

**Phase 4 (Marketplace) — In progress.**
`Purfle.Marketplace.Api`: full REST API (key registry, agent registry, OAuth2
via OpenIddict). JSON file storage backend. `Purfle.App` (MAUI): desktop app
builds for Windows.
Not yet built: WordPress site, publisher verification, usage billing.

---

## Developer Context

- **Developer:** Roman Noble (GitHub: clarksonr) — 40+ years software
  development, luthier by craft
- **IDEs:** VS Code + Visual Studio
- **Style:** Skeptical of over-engineering. No abstractions for hypothetical
  requirements. Start focused.
- **AI tooling:** Claude Code for scaffolding; GitHub Copilot for daily work.
  Start each session by reading this file. End each session by updating
  Current Status above.
