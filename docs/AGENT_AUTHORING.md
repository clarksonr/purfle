# Agent Authoring Guide

A complete walkthrough from zero to published Purfle agent.

---

## Introduction

A Purfle agent is a signed package that runs inside the AIVM (AI Virtual Machine). The AIVM is a C# runtime embedded in the Purfle desktop app. It loads your agent's manifest, enforces the capabilities and permissions you declared, provides LLM inference through the engine adapter you specified, and writes output to a sandboxed local path.

**The AIVM guards the hen house.** Your agent's LLM proposes actions. The AIVM decides whether to allow them and executes on the agent's behalf. Your agent never touches the filesystem, network, or system directly. Everything goes through the AIVM sandbox.

An agent package looks like this:

```
my-agent.purfle/
+-- agent.manifest.json     <- signed, declares everything
+-- lib/
|   +-- MyAgent.dll         <- .NET assembly, loaded into isolated AssemblyLoadContext
+-- prompts/
|   +-- system.md           <- instruction file for the LLM
+-- assets/                 <- optional embedded resources
```

The manifest is the contract. It declares what the agent can do, what engine it uses, what tools it needs, when it runs, and who authored it. The AIVM reads the manifest and enforces every declaration. If the manifest does not declare a capability, the agent cannot use it.

---

## Step 1 — Initialize

Run the CLI to scaffold a new agent:

```bash
purfle init my-agent
```

This creates the following directory structure:

```
my-agent.purfle/
+-- agent.manifest.json
+-- prompts/
|   +-- system.md
+-- lib/
+-- assets/
```

**File roles:**

| File | Purpose |
|---|---|
| `agent.manifest.json` | The signed contract. Declares identity, capabilities, permissions, schedule, engine, tools, and lifecycle hooks. The AIVM reads this at load time to decide what the agent is allowed to do. |
| `prompts/system.md` | The system prompt sent to the LLM on every invocation. This is where you define the agent's personality, instructions, and output format. |
| `lib/` | Contains .NET assemblies if your agent has custom code. For prompt-only agents, this stays empty. |
| `assets/` | Optional. Static files your agent needs (templates, reference data, etc.). |

---

## Step 2 — Write the Manifest

The manifest is the most important file in your agent package. Every field matters.

Here is a complete manifest for a "Daily Quote" agent that fetches an inspirational quote every morning at 08:00:

```json
{
  "purfle": "0.1",
  "id": "d4e5f6a7-1234-5678-9abc-def012345678",
  "name": "Daily Quote",
  "version": "1.0.0",
  "description": "Fetches an inspirational quote every morning at 08:00",

  "identity": {
    "author": "com.example",
    "email": "developer@example.com",
    "key_id": "com.example/release-2026",
    "algorithm": "ES256",
    "issued_at": "2026-04-01T00:00:00Z",
    "expires_at": "2027-04-01T00:00:00Z"
  },

  "schedule": {
    "trigger": "cron",
    "cron": "0 8 * * *"
  },

  "capabilities": [
    "llm.chat",
    "network.outbound",
    "fs.write"
  ],

  "permissions": {
    "network.outbound": {
      "hosts": ["api.quotable.io"]
    },
    "fs.write": {
      "paths": ["./output"]
    }
  },

  "runtime": {
    "requires": "purfle/0.1",
    "engine": "gemini",
    "model": "gemini-2.0-flash",
    "max_tokens": 512
  },

  "lifecycle": {
    "on_error": "log"
  },

  "tools": [],
  "io": {}
}
```

### Field-by-field walkthrough

**Top-level fields:**

| Field | Value | Notes |
|---|---|---|
| `purfle` | `"0.1"` | The manifest spec version. The AIVM rejects manifests requiring a higher version than it implements. |
| `id` | UUID | A unique identifier for this agent. Generate one with `uuidgen` or any UUID tool. |
| `name` | `"Daily Quote"` | Display name shown in the desktop app's agent card. |
| `version` | `"1.0.0"` | Semantic versioning. Increment on every publish. |
| `description` | string | One-line summary. Shown in the marketplace and agent card. |

**Identity block:**

The `identity` block declares who authored the manifest. At signing time, the CLI fills in the `signature` field with a JWS compact serialization (see Step 5). You fill in everything else.

