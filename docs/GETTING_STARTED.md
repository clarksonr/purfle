# Getting Started with Purfle

Build, sign, and run AI agents with verified identity and enforced sandboxing.

---

## Prerequisites

| Requirement | Version | Why |
|---|---|---|
| **Node.js** | 20+ | Runs the CLI and TypeScript SDK |
| **npm** | 10+ | Installs `@purfle/cli` |
| **.NET SDK** | 10.0+ | Runs the AIVM runtime (`purfle run`) |
| **API key** | Anthropic or Google | The LLM backend your agent calls |

Verify your setup:

```bash
node --version    # v20.x or higher
npm --version     # 10.x or higher
dotnet --version  # 10.x or higher
```

You will need at least one API key:

- **Anthropic** — get one at [console.anthropic.com](https://console.anthropic.com). Set it as `ANTHROPIC_API_KEY`.
- **Google Gemini** — get one at [aistudio.google.com](https://aistudio.google.com). Set it as `GEMINI_API_KEY`.

---

## 1. Install the CLI

```bash
npm install -g @purfle/cli
```

Verify it works:

```bash
purfle --help
```

If you are working from the repo source instead of a published package:

```bash
cd sdk
npm install
npm run build
npm link
```

---

## 2. Create Your First Agent

```bash
purfle init "My Agent"
```

This scaffolds a new directory:

```
my-agent/
  agent.json      # the manifest — your agent's contract
```

The generated `agent.json` looks like this:

```json
{
  "purfle": "0.1",
  "id": "a1b2c3d4-...",
  "name": "My Agent",
  "version": "0.1.0",
  "description": "My Agent agent.",
  "identity": {
    "author": "unsigned",
    "email": "unsigned@placeholder.local",
    "key_id": "unsigned",
    "algorithm": "ES256",
    "issued_at": "2026-04-01T00:00:00.000Z",
    "expires_at": "2027-04-01T00:00:00.000Z",
    "signature": ""
  },
  "capabilities": [],
  "permissions": {},
  "lifecycle": {
    "on_error": "terminate"
  },
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic"
  },
  "io": {
    "input":  { "type": "object", "properties": {}, "required": [] },
    "output": { "type": "object", "properties": {}, "required": [] }
  }
}
```

Use `--dir` to control the output directory:

```bash
purfle init "My Agent" --dir agents/my-agent
```

---

## 3. Edit the Manifest

Open `my-agent/agent.json` and make it yours. The key fields to change:

### `name` and `description`

Give your agent a clear name and purpose:

```json
{
  "name": "PR Summary Bot",
  "description": "Summarizes pull request changes into a Slack-friendly digest."
}
```

### `runtime.engine` and `runtime.model`

Pick your LLM backend:

```json
{
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic",
    "model": "claude-sonnet-4-20250514"
  }
}
```

Or use Gemini:

```json
{
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "gemini",
    "model": "gemini-2.5-flash"
  }
}
```

### `capabilities`

Declare what your agent needs. The runtime denies everything not listed here:

```json
{
  "capabilities": ["llm.chat", "network.outbound", "env.read"]
}
```

Phase 1 capability strings:

| Capability | What it grants |
|---|---|
| `llm.chat` | Conversational inference |
| `llm.completion` | Text completion inference |
| `network.outbound` | HTTP requests to allowed hosts |
| `env.read` | Read environment variables |
| `fs.read` | Read files at allowed paths |
| `fs.write` | Write files at allowed paths |
| `mcp.tool` | Call MCP tool servers |

### `permissions`

Scope each capability to specific resources:

```json
{
  "permissions": {
    "network": {
      "outbound": {
        "allow": ["api.github.com", "hooks.slack.com"]
      }
    },
    "env": {
      "read": ["ANTHROPIC_API_KEY", "GITHUB_TOKEN"]
    },
    "fs": {
      "read": ["./data/**/*.json"],
      "write": ["./output/**"]
    }
  }
}
```

If a capability is in `capabilities` but has no entry in `permissions`, the runtime grants the capability with no resource restrictions. If `permissions` lists specific resources, only those are allowed.

### `lifecycle`

Control error behavior:

```json
{
  "lifecycle": {
    "on_error": "terminate"
  }
}
```

Options: `"terminate"` (stop on error), `"log"` (log and continue), `"ignore"` (silent).

See [MANIFEST_REFERENCE.md](MANIFEST_REFERENCE.md) for the complete field reference.

---

## 4. Write a System Prompt

Create a `prompts/system.md` file in your agent directory:

```
my-agent/
  agent.json
  prompts/
    system.md
```

The system prompt tells the LLM how to behave. Write it in plain text or Markdown:

```markdown
You are PR Summary Bot.

Your job is to read pull request diffs and produce a concise summary
suitable for posting in a Slack channel.

Rules:
- Lead with a one-sentence overview of the change
- List affected files grouped by area (backend, frontend, tests, docs)
- Flag any breaking changes or security-sensitive modifications
- Keep the total summary under 300 words
```

The runtime passes this as the system message when invoking the LLM. Reference it from your agent code or pass it directly when calling `InvokeAsync`.

---

## 5. Test Locally

### Validate the manifest

```bash
purfle build my-agent
```

This checks your `agent.json` against the JSON Schema. On success:

```
  Build succeeded
  Name:         PR Summary Bot
  Version:      0.1.0
  Engine:       anthropic
  Capabilities: 3
```

On failure, you get specific field-level errors telling you what to fix.

### Simulate the agent

```bash
purfle simulate my-agent/agent.json
```

This loads the manifest, validates it, and runs a local simulation showing:
- Agent identity and metadata
- Declared capabilities and permissions
- Entrypoint execution (if a JS/TS entrypoint is defined)

---

## 6. Sign and Validate

Before publishing, your manifest must be cryptographically signed with ES256.

### Generate a key pair and sign

```bash
purfle sign my-agent --generate-key
```

This creates three things:
- `signing.key.pem` — your private key (keep this secret)
- `signing.pub.pem` — your public key (register this with the marketplace)
- Updates `agent.json` with the signature and key metadata

```
  Signed successfully
  Key ID:     a1b2c3d4-key
  Algorithm:  ES256
  Private:    signing.key.pem
  Public:     signing.pub.pem

  Register your public key before publishing.
```

### Sign with an existing key

```bash
purfle sign my-agent --key-file path/to/signing.key.pem --key-id my-key-id
```

### Validate the signed manifest

```bash
purfle build my-agent
```

Running `build` again after signing will validate both the schema and confirm the identity block is complete.

---

## 7. Run on the AIVM

The AIVM is the .NET runtime that enforces your manifest's sandbox. It loads the agent, verifies the signature, negotiates capabilities, and invokes the LLM.

### Run the demo host

```bash
dotnet run --project runtime/src/Purfle.Runtime.Host
```

This runs the full 7-step load sequence against `spec/examples/demo-agent.agent.json`:

1. **Parse** — deserialize the manifest
2. **Schema validation** — check against JSON Schema
3. **Identity verification** — verify JWS ES256 signature + check revocation
4. **Capability negotiation** — compare required vs available capabilities
5. **Permission binding** — construct the immutable sandbox
6. **I/O schema compilation** — validate input/output schemas
7. **Initialization** — create the engine adapter

To load your own agent:

```bash
dotnet run --project runtime/src/Purfle.Runtime.Host -- path/to/agent.json
```

Make sure the API key your manifest requires is set in your environment:

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
# or
export GEMINI_API_KEY="..."
```

### What happens at runtime

```
Loading agent: PR Summary Bot v0.1.0
  Engine:  anthropic (claude-sonnet-4-20250514)
  Author:  yourname
  Key ID:  a1b2c3d4-key
  Expires: 2027-04-01

  Identity verified
  Capabilities negotiated (3 required, 3 available)
  Sandbox bound (network: 2 hosts, env: 2 vars, fs: read 1 path)

  Agent loaded successfully.
```

If the signature is invalid, a key is revoked, or a required capability is missing, the load fails with a typed `LoadFailureReason` and the agent never runs.

---

## 8. Publish to the Marketplace

Once signed, you can publish your agent for others to install.

### Authenticate

```bash
purfle login --registry http://localhost:5000
```

This opens your browser for an OAuth2 PKCE flow. Credentials are saved to `~/.purfle/credentials.json`.

### Publish

```bash
purfle publish my-agent --register-key signing.pub.pem --registry http://localhost:5000
```

The `--register-key` flag registers your public key with the marketplace on first publish. Subsequent publishes of agents signed with the same key do not need it.

### Search and install

```bash
# Search
purfle search "summary bot" --registry http://localhost:5000

# Install
purfle install <agent-id> --registry http://localhost:5000
```

Installed agents are saved to `~/.purfle/agents/<agent-id>/agent.json`.

---

## Quick Reference

| Task | Command |
|---|---|
| Scaffold a new agent | `purfle init "Agent Name"` |
| Validate the manifest | `purfle build <dir>` |
| Sign the manifest | `purfle sign <dir> --generate-key` |
| Simulate locally | `purfle simulate <manifest>` |
| Run on the AIVM | `dotnet run --project runtime/src/Purfle.Runtime.Host -- <manifest>` |
| Log in to marketplace | `purfle login --registry <url>` |
| Publish | `purfle publish <dir> --register-key signing.pub.pem --registry <url>` |
| Search marketplace | `purfle search "query" --registry <url>` |
| Install an agent | `purfle install <agent-id> --registry <url>` |

---

## Polyglot Agents

Purfle agents can be implemented in both C# and TypeScript. Each agent in the `agents/` directory contains two implementations side by side:

```
agents/news-digest/
  manifest.agent.json   # the signed manifest
  prompts/
    system.md           # LLM system prompt
  csharp/               # C# implementation
    NewsDigest.csproj
    Program.cs
  typescript/           # TypeScript implementation
    package.json
    src/index.ts
```

### Running a C# agent

```bash
cd agents/news-digest/csharp
dotnet run
```

The C# implementation uses the Purfle runtime libraries directly. It builds with `dotnet build` and runs with `dotnet run`. Make sure the required API key is set in your environment before running.

### Running a TypeScript agent

```bash
cd agents/news-digest/typescript
npm install
npm start
```

The TypeScript implementation connects to its corresponding MCP server for tool access. It builds with `npm run build` and runs with `npm start`.

### Available agents

| Agent | Description |
|---|---|
| `api-guardian` | Monitors API endpoints for uptime and changes |
| `cli-generator` | Generates CLI tools from natural language specs |
| `code-reviewer` | Reviews code changes and suggests improvements |
| `db-assistant` | Helps with database queries and schema exploration |
| `email-priority` | Prioritizes and summarizes email messages |
| `file-assistant` | Reads, lists, searches, and summarizes files |
| `meeting-assistant` | Prepares meeting agendas and summarizes notes |
| `news-digest` | Curates daily news digests from configured sources |
| `purfle-pet` | A virtual pet agent (fun demo) |
| `research-assistant` | Conducts research and compiles findings |

Each agent has a corresponding MCP server in `tools/` that provides its specialized tools (e.g., `tools/mcp-news/` for `news-digest`).

---

## Dashboard

The Purfle Dashboard is an ASP.NET Core web API that provides a centralized view of all running agents, their status, and logs.

### Running the dashboard

```bash
dotnet run --project dashboard/src/Purfle.Dashboard.Api
```

The dashboard API starts on `https://localhost:5001` by default. It exposes endpoints for:

- Viewing all registered agents and their current status
- Checking run history and logs
- Monitoring agent health

---

## Next Steps

- Read the [Manifest Reference](MANIFEST_REFERENCE.md) for field-by-field documentation
- See [spec/SPEC.md](../spec/SPEC.md) for the full specification
- Check [AGENT-BUILD-AND-PUBLISH.md](AGENT-BUILD-AND-PUBLISH.md) for building `.purfle` bundles
- Read [CONTRIBUTING.md](../CONTRIBUTING.md) if you want to contribute
