[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

# Purfle

A desktop runtime that installs, schedules, and sandboxes AI agents on your machine.

Purfle runs persistently on Windows and macOS. You install agents — each defined by a signed
manifest — and the AIVM (AI Virtual Machine) runs them on a schedule, unattended. Every agent
is sandboxed: the runtime enforces what it can access, which LLM engine it uses, and where it
writes output. You see one card per agent in the desktop UI. Agents run in the background.
The AIVM guards the hen house.

---

## Architecture

```
┌─────────────────────────────────────────────┐
│      .NET MAUI DESKTOP APP                  │
│      (MSIX on Windows / .pkg on macOS)      │
│  ┌──────────────────────────────────────┐   │
│  │              AIVM (C#)               │   │
│  │  Scheduler                           │   │
│  │  ├── AgentRunner: email-monitor      │   │
│  │  ├── AgentRunner: pr-watcher         │   │
│  │  └── AgentRunner: report-builder     │   │
│  └──────────────────────────────────────┘   │
│  UI: one card per agent                     │
└──────────────┬──────────────────────────────┘
               │ talks to
┌──────────────▼──────────────────────────────┐
│           AZURE (live services)              │
│  Key Registry   — Functions (consumption)   │
│  Marketplace    — App Service (F1 free)     │
│  IdentityHub.Web — App Service (F1 free)    │
└─────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  purfle demo  — starts these locally        │
│  mcp-file-server :8100                      │
│  mcp-gmail       :8102                      │
│  mcp-github      :8111                      │
└─────────────────────────────────────────────┘
```

---

## Features

| Feature | Description |
|---------|-------------|
| Multi-engine LLM | Gemini, Anthropic, OpenAI, Ollama — engine declared per agent |
| Signed manifests | JWS with ES256 — every agent is cryptographically signed |
| 5 trigger types | interval, cron, startup, window, event (SSE) |
| MCP tool protocol | Agents call tools through MCP servers wired by the AIVM |
| Sandboxed execution | Network, filesystem, env vars — enforced per manifest |
| Windows + macOS | .NET MAUI — one codebase, two platforms |
| Marketplace | Publish, search, and install agents from a central registry |
| Engine-agnostic | No hardcoded engine — the manifest decides |
| Dashboard | Live summary of all agents with digest, status, and output preview |

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js](https://nodejs.org/) (LTS)
- An LLM API key (set `GEMINI_API_KEY`, `ANTHROPIC_API_KEY`, or `OPENAI_API_KEY`)

### Steps

1. **Clone the repository**

   ```bash
   git clone https://github.com/clarksonr/purfle.git
   cd purfle
   ```

2. **Build the runtime and SDK**

   ```bash
   dotnet build runtime/src/Purfle.Runtime/Purfle.Runtime.csproj
   cd sdk && npm install && npm run build && cd ..
   ```

3. **Start the demo environment**

   ```bash
   npx purfle demo
   ```

   This starts local MCP servers (file, Gmail, GitHub) and prints a summary table.

4. **Open the desktop app**

   ```bash
   dotnet run --project app/src/Purfle.App/Purfle.App.csproj
   ```

   The Dashboard shows installed agents, their status, and today's digest.

---

## How Agents Work

An agent is a signed package containing a manifest, optional .NET assemblies, and prompt files:

```
my-agent.purfle/
├── agent.manifest.json     ← signed, declares everything
├── lib/
│   └── MyAgent.dll         ← .NET assembly (optional)
├── prompts/
│   └── system.md           ← instruction file
└── assets/                 ← optional embedded resources
```

### Minimal manifest (Hello World)

```json
{
  "purfle": "0.1",
  "id": "11111111-1111-4111-a111-111111111111",
  "name": "Hello World",
  "version": "0.1.0",
  "description": "Minimal agent for local demonstration.",
  "identity": {
    "author": "clarksonr",
    "email": "roman@example.com",
    "key_id": "hello-key-001",
    "algorithm": "ES256",
    "issued_at": "2026-03-27T00:00:00Z",
    "expires_at": "2027-03-27T00:00:00Z"
  },
  "capabilities": ["llm.chat"],
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "gemini",
    "model": "gemini-2.0-flash"
  }
}
```

The AIVM reads this manifest, verifies the signature, checks capabilities, selects
the LLM adapter, and runs the agent on its declared schedule. The agent never touches
the system directly — the AIVM executes on its behalf.

---

## Stack

| Layer | Technology |
|---|---|
| Desktop | .NET MAUI (C#) — Windows + macOS |
| AIVM | C# class inside MAUI |
| Manifest spec | JSON Schema Draft 2020-12 |
| Agent identity | JWS / ES256 |
| Inference | Engine-agnostic: Gemini, OpenAI, Ollama, Anthropic |
| Scheduler | PeriodicTimer + NCrontab + WindowTrigger + EventTrigger (SSE) |
| SDK / CLI | TypeScript / Node.js |
| Tests | xUnit + Jest |
| Azure | Functions consumption + App Service F1 |
| IaC | Bicep |

---

## Docs

- [Getting Started](docs/GETTING_STARTED.md)
- [Manifest Reference](docs/MANIFEST_REFERENCE.md)
- [Publishing Agents](docs/PUBLISHING.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Roadmap](docs/ROADMAP.md)

---

## License

[MIT](LICENSE)
