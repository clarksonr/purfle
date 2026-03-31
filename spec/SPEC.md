# Purfle Agent Manifest Specification

**Status:** Draft
**Version:** 0.1
**Date:** 2026-03-31

---

## 1. Abstract

This document defines the Purfle Agent Manifest — a signed JSON document that declares an AI agent's identity, runtime requirements, capability dependencies, permission scope, lifecycle hooks, and tool bindings. A conforming AI Virtual Machine (AIVM) validates the manifest before any agent code executes and enforces its declared constraints for the agent's lifetime. No part of an agent executes before the manifest is verified.

---

## 2. Motivation

AI agent frameworks define behavior but not identity and not load-time safety contracts. Without a standard identity and capability declaration, there is no reliable way to answer:

- Who authored this agent, and has it been tampered with since signing?
- What does this agent need from the host in order to function?
- What resources is it permitted to access, and how is that enforced?
- When does its authorization expire?

The absence of load-time negotiation is a specific failure mode: an agent that silently degrades when required capabilities are missing is not safe. It may behave unpredictably or silently escalate to host resources it was not designed to use. The correct behavior is to refuse to load.

The manifest is the unit of distribution and trust. The AIVM enforces the manifest. An agent cannot exceed the scope the manifest declares and cannot load on an AIVM that cannot satisfy its declared requirements.

---

## 3. Terminology

| Term | Definition |
|---|---|
| **AIVM** | AI Virtual Machine. The sandboxed host process that loads, verifies, and executes agent packages. The AIVM is the sole execution environment for an agent; the agent cannot modify or escape it. |
| **Agent package** | A distributable artifact containing a signed manifest, one or more .NET assemblies, and optional supporting files (prompts, assets). The manifest declares everything the AIVM needs to load and execute the package. |
| **Manifest** | A UTF-8 JSON document that fully describes an agent package. Signed by the publisher. Verified by the AIVM before any agent code executes. |
| **Capability** | A named service the AIVM provides to agents. Agents declare which capabilities they require. The AIVM enforces that agents only use declared capabilities. |
| **Permission** | Per-capability resource configuration that narrows the scope of a granted capability. Permissions are enforced by the AIVM sandbox. |
| **Publisher** | The individual or organization that signs and distributes an agent package. Identified by `identity.author` and `identity.key_id`. |
| **Signature** | A JWS Compact Serialization produced by signing the canonical manifest body with the publisher's ES256 private key. Stored in `identity.signature`. |

---

## 4. Manifest Structure

A manifest is a UTF-8 JSON document. Fields marked **required** must be present; the AIVM MUST reject manifests where any required field is absent or fails its type constraint.

### 4.1 Top-Level Fields

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
  "runtime": { ... },
  "lifecycle": { ... },
  "tools": [ ... ],
  "io": {}
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `purfle` | string | yes | Spec version the manifest targets. Pattern: `\d+\.\d+`. |
| `id` | string | yes | UUID v4. Globally unique agent identifier. |
| `name` | string | yes | Human-readable agent name. 1–128 characters. |
| `version` | string | yes | Semantic version of this agent. |
| `description` | string | no | What the agent does. Max 1024 characters. |
| `identity` | object | yes | Cryptographic identity block. See §4.2. |
| `capabilities` | array | yes | Runtime services this agent requires. See §5. |
| `permissions` | object | no | Per-capability resource configuration. See §6. |
| `runtime` | object | yes | Inference engine requirements. See §4.3. |
| `lifecycle` | object | no | Lifecycle hooks and error policy. See §8. |
| `tools` | array | no | MCP tool bindings wired by the AIVM at load time. See §4.4. |
| `io` | object | no | Input/output schema hints. No enforcement in phase 1. |

### 4.2 Identity Block

