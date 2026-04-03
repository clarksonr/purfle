import { existsSync, readdirSync, readFileSync } from "fs";
import { join } from "path";
import { homedir } from "os";
import { execSync } from "child_process";

// ANSI helpers
const green = (s: string) => `\x1b[32m${s}\x1b[0m`;
const red = (s: string) => `\x1b[31m${s}\x1b[0m`;
const yellow = (s: string) => `\x1b[33m${s}\x1b[0m`;
const dim = (s: string) => `\x1b[2m${s}\x1b[0m`;
const bold = (s: string) => `\x1b[1m${s}\x1b[0m`;

type CheckStatus = "pass" | "warn" | "fail";

interface CheckResult {
  name: string;
  status: CheckStatus;
  detail: string;
}

const KEY_REGISTRY_URL =
  "https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net";
const MARKETPLACE_URL = "http://localhost:5000";

function getCommandVersion(cmd: string): string | null {
  try {
    return execSync(`${cmd} --version`, {
      encoding: "utf8",
      stdio: ["pipe", "pipe", "pipe"],
      timeout: 10_000,
    }).trim();
  } catch {
    return null;
  }
}

function parseVersion(raw: string): number[] {
  const match = raw.match(/(\d+)\.(\d+)\.?(\d*)/);
  if (!match) return [0, 0, 0];
  return [parseInt(match[1], 10), parseInt(match[2], 10), parseInt(match[3] || "0", 10)];
}

function versionAtLeast(raw: string, major: number, minor: number): boolean {
  const parts = parseVersion(raw);
  if (parts[0] > major) return true;
  if (parts[0] === major && parts[1] >= minor) return true;
  return false;
}

function readManifest(dir: string): Record<string, unknown> | null {
  for (const name of ["agent.manifest.json", "agent.json"]) {
    const p = join(dir, name);
    if (existsSync(p)) {
      try {
        return JSON.parse(readFileSync(p, "utf8"));
      } catch {
        return null;
      }
    }
  }
  return null;
}

function padRight(s: string, len: number): string {
  if (s.length >= len) return s.slice(0, len - 1) + " ";
  return s + " ".repeat(len - s.length);
}

