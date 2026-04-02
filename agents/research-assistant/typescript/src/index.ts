/**
 * Purfle Research Assistant Agent — TypeScript IPC implementation.
 *
 * Reads a JSON command from stdin, executes a research workflow via MCP tool
 * calls over stdout/stdin, and returns a structured research report.
 */

// ----- IPC message types -----

interface IpcCommand {
  query: string;
  maxSources?: number;
}

interface ToolCallRequest {
  tool: string;
  parameters: Record<string, unknown>;
}

interface SearchResult {
  title: string;
  url: string;
  snippet: string;
}

interface SearchResponse {
  results: SearchResult[];
}

interface FetchPageResponse {
  content: string;
}

interface FetchedSource {
  title: string;
  url: string;
  snippet: string;
  content: string;
}

interface Finding {
  subtopic: string;
  content: string;
  citationUrl: string;
}

interface SourceRef {
  title: string;
  url: string;
  accessedAt: string;
}

interface ResearchReport {
  topic: string;
  summary: string;
  findings: Finding[];
  openQuestions: string[];
  sources: SourceRef[];
}

interface IpcResponse {
  status: "done" | "error";
  report?: ResearchReport;
  error?: string;
}

// ----- IPC helpers -----

function readLine(): Promise<string> {
  return new Promise((resolve, reject) => {
    let data = "";
    process.stdin.setEncoding("utf-8");
    process.stdin.on("data", (chunk: string) => {
      data += chunk;
      const newlineIndex = data.indexOf("\n");
      if (newlineIndex !== -1) {
        resolve(data.slice(0, newlineIndex).trim());
        process.stdin.removeAllListeners();
      }
    });
    process.stdin.on("end", () => resolve(data.trim()));
    process.stdin.on("error", reject);
  });
}

function sendJson(obj: unknown): void {
  process.stdout.write(JSON.stringify(obj) + "\n");
}

function sendError(message: string): void {
  const response: IpcResponse = { status: "error", error: message };
  sendJson(response);
}

function sendResult(report: ResearchReport): void {
  const response: IpcResponse = { status: "done", report };
  sendJson(response);
}

function truncateContent(content: string, maxLength: number): string {
  if (content.length <= maxLength) return content;
  return content.slice(0, maxLength) + "...";
}

// ----- Tool call helpers -----

async function callTool(
  toolName: string,
  parameters: Record<string, unknown>
): Promise<SearchResponse | null> {
  const request: ToolCallRequest = { tool: toolName, parameters };
  sendJson(request);

  const response = await readLine();
  if (!response) return null;

  try {
    return JSON.parse(response) as SearchResponse;
  } catch {
    return null;
  }
}

async function callFetchPage(url: string): Promise<string> {
  const request: ToolCallRequest = {
    tool: "research/fetch-page",
    parameters: { url },
  };
  sendJson(request);

  const response = await readLine();
  if (!response) return "";

  try {
    const page = JSON.parse(response) as FetchPageResponse;
    return page.content ?? "";
  } catch {
    return "";
  }
}

// ----- Main workflow -----

async function executeResearchWorkflow(
  query: string,
  maxSources: number
): Promise<void> {
  // Step 1: Search the web
  const searchResponse = await callTool("research/web-search", {
    query,
    max_results: maxSources * 2,
  });

  if (!searchResponse || searchResponse.results.length === 0) {
    sendResult({
      topic: query,
      summary: "No search results found for the given query.",
      findings: [],
      openQuestions: [
        "The search returned no results. Try rephrasing the query.",
      ],
      sources: [],
    });
    return;
  }

  // Step 2: Fetch top pages
  const fetchCount = Math.min(maxSources, searchResponse.results.length);
  const sources: FetchedSource[] = [];

  for (let i = 0; i < fetchCount; i++) {
    const result = searchResponse.results[i];
    const content = await callFetchPage(result.url);
    sources.push({
      title: result.title,
      url: result.url,
      snippet: result.snippet,
      content,
    });
  }

  // Step 3: Build report for LLM synthesis
  const today = new Date().toISOString().slice(0, 10);
  const report: ResearchReport = {
    topic: query,
    summary: `Research completed. Gathered ${sources.length} sources for: ${query}`,
    findings: sources.map((s) => ({
      subtopic: s.title,
      content: truncateContent(s.content, 2000),
      citationUrl: s.url,
    })),
    openQuestions: [
      "LLM synthesis pending — the AIVM will produce the final narrative.",
    ],
    sources: sources.map((s) => ({
      title: s.title,
      url: s.url,
      accessedAt: today,
    })),
  };

  sendResult(report);
}

async function main(): Promise<void> {
  const input = await readLine();
  if (!input) {
    sendError("No input received on stdin.");
    return;
  }

  let command: IpcCommand;
  try {
    command = JSON.parse(input) as IpcCommand;
  } catch {
    sendError(`Invalid JSON input.`);
    return;
  }

  if (!command.query || command.query.trim().length === 0) {
    sendError("Command must include a non-empty 'query' field.");
    return;
  }

  await executeResearchWorkflow(command.query, command.maxSources ?? 5);
}

main().catch((err: Error) => {
  sendError(`Unhandled error: ${err.message}`);
  process.exit(1);
});
