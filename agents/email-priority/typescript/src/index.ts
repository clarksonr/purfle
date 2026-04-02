import { readFileSync } from "node:fs";
import { join } from "node:path";
import { createInterface } from "node:readline";

/**
 * IPC agent that requests an email list via tool call, receives results,
 * categorizes by priority using inference prompt patterns, and returns
 * a prioritized summary.
 */

// --- IPC message types ---

interface IpcRequest {
  method: string;
  agent_id: string;
  parameters?: Record<string, unknown>;
}

interface ToolCallRequest {
  method: string;
  tool: string;
  parameters: Record<string, unknown>;
}

interface EmailSummary {
  id: string;
  from: string;
  subject: string;
  date: string;
}

interface ToolCallResponse {
  emails: EmailSummary[];
}

interface EmailMessage {
  id: string;
  from: string;
  subject: string;
  date: string;
  body: string;
}

interface EmailReadResponse {
  email: EmailMessage | null;
}

interface InferenceRequest {
  method: string;
  messages: { role: string; content: string }[];
}

interface InferenceResponse {
  content: string;
}

interface AgentResult {
  status: string;
  agent_id: string;
  output: string;
  email_count: number;
  timestamp: string;
}

// --- IPC helpers ---

const rl = createInterface({ input: process.stdin });
const lines: string[] = [];
let lineResolve: ((line: string) => void) | null = null;

rl.on("line", (line: string) => {
  if (lineResolve) {
    const resolve = lineResolve;
    lineResolve = null;
    resolve(line);
  } else {
    lines.push(line);
  }
});

function readLine(): Promise<string> {
  if (lines.length > 0) {
    return Promise.resolve(lines.shift()!);
  }
  return new Promise((resolve) => {
    lineResolve = resolve;
  });
}

function writeLine(obj: unknown): void {
  process.stdout.write(JSON.stringify(obj) + "\n");
}

function writeError(message: string): void {
  const result: AgentResult = {
    status: "error",
    agent_id: "dev.purfle.email-priority",
    output: message,
    email_count: 0,
    timestamp: new Date().toISOString(),
  };
  writeLine(result);
}

function loadSystemPrompt(): string {
  try {
    const promptPath = join(
      import.meta.dirname ?? ".",
      "..",
      "..",
      "prompts",
      "system.md"
    );
    return readFileSync(promptPath, "utf-8");
  } catch {
    return `You are an email triage assistant. Categorize each email as URGENT, IMPORTANT, NORMAL, or LOW.
Summarize key points and flag action items.`;
  }
}

function formatEmailsForInference(emails: EmailMessage[]): string {
  return emails
    .map(
      (e) =>
        `---\nFrom: ${e.from}\nSubject: ${e.subject}\nDate: ${e.date}\nBody:\n${e.body}\n---`
    )
    .join("\n");
}

// --- Main ---

async function main(): Promise<void> {
  // Step 1: Read IPC request from stdin
  const requestJson = await readLine();
  if (!requestJson) {
    writeError("No input received on stdin");
    return;
  }

  let request: IpcRequest;
  try {
    request = JSON.parse(requestJson) as IpcRequest;
  } catch {
    writeError("Failed to parse IPC request");
    return;
  }

  // Step 2: Request email list via MCP tool call
  const listToolCall: ToolCallRequest = {
    method: "tool/call",
    tool: "mcp://localhost:8101/email/list",
    parameters: { limit: 50, unread_only: true },
  };
  writeLine(listToolCall);

  // Step 3: Read tool response
  const listResponseJson = await readLine();
  if (!listResponseJson) {
    writeError("No response from email list tool");
    return;
  }

  const listResponse: ToolCallResponse = JSON.parse(listResponseJson);
  const emailSummaries = listResponse.emails ?? [];

  // Step 4: For each email, request full body via read tool
  const fullEmails: EmailMessage[] = [];
  for (const summary of emailSummaries) {
    const readToolCall: ToolCallRequest = {
      method: "tool/call",
      tool: "mcp://localhost:8102/email/read",
      parameters: { message_id: summary.id },
    };
    writeLine(readToolCall);

    const readResponseJson = await readLine();
    if (readResponseJson) {
      const readResponse: EmailReadResponse = JSON.parse(readResponseJson);
      if (readResponse.email) {
        fullEmails.push(readResponse.email);
      }
    }
  }

  // Step 5: Build inference request for priority categorization
  const systemPrompt = loadSystemPrompt();
  const emailBlock = formatEmailsForInference(fullEmails);

  const inferenceRequest: InferenceRequest = {
    method: "inference/complete",
    messages: [
      { role: "system", content: systemPrompt },
      {
        role: "user",
        content: `Triage the following emails:\n\n${emailBlock}`,
      },
    ],
  };
  writeLine(inferenceRequest);

  // Step 6: Read inference response
  const inferenceResponseJson = await readLine();
  if (!inferenceResponseJson) {
    writeError("No response from inference");
    return;
  }

  const inferenceResponse: InferenceResponse = JSON.parse(
    inferenceResponseJson
  );

  // Step 7: Write final result
  const result: AgentResult = {
    status: "ok",
    agent_id: "dev.purfle.email-priority",
    output: inferenceResponse.content ?? "No triage result produced",
    email_count: fullEmails.length,
    timestamp: new Date().toISOString(),
  };
  writeLine(result);

  rl.close();
}

main().catch((err) => {
  writeError(String(err));
  process.exit(1);
});
