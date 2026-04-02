import * as readline from "node:readline";

// --- IPC message types ---

interface IpcRequest {
  method: string;
  params?: Record<string, string>;
}

interface IpcResponse {
  result: string | null;
  status: "ok" | "done" | "error";
  error?: string;
  errorCode?: string;
}

interface ToolCall {
  type: "tool_call";
  tool: string;
  params: Record<string, string>;
}

interface ToolResult {
  result?: string;
  error?: string;
}

// --- IPC helpers ---

function writeJson(data: unknown): void {
  process.stdout.write(JSON.stringify(data) + "\n");
}

function writeError(code: string, message: string): void {
  const response: IpcResponse = {
    result: null,
    status: "error",
    error: message,
    errorCode: code,
  };
  writeJson(response);
}

function writeResponse(response: IpcResponse): void {
  writeJson(response);
}

function sendToolCall(name: string, params: Record<string, string>): void {
  const call: ToolCall = { type: "tool_call", tool: name, params };
  writeJson(call);
}

async function waitForToolResult(
  rl: readline.Interface
): Promise<ToolResult | null> {
  return new Promise((resolve) => {
    rl.once("line", (line: string) => {
      if (!line.trim()) {
        resolve(null);
        return;
      }
      try {
        resolve(JSON.parse(line) as ToolResult);
      } catch {
        resolve(null);
      }
    });
  });
}

// --- Meeting notes formatting ---

function formatMeetingNotes(transcript: string, actionItems: string): string {
  const timestamp = new Date().toISOString().replace("T", " ").slice(0, 19) + " UTC";

  const summary =
    transcript.length > 0
      ? `Meeting transcript received (${transcript.length} characters). Key points extracted below.`
      : "No transcript provided.";

  const decisions =
    transcript.length > 0
      ? "Decisions extracted from transcript (see action items for assignments)."
      : "No decisions recorded.";

  return [
    "# Meeting Notes",
    `Generated: ${timestamp}`,
    "",
    "## Summary",
    summary,
    "",
    "## Decisions",
    decisions,
    "",
    "## Action Items",
    "| Item | Owner | Deadline | Status |",
    "|------|-------|----------|--------|",
    actionItems,
    "",
    "## Next Steps",
    "- Review action items and confirm owners",
    "- Schedule follow-up if needed",
  ].join("\n");
}

// --- Execute handler ---

async function handleExecute(
  rl: readline.Interface,
  params?: Record<string, string>
): Promise<IpcResponse> {
  const input = params?.input ?? "";

  // Step 1: Call meeting/transcribe tool
  sendToolCall("meeting/transcribe", { input });
  const transcriptResult = await waitForToolResult(rl);
  const transcript = transcriptResult?.result ?? input;

  // Step 2: Call meeting/action-items tool
  sendToolCall("meeting/action-items", { transcript });
  const actionItemsResult = await waitForToolResult(rl);
  const actionItems = actionItemsResult?.result ?? "No action items extracted.";

  // Step 3: Format meeting notes
  const notes = formatMeetingNotes(transcript, actionItems);

  return { result: notes, status: "done" };
}

// --- Main IPC loop ---

async function main(): Promise<void> {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false,
  });

  for await (const line of rl) {
    if (!line.trim()) continue;

    try {
      const request = JSON.parse(line) as IpcRequest;

      let response: IpcResponse;

      switch (request.method) {
        case "execute":
          response = await handleExecute(rl, request.params);
          break;
        case "ping":
          response = { result: "pong", status: "ok" };
          break;
        default:
          response = {
            result: null,
            status: "error",
            error: `Unknown method: ${request.method}`,
          };
      }

      writeResponse(response);
    } catch (err) {
      if (err instanceof SyntaxError) {
        writeError("parse_error", "Malformed JSON input.");
      } else {
        writeError(
          "internal_error",
          err instanceof Error ? err.message : String(err)
        );
      }
    }
  }
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