```json
"identity": {
  "author": "com.example",
  "email": "author@example.com",
  "key_id": "key-abc-001",
  "algorithm": "ES256",
  "issued_at": "2026-01-01T00:00:00Z",
  "expires_at": "2027-01-01T00:00:00Z",
  "signature": "<JWS compact serialization>"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `author` | string | yes | Reverse-domain publisher identifier or username. |
| `email` | string | yes | Contact address of the publisher. |
| `key_id` | string | yes | Identifier of the signing key in the Purfle key registry. |
| `algorithm` | string | yes | JWA algorithm. `ES256` is the only valid value in v0.1. |
| `issued_at` | string | yes | ISO 8601 datetime when the manifest was signed. |
| `expires_at` | string | yes | ISO 8601 datetime after which the manifest is invalid. The AIVM MUST reject manifests where `expires_at <= now (UTC)`. |
| `signature` | string | no | JWS Compact Serialization over the canonical manifest body. Omit at authoring time; added by the signing tool. |

### 4.3 Runtime Block

```json
"runtime": {
  "requires": "purfle/0.1",
  "engine": "anthropic",
  "model": "claude-sonnet-4-20250514",
  "max_tokens": 1000
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requires` | string | yes | Minimum Purfle runtime version. Pattern: `purfle/\d+\.\d+`. |
| `engine` | string | yes | Inference adapter to use. One of: `anthropic`, `openclaw`, `ollama`. |
| `model` | string | no | Model identifier passed to the engine adapter. |
| `max_tokens` | integer | no | Maximum tokens the model may generate per turn. |

An AIVM MUST reject a manifest where `runtime.requires` names a spec version the AIVM does not implement.

### 4.4 Tool Bindings

Each entry in `tools` describes one MCP tool the AIVM wires at load time. Requires the `mcp.tool` capability.

```json
"tools": [
  {
    "name": "web_search",
    "server": "http://localhost:5010/mcp",
    "description": "Search the web for current information."
  }
]
```

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Tool name exposed to the LLM. |
| `server` | string | yes | MCP server URL the AIVM connects to for this tool. |
| `description` | string | no | Human-readable description forwarded to the LLM tool schema. |

---

## 5. Capability Model

### 5.1 Enforcement Rule

The `capabilities` array is the agent's exhaustive declaration of what it requires from the AIVM. The enforcement rule is:

- **Declared = permitted.** The AIVM provides only the capabilities listed in `capabilities`.
- **Undeclared = blocked.** Any attempt to use a capability not listed in `capabilities` MUST be denied by the AIVM at runtime.

`capabilities` is a flat array of capability ID strings:

```json
"capabilities": ["llm.chat", "network.outbound", "env.read"]
```

An empty array is valid. The AIVM MUST NOT infer or auto-grant capabilities not declared.

### 5.2 Phase 1 Capability Strings

The following capability IDs are defined for phase 1. No other values are valid in v0.1.

| Capability ID | Permission config keys | Description |
|---|---|---|
| `llm.chat` | none | May use conversational (multi-turn) LLM inference. |
| `llm.completion` | none | May use single-turn LLM completion. |
| `network.outbound` | `hosts` (string[]) | May make outbound HTTP calls to the listed hostnames. |
| `env.read` | `vars` (string[]) | May read the listed environment variable names. |
| `fs.read` | `paths` (string[]) | May read from the listed filesystem paths. |
| `fs.write` | `paths` (string[]) | May write to the listed filesystem paths. |
| `mcp.tool` | none | May invoke MCP tool bindings declared in `tools`. |

Capabilities with no permission config keys require no entry in `permissions` (though one may be present as an empty object).

---

## 6. Permission Model

### 6.1 Purpose

Permissions provide per-capability resource configuration. Where a capability names a class of access (`network.outbound`), the permission entry narrows the scope of that access (`hosts: ["api.anthropic.com"]`).

### 6.2 Validity Rule

Every key in `permissions` MUST appear in `capabilities`. A permission entry without a matching capability is a schema violation; the AIVM MUST reject the manifest.

The inverse is not required: a capability MAY appear in `capabilities` without a corresponding entry in `permissions` when that capability requires no configuration (e.g., `llm.chat`, `mcp.tool`).

### 6.3 Permission Config Shapes

**`network.outbound`** — lists hostnames the agent may connect to:

```json
"permissions": {
  "network.outbound": {
    "hosts": ["api.anthropic.com", "api.example.com"]
  }
}
```

**`env.read`** — lists environment variable names the agent may read:

```json
"permissions": {
  "env.read": {
    "vars": ["ANTHROPIC_API_KEY", "APP_CONFIG_PATH"]
  }
}
```

**`fs.read`** / **`fs.write`** — list filesystem paths the agent may access:

```json
"permissions": {
  "fs.read":  { "paths": ["./data"] },
  "fs.write": { "paths": ["./output"] }
}
```

All paths resolve relative to the repository root.

**`llm.chat`**, **`llm.completion`**, **`mcp.tool`** — no configuration; omit the entry or supply an empty object:

```json
"permissions": {
  "llm.chat": {}
}
```

### 6.4 Default Stance

The default permission stance is deny-all. An absent `permissions` block grants no resource access beyond what the AIVM implicitly provides. An absent sub-entry for a given capability means the capability is declared but unconstrained by config — the AIVM applies its own default scope for that capability.

---

## 7. Identity and Signing

### 7.1 Algorithm

All manifests in v0.1 are signed using **ES256** (ECDSA with P-256 and SHA-256), as specified in RFC 7518. No other algorithm is valid. The `identity.algorithm` field MUST equal `"ES256"`.

### 7.2 What Is Signed

The signed payload is the **canonical JSON form** of the manifest body, produced as follows:

1. Parse the manifest into a JSON value.
2. Remove the `identity.signature` field (the rest of the `identity` block is retained).
3. Recursively sort all object keys in lexicographic (Unicode code point) order.
4. Serialize with no whitespace.

The result is a deterministic byte sequence. The same manifest always produces the same canonical form.

### 7.3 JWS Construction

Sign the canonical bytes using ES256. Encode the result as JWS Compact Serialization (RFC 7515 §7.1):

```
BASE64URL(header) . BASE64URL(payload) . BASE64URL(signature)
```

- **header** — `{"alg":"ES256","kid":"<key_id>"}`, keys in lexicographic order, no whitespace.
- **payload** — the canonical manifest bytes.
- **signature** — the ECDSA signature over the payload.

Store the resulting compact serialization string in `identity.signature`.

### 7.4 AIVM Verification

The AIVM MUST perform the following steps in order before loading any agent. Failure at any step is a load failure; the agent MUST NOT execute.

1. Retrieve the public key for `identity.key_id` from the Purfle key registry.
2. Check the key's revocation status; reject if revoked. The AIVM MUST NOT cache revocation status between agent loads.
3. Reconstruct the canonical manifest body per §7.2.
4. Verify `identity.signature` over the canonical body using ES256 and the retrieved public key.
5. Verify that `identity.expires_at > current UTC time`.

### 7.5 Development Mode

Unsigned manifests (manifests with `identity.signature` absent or containing a placeholder value) MUST NOT be loaded by a conforming AIVM under normal operation.

An AIVM MAY support a `--dev` flag that permits loading unsigned manifests for local development and testing. When `--dev` is active:

- The AIVM MUST emit a prominent warning on every load indicating that signature verification is disabled.
- The `--dev` flag MUST NOT be available or recognized in production deployment configurations.

The specific mechanism for distinguishing development from production environments is TBD.

---

## 8. Lifecycle

### 8.1 Hooks

The `lifecycle` block declares .NET type hooks invoked by the AIVM at defined points in the agent's lifetime, and the error policy for unhandled exceptions.

```json
"lifecycle": {
  "on_load":   "MyAgent.Handlers.LoadHandler, MyAgent",
  "on_unload": "MyAgent.Handlers.UnloadHandler, MyAgent",
  "on_error":  "terminate"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `on_load` | string | no | .NET type invoked after manifest verification and sandbox construction, before first inference. |
| `on_unload` | string | no | .NET type invoked before the agent is unloaded (clean exit or AIVM shutdown). |
| `on_error` | string | yes | AIVM behavior when the agent raises an unhandled error. One of: `terminate`, `log`, `ignore`. |

### 8.2 When Hooks Fire

- **`on_load`** fires after the full load sequence completes (manifest verified, sandbox built, capabilities negotiated) and before the agent processes its first input.
- **`on_unload`** fires on clean shutdown. It is not guaranteed to fire on abrupt process termination or AIVM crash.

### 8.3 .NET Type String Format

Hook values use the .NET assembly-qualified type name format:

```
Namespace.TypeName, AssemblyName
```

Example: `MyAgent.Handlers.LoadHandler, MyAgent`

The AIVM resolves the type from the assemblies in the agent package's `lib/` directory, loaded into an isolated `AssemblyLoadContext`. The type MUST implement the interface expected by the AIVM for that hook (TBD — interface definitions are specified in the runtime SDK).

### 8.4 `on_error` Values

| Value | Behavior |
|---|---|
| `terminate` | Unload the agent immediately on unhandled error. |
| `log` | Log the error and continue running. |
| `ignore` | Suppress the error silently and continue running. |

---

## 9. Versioning

### 9.1 Fields

Two separate version fields appear in a manifest:

- **`purfle`** (top-level) — the spec version used when the manifest was authored. Example: `"0.1"`. Pattern: `\d+\.\d+`.
- **`runtime.requires`** — the minimum spec version the AIVM must implement to load this agent. Example: `"purfle/0.1"`. Pattern: `purfle/\d+\.\d+`.
- **`version`** (top-level) — the agent's own semantic version. Independent of the spec version.

### 9.2 Compatibility Rules

- An AIVM MUST reject a manifest where `runtime.requires` names a spec version greater than the AIVM implements.
- Minor version increments (`0.1` → `0.2`) are additive and backward-compatible by convention: new optional fields may be added; no existing required fields may be removed or renamed.
- Major version increments may introduce breaking changes. An AIVM implementing `0.x` is not required to load manifests targeting `1.x`.

### 9.3 Agent Versioning

The `version` field is a semantic version (`MAJOR.MINOR.PATCH`) for the agent itself. It is informational for the AIVM; the AIVM does not use it to make load decisions. Publishers are responsible for incrementing agent versions consistently.

---

## 10. Security Considerations

### 10.1 What This Specification Protects

- **Tamper detection.** The signature covers the canonical manifest body. Any modification to manifest content after signing invalidates the signature, and the AIVM MUST reject the manifest.
- **Identity attribution.** The `identity.key_id` ties a manifest to a registered publisher key. Revocation of that key invalidates all manifests signed with it.
- **Expiry enforcement.** `identity.expires_at` provides a time-bounded authorization window. Manifests cannot be used indefinitely after signing.
- **Capability confinement.** The AIVM enforces the declared capability set for the entire agent lifetime. Undeclared capabilities are inaccessible regardless of what agent code requests.
- **Permission scoping.** The permission block narrows each capability to a declared resource set. An agent with `network.outbound` can only reach the listed hosts. An agent with `env.read` can only read the listed variables.
- **Credential isolation.** API keys and credentials are owned by the AIVM runtime (Windows Credential Manager in phase 1). The manifest contains no credentials. The agent cannot access credentials outside its declared `env.read` scope.

### 10.2 What This Specification Does Not Protect

- **AIVM compromise.** If the AIVM process itself is compromised, manifest enforcement is moot. The security model assumes the AIVM is trusted.
- **Agent behavior within declared scope.** The manifest cannot constrain what an agent does within its permitted capabilities. An agent with `fs.write` permission to `./output` may write any content to that path.
- **Network content.** `network.outbound` restricts which hosts an agent may contact but does not inspect or filter the data transmitted.
- **Signature algorithm agility attacks.** Only `ES256` is valid in v0.1. Runtimes MUST NOT accept manifests declaring any other algorithm value.
- **Key registry availability.** If the Purfle key registry is unreachable, the AIVM cannot verify signatures. Caching strategies for offline operation are TBD.

### 10.3 Implementation Requirements

- Private key material MUST NOT appear in the manifest. Only `key_id` is present.
- Runtimes MUST NOT cache key revocation status between agent loads.
- Runtimes MUST NOT skip or bypass signature verification in any non-development mode.
- The `--dev` flag, if supported, MUST NOT be available in production runtime configurations.
- A permission entry that names a capability absent from `capabilities` is a manifest error. Runtimes MUST reject it.
