# Purfle Agent Manifest Specification

**Status:** Draft
**Version:** 0.1.0
**Date:** 2026-03-27

---

## Abstract

This document defines the Purfle Agent Manifest — a structured, signable JSON document that describes an AI agent's identity, runtime requirements, capability dependencies, permission scope, lifecycle constraints, and I/O contract. A conforming AIVM uses the manifest to verify, negotiate, sandbox, and execute an agent. No part of an agent executes before the manifest is verified.

---

## 1. Motivation

AI agent frameworks define behavior but not identity and not load-time safety contracts. There is no standard way to answer:

- Who authored this agent, and has it been tampered with since signing?
- What does this agent need from the host AIVM in order to function?
- What resources is it permitted to access, and how is that enforced?
- When does its authorization expire?

The absence of load-time negotiation is a specific failure mode: an agent that silently degrades when required capabilities are missing is not safe. It may behave unpredictably, produce incorrect output, or silently escalate to available resources it was not designed to use. The correct behavior is to refuse to load.

Purfle solves both problems. The manifest is the unit of distribution and trust. The AIVM enforces the manifest. The agent cannot exceed the scope the manifest declares, and it cannot load on an AIVM that cannot satisfy its declared requirements.

The Purfle runtime is an AI Virtual Machine (AIVM). Like a conventional virtual machine, it is the sole execution environment for the agent. The manifest is the contract. The AIVM enforces it. The agent cannot modify its own sandbox, cannot exceed its declared permissions, and cannot load on a runtime that cannot satisfy its requirements.

---

## 2. Terminology

| Term | Definition |
|---|---|
| **Agent** | A bounded, autonomous software process that takes structured input and produces structured output using an inference engine or other mechanism. |
| **Manifest** | A UTF-8 JSON document that fully describes an agent. Signed by the author. Verified by the AIVM before any agent code executes. |
| **Identity** | The cryptographically verifiable authorship and integrity claim on a manifest. |
| **Capability** | A named service that the AIVM provides to agents. Agents declare which capabilities they require. |
| **Runtime capability set** | The enumerable set of capability IDs an AIVM advertises as available at load time. |
| **Capability negotiation** | The load-time comparison of an agent's declared required capabilities against the runtime capability set. A mismatch on any required capability is a load failure. |
| **Permission** | A resource or action the AIVM is authorized to grant the agent within the sandbox. |
| **Sandbox** | The enforced resource boundary derived from the manifest's permissions block. The AIVM creates it; the agent cannot modify or escape it. |
| **Load failure** | The condition where the AIVM refuses to load an agent. Causes: malformed manifest, schema violation, invalid signature, expired manifest, unsatisfied required capability. |
| **Signing key** | An asymmetric key pair. The private half signs manifests. The public half is registered in the Purfle key registry. |

---

## 3. Manifest Format

A manifest is a UTF-8 JSON document. All top-level fields are required unless explicitly marked optional.

### 3.1 Top-Level Structure

```json
{
  "purfle": "0.1",
  "id": "<uuid-v4>",
  "name": "<string>",
  "version": "<semver>",
  "description": "<string>",
  "identity": { ... },
  "capabilities": [ ... ],
  "permissions": { ... },
  "lifecycle": { ... },
  "runtime": { ... },
  "io": { ... }
}
```

| Field | Type | Description |
|---|---|---|
| `purfle` | string | Spec version the manifest targets. Pattern: `\d+\.\d+`. |
| `id` | string | UUID v4. Globally unique manifest identifier. |
| `name` | string | Human-readable agent name. |
| `version` | string | Semver agent version. |
| `description` | string | What the agent does. Max 1024 characters. |
| `identity` | object | Cryptographic identity block. See §3.2. |
| `capabilities` | array | Runtime service requirements. See §3.3. |
| `permissions` | object | Resource access grants. See §3.4. |
| `lifecycle` | object | Timing and error policy. See §3.5. |
| `runtime` | object | Inference engine requirements. See §3.6. |
| `io` | object | Input and output schemas. See §3.7. |

