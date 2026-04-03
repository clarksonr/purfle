import express, { Request, Response } from "express";
import { readFileSync, existsSync } from "fs";
import { join } from "path";
import { homedir } from "os";

const app = express();
app.use(express.json());

const PORT = 8111;
const GITHUB_API = "https://api.github.com";

// --- Token resolution ---

function getGitHubToken(): string | null {
  // 1. Environment variable
  if (process.env.GITHUB_TOKEN) return process.env.GITHUB_TOKEN;

  // 2. Purfle credential store
  const credPath = join(homedir(), ".purfle", "github-token");
  if (existsSync(credPath)) {
    return readFileSync(credPath, "utf8").trim();
  }

  return null;
}

function requireToken(res: Response): string | null {
  const token = getGitHubToken();
  if (!token) {
    res.status(401).json({
      error: "GitHub token not configured.",
      help: "Set GITHUB_TOKEN env var or run: purfle setup (stores token in ~/.purfle/github-token)",
    });
    return null;
  }
  return token;
}

async function githubFetch(path: string, token: string): Promise<globalThis.Response> {
  return fetch(`${GITHUB_API}${path}`, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/vnd.github.v3+json",
      "User-Agent": "purfle-mcp-github/1.0.0",
    },
  });
}

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  const hasToken = !!getGitHubToken();
  res.json({
    name: "@purfle/mcp-github",
    version: "2.0.0",
    status: hasToken ? "ok" : "no-token",
    provider: "GitHub API",
    tools: [
      "github/pulls (list_pull_requests)",
      "github/pulls/:number (get_pull_request)",
      "github/pulls/:number/reviews (list_reviews)",
    ],
  });
});

// --- list_pull_requests ---

app.get("/tools/github/pulls", async (req: Request, res: Response) => {
  const token = requireToken(res);
  if (!token) return;

  const owner = req.query.owner as string;
  const repo = req.query.repo as string;
  const state = (req.query.state as string) ?? "open";

  if (!owner || !repo) {
    res.status(400).json({ error: "Missing required parameters: owner, repo" });
    return;
  }

  try {
    const resp = await githubFetch(`/repos/${owner}/${repo}/pulls?state=${state}&per_page=30`, token);
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `GitHub API error: ${body}` });
      return;
    }

    const pulls = (await resp.json()) as Array<Record<string, unknown>>;
    res.json({
      tool: "github/pulls",
      provider: "github",
      result: {
        total_count: pulls.length,
        pull_requests: pulls.map((p: Record<string, unknown>) => ({
          number: p.number,
          title: p.title,
          state: p.state,
          user: (p.user as Record<string, unknown>)?.login,
          created_at: p.created_at,
          updated_at: p.updated_at,
          labels: (p.labels as Array<Record<string, unknown>>)?.map((l) => l.name),
          draft: p.draft,
          additions: p.additions,
          deletions: p.deletions,
          changed_files: p.changed_files,
        })),
      },
    });
  } catch (err) {
    res.status(500).json({ error: `Failed to fetch PRs: ${(err as Error).message}` });
  }
});

app.post("/tools/github/pulls", async (req: Request, res: Response) => {
  const token = requireToken(res);
  if (!token) return;

  const owner = req.body?.owner;
  const repo = req.body?.repo;
  const state = req.body?.state ?? "open";

  if (!owner || !repo) {
    res.status(400).json({ error: "Missing required parameters: owner, repo" });
    return;
  }

  try {
    const resp = await githubFetch(`/repos/${owner}/${repo}/pulls?state=${state}&per_page=30`, token);
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `GitHub API error: ${body}` });
      return;
    }

    const pulls = (await resp.json()) as Array<Record<string, unknown>>;
    res.json({
      tool: "github/pulls",
      provider: "github",
      result: {
        total_count: pulls.length,
        pull_requests: pulls.map((p: Record<string, unknown>) => ({
          number: p.number,
          title: p.title,
          state: p.state,
          user: (p.user as Record<string, unknown>)?.login,
          created_at: p.created_at,
          updated_at: p.updated_at,
          labels: (p.labels as Array<Record<string, unknown>>)?.map((l) => l.name),
          draft: p.draft,
        })),
      },
    });
  } catch (err) {
    res.status(500).json({ error: `Failed to fetch PRs: ${(err as Error).message}` });
  }
});

// --- get_pull_request ---

app.get("/tools/github/pulls/:number", async (req: Request, res: Response) => {
  const token = requireToken(res);
  if (!token) return;

  const owner = req.query.owner as string;
  const repo = req.query.repo as string;
  const num = req.params.number;

  if (!owner || !repo) {
    res.status(400).json({ error: "Missing required parameters: owner, repo" });
    return;
  }

  try {
    const resp = await githubFetch(`/repos/${owner}/${repo}/pulls/${num}`, token);
    if (!resp.ok) {
      if (resp.status === 404) {
        res.status(404).json({ error: `Pull request #${num} not found` });
        return;
      }
      const body = await resp.text();
      res.status(resp.status).json({ error: `GitHub API error: ${body}` });
      return;
    }

    const pr = await resp.json();
    res.json({ tool: "github/pulls/detail", provider: "github", result: pr });
  } catch (err) {
    res.status(500).json({ error: `Failed to fetch PR: ${(err as Error).message}` });
  }
});

// --- list_reviews ---

app.get("/tools/github/pulls/:number/reviews", async (req: Request, res: Response) => {
  const token = requireToken(res);
  if (!token) return;

  const owner = req.query.owner as string;
  const repo = req.query.repo as string;
  const num = req.params.number;

  if (!owner || !repo) {
    res.status(400).json({ error: "Missing required parameters: owner, repo" });
    return;
  }

  try {
    const resp = await githubFetch(`/repos/${owner}/${repo}/pulls/${num}/reviews`, token);
    if (!resp.ok) {
      const body = await resp.text();
      res.status(resp.status).json({ error: `GitHub API error: ${body}` });
      return;
    }

    const reviews = await resp.json();
    res.json({ tool: "github/pulls/reviews", provider: "github", result: reviews });
  } catch (err) {
    res.status(500).json({ error: `Failed to fetch reviews: ${(err as Error).message}` });
  }
});

app.listen(PORT, () => {
  const hasToken = !!getGitHubToken();
  console.log(`MCP GitHub server running on http://localhost:${PORT}`);
  if (!hasToken) {
    console.log("  WARNING: No GitHub token found. Set GITHUB_TOKEN or run purfle setup.");
  }
});
