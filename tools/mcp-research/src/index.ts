import express, { Request, Response } from "express";

const app = express();
const PORT = 8109;

app.use(express.json());

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({
    name: "@purfle/mcp-research",
    version: "1.0.0",
    status: "healthy",
    tools: [
      "research/web-search",
      "research/fetch-page",
      "research/extract-links",
    ],
  });
});

// POST /tools/research/web-search
app.post("/tools/research/web-search", (req: Request, res: Response) => {
  const { query } = req.body ?? {};
  const searchQuery = query ?? "multi-agent AI systems";

  res.json({
    tool: "research/web-search",
    query: searchQuery,
    results: [
      {
        title: "Multi-Agent Systems: A Survey of Architectures and Applications",
        url: "https://arxiv.org/abs/2401.12345",
        snippet:
          "This paper surveys recent advances in multi-agent AI systems, covering cooperative, competitive, and mixed architectures. We analyze 150+ systems deployed between 2024-2026.",
      },
      {
        title: "Building Reliable AI Agents — Anthropic Research",
        url: "https://www.anthropic.com/research/reliable-agents",
        snippet:
          "We present techniques for building AI agents that operate reliably in production environments, including sandboxing, capability negotiation, and output verification.",
      },
      {
        title: "The Agent Runtime Pattern: Lessons from Production Deployments",
        url: "https://blog.example.com/agent-runtime-pattern",
        snippet:
          "After deploying agent runtimes to 10,000+ users, we share lessons learned about scheduling, resource isolation, and credential management.",
      },
      {
        title: "MCP: Model Context Protocol Specification",
        url: "https://modelcontextprotocol.io/spec",
        snippet:
          "The Model Context Protocol (MCP) is an open standard for connecting AI assistants to external tools and data sources. Version 1.2 adds streaming and batch operations.",
      },
      {
        title: "Sandboxing AI Agents: Security Considerations",
        url: "https://security.example.org/ai-agent-sandboxing",
        snippet:
          "A comprehensive guide to sandboxing AI agents in desktop and server environments. Covers filesystem isolation, network policies, and capability-based security.",
      },
    ],
    total_results: 48200,
    search_time_ms: 245,
  });
});

// POST /tools/research/fetch-page
app.post("/tools/research/fetch-page", (req: Request, res: Response) => {
  const { url } = req.body ?? {};
  const pageUrl = url ?? "https://www.anthropic.com/research/reliable-agents";

  res.json({
    tool: "research/fetch-page",
    url: pageUrl,
    page: {
      title: "Building Reliable AI Agents — Anthropic Research",
      fetched_at: "2026-04-01T10:30:00Z",
      content_type: "text/html",
      text_excerpt:
        "Building Reliable AI Agents\n\n" +
        "Introduction\n" +
        "As AI agents move from research prototypes to production systems, reliability becomes " +
        "the primary engineering challenge. An agent that works 95% of the time in a demo may " +
        "fail unpredictably when running unattended on a schedule.\n\n" +
        "Key Principles\n" +
        "1. Capability Negotiation — Agents declare what they need; the runtime decides what to grant. " +
        "This eliminates an entire class of permission-escalation bugs.\n\n" +
        "2. Sandboxed Execution — Each agent runs in an isolated context with filesystem, network, " +
        "and environment access restricted to its declared permissions.\n\n" +
        "3. Structured Output — Agents write to runtime-assigned paths. The runtime validates output " +
        "format and size before persisting.\n\n" +
        "4. Credential Isolation — API keys and tokens are held by the runtime, never exposed to " +
        "agent code. The runtime injects authenticated clients at execution time.\n\n" +
        "Results\n" +
        "In a deployment of 500 agents across 12,000 desktop installations, the capability-negotiation " +
        "pattern reduced security incidents by 98% compared to the previous unrestricted model.",
      word_count: 1847,
      language: "en",
    },
  });
});

// POST /tools/research/extract-links
app.post("/tools/research/extract-links", (req: Request, res: Response) => {
  const { url } = req.body ?? {};
  const pageUrl = url ?? "https://www.anthropic.com/research/reliable-agents";

  res.json({
    tool: "research/extract-links",
    url: pageUrl,
    links: [
      {
        text: "Capability Negotiation Deep Dive",
        url: "https://www.anthropic.com/research/capability-negotiation",
        type: "internal",
      },
      {
        text: "Model Context Protocol Specification",
        url: "https://modelcontextprotocol.io/spec",
        type: "external",
      },
      {
        text: "Agent Sandboxing Technical Report (PDF)",
        url: "https://www.anthropic.com/papers/agent-sandboxing.pdf",
        type: "internal",
      },
      {
        text: "Multi-Agent Systems Survey",
        url: "https://arxiv.org/abs/2401.12345",
        type: "external",
      },
      {
        text: "GitHub: Reference Runtime Implementation",
        url: "https://github.com/anthropics/agent-runtime-reference",
        type: "external",
      },
      {
        text: "Structured Output Validation",
        url: "https://www.anthropic.com/research/structured-output",
        type: "internal",
      },
      {
        text: "Security Best Practices for AI Agents",
        url: "https://security.example.org/ai-agent-best-practices",
        type: "external",
      },
    ],
    total_links: 7,
    internal_count: 3,
    external_count: 4,
  });
});

app.listen(PORT, () => {
  console.log(`@purfle/mcp-research running on http://localhost:${PORT}`);
});
