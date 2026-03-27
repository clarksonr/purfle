# Purfle Roadmap

## Phase 1 — Manifest Spec

**Goal:** A stable, signed manifest format that third parties can implement against.

- [x] `spec/SPEC.md` — human-readable spec, RFC tone
- [x] `spec/schema/agent.manifest.schema.json` — JSON Schema (Draft 2020-12)
- [x] `spec/schema/agent.identity.schema.json` — identity block schema
- [x] `spec/rfcs/0001-identity-model.md` — JWS vs DID, JWS accepted, DID migration path
- [x] `spec/examples/` — hello-world, voice-assistant, trusted-agent
- [ ] Spec review with one external runtime implementor

**Status: Complete.**

---

## Phase 2 — AIVM Runtime

**Goal:** A working AIVM host that enforces the load sequence from spec §4. A signed manifest loads; an invalid or tampered one does not.

- [x] `Manifest/` — parse, schema validate, canonical JSON
- [x] `Identity/` — JWS ES256 verify, key revocation check, expiry check
- [x] `Sandbox/CapabilityNegotiator` — required/optional capability negotiation
- [x] `Sandbox/AgentSandbox` — network, filesystem, environment, MCP enforcement
- [x] `Lifecycle/` — load result types with failure reasons
- [x] `AgentLoader` — full 6-step load sequence, 31 tests
- [x] `Purfle.Runtime.Host` — runnable demo (sign → load → tamper demo → cap. neg. demo)
- [ ] Live key registry API — `GET /keys/{id}`, `POST /keys`, `DELETE /keys/{id}`
- [ ] `HttpKeyRegistryClient` — replace `StaticKeyRegistry` in production
- [ ] Audit log — every load attempt logged with outcome
- [ ] `Purfle.Runtime.OpenClaw/OpenClawAdapter.cs` — bridge to OpenClaw
- [ ] `Lifecycle/` — init timeout enforcement (step 7)

**Status: Core complete. Key registry API, OpenClaw adapter, and lifecycle enforcement pending.**

---

## Phase 3 — SDK + Tooling

**Goal:** Developers can scaffold, sign, and simulate an agent end-to-end from the CLI.

- [x] `@purfle/core` — manifest types, structural validation, JWS sign/verify, canonical JSON
- [x] `@purfle/cli` — CLI entry point with all commands registered (commander)
- [x] `purfle init` — scaffolds agent directory with manifest template
- [x] `purfle build` — validates manifest against schema
- [x] `purfle sign` — signs with existing key or generates new key pair
- [ ] `purfle publish` — posts to key registry + agent registry (blocked on phase 2 registry)
- [ ] `purfle simulate` — runs agent locally with sandbox enforcement
- [ ] Full JSON Schema validation in `@purfle/core` (Ajv, Draft 2020-12)
- [ ] CI/CD — GitHub Actions: build, test, validate spec examples against schema
- [ ] `@purfle/core` tests

**Status: Stubs in place. `init`, `build`, `sign` are functional. `publish` and `simulate` blocked on phase 2 registry and local engine adapter.**

---

## Phase 4 — Marketplace + Devices

**Goal:** Signed agents are discoverable, installable, and runnable on any conforming runtime including edge devices.

- [ ] Purfle Marketplace — agent registry, search, version history
- [ ] Publisher accounts — identity verification, key management UI
- [ ] Monetization — paid listings, usage-based billing
- [ ] Device identity layer — hardware attestation for on-device AIVM hosts
- [ ] DID migration (v0.2) — self-sovereign key identity (see RFC 0001)

**Status: Not started.**

---

## Not on the Roadmap

- Custom inference engine — the AIVM delegates to the engine declared in the manifest
- Agent framework features (memory, planning, multi-agent orchestration) — that is the engine's job
- Browser runtime — Windows host only for now
