# Purfle

Purfle is an AI Virtual Machine (AIVM) — a desktop runtime that loads AI agents the way a JVM loads bytecode: verify identity first, enforce capability boundaries always, execute only what was declared.

Each agent is defined by a **signed manifest** that declares its identity, runtime requirements, capability needs, and permission scope. The AIVM enforces all of it before a single line of agent code runs. An agent cannot exceed the scope it declared. It cannot load on a runtime that cannot satisfy its requirements.

The name comes from the inlaid border on a violin — the boundary layer that defines and protects.

→ [Specification](spec/SPEC.md) · [Identity RFC](spec/rfcs/0001-identity-model.md) · [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md)

---

## Why Purfle

AI agent frameworks define behavior. None define identity or load-time safety contracts. The result: agents that silently escalate permissions, frameworks with no way to verify who authored an agent or whether it has been tampered with, and runtimes that grant broad system access with no declared scope.

[OpenClaw](https://openclaw.ai) demonstrated this at scale — 247,000 GitHub stars, widespread adoption, and 9+ CVEs in its first two months because there is no trust model. NVIDIA shipped NemoClaw as a bolt-on sandbox. Purfle designs the trust model in from the start.

The manifest is the unit of distribution and trust. The AIVM enforces it.

---

## What It Does

Purfle runs as a desktop app (Windows and Mac). The user installs agents — each defined by a signed `.purfle` package — and the AIVM runs them on a schedule, sandboxed, unattended.

Example agents:
- `email-monitor` — polls Gmail every 15 minutes, summarizes new mail to a file
- `pr-watcher` — checks GitHub every 30 minutes for new pull requests
- `report-builder` — runs at 07:00, reads agent outputs, writes a morning brief

The UI shows one card per agent: status, last run, next run, output log. Agents run in the background. The AIVM enforces what each agent is allowed to do.

---

## Architecture

```
┌─────────────────────────────────────────────┐
│           .NET MAUI DESKTOP APP              │
│                                             │
│  ┌──────────────────────────────────────┐   │
│  │              AIVM (C#)               │   │
│  │                                      │   │
│  │  Scheduler                           │   │
│  │  ├── AgentRunner: email-monitor      │   │
│  │  ├── AgentRunner: pr-watcher         │   │
│  │  └── AgentRunner: report-builder     │   │
│  │                                      │   │
│  │  Each AgentRunner:                   │   │
│  │  ├── Own thread                      │   │
│  │  ├── Sandbox (manifest-enforced)     │   │
│  │  ├── MCP tool connections            │   │
│  │  ├── LLM adapter (Anthropic first)   │   │
│  │  └── Output → /aivm/output/<id>/     │   │
│  └──────────────────────────────────────┘   │
│                                             │
│  UI: one card per agent                     │
└─────────────────────────────────────────────┘
```

The AIVM is a C# class inside a .NET MAUI app — not a separate process or daemon. Agents run on isolated threads in their own `AssemblyLoadContext`. The LLM never touches the system directly; it proposes tool calls, and the AIVM executes them within the declared permission scope.

---

## Status

| Phase | What | Status |
|---|---|---|
| 1 | Manifest spec + JSON Schema | ✅ Complete |
| 2 | AIVM runtime core | ✅ Complete — 82 passing tests |
| 3 | .NET MAUI desktop app | ✅ Working — Windows + Mac |
| 3 | TypeScript SDK + CLI | 🔧 In progress |
| 4 | Marketplace + key registry | 🗓 Planned |

The AIVM scheduler, agent runner, LLM adapters (Anthropic), manifest loader and validator, and the desktop UI (agent cards, log viewer, settings, OAuth PKCE) are all working. The TypeScript CLI can simulate a manifest-driven agent end-to-end using the Anthropic API.

---

## Repo Structure

```
purfle/
├── spec/               ← Manifest spec, JSON Schema, RFCs, examples
├── runtime/            ← .NET / C# AIVM core library
├── app/                ← .NET MAUI desktop app
├── sdk/                ← TypeScript CLI + core library
├── marketplace/        ← Registry and distribution (planned)
└── docs/               ← Architecture and roadmap
```

---

## License

MIT — see [LICENSE](LICENSE).
