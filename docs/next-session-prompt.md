# Next Session Prompt

Paste this at the start of the next session to resume where we left off.

---

We have been building Purfle — a signed AI agent identity and trust platform. Read CLAUDE.md fully before doing anything.

## What we built this session

1. **MCP server wiring** (`runtime/src/Purfle.Runtime/Mcp/`)
   - `IMcpClient` interface — `ListToolsAsync()` discovers tools, `CallToolAsync()` invokes them
   - `McpClient` implementation — launches an MCP server as a child process, communicates via JSON-RPC 2.0 over stdio, handles the `initialize` handshake, tool listing, and tool invocation
   - `McpToolInfo` record — name, description, input schema JSON

2. **MCP integration in AnthropicAdapter** (`runtime/src/Purfle.Runtime.Anthropic/AnthropicAdapter.cs`)
   - Constructor accepts optional `IReadOnlyList<IMcpClient>` — discovers tools from each, filters through `sandbox.CanUseMcpTool()`, builds Anthropic tool definitions
   - `_mcpToolRoutes` dictionary maps tool name → owning MCP client for dispatch
   - `ExecuteToolAsync()` routes unknown tool names to MCP clients (with sandbox re-check)
   - `AdapterFactory` passes MCP clients through to the adapter

3. **Conversation history** (`runtime/src/Purfle.Runtime/Sessions/ConversationSession.cs`)
   - `ConversationSession` wraps any `IInferenceAdapter` and accumulates message history
   - `SendAsync()` calls `InvokeMultiTurnAsync()` with the full history, appends the new turn
   - `Reset()` clears history for a fresh session
   - `TurnCount` tracks how many turns have occurred
   - `IInferenceAdapter` now has `InvokeMultiTurnAsync()` for multi-turn support
   - `AnthropicAdapter.InvokeMultiTurnAsync()` sends full history + tools in every request

4. **Host demo updated** (`runtime/src/Purfle.Runtime.Host/Program.cs`)
   - Added multi-turn conversation demo after the single-turn invocation
   - Demonstrates that the model retains context across turns via `ConversationSession`

## What is not yet built (priority order)

1. **MCP end-to-end test** — The MCP wiring compiles and builds but needs a real MCP server to test against. Try with `npx -y @modelcontextprotocol/server-filesystem /tmp` or a custom MCP server. Create a test agent manifest with `permissions.tools.mcp` entries to exercise the full path.

2. **Streaming** — The Anthropic adapter uses the blocking Messages API. Streaming responses (`text-delta` events) are not yet wired.

3. **Audit logging** — Every sandbox permission check should be logged with agent ID, timestamp, and outcome. Not yet built.

4. **OpenClaw / Ollama adapters** — `Purfle.Runtime.OpenClaw` and `Purfle.Runtime.Ollama` exist as stubs but are not functional. Both now implement `InvokeMultiTurnAsync` (throws `NotImplementedException`).

5. **WordPress marketplace site** — The API is complete but the public-facing web front end is not built.

6. **Publisher verification workflow** — Any user can register as a publisher. A verification/approval step is not yet built.

## Suggested next task

Test MCP end-to-end: create a sample agent manifest that declares MCP tools, launch an MCP server, and verify the full tool-call loop works through the sandbox.

Or: tackle streaming — switch the Anthropic adapter from `messages` to `messages` with `stream: true`, yield `text-delta` events, and provide a streaming variant of `InvokeAsync`.

Or: build audit logging — add an `IAuditLog` interface, inject it into `AgentSandbox` or the adapter, and log every permission check with agent ID + timestamp + outcome.
