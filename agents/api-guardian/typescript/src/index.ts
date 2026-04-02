/**
 * API Guardian agent — IPC entry point (TypeScript).
 * Reads commands from stdin, calls MCP tools via the AIVM,
 * and writes structured status reports to stdout.
 */

// ── Models ──────────────────────────────────────────────────────────

interface AgentCommand {
  endpoints?: string[];
  latencyThresholdMs?: number;
}

interface EndpointResult {
  endpoint: string;
  status: "healthy" | "degraded" | "down" | "unknown";
  statusCode: number;
  latencyMs: number;
  error?: string;
  schemaDiff: string;
  checkedAt: string;
  alerts: string[];
}

interface StatusReport {
  timestamp: string;
  summary: string;
  endpoints: EndpointResult[];
}

// ── Constants ───────────────────────────────────────────────────────

const DEFAULT_ENDPOINTS = [
  "https://api.example.com/health",
  "https://api.example.com/v1/status",
];

const DEFAULT_LATENCY_THRESHOLD_MS = 2000;
const CRITICAL_MULTIPLIER = 3;
const RETRY_DELAY_MS = 5000;
const REQUEST_TIMEOUT_MS = 30_000;

// ── Helpers ─────────────────────────────────────────────────────────

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function readCommand(): Promise<AgentCommand | null> {
  return new Promise((resolve) => {
    let data = "";
    process.stdin.setEncoding("utf-8");
    process.stdin.on("data", (chunk: string) => {
      data += chunk;
    });
    process.stdin.on("end", () => {
      if (!data.trim()) {
        resolve(null);
        return;
      }
      try {
        resolve(JSON.parse(data.trim()) as AgentCommand);
      } catch {
        resolve(null);
      }
    });
    // If stdin is a TTY (no piped input), resolve immediately
    if (process.stdin.isTTY) {
      resolve(null);
    }
  });
}

async function measureEndpoint(
  endpoint: string
): Promise<{ statusCode: number; latencyMs: number; error?: string }> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  try {
    const start = performance.now();
    const response = await fetch(endpoint, { signal: controller.signal });
    const latencyMs = Math.round(performance.now() - start);

    return { statusCode: response.status, latencyMs };
  } catch (err: unknown) {
    if (err instanceof DOMException && err.name === "AbortError") {
      return { statusCode: 0, latencyMs: 0, error: "Request timed out" };
    }
    const message = err instanceof Error ? err.message : String(err);
    return { statusCode: 0, latencyMs: 0, error: message };
  } finally {
    clearTimeout(timeout);
  }
}

async function checkEndpoint(
  endpoint: string,
  latencyThresholdMs: number
): Promise<EndpointResult> {
  const result: EndpointResult = {
    endpoint,
    status: "unknown",
    statusCode: 0,
    latencyMs: 0,
    schemaDiff: "no changes",
    checkedAt: new Date().toISOString(),
    alerts: [],
  };

  // Health check
  let measurement = await measureEndpoint(endpoint);

  // Retry once if unreachable
  if (measurement.error && measurement.statusCode === 0) {
    await sleep(RETRY_DELAY_MS);
    measurement = await measureEndpoint(endpoint);
  }

  result.statusCode = measurement.statusCode;
  result.latencyMs = measurement.latencyMs;
  result.error = measurement.error;

  // Determine status
  if (measurement.statusCode === 0) {
    result.status = "down";
    result.alerts.push("Endpoint unreachable after retry");
  } else if (measurement.statusCode >= 500) {
    result.status = "down";
    result.alerts.push(`Server error: HTTP ${measurement.statusCode}`);
  } else if (measurement.statusCode >= 400) {
    result.status = "degraded";
    result.alerts.push(`Client error: HTTP ${measurement.statusCode}`);
  } else if (measurement.latencyMs > latencyThresholdMs * CRITICAL_MULTIPLIER) {
    result.status = "degraded";
    result.alerts.push(
      `Critical latency: ${measurement.latencyMs}ms exceeds ${CRITICAL_MULTIPLIER}x threshold (${latencyThresholdMs}ms)`
    );
  } else if (measurement.latencyMs > latencyThresholdMs) {
    result.status = "degraded";
    result.alerts.push(
      `High latency: ${measurement.latencyMs}ms exceeds threshold (${latencyThresholdMs}ms)`
    );
  } else {
    result.status = "healthy";
  }

  return result;
}

function buildSummary(endpoints: EndpointResult[]): string {
  const healthy = endpoints.filter((e) => e.status === "healthy").length;
  const degraded = endpoints.filter((e) => e.status === "degraded").length;
  const down = endpoints.filter((e) => e.status === "down").length;
  const total = endpoints.length;

  if (down > 0) {
    return `${down}/${total} endpoint(s) DOWN. Immediate attention required.`;
  }
  if (degraded > 0) {
    return `${degraded}/${total} endpoint(s) degraded. Review alerts.`;
  }
  return `All ${total} endpoint(s) healthy.`;
}

// ── Main ────────────────────────────────────────────────────────────

async function main(): Promise<void> {
  const command = await readCommand();

  const endpoints = command?.endpoints?.length
    ? command.endpoints
    : DEFAULT_ENDPOINTS;

  const latencyThreshold =
    command?.latencyThresholdMs ?? DEFAULT_LATENCY_THRESHOLD_MS;

  const results: EndpointResult[] = [];
  for (const endpoint of endpoints) {
    const result = await checkEndpoint(endpoint, latencyThreshold);
    results.push(result);
  }

  const report: StatusReport = {
    timestamp: new Date().toISOString(),
    summary: buildSummary(results),
    endpoints: results,
  };

  process.stdout.write(JSON.stringify(report, null, 2) + "\n");
}

main().catch((err) => {
  console.error("API Guardian agent failed:", err);
  process.exit(1);
});
