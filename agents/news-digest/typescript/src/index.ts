import * as readline from "node:readline";

/**
 * News Digest agent -- IPC entry point.
 * Reads JSON commands from stdin, writes JSON responses to stdout.
 * On "execute": calls the news/headlines tool, categorizes results, formats a digest.
 */

interface IpcMessage {
  method: string;
  params?: Record<string, unknown>;
}

interface NewsArticle {
  title: string;
  description: string;
  source: string;
  publishedAt: string;
}

interface IpcToolResult {
  articles: NewsArticle[];
}

const CATEGORIES = [
  "Tech",
  "Business",
  "Science",
  "Health",
  "Politics",
  "World",
  "Sports",
  "Entertainment",
] as const;

type Category = (typeof CATEGORIES)[number];

function classifyArticle(article: NewsArticle): Category {
  const text = `${article.title} ${article.description}`.toLowerCase();

  if (/tech|software|ai|cyber|startup/.test(text)) return "Tech";
  if (/market|stock|economy|finance|trade/.test(text)) return "Business";
  if (/science|research|study|space|climate/.test(text)) return "Science";
  if (/health|medical|disease|vaccine|hospital/.test(text)) return "Health";
  if (/politic|election|congress|senate|president/.test(text)) return "Politics";
  if (/sport|game|team|league|champion/.test(text)) return "Sports";
  if (/movie|music|celebrity|entertainment|award/.test(text)) return "Entertainment";

  return "World";
}

function formatDigest(categorized: Map<Category, NewsArticle[]>): string {
  const now = new Date().toISOString().replace("T", " ").slice(0, 16);
  const lines: string[] = [
    `# Daily News Digest`,
    `*Generated ${now} UTC*`,
    "",
  ];

  for (const category of CATEGORIES) {
    const articles = categorized.get(category);
    if (!articles || articles.length === 0) continue;

    lines.push(`## ${category}`);
    lines.push("");

    for (const article of articles) {
      const source = article.source || "Unknown";
      const time = article.publishedAt ? ` (${article.publishedAt})` : "";
      const summary = article.description || article.title;
      lines.push(`- **${article.title}** -- ${summary} *[${source}${time}]*`);
    }

    lines.push("");
  }

  return lines.join("\n");
}

function writeLine(obj: Record<string, unknown>): void {
  process.stdout.write(JSON.stringify(obj) + "\n");
}

async function handleExecute(rl: readline.Interface): Promise<void> {
  // Step 1: Request headlines from the news MCP tool
  writeLine({
    method: "tool_call",
    tool: "mcp://localhost:8103/news/headlines",
    arguments: { category: "general", count: 20 },
  });

  // Step 2: Read tool result from stdin
  const resultLine = await new Promise<string | null>((resolve) => {
    rl.once("line", (line) => resolve(line));
    rl.once("close", () => resolve(null));
  });

  let articles: NewsArticle[] = [];
  if (resultLine) {
    try {
      const toolResult = JSON.parse(resultLine) as IpcToolResult;
      articles = toolResult.articles ?? [];
    } catch {
      articles = [];
    }
  }

  // Step 3: Categorize articles
  const categorized = new Map<Category, NewsArticle[]>();
  for (const article of articles) {
    const category = classifyArticle(article);
    if (!categorized.has(category)) categorized.set(category, []);
    categorized.get(category)!.push(article);
  }

  // Step 4: Format and return digest
  const digest = formatDigest(categorized);
  writeLine({ status: "done", data: digest });
}

async function main(): Promise<void> {
  const rl = readline.createInterface({
    input: process.stdin,
    terminal: false,
  });

  for await (const line of rl) {
    if (!line.trim()) continue;

    let message: IpcMessage;
    try {
      message = JSON.parse(line) as IpcMessage;
    } catch {
      continue;
    }

    switch (message.method) {
      case "execute":
        await handleExecute(rl);
        break;

      case "ping":
        writeLine({ status: "pong", data: null });
        break;

      default:
        writeLine({ status: "error", data: `Unknown method: ${message.method}` });
        break;
    }
  }
}

main().catch((err) => {
  console.error("Fatal:", err);
  process.exit(1);
});
