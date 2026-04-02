/**
 * Purfle DB Assistant Agent — TypeScript IPC entry point.
 *
 * Reads JSON commands from stdin (newline-delimited), writes JSON responses to stdout.
 * Protocol: one JSON object per line.
 */

import * as readline from "node:readline";

interface IpcMessage {
  type: string;
  data: string;
}

interface ToolCall {
  tool: string;
  args: Record<string, unknown>;
}

interface AnalysisResult {
  status: string;
  toolCalls: string[];
  checks: string[];
}

function send(msg: IpcMessage | ToolCall): void {
  process.stdout.write(JSON.stringify(msg) + "\n");
}

function sendMessage(type: string, data: string): void {
  send({ type, data } satisfies IpcMessage);
}

async function handleExecute(): Promise<void> {
  // Step 1: Call db/schema tool to get table structure
  send({ tool: "db/schema", args: {} } satisfies ToolCall);

  // In a real agent, the AIVM would respond with schema data on stdin.
  // For now, we emit the tool call and proceed with analysis.

  // Step 2: Analyze schema and suggest optimizations
  const analysis: AnalysisResult = {
    status: "done",
    toolCalls: [
      "db/schema — retrieve table structures",
      "db/query-explain — analyze slow queries",
      "db/suggest-index — recommend missing indexes",
    ],
    checks: [
      "Foreign key columns missing indexes",
      "N+1 query pattern detection",
      "Full table scan identification",
      "Over-indexed table warnings",
      "Implicit type conversion detection",
      "Covering index opportunities",
    ],
  };

  sendMessage("result", JSON.stringify(analysis));
}

async function main(): Promise<void> {
  sendMessage("ready", "db-assistant ready");

  const rl = readline.createInterface({
    input: process.stdin,
    terminal: false,
  });

  for await (const line of rl) {
    if (!line.trim()) continue;

    let request: IpcMessage;
    try {
      request = JSON.parse(line) as IpcMessage;
    } catch {
      sendMessage("error", "Invalid JSON input");
      continue;
    }

    switch (request.type) {
      case "execute":
        await handleExecute();
        break;

      case "ping":
        sendMessage("pong", "alive");
        break;

      case "shutdown":
        sendMessage("shutdown", "goodbye");
        process.exit(0);
        break;

      default:
        sendMessage("error", `Unknown command: ${request.type}`);
        break;
    }
  }
}

main().catch((err: unknown) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