| Field | Purpose |
|---|---|
| `author` | Reverse-domain publisher identifier (e.g., `com.example`). |
| `email` | Contact email for the publisher. |
| `key_id` | The signing key identifier registered in the Purfle key registry. |
| `algorithm` | Always `ES256`. This is locked and cannot be changed. |
| `issued_at` | ISO 8601 timestamp when the manifest was authored. |
| `expires_at` | ISO 8601 timestamp when the manifest expires. The AIVM rejects expired manifests. |

**Schedule block:**

The `trigger` field determines when the agent runs. Available trigger types:

| Trigger | Required fields | Behavior |
|---|---|---|
| `interval` | `interval_minutes` | Runs every N minutes, indefinitely. |
| `cron` | `cron` | Runs on an NCrontab expression (e.g., `0 8 * * *` = daily at 08:00). |
| `startup` | none | Runs once when the AIVM starts. |
| `window` | `window.start`, `window.end`, `window.run_at` | Runs relative to a declared time window. |
| `event` | `event.source`, `event.topic` | Runs when an MCP server emits a named event. |

For the daily-quote agent, `cron` with `0 8 * * *` fires once per day at 08:00.

**Capabilities and permissions:**

Capabilities declare what the agent is allowed to do. Permissions provide the configuration for capabilities that need it.

The daily-quote agent declares three capabilities:

| Capability | Why needed | Permission config |
|---|---|---|
| `llm.chat` | The agent uses conversational LLM inference to format the quote. | None required. |
| `network.outbound` | The agent calls `api.quotable.io` to fetch a quote. | `hosts: ["api.quotable.io"]` — only this host is allowed. |
| `fs.write` | The agent writes the formatted quote to its output directory. | `paths: ["./output"]` — only this directory is writable. |

**What breaks without them:**

- Remove `llm.chat` and the AIVM will not provide inference. The agent cannot talk to the LLM.
- Remove `network.outbound` and the sandbox blocks all outbound HTTP. The API call fails.
- Remove the `hosts` permission and the AIVM has no allow-list. The network call is denied.
- Remove `fs.write` and the agent cannot write output. The file write is blocked by the sandbox.
- Add a host not in the `hosts` list and the AIVM blocks the request. Only declared hosts are reachable.

The rule is simple: if it is not declared, it is denied. The AIVM defaults to deny on everything.

**Runtime block:**

| Field | Purpose |
|---|---|
| `requires` | The minimum Purfle runtime version. Currently `purfle/0.1`. |
| `engine` | Which LLM engine adapter to use. See Step 4 for options. |
| `model` | The specific model string for the chosen engine. |
| `max_tokens` | Maximum tokens per LLM response. |

**Lifecycle block:**

| Field | Values | Meaning |
|---|---|---|
| `on_error` | `terminate`, `log`, `ignore` | What the AIVM does when the agent encounters an error. `log` writes the error to `run.log` and continues. `terminate` stops the agent. `ignore` suppresses the error silently. |

**Tools array:**

Declare MCP tools your agent needs. Each entry names a tool, the MCP server URL that provides it, and a description. The AIVM wires the tools to the LLM at load time. For the daily-quote agent, no tools are needed — the LLM handles everything through inference.

**IO block:**

Currently optional with no enforcement. Used for cross-agent output sharing (see RFC 0003).

---

## Step 3 — Write the System Prompt

The system prompt lives in `prompts/system.md`. This file is sent to the LLM as the system message on every invocation. It defines what the agent does, how it behaves, and what format its output should take.

Here is the system prompt for the daily-quote agent:

```markdown
# Daily Quote Agent

You are a daily quote agent. Your job is to fetch an inspirational quote
and write it to a file.

## Instructions

1. Call the quotable.io API to fetch a random quote.
2. Format the quote with the author's name.
3. Write the result to a file named `quote-YYYY-MM-DD.md` in the output directory.

## Output format

Write a short Markdown file:

```
# Quote of the Day — {date}

> "{quote text}"
>
> — {author}
```

Do not add commentary. Do not editorialize. Just the quote, the author, and the date.
```

### Good prompt practices

- **Be specific.** Tell the LLM exactly what to do, in what order, and in what format.
- **Name the output file.** The AIVM writes to the agent's sandboxed output directory. Your prompt should specify the filename so output is predictable and parseable.
- **Constrain behavior.** If the agent should not do something, say so explicitly. LLMs follow negative instructions better than implied constraints.
- **Keep it short.** Every token in the system prompt costs inference time and money. Say what is necessary and stop.
- **Do not include API keys, secrets, or credentials.** The AIVM handles credentials. The prompt should never reference them.

