# Next Session Prompt

Paste this at the start of the next session to resume where we left off.

---

We have been building Purfle — a signed AI agent identity and trust platform. Read CLAUDE.md fully before doing anything.

## What we built this session

1. **Sample agents** (`agents/` folder)
   - `agents/chat.agent.json` — minimal chat agent, env key only, no OS access
   - `agents/file-summarizer.agent.json` — reads `C:/Users/**/*.txt|md|pdf`, declares `filesystem-read` capability

2. **Seeder** (`tools/Purfle.Agents.Seeder/`)
   - .NET console app that generates a P-256 key, signs both manifests, and writes publishers/signing-keys/agent-listings/agent-versions JSON files + manifest blobs directly to the marketplace data directory
   - Run with: `dotnet run --project tools/Purfle.Agents.Seeder`
   - After seeding, `GET http://localhost:5000/api/agents` returns both agents

3. **Tool-call loop** (`runtime/src/Purfle.Runtime.Anthropic/AnthropicAdapter.cs`)
   - `AnthropicAdapter` now inspects the sandbox on construction and builds a tool list
   - `read_file` tool offered when `permissions.filesystem.read` is non-empty
   - `write_file` tool offered when `permissions.filesystem.write` is non-empty
   - `http_get` tool offered when `permissions.network.allow` is non-empty
   - `InvokeAsync` runs a tool-call loop (max 10 iterations): posts tools to Claude API, executes `tool_use` blocks with sandbox checks before every file/network access, feeds results back until `end_turn`
   - Agents with no OS permissions (chat agent) take the original single-turn path unchanged
   - Build: clean. Tests: 52/52 pass.

## What is not yet built (priority order)

1. **MCP server wiring** — `permissions.tools.mcp` is declared and sandbox-checked but the adapter does not yet connect to external MCP servers. The tool-call loop currently handles file/network inline. True MCP integration (connecting to a running MCP server process) is the next step.

2. **Conversation history** — `InvokeAsync` is stateless. Each call starts fresh. Multi-turn conversation (passing prior messages back) needs a session/context layer above the adapter.

3. **Streaming** — The Anthropic adapter uses the blocking Messages API. Streaming responses (`text-delta` events) are not yet wired.

4. **Audit logging** — Every sandbox permission check should be logged with agent ID, timestamp, and outcome. Not yet built.

5. **OpenClaw / Ollama adapters** — `Purfle.Runtime.OpenClaw` and `Purfle.Runtime.Ollama` exist as stubs but are not functional.

6. **WordPress marketplace site** — The API is complete but the public-facing web front end is not built.

7. **Publisher verification workflow** — Any user can register as a publisher. A verification/approval step is not yet built.

## Suggested next task

Wire MCP server support into the tool-call loop. The `AgentSandbox.CanUseMcpTool(toolId)` check already exists. The next step is:
- Define an `IMcpClient` interface (connect to an MCP server, list tools, call a tool)
- Inject available MCP clients into `AnthropicAdapter` (keyed by tool ID)
- In `BuildTools()`, also advertise tools from connected MCP servers that pass `sandbox.CanUseMcpTool()`
- In `ExecuteToolAsync()`, route unrecognised tool names to the appropriate MCP client

Or alternatively: tackle conversation history — add a `ConversationSession` wrapper that holds message history and calls `InvokeAsync` with accumulated context.
