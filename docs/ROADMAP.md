# Purfle Roadmap

## Phase 1 — Manifest Spec

**Goal:** A stable, signed manifest format that third parties can implement against.

- [x] `spec/SPEC.md` — human-readable spec, RFC tone
- [x] `spec/schema/agent.manifest.schema.json` — JSON Schema (Draft 2020-12)
- [x] `spec/schema/agent.identity.schema.json` — identity block schema
- [x] `spec/rfcs/0001-identity-model.md` — JWS vs DID, JWS accepted, DID migration path
- [x] `spec/examples/` — hello-world, assistant, email-monitor, demo-agent
- [ ] Spec review with one external runtime implementor

**Status: Complete.**

---

## Phase 2 — AIVM Runtime

**Goal:** A working AIVM host that enforces the load sequence from spec §4. A signed manifest loads; an invalid or tampered one does not.

- [x] `Manifest/` — parse, schema validate, canonical JSON
- [x] `Identity/` — JWS ES256 verify, key revocation check, expiry check
- [x] `Sandbox/CapabilityNegotiator` — required/optional capability negotiation
- [x] `Sandbox/AgentSandbox` — network, filesystem, environment, MCP enforcement
- [x] `Lifecycle/` — LoadResult, LoadFailureReason (12 typed failure reasons)
- [x] `AgentLoader` — full 7-step load sequence, 82+ tests
- [x] `Purfle.Runtime.Host` — runnable demo (sign → load → tamper demo → cap. neg. demo)
- [x] Live key registry API — Azure Functions `GET/POST/DELETE /keys/{id}`
- [x] `HttpKeyRegistryClient` — replaces `StaticKeyRegistry` in production
- [x] End-to-end trust loop — sign → register → load → verify → tamper detection
- [x] `Tools/BuiltInToolExecutor` — read_file, write_file, http_get, find_files, search_files
- [x] `Sessions/ConversationSession` — multi-turn chat with tool use
- [x] `Purfle.Runtime.Anthropic` — AnthropicAdapter
- [x] `Purfle.Runtime.Gemini` — GeminiAdapter
- [ ] `Purfle.Runtime.OpenClaw` — bridge to OpenClaw (stubbed)
- [ ] `Purfle.Runtime.Ollama` — local model adapter (stubbed)
- [ ] Audit log — every load attempt logged with outcome
- [ ] `Lifecycle/` — init timeout enforcement (step 7)

**Status: Complete. OpenClaw/Ollama adapters and audit log deferred.**

---

## Phase 3 — SDK + Tooling + Desktop App

**Goal:** Developers can scaffold, sign, and simulate an agent end-to-end. Users can install and run agents from the desktop app.

### SDK & CLI
- [x] `@purfle/core` — manifest types, structural validation, JWS sign/verify, canonical JSON
- [x] `@purfle/cli` — CLI entry point with all commands registered (commander)
- [x] `purfle init` — scaffolds agent directory with manifest template
- [x] `purfle build` — validates manifest against schema
- [x] `purfle sign` — signs with existing key or generates new key pair
- [x] `purfle simulate` — local simulation
- [x] `purfle publish` — publish command (wiring to marketplace pending)
- [x] `purfle search` — search command (wiring to marketplace pending)
- [x] `purfle install` — install command (wiring to marketplace pending)
- [x] `purfle login` — OAuth PKCE login
- [ ] Full Ajv JSON Schema validation in `@purfle/core` (Draft 2020-12)
- [ ] CI/CD — GitHub Actions: build, test, validate spec examples against schema

### Desktop App
- [x] .NET MAUI app — builds for Windows and Mac
- [x] Search page — marketplace browser
- [x] My Agents page — agent cards with status, last/next run
- [x] Settings page — marketplace URL, engine picker, API key storage, OAuth PKCE login
- [x] AgentRun page — interactive chat UI with ConversationSession
- [x] AgentDetail page — agent metadata and actions
- [x] LogView page — scrollable run.log viewer
- [x] AgentStore — local install, raw manifest and `.purfle` ZIP support
- [x] AppAdapterFactory — AnthropicAdapter or GeminiAdapter based on preference
- [x] AgentExecutorService — ephemeral P-256 re-signing for local dev trust

