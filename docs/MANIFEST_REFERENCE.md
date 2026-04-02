# Manifest Reference

Field-by-field reference for `agent.json` — the Purfle Agent Manifest.

The manifest is a JSON file conforming to [agent.manifest.schema.json](../spec/schema/agent.manifest.schema.json) (JSON Schema Draft 2020-12). It is the single source of truth for an agent's identity, capabilities, permissions, and runtime configuration.

---

## Top-Level Fields

### `purfle` (required)

Spec version this manifest targets.

```json
{ "purfle": "0.1" }
```

- **Type:** string
- **Pattern:** `^\d+\.\d+$`
- **Current value:** `"0.1"`

The runtime uses this to determine which schema version to validate against. Bump this only when the spec itself changes, not when your agent changes.

---

### `id` (required)

Globally unique identifier for this agent.

```json
{ "id": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d" }
```

- **Type:** string (UUID v4)
- **Pattern:** `^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$`

Generated automatically by `purfle init`. Do not reuse IDs across different agents. A new version of the same agent keeps the same ID — the `version` field distinguishes releases.

---

### `name` (required)

Human-readable agent name.

```json
{ "name": "PR Summary Bot" }
```

- **Type:** string
- **Min length:** 1
- **Max length:** 128

Displayed in the marketplace, desktop app, and CLI output.

---

### `version` (required)

Semantic version of this agent release.

```json
{ "version": "1.2.0" }
```

- **Type:** string
- **Pattern:** `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$`