### 3.2 Identity Block

```json
"identity": {
  "author": "<string>",
  "email": "<email>",
  "key_id": "<string>",
  "algorithm": "ES256",
  "issued_at": "<ISO 8601 datetime>",
  "expires_at": "<ISO 8601 datetime>",
  "signature": "<JWS compact serialization>"
}
```

| Field | Description |
|---|---|
| `author` | Display name of the author or publishing organization. |
| `email` | Contact address for the author. |
| `key_id` | Identifier of the public key registered in the Purfle key registry. In v0.2+, may be a DID string. |
| `algorithm` | JWA algorithm. `ES256` is the only supported value in v0.1. |
| `issued_at` | Timestamp of signing. |
| `expires_at` | Timestamp after which the manifest is invalid. The AIVM MUST reject manifests where `expires_at <= now`. |
| `signature` | JWS Compact Serialization over the canonical manifest body. See §5. |

### 3.3 Capabilities

The capabilities array is the agent's declaration of what it requires from the AIVM. Each entry names a service the AIVM must be able to provide. This is the capability negotiation contract.

**Negotiation semantics:**

- A capability with `"required": true` is a hard requirement. If the AIVM's capability set does not include this capability ID, the AIVM MUST refuse to load the agent (load failure).
- A capability with `"required": false` is optional. If absent from the AIVM's capability set, the AIVM emits a warning and continues loading. The agent is responsible for degrading gracefully.
- `inference` — basic LLM invocation — is always implicitly required and need not be declared. An AIVM that cannot do inference cannot load any agent.

```json
"capabilities": [
  {
    "id": "web-search",
    "description": "Query a search API; required for primary function.",
    "required": true
  },
  {
    "id": "text-to-speech",
    "description": "Synthesize audio responses; agent degrades to text-only if absent.",
    "required": false
  }
]
```

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | yes | Capability identifier. Must match `^[a-z][a-z0-9-]*$` or be namespaced (see below). |
| `description` | string | no | Why the agent needs this capability. |
| `required` | boolean | no | Default `false`. If `true`, load fails when capability is absent. |

**Well-known capability IDs (v0.1):**

| ID | Description |
|---|---|
| `inference` | LLM inference. Always implicitly required; declaring it has no effect. |
| `web-search` | Outbound search requests via the AIVM's search interface. |
| `filesystem` | Read/write filesystem access. Scope controlled by `permissions.filesystem`. |
| `mcp-tools` | MCP tool invocation. Permitted tools controlled by `permissions.tools.mcp`. |
| `code-execution` | Sandboxed code execution. |
| `text-to-speech` | Audio synthesis from text. |
| `speech-to-text` | Audio transcription. |

Third-party capability IDs MUST use a reverse-domain prefix: `com.example.my-capability`. Unprefixed IDs are reserved for the Purfle capability registry.

An empty `capabilities` array is valid. It means the agent requires only implicit inference.

### 3.4 Permissions

The permissions block defines the sandbox. The AIVM creates and enforces this boundary before the agent initializes. The AIVM MUST deny access to any resource not explicitly listed. `deny` entries take precedence over `allow` entries.

```json
"permissions": {
  "network": {
    "allow": ["https://api.example.com/*"],
    "deny": ["*"]
  },
  "filesystem": {
    "read": ["/tmp/agent-workspace"],
    "write": ["/tmp/agent-workspace"]
  },
  "environment": {
    "allow": ["AGENT_API_KEY"]
  },
  "tools": {
    "mcp": ["file", "search"]
  }
}
```

All sub-blocks are optional. An absent sub-block means no access is granted for that resource type.

| Sub-block | Field | Description |
|---|---|---|
| `network` | `allow` | URL patterns the agent may access. Glob-style matching. |
| `network` | `deny` | URL patterns blocked. Takes precedence over `allow`. |
| `filesystem` | `read` | Absolute paths or glob patterns the agent may read. |
| `filesystem` | `write` | Absolute paths or glob patterns the agent may write. |
| `environment` | `allow` | Environment variable names the agent may read. |
| `tools` | `mcp` | MCP tool identifiers the agent may invoke. |

