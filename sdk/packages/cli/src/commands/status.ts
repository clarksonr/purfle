import { readdirSync, readFileSync, existsSync } from "fs";
import { join } from "path";
import { homedir } from "os";

// ANSI helpers
const green = (s: string) => `\x1b[32m${s}\x1b[0m`;
const red = (s: string) => `\x1b[31m${s}\x1b[0m`;
const dim = (s: string) => `\x1b[2m${s}\x1b[0m`;
const bold = (s: string) => `\x1b[1m${s}\x1b[0m`;

interface RunEntry {
  agent_id: string;
  agent_name: string;
  trigger_time: string;
  duration_ms: number;
  status: string;
  input_tokens: number;
  output_tokens: number;
  output_path: string;
  error: string | null;
}

interface ManifestData {
  id?: string;
  name?: string;
  schedule?: {
    trigger?: string;
    interval_minutes?: number;
    cron?: string;
  };
}

function getOutputDir(): string {
  if (process.platform === "win32") {
    return join(process.env.LOCALAPPDATA ?? join(homedir(), "AppData", "Local"), "aivm", "output");
  }
  return join(homedir(), ".local", "share", "aivm", "output");
}

function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  const now = Date.now();
  const diffMs = now - then;

  if (diffMs < 0) return "in the future";
  if (diffMs < 60_000) return "just now";
  if (diffMs < 3_600_000) {
    const mins = Math.floor(diffMs / 60_000);
    return `${mins} minute${mins === 1 ? "" : "s"} ago`;
  }
  if (diffMs < 86_400_000) {
    const hrs = Math.floor(diffMs / 3_600_000);
    return `${hrs} hour${hrs === 1 ? "" : "s"} ago`;
  }
  const days = Math.floor(diffMs / 86_400_000);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}

function computeNextRun(manifest: ManifestData, lastRunIso: string | null): string {
  const trigger = manifest.schedule?.trigger;
  if (!trigger) return "unknown";

  if (trigger === "startup") return "on startup";
  if (trigger === "event") return "on event";

  if (trigger === "interval" && manifest.schedule?.interval_minutes) {
    if (!lastRunIso) return "pending";
    const lastRun = new Date(lastRunIso).getTime();
    const nextMs = lastRun + manifest.schedule.interval_minutes * 60_000;
    const now = Date.now();
    if (nextMs <= now) return "overdue";
    const diffMs = nextMs - now;
    if (diffMs < 60_000) return "< 1 minute";
    if (diffMs < 3_600_000) {
      const mins = Math.floor(diffMs / 60_000);
      return `in ${mins} min`;
    }
    const hrs = Math.floor(diffMs / 3_600_000);
    return `in ${hrs} hr`;
  }

  if (trigger === "cron" && manifest.schedule?.cron) {
    return `cron: ${manifest.schedule.cron}`;
  }

  if (trigger === "window") return "windowed";

  return "unknown";
}

function readManifest(agentDir: string): ManifestData | null {
  for (const name of ["agent.manifest.json", "agent.json"]) {
    const p = join(agentDir, name);
    if (existsSync(p)) {
      try {
        return JSON.parse(readFileSync(p, "utf8")) as ManifestData;
      } catch {
        return null;
      }
    }
  }
  return null;
}

function getLastRun(agentId: string): RunEntry | null {
  const outputDir = getOutputDir();
  const runJsonl = join(outputDir, agentId, "run.jsonl");
  if (!existsSync(runJsonl)) return null;

  try {
    const content = readFileSync(runJsonl, "utf8").trim();
    if (!content) return null;
    const lines = content.split("\n");
    const lastLine = lines[lines.length - 1];
    return JSON.parse(lastLine) as RunEntry;
  } catch {
    return null;
  }
}

function padRight(s: string, len: number): string {
  if (s.length >= len) return s.slice(0, len - 1) + " ";
  return s + " ".repeat(len - s.length);
}

export async function statusCommand(): Promise<void> {
  const agentsDir = join(homedir(), ".purfle", "agents");

  if (!existsSync(agentsDir)) {
    console.log("No agents installed. Try: purfle demo");
    return;
  }

  let entries: string[];
  try {
    entries = readdirSync(agentsDir, { withFileTypes: true })
      .filter((d) => d.isDirectory())
      .map((d) => d.name);
  } catch {
    console.log("No agents installed. Try: purfle demo");
    return;
  }

  if (entries.length === 0) {
    console.log("No agents installed. Try: purfle demo");
    return;
  }

  const colId = 24;
  const colName = 22;
  const colTrigger = 14;
  const colLastRun = 20;
  const colNextRun = 16;
  const colStatus = 12;

  const header =
    padRight("ID", colId) +
    padRight("Name", colName) +
    padRight("Trigger", colTrigger) +
    padRight("Last Run", colLastRun) +
    padRight("Next Run", colNextRun) +
    "Status";

  console.log(bold(header));
  console.log(dim("-".repeat(colId + colName + colTrigger + colLastRun + colNextRun + colStatus)));

  for (const agentId of entries) {
    const agentDir = join(agentsDir, agentId);
    const manifest = readManifest(agentDir);
    const lastRun = getLastRun(agentId);

    const name = manifest?.name ?? agentId;
    const trigger = manifest?.schedule?.trigger ?? "unknown";
    const lastRunStr = lastRun ? relativeTime(lastRun.trigger_time) : "Never";
    const nextRunStr = computeNextRun(manifest ?? {}, lastRun?.trigger_time ?? null);

    let statusStr: string;
    if (!lastRun) {
      statusStr = dim("NEVER RUN");
    } else if (lastRun.status === "success") {
      statusStr = green("OK");
    } else {
      statusStr = red("ERROR");
    }

    console.log(
      padRight(agentId.slice(0, colId - 2), colId) +
      padRight(name.slice(0, colName - 2), colName) +
      padRight(trigger, colTrigger) +
      padRight(lastRunStr, colLastRun) +
      padRight(nextRunStr, colNextRun) +
      statusStr
    );
  }

  console.log(dim(`\n${entries.length} agent(s) installed.`));
}