---

## Step 4 — Choose the Engine

The `runtime.engine` field in the manifest tells the AIVM which LLM adapter to use. The AIVM is engine-agnostic — it supports four engines and selects the correct adapter at load time based on the manifest.

| Engine | `runtime.engine` | Example model | API key env var |
|---|---|---|---|
| Google Gemini | `gemini` | `gemini-2.0-flash` | `GEMINI_API_KEY` |
| Anthropic | `anthropic` | `claude-sonnet-4-20250514` | `ANTHROPIC_API_KEY` |
| OpenAI | `openai` | `gpt-4o` | `OPENAI_API_KEY` |
| Ollama (local) | `ollama` | `llama3` | none (localhost:11434) |

**How it works:**

1. The AIVM reads `runtime.engine` from your manifest.
2. It selects the corresponding adapter (GeminiAdapter, AnthropicAdapter, OpenClawAdapter, OllamaAdapter).
3. The adapter handles authentication, request formatting, and response parsing for that engine.
4. Your agent never interacts with the engine directly. The AIVM manages inference on the agent's behalf.

**Choosing an engine:**

- Use `gemini` for cost-effective, fast inference with good general capabilities.
- Use `anthropic` for tasks that benefit from strong reasoning and instruction following.
- Use `openai` for compatibility with OpenAI-specific model features.
- Use `ollama` for fully local, offline inference with no API key and no network dependency.

The engine choice is entirely yours. The AIVM enforces no preference. Set `runtime.engine` and `runtime.model` in your manifest and the AIVM handles the rest.

---

## Step 5 — Build and Sign

### Build

```bash
purfle build my-agent
```

The build step validates the manifest against the JSON schema, checks that all referenced files exist, and prepares the package structure.

### Sign

```bash
purfle sign my-agent
```

Signing does the following:

1. Serializes the manifest to canonical JSON (keys sorted, no whitespace, `signature` field omitted).
2. Signs the canonical JSON with your ES256 private key (ECDSA P-256 / SHA-256).
3. Produces a JWS Compact Serialization and writes it to `identity.signature` in the manifest.

After signing, the manifest is tamper-evident. Any modification to any field invalidates the signature. The AIVM verifies the signature at load time using the public key registered in the Purfle key registry.

**Prerequisites for signing:**

- You must have an EC P-256 private key on your machine (generated during `purfle setup`).
- Your corresponding public key must be registered in the Purfle key registry (a one-time operation — see `purfle setup`).
- The `identity.key_id` in your manifest must match the registered key.

---

## Step 6 — Simulate

Before publishing, simulate the agent locally:

```bash
purfle simulate my-agent --trigger startup
```

The `--trigger startup` flag runs the agent immediately rather than waiting for the cron schedule. This is useful for testing.

Other simulation options:

```bash
# Simulate as if triggered by a window opening
purfle simulate my-agent --trigger window_open

# Simulate as if triggered by an event
purfle simulate my-agent --trigger event
```

**What to check:**

1. The agent runs without errors.
2. Output files appear in the agent's output directory (`<app-data>/aivm/output/<agent-id>/`).
3. The output format matches what your system prompt specified.
4. The `run.log` and `run.jsonl` files show the expected execution flow.

If the simulation fails, check the `run.log` for error details. Common issues are covered in the Troubleshooting section below.

---

## Step 7 — Security Scan

Run the security scanner to catch common issues before publishing:

```bash
purfle security-scan my-agent
```

The security scan checks for:

- **Overly broad permissions.** Wildcards in `hosts`, `paths`, or `vars` that grant more access than the agent needs.
- **Missing capabilities.** Tools or behaviors in the agent code that require a capability not declared in the manifest.
- **Unused capabilities.** Declared capabilities that the agent does not appear to use. These bloat the trust surface.
- **Credential leaks.** API keys, tokens, or secrets embedded in the manifest, prompts, or assets.
- **Expired or unsigned manifests.** The `expires_at` field is in the past or the `signature` field is missing.
- **Path traversal risks.** Permission paths that could allow escaping the sandbox.

Fix all issues reported by the scan before publishing. The marketplace may reject packages that fail the security scan.

---

## Step 8 — Pack and Publish