**Capability/permission relationship:** Declaring a capability that requires a permission scope (e.g., `filesystem`) without declaring the corresponding permission is not a schema error, but the AIVM will enforce an empty scope, which means any attempt to use that capability will fail at runtime. Agents SHOULD declare both consistently.

### 3.5 Lifecycle

```json
"lifecycle": {
  "init_timeout_ms": 5000,
  "max_runtime_ms": 300000,
  "on_error": "terminate",
  "restartable": false
}
```

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `init_timeout_ms` | integer | no | 5000 | Max milliseconds allowed from load completion to agent ready state. |
| `max_runtime_ms` | integer | no | 300000 | Max milliseconds for a single invocation. `0` means no enforced limit. |
| `on_error` | string | yes | — | AIVM behavior on agent error exit: `terminate`, `suspend`, or `retry`. |
| `restartable` | boolean | no | false | If `true`, the AIVM MAY restart the agent on clean exit. |

`on_error` values:

- `terminate` — unload the agent immediately
- `suspend` — pause the agent; await external signal before terminating or resuming
- `retry` — restart the agent once; if it fails again, treat as `terminate`

### 3.6 Runtime

The runtime block declares the inference engine interface the agent expects. This is distinct from capability negotiation: capabilities name services the AIVM provides; the runtime block specifies the API surface the agent's code targets.

```json
"runtime": {
  "requires": "purfle/0.1",
  "engine": "openai-compatible",
  "model": "gpt-4o",
  "adapter": "openclaw"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requires` | string | yes | Minimum Purfle spec version. Pattern: `purfle/\d+\.\d+`. |
| `engine` | string | yes | Inference engine interface: `openai-compatible`, `anthropic`, or `ollama`. |
| `model` | string | no | Model identifier passed to the engine. |
| `adapter` | string | no | Runtime adapter hint (e.g., `openclaw`). |

An AIVM MUST reject manifests where `runtime.requires` specifies a spec version greater than the AIVM supports.

### 3.7 I/O Contract

```json
"io": {
  "input": {
    "type": "object",
    "properties": {
      "query": { "type": "string" }
    },
    "required": ["query"]
  },
  "output": {
    "type": "object",
    "properties": {
      "result": { "type": "string" }
    }
  }
}
```

`input` and `output` are JSON Schema fragments (any valid JSON Schema object). The AIVM:

- Compiles both schemas before agent initialization
- Validates each invocation's input against `io.input` before dispatching to the agent; rejects invalid input
- Validates each invocation's output against `io.output` before returning to the caller; rejects invalid output

---

## 4. Load Sequence

A conforming AIVM MUST execute this sequence in order. Failure at any step is a load failure: the agent MUST NOT execute, and the failure reason MUST be reported to the caller.

```
1. PARSE
   Deserialize manifest JSON.
   Reject if: not valid UTF-8, not valid JSON, root value is not an object.

2. SCHEMA VALIDATION
   Validate the parsed document against agent.manifest.schema.json.
   Reject if: any required field is absent, any field fails its type or pattern constraint.

3. IDENTITY VERIFICATION
   a. Retrieve public key for identity.key_id from the Purfle key registry.
   b. Check key revocation status; reject if revoked.
   c. Reconstruct the canonical manifest body (see §5.1).
   d. Verify identity.signature over the canonical body using identity.algorithm.
   e. Verify identity.expires_at > current UTC time.
   Reject if: key not found, key revoked, signature invalid, manifest expired.

4. CAPABILITY NEGOTIATION
   Obtain the AIVM's capability set.
   For each entry in capabilities where required == true:
     If entry.id not in runtime capability set → load failure.
   For each entry in capabilities where required == false:
     If entry.id not in runtime capability set → emit warning; continue.

5. PERMISSION BINDING
   Construct the sandbox from the permissions block.
   The sandbox is immutable for the lifetime of the agent.

6. I/O SCHEMA COMPILATION
   Compile io.input and io.output as JSON Schema validators.
   Reject if: either schema is not a valid JSON Schema object.

7. INITIALIZATION
   Start the agent within the sandbox.
   Enforce lifecycle.init_timeout_ms.
   Reject if: agent does not reach ready state within the timeout.
```

