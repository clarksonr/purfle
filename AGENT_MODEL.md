# Purfle — Agent Model Reference

This document exists to prevent a specific and recurring mistake: conflating
**agent packages** with **MCP servers**. Read this if you are confused about
what an agent is, how it is packaged, or how it executes.

For project-wide context, build commands, and current phase status, see `CLAUDE.md`.

---

## The One-Sentence Summary

An agent is a **signed .NET assembly bundle** that the AIVM loads, sandboxes,
and runs — it is not an MCP server, not a chat session, and not a script.

---

## What Is an Agent (Precisely)

An agent in Purfle is a distributable unit of behavior consisting of:

1. A **signed manifest** (`agent.manifest.json`) that declares identity,
   required capabilities, permissions, and which LLM backend to use
2. One or more **.NET managed assemblies** (`.dll` files) containing the
   agent's executable logic
3. **Prompt files** — static text injected as the system prompt at invocation
4. Optional **MCP tool bindings** — declarations of which external MCP servers
   this agent is permitted to call
5. Optional **embedded assets** — images, data files, etc.

These are bundled into a `.purfle` package (zip format) and distributed via
the marketplace.

---

## What an Agent Is NOT

| This thing | Its actual role |
|---|---|
| MCP server | External tool provider (file, git, DB). The agent calls it; the agent is not one. |
| LLM / inference engine | Provided by the AIVM via the adapter declared in `runtime.engine`. Not in the package. |
| API key / credential | Held by the AIVM in the platform credential store. Never in the package. |
| Long-running process | Agents are stateless and request/response at the inference layer. |
| OpenClaw agent | A different ecosystem with no identity or trust model. |

---

## The AIVM Is the Enforcer

AIVM = AI Virtual Machine. This is Roman's term for the sandboxed enforcement
host. Think of it as the JVM for agents.

```
┌─────────────────────────────────────────┐
│                  AIVM                   │
│                                         │
│  ┌─────────────────────────────────┐    │
│  │        AgentSandbox             │    │
│  │  (immutable; built from         │    │
│  │   manifest permissions block)   │    │
│  │                                 │    │
│  │  ┌───────────────────────────┐  │    │
│  │  │  AssemblyLoadContext      │  │    │
│  │  │  (isolated per agent)     │  │    │
│  │  │                           │  │    │
│  │  │   MyAgent.dll             │  │    │
│  │  │   (agent logic runs here) │  │    │
│  │  └───────────────────────────┘  │    │
│  └─────────────────────────────────┘    │
│                                         │
│  Every OS call routes through           │
│  AgentSandbox before execution.         │
│  Denied calls throw; they do not        │
│  silently fail or return empty.         │
└─────────────────────────────────────────┘
```

The AIVM is **not** an MCP server. The AIVM is the runtime that hosts agents.
MCP servers sit outside the AIVM and are called by agents through the sandbox's
tool permission layer.

---

## MCP — Exactly Where It Fits

MCP (Model Context Protocol) is the protocol agents use to call external tools.
It is one piece of the permissions model, not the agent model itself.

```
                    AIVM
                   ┌────────────────────────────┐
                   │                            │
  user request ──► │  AgentSandbox              │
                   │    └─ MyAgent.dll          │──► Anthropic API
                   │         │                  │    (LLM inference)
                   │         │ tool call        │
                   │         ▼                  │
                   │  sandbox.CanUseMcpTool()?  │
                   │         │                  │
                   └─────────┼──────────────────┘
                             │ YES (listed in manifest)
                             ▼
                    MCP Server (external process)
                    e.g. filesystem, git, sqlite
```

An agent declares MCP bindings in its manifest:

```json
"permissions": {
  "tools": {
    "mcp": ["filesystem", "git"]
  }
}
```

The AIVM enforces this. Calls to unlisted MCP servers are blocked. The agent
does not connect to MCP servers directly — the AIVM mediates every call.

---

## Assembly Loading — Cross-Platform

Agent DLLs are .NET managed assemblies. The `.dll` extension is a .NET
convention used on all platforms, not a Windows-only format.

```csharp
// This works identically on Windows, macOS, and Linux
var context = new AssemblyLoadContext(agentId, isCollectible: true);
var assembly = context.LoadFromAssemblyPath(path);
```

Platform differences in Purfle are confined to the **credential store layer**:

| Platform | Credential store |
|---|---|
| Windows | Windows Credential Manager |
| macOS | Keychain |
| Linux | libsecret |

The agent package format, assembly loading, sandbox enforcement, and manifest
verification are identical across all three platforms.

---

## Capability Negotiation — Why It Matters

Before an agent can run, the AIVM performs capability negotiation:

- The **manifest** declares what the agent **requires** and what it can
  optionally use
- The **runtime** declares what it **offers**
- A mismatch on a required capability → the agent fails to load with a typed
  error; it does not degrade silently

This is the "Android permissions" model applied to AI agents. A user (or
automated system) can inspect the manifest and know the exact capability
surface before allowing an agent to load.

Example: an agent that requires `llm.chat` and `filesystem.read` will fail
to load on a runtime that offers `llm.chat` but not `filesystem.read`. The
failure is explicit, logged, and surfaces a `LoadFailureReason` enum value.

---

## The Credential Flow (Agents Never See Keys)

```
Publisher registers public key with marketplace
    └─ AIVM stores association: agentId → publicKey

User installs agent
    └─ AIVM downloads package, verifies JWS signature
    └─ AIVM runs OAuth2 PKCE flow for any required services
    └─ Tokens stored in platform credential store (Keychain, etc.)
    └─ Agent assemblies stored in local agent store

Agent invoked
    └─ AIVM loads assemblies into AssemblyLoadContext
    └─ AIVM retrieves token from credential store
    └─ AIVM calls LLM API directly; agent sees only the response
    └─ Agent never has access to raw API keys or OAuth tokens
```

This is intentional. The agent is a behavior declaration, not a credential
holder. The AIVM is the trust boundary.

---

## Common Mistakes to Avoid

**Do not treat MCP servers as agents.**
MCP servers provide tools. Agents use tools. They are different things at
different layers of the stack.

**Do not put credentials in agent packages.**
No API keys, no OAuth tokens, no secrets. The AIVM handles all of this via
the platform credential store. If you find yourself writing credential
handling inside an agent assembly, stop — that belongs in the AIVM adapter.

**Do not add abstractions for hypothetical future requirements.**
Roman's explicit preference: start focused, no over-engineering. If a
requirement is not in the current phase, do not design for it.

**Do not assume Windows-only.**
The package format and assembly loading are cross-platform from day one.
The only platform-specific code is in the credential store adapters.

**Do not make the AIVM an MCP server.**
The AIVM hosts agents. It enforces sandbox boundaries. It is not a tool
provider and should not expose an MCP interface.
