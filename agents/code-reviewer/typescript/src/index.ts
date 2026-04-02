import * as readline from "node:readline";

// Purfle Code Reviewer — IPC stdin/stdout agent (TypeScript)
// Reads JSON requests from stdin, writes JSON responses to stdout.
// Protocol:
//   -> { "type": "execute", "input": { "code": "...", "language": "csharp" } }
//   <- { "type": "response", "toolCall": { "name": "code/analyze", "args": { ... } } }
//   -> { "type": "toolResult", "callId": "analyze-1", "result": "..." }
//   <- { "type": "response", "toolCall": { "name": "code/lint", "args": { ... } } }
//   -> { "type": "toolResult", "callId": "lint-1", "result": "..." }
//   <- { "type": "response", "toolCall": { "name": "code/security-scan", "args": { ... } } }
//   -> { "type": "toolResult", "callId": "scan-1", "result": "..." }
//   <- { "type": "response", "content": "<formatted review>", "done": true }

interface IpcRequest {
  type: string;
  input?: { code?: string; language?: string };
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

type ReviewPhase = "idle" | "waiting-analyze" | "waiting-lint" | "waiting-scan";

// Review state
let phase: ReviewPhase = "idle";
let pendingCode = "";
let pendingLanguage = "";
let analyzeResult = "";
let lintResult = "";

function writeResponse(response: IpcResponse): void {
  process.stdout.write(JSON.stringify(response) + "\n");
}

function formatReview(
  analyze: string,
  lint: string,
  securityScan: string,
): string {
  return [
    "## Code Review Results",
    "",
    "### Analysis (bugs, performance, maintainability)",
    analyze,
    "",
    "### Lint (style violations)",
    lint,
    "",
    "### Security Scan",
    securityScan,
    "",
    "---",
    "Severity legend: **critical** = must fix | **warning** = should fix | **info** = nice to fix",
  ].join("\n");
}

function handleExecute(request: IpcRequest): void {
  pendingCode = request.input?.code ?? "";
  pendingLanguage = request.input?.language ?? "unknown";

  if (!pendingCode.trim()) {
    writeResponse({
      type: "error",
      content: "Missing 'code' in input",
      done: true,
    });
    return;
  }

  // Step 1: Call code/analyze
  phase = "waiting-analyze";
  writeResponse({
    type: "response",
    toolCall: {
      name: "code/analyze",
      args: { code: pendingCode, language: pendingLanguage },
    },
  });
}

function handleToolResult(request: IpcRequest): void {
  const result = request.result ?? "(no result)";

  switch (phase) {
    case "waiting-analyze":
      analyzeResult = result;
      // Step 2: Call code/lint
      phase = "waiting-lint";
      writeResponse({
        type: "response",
        toolCall: {
          name: "code/lint",
          args: { code: pendingCode, language: pendingLanguage },
        },
      });
      break;

    case "waiting-lint":
      lintResult = result;
      // Step 3: Call code/security-scan
      phase = "waiting-scan";
      writeResponse({
        type: "response",
        toolCall: {
          name: "code/security-scan",
          args: { code: pendingCode, language: pendingLanguage },
        },
      });
      break;

    case "waiting-scan": {
      // All three tool results collected — format the review
      const review = formatReview(analyzeResult, lintResult, result);
      phase = "idle";
      writeResponse({
        type: "response",
        content: review,
        done: true,
      });
      break;
    }

    default:
      writeResponse({
        type: "error",
        content: "Unexpected tool result — no pending review phase",
        done: true,
      });
      break;
  }
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
      handleExecute(request);
      break;

    case "toolResult":
      handleToolResult(request);
      break;

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
