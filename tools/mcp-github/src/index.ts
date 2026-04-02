import express, { Request, Response } from "express";

const app = express();
app.use(express.json());

const PORT = 8111;

// --- Mock data ---

const mockPulls = [
  {
    number: 42,
    title: "feat(runtime): add exponential backoff for LLM retries",
    state: "open",
    user: { login: "clarksonr", avatar_url: "" },
    created_at: "2026-04-01T14:30:00Z",
    updated_at: "2026-04-01T16:45:00Z",
    labels: [{ name: "enhancement" }, { name: "runtime" }],
    body: "Adds exponential backoff with jitter for HTTP 429 and timeout responses from the LLM API. Starts at 1s, caps at 60s.",
    head: { ref: "feat/llm-backoff" },
    base: { ref: "main" },
    draft: false,
    additions: 87,
    deletions: 12,
    changed_files: 3,
    mergeable: true,
    files: [
      { filename: "runtime/src/Purfle.Runtime.Anthropic/AnthropicAdapter.cs", additions: 45, deletions: 8 },
      { filename: "runtime/tests/Purfle.Runtime.Tests/AnthropicAdapterTests.cs", additions: 38, deletions: 2 },
      { filename: "docs/TROUBLESHOOTING.md", additions: 4, deletions: 2 },
    ],
  },
  {
    number: 41,
    title: "fix(sandbox): block symlink traversal outside sandbox root",
    state: "open",
    user: { login: "elena-k", avatar_url: "" },
    created_at: "2026-04-01T10:15:00Z",
    updated_at: "2026-04-01T11:00:00Z",
    labels: [{ name: "security" }, { name: "sandbox" }],
    body: "Resolves symlinks to their real path before checking sandbox bounds. Prevents directory traversal via symlink chains.",
    head: { ref: "fix/symlink-sandbox" },
    base: { ref: "main" },
    draft: false,
    additions: 34,
    deletions: 5,
    changed_files: 2,
    mergeable: true,
    files: [
      { filename: "runtime/src/Purfle.Runtime/Sandbox/AgentSandbox.cs", additions: 28, deletions: 3 },
      { filename: "runtime/tests/Purfle.Runtime.Tests/SandboxTests.cs", additions: 6, deletions: 2 },
    ],
  },
  {
    number: 40,
    title: "chore(deps): bump Anthropic SDK to 1.4.0",
    state: "open",
    user: { login: "dependabot[bot]", avatar_url: "" },
    created_at: "2026-03-31T06:00:00Z",
    updated_at: "2026-03-31T06:00:00Z",
    labels: [{ name: "dependencies" }],
    body: "Bumps the Anthropic SDK from 1.3.2 to 1.4.0.\n\n### Changelog\n- Added streaming support for tool use\n- Fixed token counting for multi-turn conversations",
    head: { ref: "dependabot/nuget/anthropic-sdk-1.4.0" },
    base: { ref: "main" },
    draft: false,
    additions: 3,
    deletions: 3,
    changed_files: 1,
    mergeable: true,
    files: [
      { filename: "runtime/src/Purfle.Runtime.Anthropic/Purfle.Runtime.Anthropic.csproj", additions: 3, deletions: 3 },
    ],
  },
  {
    number: 39,
    title: "feat(ui): add dark mode toggle to settings page",
    state: "closed",
    user: { login: "marcus-r", avatar_url: "" },
    created_at: "2026-03-30T09:00:00Z",
    updated_at: "2026-03-31T14:00:00Z",
    labels: [{ name: "ui" }],
    body: "Adds a dark mode toggle to the Settings page. Persists preference via MAUI Preferences API.",
    head: { ref: "feat/dark-mode" },
    base: { ref: "main" },
    draft: false,
    additions: 62,
    deletions: 8,
    changed_files: 4,
    mergeable: true,
    files: [],
  },
];

// --- Health check ---

app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-github",
    version: "1.0.0",
    status: "ok",
    provider: "GitHub API (simulated)",
    tools: ["github/pulls", "github/pulls/:number"],
  });
});

// --- Tool endpoints (GET + POST) ---

app.get("/tools/github/pulls", (req: Request, res: Response) => {
  const state = (req.query.state as string) ?? "open";
  const results = mockPulls.filter((p) => state === "all" || p.state === state);

  res.json({
    tool: "github/pulls",
    provider: "github",
    result: {
      total_count: results.length,
      pull_requests: results.map((p) => ({
        number: p.number,
        title: p.title,
        state: p.state,
        user: p.user,
        created_at: p.created_at,
        updated_at: p.updated_at,
        labels: p.labels,
        draft: p.draft,
        additions: p.additions,
        deletions: p.deletions,
        changed_files: p.changed_files,
      })),
    },
  });
});

app.get("/tools/github/pulls/:number", (req: Request, res: Response) => {
  const num = parseInt(req.params.number);
  const pr = mockPulls.find((p) => p.number === num);
  if (!pr) {
    res.status(404).json({ error: `Pull request #${num} not found` });
    return;
  }
  res.json({ tool: "github/pulls/detail", provider: "github", result: pr });
});

app.post("/tools/github/pulls", (req: Request, res: Response) => {
  const state = req.body?.state ?? "open";
  const results = mockPulls.filter((p) => state === "all" || p.state === state);

  res.json({
    tool: "github/pulls",
    provider: "github",
    result: {
      total_count: results.length,
      pull_requests: results.map((p) => ({
        number: p.number,
        title: p.title,
        state: p.state,
        user: p.user,
        created_at: p.created_at,
        labels: p.labels,
        additions: p.additions,
        deletions: p.deletions,
        changed_files: p.changed_files,
      })),
    },
  });
});

app.listen(PORT, () => {
  console.log(`MCP GitHub server running on http://localhost:${PORT}`);
});