Follows [semver](https://semver.org). The marketplace uses `id` + `version` as the unique key for a published release. Pre-release and build metadata suffixes are allowed.

---

### `description` (optional)

Short explanation of what the agent does.

```json
{ "description": "Summarizes pull request diffs into Slack-friendly digests." }
```

- **Type:** string
- **Max length:** 1024

Shown in marketplace search results and agent detail pages.

---

## Identity Block

### `identity` (required)

Cryptographic identity — who signed this manifest and when.

```json
{
  "identity": {
    "author": "clarksonr",
    "email": "roman@example.com",
    "key_id": "com.clarksonr/release-2026",
    "algorithm": "ES256",
    "issued_at": "2026-04-01T00:00:00.000Z",
    "expires_at": "2027-04-01T00:00:00.000Z",
    "signature": "eyJhbGciOiJFUzI1NiJ9.eyJpZCI6Ii..."
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `author` | string (1–256 chars) | Yes | Publisher identifier — reverse-domain or username |
| `email` | string (email format) | Yes | Publisher contact email |
| `key_id` | string (min 1 char) | Yes | ID of the signing key registered in the Purfle key registry |
| `algorithm` | const `"ES256"` | Yes | Always `"ES256"` (ECDSA P-256 / SHA-256). Locked — no other value accepted |
| `issued_at` | string (date-time) | Yes | ISO 8601 timestamp when the manifest was signed |
| `expires_at` | string (date-time) | Yes | ISO 8601 timestamp after which the signature is invalid |
| `signature` | string | No | JWS Compact Serialization (`header.payload.signature`). Absent during authoring, added by `purfle sign` |

**Signature pattern:** `^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+$`

**How signing works:**

1. The `signature` field is removed from the manifest
2. The remaining JSON is serialized to canonical form (sorted keys, no whitespace)
3. The canonical bytes are signed with ES256 (ECDSA P-256)
4. The result is a JWS Compact Serialization string written back to `signature`

**At load time the runtime:**

1. Strips `signature` from the manifest and re-canonicalizes
2. Fetches the public key from the key registry using `key_id`
3. Checks the key is not revoked
4. Verifies the JWS signature against the public key
5. Checks `expires_at` has not passed

Failure at any step produces a typed `LoadFailureReason` and stops the load sequence.

---

## Capabilities

### `capabilities` (required)

Array of capability identifiers the agent requires.

```json
{ "capabilities": ["llm.chat", "network.outbound", "env.read", "fs.read"] }
```

- **Type:** array of strings
- **Unique items:** yes
- **Min items:** 0

**Phase 1 capability strings (enum):**

| String | Description |
|---|---|
| `llm.chat` | Conversational inference (multi-turn) |
| `llm.completion` | Single-turn text completion |
| `network.outbound` | HTTP/HTTPS requests to external hosts |
| `env.read` | Read environment variables |
| `fs.read` | Read files from the local filesystem |
| `fs.write` | Write files to the local filesystem |
| `mcp.tool` | Call external MCP tool servers |

**Enforcement rule:** Declared = permitted, Undeclared = blocked. The runtime denies any operation whose capability is not in this array. An agent with `"capabilities": ["llm.chat"]` cannot read files, make network calls, or access environment variables — even if `permissions` entries exist for those resources.

**Capability negotiation:** At load time, the runtime compares this list against its own declared capability set. If a required capability is missing from the runtime, the agent fails to load with `LoadFailureReason.CapabilityMissing`.

---

## Permissions

### `permissions` (optional)

Per-capability resource restrictions. Keys are capability strings; each key must appear in `capabilities[]`.

```json
{
  "permissions": {
    "network.outbound": { "hosts": ["api.github.com", "hooks.slack.com"] },
    "env.read":         { "vars": ["ANTHROPIC_API_KEY", "GITHUB_TOKEN"] },
    "fs.read":          { "paths": ["./data"] },
    "fs.write":         { "paths": ["./output"] }
  }
}
```

- **Type:** object
- **Additional properties:** not allowed — only valid capability keys accepted

If `permissions` is omitted or empty, capabilities are granted without resource restrictions (the capability itself is still required in `capabilities`).

#### `network.outbound`

Controls outbound HTTP access.

| Field | Type | Required | Description |
|---|---|---|---|
| `hosts` | array of strings | Yes | Hostnames the agent may connect to (min 1 item) |

```json
{
  "network.outbound": {
    "hosts": ["api.github.com", "hooks.slack.com"]
  }
}
```

The sandbox blocks any HTTP request to a host not in the list.

#### `env.read`

Controls environment variable access.

| Field | Type | Required | Description |
|---|---|---|---|
| `vars` | array of strings | Yes | Exact variable names the agent can read (min 1 item) |

```json
{
  "env.read": {
    "vars": ["ANTHROPIC_API_KEY", "DATABASE_URL"]
  }
}
```

Exact match only — no wildcards.

#### `fs.read`

Controls filesystem read access.

| Field | Type | Required | Description |
|---|---|---|---|
| `paths` | array of strings | Yes | Paths the agent may read from (min 1 item) |

```json
{
  "fs.read": {
    "paths": ["./data", "./config"]
  }
}
```

#### `fs.write`

Controls filesystem write access.

| Field | Type | Required | Description |
|---|---|---|---|
| `paths` | array of strings | Yes | Paths the agent may write to (min 1 item) |

```json
{
  "fs.write": {
    "paths": ["./output"]
  }
}
```

#### `llm.chat`, `llm.completion`, `mcp.tool`

These capabilities accept an empty permission config (no fields). Including them in `permissions` is valid but has no effect — the capability alone is sufficient.

```json
{
  "llm.chat": {},
  "mcp.tool": {}
}
```

---

## Schedule

### `schedule` (optional)

Defines when the agent should run automatically.

```json
{
  "schedule": {
    "trigger": "interval",
    "interval_minutes": 15
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `trigger` | string enum | Yes | `"interval"`, `"cron"`, or `"startup"` |
| `interval_minutes` | integer (min 1) | Conditional | Required when trigger is `"interval"` |
| `cron` | string (min 1 char) | Conditional | Required when trigger is `"cron"`. Standard 5-field cron expression |

- **Additional properties:** not allowed

**Trigger types:**

| Trigger | Behavior | Required field |
|---|---|---|
| `interval` | Runs every N minutes | `interval_minutes` |
| `cron` | Runs on a cron schedule | `cron` |
| `startup` | Runs once when the AIVM starts | none |

**Examples:**

```json
{ "schedule": { "trigger": "interval", "interval_minutes": 15 } }
```

```json
{ "schedule": { "trigger": "cron", "cron": "0 7 * * *" } }
```

```json
{ "schedule": { "trigger": "startup" } }
```

---

## Runtime

### `runtime` (required)

Specifies the inference engine and model.

```json
{
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic",
    "model": "claude-sonnet-4-20250514",
    "max_tokens": 4096
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requires` | string | Yes | Minimum Purfle runtime version. Pattern: `^purfle/\d+\.\d+$` |
| `engine` | string enum | Yes | Inference adapter to use |
| `model` | string | No | Model identifier passed to the engine adapter |
| `max_tokens` | integer (min 1) | No | Maximum tokens the model may generate per turn |

- **Additional properties:** not allowed

**Engine values:**

| Engine | Default model | API key env var |
|---|---|---|
| `anthropic` | `claude-sonnet-4-20250514` | `ANTHROPIC_API_KEY` |
| `gemini` | `gemini-2.5-flash` | `GEMINI_API_KEY` |
| `openai-compatible` | — | varies |
| `openclaw` | — | — |
| `ollama` | — | — |

---

## Lifecycle

### `lifecycle` (optional)

Controls agent startup, shutdown, and error behavior.

```json
{
  "lifecycle": {
    "on_load": "Purfle.Agents.Chat.ChatAgent, Purfle.Agents.Chat",
    "on_unload": "Purfle.Agents.Chat.ChatAgent, Purfle.Agents.Chat",
    "on_error": "terminate"
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `on_load` | string | No | .NET assembly-qualified type name invoked after the agent is loaded |
| `on_unload` | string | No | .NET assembly-qualified type name invoked before the agent is unloaded |
| `on_error` | string enum | Yes | Error policy: `"terminate"`, `"log"`, or `"ignore"` |

- **Additional properties:** not allowed

**`on_error` values:**

| Value | Behavior |
|---|---|
| `terminate` | Stop the agent immediately on error |
| `log` | Log the error and continue |
| `ignore` | Silently continue |

---

## Tools

### `tools` (optional)

MCP tool bindings — external tool servers the agent can call.

```json
{
  "tools": [
    {
      "name": "filesystem",
      "server": "npx @modelcontextprotocol/server-filesystem",
      "description": "Read and write files"
    }
  ]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string (min 1 char) | Yes | Tool server identifier, as exposed to the LLM |
| `server` | string | Yes | MCP server URL or command (stdio transport) |
| `description` | string | No | Human-readable purpose, forwarded to the LLM tool schema |

- **Additional properties:** not allowed

The AIVM connects to each listed MCP server, discovers its tools, and filters them through `permissions` before advertising them to the LLM. Requires `mcp.tool` in `capabilities`.

---

## I/O Schema

### `io` (optional)

Defines the shape of agent input and output for validation.

```json
{
  "io": {
    "input": {
      "type": "object",
      "properties": {
        "message": { "type": "string", "description": "User message" }
      },
      "required": ["message"]
    },
    "output": {
      "type": "object",
      "properties": {
        "response": { "type": "string", "description": "Agent reply" }
      },
      "required": ["response"]
    }
  }
}
```

- **Type:** object (no enforced structure in phase 1)

Both `input` and `output` are JSON Schema objects. The runtime can use these to validate data at invocation boundaries. No enforcement in v0.1 — this is a forward declaration.

---

## Schema Validation Rules

The JSON Schema enforces these conditional constraints via `allOf` / `if-then`:

| If `permissions` contains... | Then `capabilities` must include... |
|---|---|
| `llm.chat` | `"llm.chat"` |
| `llm.completion` | `"llm.completion"` |
| `network.outbound` | `"network.outbound"` |
| `env.read` | `"env.read"` |
| `fs.read` | `"fs.read"` |
| `fs.write` | `"fs.write"` |
| `mcp.tool` | `"mcp.tool"` |

You cannot have a permissions entry without a matching capability. You can have a capability without a permissions entry.

Validate anytime with:

```bash
purfle build <dir>
```

---

## Complete Example

A full manifest using all sections:

```json
{
  "purfle": "0.1",
  "id": "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
  "name": "Email Monitor",
  "version": "2.1.0",
  "description": "Monitors Gmail for new messages and writes daily summaries.",
  "identity": {
    "author": "acme-corp",
    "email": "platform@acme.com",
    "key_id": "acme-prod-2026",
    "algorithm": "ES256",
    "issued_at": "2026-03-15T00:00:00.000Z",
    "expires_at": "2027-03-15T00:00:00.000Z",
    "signature": "eyJhbGciOiJFUzI1NiJ9..."
  },
  "capabilities": [
    "llm.chat",
    "network.outbound",
    "env.read",
    "fs.write",
    "mcp.tool"
  ],
  "permissions": {
    "network.outbound": { "hosts": ["gmail.googleapis.com", "hooks.slack.com"] },
    "env.read":         { "vars": ["ANTHROPIC_API_KEY", "GMAIL_TOKEN", "SLACK_WEBHOOK"] },
    "fs.write":         { "paths": ["./reports"] }
  },
  "schedule": {
    "trigger": "interval",
    "interval_minutes": 15
  },
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic",
    "model": "claude-sonnet-4-20250514",
    "max_tokens": 1000
  },
  "lifecycle": {
    "on_error": "log"
  },
  "tools": [
    {
      "name": "filesystem",
      "server": "npx @modelcontextprotocol/server-filesystem",
      "description": "Write report files"
    }
  ],
  "io": {
    "input":  { "type": "object", "properties": {} },
    "output": { "type": "object", "properties": { "summary": { "type": "string" } } }
  }
}
```