export async function doctorCommand(): Promise<void> {
  console.log(bold("\nPurfle Doctor\n"));
  console.log("Running diagnostics...\n");

  const results: CheckResult[] = [];

  // 1. dotnet runtime >= 8.0
  const dotnetVersion = getCommandVersion("dotnet");
  if (dotnetVersion && versionAtLeast(dotnetVersion, 8, 0)) {
    results.push({ name: ".NET Runtime", status: "pass", detail: dotnetVersion.split("\n")[0] });
  } else if (dotnetVersion) {
    results.push({ name: ".NET Runtime", status: "warn", detail: `${dotnetVersion.split("\n")[0]} (>= 8.0 recommended)` });
  } else {
    results.push({ name: ".NET Runtime", status: "fail", detail: "not found — install from https://dot.net" });
  }

  // 2. node runtime >= 18.0
  const nodeVersion = getCommandVersion("node");
  if (nodeVersion && versionAtLeast(nodeVersion, 18, 0)) {
    results.push({ name: "Node.js Runtime", status: "pass", detail: nodeVersion });
  } else if (nodeVersion) {
    results.push({ name: "Node.js Runtime", status: "warn", detail: `${nodeVersion} (>= 18.0 recommended)` });
  } else {
    results.push({ name: "Node.js Runtime", status: "fail", detail: "not found — install from https://nodejs.org" });
  }

  // 3. Signing key exists
  const signingKeyPath = join("temp-agent", "signing.key.pem");
  if (existsSync(signingKeyPath)) {
    results.push({ name: "Signing Key", status: "pass", detail: signingKeyPath });
  } else {
    results.push({ name: "Signing Key", status: "warn", detail: "not found at temp-agent/signing.key.pem" });
  }

  // 4-6. API keys
  const geminiKey = process.env.GEMINI_API_KEY;
  const anthropicKey = process.env.ANTHROPIC_API_KEY;
  const openaiKey = process.env.OPENAI_API_KEY;

  results.push({
    name: "GEMINI_API_KEY",
    status: geminiKey ? "pass" : "warn",
    detail: geminiKey ? `set (${geminiKey.slice(0, 6)}...)` : "not set",
  });

  results.push({
    name: "ANTHROPIC_API_KEY",
    status: anthropicKey ? "pass" : "warn",
    detail: anthropicKey ? `set (${anthropicKey.slice(0, 6)}...)` : "not set",
  });

  results.push({
    name: "OPENAI_API_KEY",
    status: openaiKey ? "pass" : "warn",
    detail: openaiKey ? `set (${openaiKey.slice(0, 6)}...)` : "not set",
  });

  // 7. At least one API key set
  if (geminiKey || anthropicKey || openaiKey) {
    results.push({ name: "Any API Key", status: "pass", detail: "at least one LLM key configured" });
  } else {
    results.push({ name: "Any API Key", status: "fail", detail: "no LLM API key set — agents cannot run inference" });
  }

  // 8. Key Registry reachable
  try {
    const resp = await fetch(`${KEY_REGISTRY_URL}/api/health`, { signal: AbortSignal.timeout(5000) });
    if (resp.ok || resp.status === 404) {
      results.push({ name: "Key Registry", status: "pass", detail: `reachable (HTTP ${resp.status})` });
    } else {
      results.push({ name: "Key Registry", status: "warn", detail: `HTTP ${resp.status}` });
    }
  } catch (e) {
    results.push({ name: "Key Registry", status: "warn", detail: `unreachable: ${(e as Error).message.slice(0, 50)}` });
  }

  // 9. Marketplace API reachable
  const marketplaceUrl = process.env.PURFLE_REGISTRY ?? MARKETPLACE_URL;
  try {
    const resp = await fetch(`${marketplaceUrl}/health`, { signal: AbortSignal.timeout(5000) });
    if (resp.ok) {
      results.push({ name: "Marketplace API", status: "pass", detail: `reachable at ${marketplaceUrl}` });
    } else {
      results.push({ name: "Marketplace API", status: "warn", detail: `HTTP ${resp.status} at ${marketplaceUrl}` });
    }
  } catch {
    results.push({ name: "Marketplace API", status: "warn", detail: `not reachable at ${marketplaceUrl}` });
  }

  // 10. Installed agents count
  const agentsDir = join(homedir(), ".purfle", "agents");
  let agentDirs: string[] = [];
  if (existsSync(agentsDir)) {
    try {
      agentDirs = readdirSync(agentsDir, { withFileTypes: true })
        .filter((d) => d.isDirectory())
        .map((d) => d.name);
    } catch {
      // ignore
    }
  }
  results.push({
    name: "Installed Agents",
    status: agentDirs.length > 0 ? "pass" : "warn",
    detail: `${agentDirs.length} agent(s) found`,
  });

  // 11. Manifest validity
  let validManifests = 0;
  let invalidManifests = 0;
  const manifestErrors: string[] = [];

  for (const id of agentDirs) {
    const agentDir = join(agentsDir, id);
    const manifest = readManifest(agentDir);
    if (!manifest) {
      invalidManifests++;
      manifestErrors.push(`${id}: no manifest file`);
      continue;
    }

    // Check required fields
    const requiredFields = ["purfle", "id", "name", "version"];
    const missing = requiredFields.filter((f) => !(f in manifest));
    if (missing.length > 0) {
      invalidManifests++;
      manifestErrors.push(`${id}: missing ${missing.join(", ")}`);
    } else {
      validManifests++;
    }
  }

  if (agentDirs.length === 0) {
    results.push({ name: "Manifest Validity", status: "warn", detail: "no agents to check" });
  } else if (invalidManifests === 0) {
    results.push({ name: "Manifest Validity", status: "pass", detail: `${validManifests}/${agentDirs.length} valid` });
  } else {
    results.push({
      name: "Manifest Validity",
      status: "fail",
      detail: `${invalidManifests} invalid: ${manifestErrors.slice(0, 2).join("; ")}`,
    });
  }

  // 12. Signature freshness
  let expiredCount = 0;
  let checkedCount = 0;
  for (const id of agentDirs) {
    const agentDir = join(agentsDir, id);
    const manifest = readManifest(agentDir);
    if (!manifest) continue;

    const identity = manifest.identity as { expires_at?: string } | undefined;
    if (identity?.expires_at) {
      checkedCount++;
      const expiresAt = new Date(identity.expires_at);
      if (expiresAt.getTime() < Date.now()) {
        expiredCount++;
      }
    }
  }

  if (checkedCount === 0) {
    results.push({ name: "Signature Freshness", status: "warn", detail: "no signatures with expires_at to check" });
  } else if (expiredCount === 0) {
    results.push({ name: "Signature Freshness", status: "pass", detail: `${checkedCount} signature(s) current` });
  } else {
    results.push({ name: "Signature Freshness", status: "fail", detail: `${expiredCount} expired signature(s)` });
  }

  // Print results table
  const colCheck = 22;
  const colStatus = 10;

  const header = padRight("Check", colCheck) + padRight("Status", colStatus) + "Detail";
  console.log(bold(header));
  console.log(dim("-".repeat(colCheck + colStatus + 40)));

  let hasFail = false;

  for (const r of results) {
    let statusIcon: string;
    switch (r.status) {
      case "pass":
        statusIcon = green("\u2713 PASS");
        break;
      case "warn":
        statusIcon = yellow("\u26A0 WARN");
        break;
      case "fail":
        statusIcon = red("\u2717 FAIL");
        hasFail = true;
        break;
    }

    console.log(
      padRight(r.name, colCheck) +
      statusIcon! + " ".repeat(Math.max(1, colStatus - 6)) +
      r.detail
    );
  }

  const passCount = results.filter((r) => r.status === "pass").length;
  const warnCount = results.filter((r) => r.status === "warn").length;
  const failCount = results.filter((r) => r.status === "fail").length;

  console.log(dim(`\n${results.length} checks: `) +
    green(`${passCount} passed`) + ", " +
    yellow(`${warnCount} warnings`) + ", " +
    (failCount > 0 ? red(`${failCount} failed`) : `${failCount} failed`)
  );

  if (hasFail) {
    console.log(red("\nSome checks failed. See details above."));
    process.exit(1);
  } else {
    console.log(green("\nAll checks passed."));
  }
}
