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
- **MCP for tools:** Agent-to-tool communication uses MCP; do not invent a new protocol
- **Open core:** Spec + runtime + SDK will be open; marketplace is monetized
- **Deny by default:** Permissions in the manifest are allowlists; the runtime denies everything not listed
- **No hardware yet:** Device attestation is phase 4; do not design for it now
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
| Marketplace API | ASP.NET Core + EF Core SQLite (`marketplace/`) |
| Marketplace Auth | ASP.NET Identity + OpenIddict (OAuth2/OIDC with PKCE) |
| Desktop App | .NET MAUI (`app/`) |
| Web Client | WordPress (external, calls marketplace API) |
| CI/CD | GitHub Actions (not yet configured) |

---

## Build Commands

### Runtime (.NET)

```bash
cd runtime

# Build
dotnet build

# Test all
dotnet test

# Run a single test class
dotnet test --filter "ClassName=AgentSandboxTests"

# Run the demo host (loads hello-world, demonstrates tampering detection)
dotnet run --project src/Purfle.Runtime.Host
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
cd marketplace

# Build
dotnet build

# Run the API (default: http://localhost:5000)
dotnet run --project src/Purfle.Marketplace.Api
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
`Purfle.Runtime` implements the full seven-step load sequence from spec §4: parse → schema validation → JWS identity verification → capability negotiation → permission binding → I/O schema compilation → initialization. 52 tests pass. `Purfle.Runtime.Host` is a runnable demo. `HttpKeyRegistryClient` implements `IKeyRegistry` against the marketplace API. What is not yet built: audit logging, the OpenClaw/Ollama adapters, and `Lifecycle/` init timeout enforcement. The Anthropic adapter (`Purfle.Runtime.Anthropic`) is functional and requires `ANTHROPIC_API_KEY` listed in the manifest's `permissions.environment.allow`.

**Phase 3 (SDK + Tooling) — Core complete.**
`@purfle/core` has working TypeScript types, ES256 signing/verification, canonical JSON, and manifest validation. `@purfle/cli` has working `init`, `build`, `sign`, `simulate`, `publish`, `search`, `install`, and `login` commands. `publish` calls the marketplace API with auth. `login` uses OAuth2 PKCE flow. Full JSON Schema validation via Ajv and `@purfle/core` tests are pending.

**Phase 4 (Marketplace) — In progress.**
`Purfle.Marketplace.Api` (ASP.NET Core) implements the full REST API: key registry (GET/POST/DELETE), agent registry (search, detail, version download, publish with JWS signature validation), and OAuth2/OIDC via OpenIddict (PKCE flow for CLI/MAUI). Auth protects write endpoints; read endpoints are public. Data layer uses EF Core + SQLite. `Purfle.App` (.NET MAUI) is a desktop app with search, install, agent management, and OAuth authentication — builds for Windows. Web frontend planned via WordPress calling the API. What is not yet built: WordPress marketplace site, publisher verification workflow, usage billing, device attestation.

---

## Developer Context

- **Developer:** Roman Noble — 40+ years software development, luthier by craft
- **IDEs:** VS Code + Visual Studio
- **Style:** Skeptical of over-engineering. Start focused. Ship the spec first. Do not add abstractions for hypothetical future requirements.
