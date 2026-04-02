import express, { Request, Response } from "express";

const app = express();
const PORT = 8105;

app.use(express.json());

// Health check
app.get("/", (_req: Request, res: Response) => {
  res.json({ status: "ok", server: "mcp-api-tools", port: PORT });
});

// api/health — checks a URL and returns status code + response time (mock)
app.post("/tools/api/health", (req: Request, res: Response) => {
  const { url } = req.body ?? {};
  if (!url) {
    res.status(400).json({ error: "Missing required parameter: url" });
    return;
  }

  const statusCode = 200;
  const responseTimeMs = Math.floor(Math.random() * 300) + 20;

  res.json({
    tool: "api/health",
    result: {
      url,
      status_code: statusCode,
      response_time_ms: responseTimeMs,
      healthy: statusCode >= 200 && statusCode < 400,
      checked_at: new Date().toISOString(),
    },
  });
});

// api/latency — returns latency percentiles for an endpoint (mock)
app.post("/tools/api/latency", (req: Request, res: Response) => {
  const { endpoint } = req.body ?? {};
  if (!endpoint) {
    res.status(400).json({ error: "Missing required parameter: endpoint" });
    return;
  }

  const p50 = Math.floor(Math.random() * 100) + 10;
  const p95 = p50 + Math.floor(Math.random() * 200) + 50;
  const p99 = p95 + Math.floor(Math.random() * 300) + 100;

  res.json({
    tool: "api/latency",
    result: {
      endpoint,
      percentiles: {
        p50: `${p50}ms`,
        p95: `${p95}ms`,
        p99: `${p99}ms`,
      },
      sample_count: Math.floor(Math.random() * 9000) + 1000,
      window: "last 5 minutes",
      measured_at: new Date().toISOString(),
    },
  });
});

// api/schema-diff — compares two OpenAPI schemas and returns differences (mock)
app.post("/tools/api/schema-diff", (req: Request, res: Response) => {
  const { schema_a, schema_b } = req.body ?? {};
  if (!schema_a || !schema_b) {
    res
      .status(400)
      .json({ error: "Missing required parameters: schema_a, schema_b" });
    return;
  }

  res.json({
    tool: "api/schema-diff",
    result: {
      schema_a,
      schema_b,
      breaking_changes: [
        {
          type: "removed_endpoint",
          path: "/api/v1/users/{id}/profile",
          method: "DELETE",
          severity: "breaking",
        },
      ],
      non_breaking_changes: [
        {
          type: "added_endpoint",
          path: "/api/v2/users/{id}/settings",
          method: "GET",
          severity: "info",
        },
        {
          type: "added_field",
          path: "/api/v1/users",
          field: "created_at",
          severity: "info",
        },
      ],
      summary: {
        breaking: 1,
        non_breaking: 2,
        total: 3,
      },
      compared_at: new Date().toISOString(),
    },
  });
});

app.listen(PORT, () => {
  console.log(`mcp-api-tools server running on http://localhost:${PORT}`);
});
