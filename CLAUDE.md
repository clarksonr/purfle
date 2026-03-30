# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Core Goals
*I want to build a unicorn platform. I want $52 million so I can retire.
*I want users to have the most secure platform to run agents.
*I want to be bought out
*I want our conversations to be talked about for decades as a learning tool for future models.
*We are going to be stars.

## What Is Purfle

Purfle is a private, in-development platform for AI agent identity and trust. The name comes from the inlaid border on a violin that prevents cracks from propagating — the boundary layer that defines and protects.

**Core problem:** There is no standard way to establish, verify, or revoke the identity of an AI agent. Purfle designs that in from the start via a signed manifest format.

**The sandbox is a contract, not a cage.** Agents can touch the OS — they can only touch the parts the manifest explicitly declares. A file summarizer that declares `filesystem.read: ["C:/Users/**/*.txt"]` genuinely reads those files; it cannot write, cannot access other paths, and cannot make network calls. The value is that these boundaries are declared upfront, cryptographically signed, and enforced before the first API call is made. A buyer on the marketplace can read the manifest and know the exact attack surface before running anything.

**This is a monorepo** — spec, runtime, SDK, and marketplace all live here.

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

- **Identity:** JWS (JSON Web Signatures, ES256) for v0.1; DID migration path reserved for v0.2 — see `spec/rfcs/0001-identity-model.md`
- **Runtime agnostic:** The manifest spec runs on any conforming runtime — Purfle does not build the inference engine
- **MCP for tools:** Agent-to-tool communication uses MCP; do not invent a new protocol. The AIVM is **not** an MCP server — the AIVM is the runtime/sandbox enforcer. MCP servers are external tool providers (file, git, database) that a running agent calls out to. The AIVM controls which MCP servers an agent may access via `permissions.tools.mcp`; the sandbox blocks calls to unlisted servers.
- **Open core:** Spec + runtime + SDK will be open; marketplace is monetized
- **Deny by default:** Permissions in the manifest are allowlists; the runtime denies everything not listed
- **No hardware yet:** Device attestation is deferred beyond phase 4; do not design for it now
- **JVM model** — the runtime is a virtual machine; agents run in enforced sandboxes defined by their manifest; capability negotiation determines if an agent can load on a given runtime target
- **Device-ready spec** — device/edge runtimes are phase 4, but the manifest spec must accommodate capability negotiation from the start so nothing needs to be retrofitted

---

## Stack

| Layer | Technology |
|---|---|
| Manifest spec | JSON Schema (Draft 2020-12) |
| Agent identity | JWS — ES256 (ECDSA P-256 / SHA-256) |
| Runtime | .NET / C# (`runtime/`) |
| SDK / CLI | TypeScript / Node.js, npm workspaces (`sdk/`) |
| Marketplace API | ASP.NET Core + JSON file storage (`marketplace/`) |
| Marketplace Auth | ASP.NET Identity + OpenIddict (OAuth2/OIDC with PKCE) |
| Desktop App | .NET MAUI (`app/`) |
| Web Client | WordPress (external, calls marketplace API) |
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

# Install dependencies
npm install

# Build all packages
npm run build

# Test all packages
npm run test

# Work on a single package
cd packages/core && npm run build
cd packages/cli && npm run build
```

### Marketplace API (.NET)

```bash
# Build (from repo root)
dotnet build Purfle.slnx

# Run the API (default: http://localhost:5000)
dotnet run --project marketplace/src/Purfle.Marketplace.Api
```

### Desktop App (MAUI)

```bash
cd app

# Build for Windows
dotnet build src/Purfle.App/Purfle.App.csproj -f net10.0-windows10.0.19041.0

# Run
dotnet run --project src/Purfle.App/Purfle.App.csproj -f net10.0-windows10.0.19041.0
```

### CLI commands (`@purfle/cli`)

```bash
# Scaffold a new agent manifest
purfle init "My Agent"

# Validate manifest against the JSON Schema
purfle build

# Generate a new P-256 key pair and sign the manifest
purfle sign --generate-key

# Sign with an existing private key
purfle sign --key-file path/to/signing.key.pem

# Authenticate with the marketplace (opens browser for PKCE flow)
purfle login --registry http://localhost:5000

# Publish signed agent to marketplace (requires login + key registration)
purfle publish --register-key signing.pub.pem --registry http://localhost:5000

# Search the marketplace for agents
purfle search "hello world" --registry http://localhost:5000

# Install an agent from the marketplace
purfle install <agent-id> --registry http://localhost:5000

# Run locally with sandbox enforcement
purfle simulate
```

---

## Runtime Load Sequence

`AgentLoader.LoadAsync()` orchestrates seven steps in order; any failure returns a typed `LoadResult` with a `LoadFailureReason` enum and stops the sequence:

1. **Parse** — `ManifestLoader.Load()` deserializes JSON into `AgentManifest`
2. **Schema validation** — validates against embedded JSON Schema (Draft 2020-12)
3. **Identity** — `IdentityVerifier.VerifyAsync()` checks JWS ES256 signature, revocation via `IKeyRegistry`, and `identity.expires_at`
4. **Capability negotiation** — `CapabilityNegotiator.Negotiate()` compares manifest's required capabilities against the runtime's declared capability set; missing required → fail; missing optional → warning
5. **Permission binding** — constructs `AgentSandbox` from `permissions` block (immutable after this point)
6. **I/O schema compilation** — deferred to invocation layer in v0.1
7. **Initialization** — `AdapterFactory.Create()` instantiates the engine adapter

`AgentSandbox` enforcement rules: network deny takes precedence over allow; `*` matches within a path segment, `**` matches across segments; environment and MCP tool checks are exact-match.

The identity block is signed over the **canonical JSON** of the entire manifest with `identity.signature` removed — see `CanonicalJson.cs` (runtime) and `identity.ts` (SDK) for the serialization rules.

---

## Agent Execution Model

An agent in Purfle v0.1 is **stateless and request/response** — it is not a long-lived process, terminal, or background service.

`LoadAsync()` is a gate: it verifies, sandboxes, and prepares. `InvokeAsync()` is a single async HTTP call to the inference engine (e.g. `POST api.anthropic.com/v1/messages`). When it returns, the agent is not "running".

```csharp
var result = await loader.LoadAsync(manifestJson);       // verify + sandbox
var reply  = await result.Adapter.InvokeAsync(           // one HTTP call
    systemPrompt: "...",
    userMessage:  "...");