### Pack

```bash
purfle pack my-agent
```

Packing produces two files:

- `my-agent-1.0.0.purfle` — the agent bundle (a compressed archive of the package directory).
- `my-agent-1.0.0.purfle.sha256` — the SHA-256 hash sidecar for integrity verification.

The SHA-256 sidecar allows consumers to verify that the bundle they downloaded matches what you published. The marketplace stores the hash and verifies it on upload.

### Publish

```bash
purfle publish my-agent
```

Publishing uploads the bundle to the Purfle marketplace. The marketplace:

1. Validates the manifest signature.
2. Verifies the public key against the key registry.
3. Stores the bundle with its SHA-256 hash.
4. Makes the agent discoverable via `purfle search`.

You must be logged in (`purfle login`) and have a registered signing key before publishing.

---

## Step 9 — Verify from the Consumer Side

After publishing, verify that your agent is discoverable and installable:

### Search

```bash
purfle search "daily quote"
```

This queries the marketplace and returns matching agents with their name, version, author, and description.

### Install

```bash
purfle install daily-quote
```

The install command:

1. Downloads the bundle from the marketplace.
2. Verifies the SHA-256 hash against the sidecar.
3. Verifies the manifest signature against the registered public key.
4. Extracts the package to the local agent directory.
5. The agent appears in the desktop app's dashboard and runs on its declared schedule.

If the hash or signature verification fails, the install is aborted and the downloaded bundle is deleted.

---

## Troubleshooting

### Signature mismatch

**Symptom:** The AIVM rejects the manifest with `LoadFailureReason.SignatureInvalid`.

**Cause:** The manifest was modified after signing. Any change to any field — even whitespace — invalidates the signature.

**Fix:** Re-sign the manifest with `purfle sign my-agent`. Do not edit the manifest after signing.

---

### Capability denied

**Symptom:** The agent fails at runtime with a capability error. The `run.log` shows a denied operation.

**Cause:** The agent attempted an operation that requires a capability not declared in the manifest.

**Fix:** Add the missing capability to the `capabilities` array. If the capability requires permissions (e.g., `network.outbound` requires `hosts`), add the corresponding `permissions` entry as well.

---

### Output not written

**Symptom:** The agent runs successfully but no output file appears.

**Causes:**
1. The agent does not have `fs.write` in its capabilities.
2. The `fs.write` permissions do not include the path the agent is trying to write to.
3. The LLM did not generate a write instruction (check your system prompt).
4. The output path in the prompt does not match the permission path.

**Fix:** Verify that `fs.write` is in `capabilities`, that `permissions.fs.write.paths` includes the correct directory, and that your system prompt instructs the LLM to write to a matching path.

---

### Agent never runs

**Symptom:** The agent appears in the dashboard but never executes.

**Causes:**
1. The cron expression is wrong. `0 8 * * *` means 08:00 UTC. If you expected local time, adjust the expression.
2. The schedule trigger type does not match the schedule fields. A `cron` trigger requires the `cron` field; an `interval` trigger requires `interval_minutes`.
3. The manifest has expired (`expires_at` is in the past).
4. The AIVM is not running. The desktop app must be open for agents to execute.

**Fix:** Check the cron expression with an online cron validator. Verify the trigger type matches the schedule fields. Check `expires_at`. Ensure the desktop app is running.

---

### LLM returns empty or unexpected output

**Symptom:** The agent runs but the output is empty, garbled, or unrelated to the prompt.

**Causes:**
1. `max_tokens` is too low for the expected output.
2. The system prompt is ambiguous or too vague.
3. The model string is wrong for the chosen engine.

**Fix:** Increase `max_tokens`. Revise the system prompt to be more specific. Verify the model string matches a valid model for the declared engine (see the engine table in Step 4).

---

### API key not found

**Symptom:** The agent fails with an authentication error from the LLM provider.

**Cause:** The API key environment variable for the chosen engine is not set.

**Fix:** Set the appropriate environment variable:

```bash
export GEMINI_API_KEY=your-key       # for engine: gemini
export ANTHROPIC_API_KEY=your-key    # for engine: anthropic
export OPENAI_API_KEY=your-key       # for engine: openai
# Ollama does not require an API key
```

The AIVM reads credentials from the system credential store (Windows Credential Manager on Windows, Keychain on macOS). For development, environment variables are also supported.
