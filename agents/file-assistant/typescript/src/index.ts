import * as readline from "node:readline";

// Purfle File Assistant — IPC stdin/stdout agent (TypeScript)
// Reads JSON requests from stdin, writes JSON responses to stdout.
// Protocol:
//   -> { "type": "execute", "input": { "message": "..." } }
//   <- { "type": "response", "toolCall": { "name": "list_directory", "args": { "path": "./workspace" } } }
//   -> { "type": "toolResult", "callId": "...", "result": "..." }
//   <- { "type": "response", "content": "...", "done": true }

interface IpcRequest {
  type: string;
  input?: { message?: string };
  callId?: string;
  result?: string;
}

interface ToolCall {
  name: string;
  args?: Record<string, string>;
}

interface IpcResponse {
  type: string;
  content?: string;
  toolCall?: ToolCall;
  done?: boolean;
}

function writeResponse(response: IpcResponse): void {
  process.stdout.write(JSON.stringify(response) + "\n");
}

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false,
});

rl.on("line", (line: string) => {
  let request: IpcRequest;
  try {
    request = JSON.parse(line) as IpcRequest;
  } catch {
    writeResponse({
      type: "error",
      content: "Invalid JSON input",
      done: true,
    });
    return;
  }

  switch (request.type) {
    case "execute":
      // First step: request a tool call to list the workspace directory
      writeResponse({
        type: "response",
        toolCall: {
          name: "list_directory",
          args: { path: "./workspace" },
        },
      });
      break;

    case "toolResult": {
      // Tool result received — return final response
      const summary = request.result ?? "(no result)";
      writeResponse({
        type: "response",
        content: `Here are the files I found:\n${summary}`,
        done: true,
      });
      break;
    }

    default:
      writeResponse({
        type: "error",
        content: `Unknown request type: ${request.type}`,
        done: true,
      });
      break;
  }
});

rl.on("close", () => {
  process.exit(0);
});