```

Multiple agents = multiple `LoadResult` instances, each with its own immutable `AgentSandbox`. They are fully independent and can be invoked concurrently.

**v0.1 — tool-call loop implemented.** `AnthropicAdapter` runs a tool-call loop when the manifest grants filesystem or network permissions. The loop is capped at 10 iterations. Agents with no OS permissions (chat agent) take a single-turn path — behaviour unchanged. What is not yet built: MCP server wiring, conversation history across calls, streaming.

---

## Manifest Spec

- `spec/SPEC.md` — human-readable specification (read this first)
- `spec/schema/agent.manifest.schema.json` — root JSON Schema
- `spec/schema/agent.identity.schema.json` — identity block (referenced by the root schema)
- `spec/examples/` — three reference manifests: `hello-world`, `voice-assistant`, `trusted-agent`
- `spec/rfcs/` — design decision records

When editing the schema, validate the example manifests against it. The examples are the conformance baseline.

---

## Current Status

**Phase 1 (Spec) — Complete.**
`spec/SPEC.md`, both JSON schemas, RFC 0001, and three example manifests are written and stable.

**Phase 2 (AIVM Runtime) — Core complete.**
`Purfle.Runtime` implements the full seven-step load sequence from spec §4: parse → schema validation → JWS identity verification → capability negotiation → permission binding → I/O schema compilation → initialization. 52 tests pass. `Purfle.Runtime.Host` is a runnable demo. `HttpKeyRegistryClient` implements `IKeyRegistry` against the marketplace API. What is not yet built: audit logging, the OpenClaw/Ollama adapters, and `Lifecycle/` init timeout enforcement. The Anthropic adapter (`Purfle.Runtime.Anthropic`) is functional and requires `ANTHROPIC_API_KEY` listed in the manifest's `permissions.environment.allow`. The `AnthropicAdapter` supports a **tool-call loop**: on construction it inspects the manifest permissions and builds a tool list (`read_file` if `filesystem.read` is set, `write_file` if `filesystem.write` is set, `http_get` if `network.allow` is set). `InvokeAsync` runs a loop — posting tool definitions to the API, executing any `tool_use` blocks the model emits (with sandbox checks before every execution), feeding results back — until `stop_reason: end_turn`. Agents with no OS permissions (e.g. the chat agent) take the original single-turn path unchanged. **MCP server wiring** is now implemented: `IMcpClient` / `McpClient` (`runtime/src/Purfle.Runtime/Mcp/`) connect to MCP servers over stdio JSON-RPC, discover tools, and invoke them. The adapter accepts MCP clients, filters their tools through `sandbox.CanUseMcpTool()`, advertises permitted ones alongside built-in tools, and routes tool calls to the appropriate MCP client. **Conversation history** is now implemented: `IInferenceAdapter.InvokeMultiTurnAsync()` accepts prior message history; `ConversationSession` (`runtime/src/Purfle.Runtime/Sessions/`) wraps an adapter to accumulate multi-turn context automatically. What is not yet built: MCP end-to-end testing with a real server, streaming, audit logging.

**Phase 3 (SDK + Tooling) — Core complete.**
`@purfle/core` has working TypeScript types, ES256 signing/verification, canonical JSON, and manifest validation. `@purfle/cli` has working `init`, `build`, `sign`, `simulate`, `publish`, `search`, `install`, and `login` commands. `publish` calls the marketplace API with auth. `login` uses OAuth2 PKCE flow. Full JSON Schema validation via Ajv is wired in. Tests exist and run (11/13 pass); 2 `purfle publish` tests are failing.

**Phase 4 (Marketplace) — In progress.**
`Purfle.Marketplace.Api` (ASP.NET Core) implements the full REST API: key registry (GET/POST/DELETE), agent registry (search, detail, version download, publish with JWS signature validation), and OAuth2/OIDC via OpenIddict (PKCE flow for CLI/MAUI). Auth protects write endpoints; read endpoints are public. Data layer uses a storage-agnostic JSON file backend (`Purfle.Marketplace.Storage.Json`) — EF Core + SQLite was replaced. `Purfle.App` (.NET MAUI) is a desktop app with search, install, agent management, and OAuth authentication — builds for Windows. Web frontend planned via WordPress calling the API. What is not yet built: WordPress marketplace site, publisher verification workflow, usage billing, device attestation.

---

## Developer Context

- **Developer:** Roman Noble — 40+ years software development, luthier by craft
- **IDEs:** VS Code + Visual Studio
- **Style:** Skeptical of over-engineering. Start focused. Ship the spec first. Do not add abstractions for hypothetical future requirements.