### Example Agents
- [x] `agents/chat.agent.json` + `Purfle.Agents.Chat` — conversational agent
- [x] `agents/file-search.agent.json` + `Purfle.Agents.FileSearch` — file content search
- [x] `agents/file-summarizer.agent.json` — file summarization
- [x] `agents/web-research.agent.json` + `Purfle.Agents.WebResearch` — web research + link extraction

### Documentation
- [x] `docs/GETTING_STARTED.md` — end-to-end walkthrough
- [x] `docs/MANIFEST_REFERENCE.md` — field-by-field reference aligned with schema
- [x] `docs/TROUBLESHOOTING.md` — error messages, causes, and fixes
- [x] `docs/ROADMAP.md` — this file

**Status: Complete. CLI commands exist; full marketplace wiring is Phase 4.**

---

## Phase 4 — Marketplace

**Goal:** Signed agents are discoverable, installable, and runnable. Publishers can manage their keys and listings.

### Marketplace API
- [x] `Purfle.Marketplace.Api` — ASP.NET Core with Agents, Auth, Keys, Publishers, Attestations controllers
- [x] `Purfle.Marketplace.Core` — AgentListing, AgentVersion, Publisher, SigningKey, Attestation entities
- [x] Repository interfaces — all entities with JSON file-backed implementations
- [x] `Purfle.Marketplace.Storage.Json` — JSON file storage (no database), Azure Blob option for manifests
- [x] DbKeyRegistry service — bridges signing keys to runtime identity verifier
- [x] OAuth PKCE login page + token endpoints

### Publisher Verification
- [x] Publisher registration with domain claim
- [x] Domain verification via `.well-known/purfle-verify.txt`
- [x] Verified status persisted and checked on attestation issuance

### Attestation Service
- [x] `marketplace-listed` — auto-issued on publish
- [x] `publisher-verified` — auto-issued when publisher is domain-verified
- [x] Attestation query endpoint — `GET /api/attestations/{agentId}`
- [x] Duplicate attestation prevention

### CLI Integration
- [x] `purfle publish` — uploads signed manifest to marketplace API
- [x] `purfle search` — queries marketplace with pagination
- [x] `purfle install` — downloads and stores agent locally

### Dogfood Agent
- [x] `agents/file-assistant/` — reads, lists, searches, summarizes files
- [x] `tools/mcp-file-server/` — MCP server providing file tools for agents

### Tests
- [x] 13 marketplace tests — registry, attestation, publisher verification
- [x] All 49 SDK tests pass (13 CLI + 36 core)

### Remaining
- [ ] Publisher accounts — key management UI
- [ ] Agent bundle hosting — signed `.purfle` ZIP upload and retrieval
- [ ] Search ranking — relevance-based ordering

---

## Phase A — IPC Protocol + Inference Adapters + Credential Stores

**Goal:** Runtime infrastructure for inter-process communication, pluggable inference, and secure credential storage.

- [x] IPC protocol for agent-runtime communication
- [x] Inference adapter abstraction with pluggable backends
- [x] Credential store abstraction (Windows Credential Manager / Mac Keychain path)

**Status: Complete.**

---

## Phase B — IdentityHub

**Goal:** Centralized identity management service for publisher keys and agent verification.

- [x] `Purfle.IdentityHub.Api` — ASP.NET Core identity service
- [x] `Purfle.IdentityHub.Core` — core entities and logic
- [x] Key management endpoints
- [x] Publisher identity verification

**Status: Complete.**

---

## Phase C — Dashboard API

**Goal:** Web API for monitoring agent status, run history, and system health.