Steps 1–3 MUST be completed before step 4. Steps 4–6 MAY execute concurrently. Step 7 MUST follow steps 4–6.

---

## 5. Signing

### 5.1 Canonical Form

The signed payload is the canonical JSON form of the manifest body. To produce it:

1. Parse the manifest into a JSON value
2. Remove the `identity.signature` field
3. Serialize all object keys in lexicographic (Unicode code point) order, recursively
4. Emit with no whitespace

The result is a deterministic byte sequence. The same manifest always produces the same canonical form.

### 5.2 JWS Construction

Sign the canonical bytes using the algorithm named in `identity.algorithm` (ES256 in v0.1). Encode as JWS Compact Serialization:

```
BASE64URL(header) . BASE64URL(payload) . BASE64URL(signature)
```

- `header` — `{"alg":"ES256","kid":"<key_id>"}`, serialized with keys in lexicographic order, no whitespace
- `payload` — the canonical manifest bytes (not re-encoded; this is a detached-payload JWS variant: the payload bytes are the canonical manifest, not an independently base64url-encoded value)

Store the resulting compact serialization in `identity.signature`.

### 5.3 Verification

See load sequence step 3. An AIVM MUST NOT cache key revocation status between agent loads.

---

## 6. Versioning

The `purfle` field in the manifest and the `runtime.requires` field both carry spec version strings.

- `purfle` — the spec version used when authoring the manifest
- `runtime.requires` — the minimum spec version the runtime must support

A runtime MUST reject a manifest where `runtime.requires` names a version it does not implement. Minor version increments (`0.1` → `0.2`) are additive and backward-compatible by convention; major version increments may not be.

---

## 7. Conformance

### 7.1 Conforming Runtime

A runtime is conforming if it:

- Executes the load sequence in §4 in full and in order
- Maintains and advertises a stable, enumerable capability set
- Refuses to load an agent with any unsatisfied required capability
- Enforces the sandbox derived from `permissions` for the entire agent lifetime
- Validates invocation inputs against `io.input` and outputs against `io.output`
- Refuses to execute manifests with invalid, expired, or revoked signatures
- Does not provide any capability to an agent that is not declared in `permissions`

### 7.2 Conforming Manifest

A manifest is conforming if it:

- Passes JSON Schema validation against `spec/schema/agent.manifest.schema.json`
- Has a signature that verifies against the declared `key_id`
- Has `expires_at` in the future at the time of load
- Declares all capabilities it genuinely requires from the runtime
- Does not declare permissions broader than the agent's actual access needs

---

## 8. Security Considerations

- Private key material MUST NOT appear in the manifest. Only `key_id` is present.
- Runtimes MUST NOT cache key revocation status. Check status on every load.
- The default permission stance is deny-all. An absent permission sub-block grants nothing.
- Runtimes MUST NOT skip signature verification in any mode. Development tooling may use a locally trusted key; it MUST NOT disable verification entirely.
- Optional capabilities MUST NOT silently expand the agent's access scope. If a capability is absent, its associated permission scope is inert — no access is granted.
- Agents MUST NOT be loaded if the capability negotiation step (§4, step 4) is skipped or bypassed. Bypassing negotiation defeats the sandbox contract.

---

## 9. Future Work

- DID (Decentralized Identifiers) as an alternative to the central key registry — see `spec/rfcs/0001-identity-model.md`
- AIVM capability advertisement format — a machine-readable document that AIVMs publish describing their capability set and version
- Hardware attestation — device identity layer (phase 4); capability negotiation accommodates this without spec changes
- Manifest composition — shared capability block imports across agents
