import { getRegistryUrl, apiGet } from "../marketplace.js";

interface SearchOptions {
  registry?: string;
  page?: string;
}

interface AgentSearchResult {
  agentId: string;
  name: string;
  description: string;
  latestVersion: string;
  author: string;
  totalDownloads: number;
}

interface AgentSearchResponse {
  agents: AgentSearchResult[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export async function searchCommand(query: string, options: SearchOptions): Promise<void> {
  const registry = getRegistryUrl(options.registry);
  const page = options.page ?? "1";
  const path = `api/agents?q=${encodeURIComponent(query)}&page=${page}&pageSize=20`;

  try {
    const result = await apiGet<AgentSearchResponse>(registry, path);

    if (result.agents.length === 0) {
      console.log(`No agents found for "${query}".`);
      return;
    }

    console.log(`Found ${result.totalCount} agent(s) (page ${result.page}):\n`);
    console.log(
      padRight("NAME", 30) +
      padRight("VERSION", 12) +
      padRight("AUTHOR", 20) +
      "DESCRIPTION"
    );
    console.log("-".repeat(80));

    for (const agent of result.agents) {
      console.log(
        padRight(agent.name, 30) +
        padRight(agent.latestVersion, 12) +
        padRight(agent.author, 20) +
        truncate(agent.description, 40)
      );
    }

    if (result.totalCount > result.page * result.pageSize) {
      console.log(`\nPage ${result.page} of ${Math.ceil(result.totalCount / result.pageSize)}. Use --page to see more.`);
    }
  } catch (err) {
    console.error(`Search failed: ${(err as Error).message}`);
    process.exit(1);
  }
}

function padRight(s: string, len: number): string {
  return s.length >= len ? s.slice(0, len - 1) + " " : s + " ".repeat(len - s.length);
}

function truncate(s: string, len: number): string {
  return s.length > len ? s.slice(0, len - 3) + "..." : s;
}