- [x] `Purfle.Dashboard.Api` — ASP.NET Core dashboard service
- [x] Agent status and run history endpoints
- [x] Log viewing endpoints

**Status: Complete.**

---

## Phase D — Polyglot Agent Model

**Goal:** Agents can be implemented in both C# and TypeScript, with dual implementations for every agent.

- [x] C# agent implementation pattern (dotnet run)
- [x] TypeScript agent implementation pattern (npm start)
- [x] 10 agents with dual C#/TypeScript implementations

**Status: Complete.**

---

## Phase E — MCP Tool Servers

**Goal:** Dedicated MCP servers providing specialized tools for each agent domain.

- [x] `mcp-api-tools` — API monitoring and testing tools
- [x] `mcp-cli-gen` — CLI generation tools
- [x] `mcp-code-tools` — Code review and analysis tools
- [x] `mcp-db-tools` — Database query and schema tools
- [x] `mcp-file-server` — File read/list/search tools
- [x] `mcp-gmail` — Gmail integration tools
- [x] `mcp-meeting` — Meeting management tools
- [x] `mcp-microsoft-email` — Microsoft email integration tools
- [x] `mcp-news` — News aggregation tools
- [x] `mcp-pet` — Virtual pet tools
- [x] `mcp-research` — Research and web search tools

**Status: Complete.**

---

## Phase F — Agent Expansion

**Goal:** A rich library of example agents covering common use cases.

- [x] `api-guardian` — API uptime and change monitoring
- [x] `cli-generator` — Natural language to CLI tools
- [x] `code-reviewer` — Automated code review
- [x] `db-assistant` — Database exploration and queries
- [x] `email-priority` — Email prioritization and summarization
- [x] `file-assistant` — File operations and summarization
- [x] `meeting-assistant` — Meeting agendas and notes
- [x] `news-digest` — Daily news curation
- [x] `purfle-pet` — Virtual pet demo agent
- [x] `research-assistant` — Research compilation

**Status: Complete.**

---

## Phase G — Gemini + Additional Adapters

**Goal:** Multi-provider LLM support beyond Anthropic.

- [x] `Purfle.Runtime.Gemini` — Google Gemini adapter
- [x] AppAdapterFactory — engine picker based on manifest or user preference
- [x] `Purfle.Runtime.OpenClaw` — stubbed
- [x] `Purfle.Runtime.Ollama` — stubbed

**Status: Complete. OpenClaw/Ollama adapters stubbed for future implementation.**

---

## Phase H — CI/CD + Documentation

**Goal:** Automated builds, dependency monitoring, and updated documentation.

- [x] `.github/workflows/ci.yml` — matrix build for runtime, SDK, dashboard, IdentityHub, agents, MCP servers
- [x] `.github/workflows/release.yml` — tag-triggered release with artifacts
- [x] `.github/dependabot.yml` — weekly NuGet and npm dependency monitoring
- [x] Updated `docs/GETTING_STARTED.md` — polyglot agents, dashboard section
- [x] Updated `docs/ROADMAP.md` — phases A-H marked complete

**Status: Complete.**

---

## Phase 5 — Production Hardening

**Goal:** Ready for real-world use with monitoring, credentials, and device support.

- [ ] Windows Credential Manager / Mac Keychain integration for API key storage
- [ ] Audit log — every agent load attempt logged with outcome
- [ ] Agent assembly loading end-to-end — test with real `.dll` in AssemblyLoadContext
- [ ] Init timeout enforcement (lifecycle step 7)
- [ ] Monetization — paid listings, usage-based billing
- [ ] Device identity layer — hardware attestation for on-device AIVM hosts
- [ ] DID migration (v0.2) — self-sovereign key identity (see RFC 0001)

---

## Not on the Roadmap

- Custom inference engine — the AIVM delegates to the engine declared in the manifest
- Agent framework features (memory, planning, multi-agent orchestration) — that is the engine's job
- Browser runtime — Windows + Mac host only for now
