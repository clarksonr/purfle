/**
 * CLI Generator agent — IPC stdin/stdout protocol.
 * Reads JSON messages from stdin, calls MCP tools (scaffold, add-command, generate-help),
 * writes JSON responses to stdout.
 */

import * as readline from "node:readline";

interface AgentMessage {
  type: string;
  payload: string;
}

interface AgentRequest {
  action: string;
  framework?: string;
  projectName?: string;
  commands?: string[];
}

interface McpToolCall {
  tool: string;
  parameters: Record<string, unknown>;
}

function sendMessage(message: AgentMessage): void {
  process.stdout.write(JSON.stringify(message) + "\n");
}

async function handleExecute(request: AgentRequest): Promise<void> {
  const framework = request.framework ?? "dotnet";
  const projectName = request.projectName ?? "my-cli";
  const commands = request.commands ?? ["greet", "version"];

  // Step 1: Call cli/scaffold to create project structure
  const scaffoldCall: McpToolCall = {
    tool: "mcp://localhost:8110/cli/scaffold",
    parameters: {
      framework,
      projectName,
      outputDir: "./output",
    },
  };
  sendMessage({ type: "tool_call", payload: JSON.stringify(scaffoldCall) });

  // Step 2: Add commands based on user input
  for (const command of commands) {
    const addCommandCall: McpToolCall = {
      tool: "mcp://localhost:8110/cli/add-command",
      parameters: {
        projectName,
        commandName: command,
        framework,
        outputDir: "./output",
      },
    };
    sendMessage({ type: "tool_call", payload: JSON.stringify(addCommandCall) });
  }

  // Step 3: Generate help documentation
  const helpCall: McpToolCall = {
    tool: "mcp://localhost:8110/cli/generate-help",
    parameters: {
      projectName,
      framework,
      format: "markdown",
      outputDir: "./output",
    },
  };
  sendMessage({ type: "tool_call", payload: JSON.stringify(helpCall) });

  sendMessage({
    type: "done",
    payload: `CLI project '${projectName}' generated with ${commands.length} command(s)`,
  });
}

async function main(): Promise<void> {
  sendMessage({ type: "status", payload: "cli-generator agent ready" });

  const rl = readline.createInterface({
    input: process.stdin,
    terminal: false,
  });

  for await (const line of rl) {
    try {
      const request: AgentRequest = JSON.parse(line);

      switch (request.action) {
        case "execute":
          await handleExecute(request);
          break;

        case "ping":
          sendMessage({ type: "pong", payload: "ok" });
          break;

        default:
          sendMessage({
            type: "error",
            payload: `Unknown action: ${request.action}`,
          });
          break;
      }
    } catch (err) {
      sendMessage({
        type: "error",
        payload: err instanceof Error ? err.message : String(err),
      });
    }
  }
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
