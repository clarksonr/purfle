# Purfle Architecture

## The AIVM Model

Purfle is built around a single organizing concept: the **AIVM** (AI Virtual Machine).

A JVM loads bytecode, verifies it, enforces a security model, and executes it. It does not trust the bytecode. It does not give the bytecode capabilities it did not declare. It does not load bytecode the runtime cannot support. The AIVM does the same for AI agents.

The manifest is the bytecode. It is a signed JSON document that fully describes the agent before any agent code runs. The AIVM host (the runtime) enforces what the manifest says. The agent cannot exceed the scope it declared; it cannot load on a runtime that cannot satisfy its requirements.

This is the architectural commitment: **the runtime is a VM, not a framework.** It does not provide agent behavior. It enforces agent boundaries.

---

## Load Sequence (The VM Boot)

Defined in full in `spec/SPEC.md §4`. Implemented in `runtime/src/Purfle.Runtime/AgentLoader.cs`.

```
1. PARSE              — deserialize manifest JSON; reject if malformed
2. SCHEMA VALIDATION  — validate against agent.manifest.schema.json
3. IDENTITY           — verify JWS signature; check key revocation; check expiry
4. CAPABILITY NEG.    — compare agent requirements against runtime capability set
5. PERMISSION BIND    — construct the sandbox from the permissions block
6. I/O SCHEMA         — compile input/output schemas as validators
7. INITIALIZATION     — start agent within sandbox (engine adapter's responsibility)
```

Failure at any step aborts loading. The agent never executes. This is not configurable.

---

## Capability Negotiation

The agent declares what it needs from the runtime. The runtime declares what it offers. A mismatch on a required capability is a load failure.

```
Agent manifest                     Runtime
──────────────                     ───────
capabilities: [                    capability set: {
  { id: "web-search",                "inference",
    required: true },                "web-search",
  { id: "text-to-speech",            "filesystem"
    required: false }              }
]
                ↓  negotiate  ↓
  web-search    → present      → continue
  text-to-speech→ absent       → warning (optional, agent degrades)
                                 → LOAD OK
```

If `required: true` and the capability is absent → load failure with `CapabilityMissing`.

Well-known capability IDs are defined in the Purfle capability registry (`spec/SPEC.md §3.3`). Third-party capabilities use reverse-domain namespacing: `com.example.my-cap`.

---

## The Sandbox

The sandbox is constructed at load sequence step 5 from the manifest's `permissions` block. It is immutable for the agent's lifetime. The runtime enforces it; the agent cannot inspect or modify it.

```
permissions: {
  network:     { allow: ["https://api.example.com/*"], deny: ["*"] }
  filesystem:  { read: ["/workspace/src/**"], write: ["/workspace/out"] }
  environment: { allow: ["API_KEY"] }
  tools:       { mcp: ["file", "search"] }
}
```

Default stance: **deny everything**. An absent permission block grants nothing. `deny` entries take precedence over `allow` entries.

The sandbox is not advisory. A runtime that does not enforce it is not conforming.

---

## Monorepo Structure

```
purfle/
├── spec/          ← The contract. Schema, examples, RFCs.
├── runtime/       ← The AIVM host (.NET / C#)
│   └── src/
│       ├── Purfle.Runtime/          ← Core AIVM
│       │   ├── Manifest/            — parse, validate, canonical JSON
│       │   ├── Identity/            — JWS verification, key registry
│       │   ├── Sandbox/             — capability negotiation, permission binding
│       │   └── Lifecycle/           — load result types
│       ├── Purfle.Runtime.OpenClaw/ ← Adapter for OpenClaw runtime
│       └── Purfle.Runtime.Host/     ← Runnable demo host
├── sdk/           ← Developer tooling (TypeScript / Node.js)
│   └── packages/
│       ├── @purfle/core  — manifest types, schema validation, JWS signing
│       └── @purfle/cli   — purfle init | build | sign | publish | simulate
├── marketplace/   ← Registry and distribution (phase 4)
└── docs/
```

---

## Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Runtime model | AIVM — manifest defines the sandbox | Agents cannot exceed declared scope; the VM enforces it |
| Identity | JWS ES256 | Mature, well-implemented; DID migration path in v0.2 (see RFC 0001) |
| Capability model | Agent declares requirements; runtime declares offers | Load-time safety contract, not advisory |
| Permission stance | Deny all by default | Explicit allowlists; runtime blocks everything else |
| Agent-to-tool protocol | MCP | Standard; avoids inventing a new protocol |
| Runtime platform | .NET / C# | Windows-first; strong sandbox primitives |
| Business model | Open core | Spec + runtime + SDK open; marketplace monetized |
| Hardware layer | Deferred to phase 4 | Spec accommodates it via capability negotiation; no premature design |

---

## Data Flow

```
Developer
  │
  ├── purfle init "My Agent"    → agent.json  (scaffold)
  ├── purfle build .            → validation  (schema check)
  ├── purfle sign   . --generate-key → signature embedded
  └── purfle publish .          → Purfle Registry

Runtime (AIVM host)
  │
  ├── 1. fetch manifest from registry
  ├── 2. parse + schema validate
  ├── 3. verify JWS signature (key registry)
  ├── 4. capability negotiation
  ├── 5. construct sandbox
  ├── 6. compile I/O schemas
  └── 7. initialize via engine adapter
         │
         └── engine (openai-compatible / anthropic / ollama)
               │
               └── validate output → return to caller
```
