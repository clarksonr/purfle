import { readdirSync, readFileSync, existsSync, rmSync, mkdirSync } from "fs";
import { createHash } from "crypto";
import { join } from "path";
import { homedir } from "os";
import { getRegistryUrl, apiGet, apiDownloadBinary, agentStorePath } from "../marketplace.js";
import { extractZip } from "../zip.js";

// ANSI helpers
const green = (s: string) => `\x1b[32m${s}\x1b[0m`;
const yellow = (s: string) => `\x1b[33m${s}\x1b[0m`;
const dim = (s: string) => `\x1b[2m${s}\x1b[0m`;
const bold = (s: string) => `\x1b[1m${s}\x1b[0m`;

interface ManifestData {
  id?: string;
  name?: string;
  version?: string;
  identity?: {
    signature?: string;
  };
}

interface AgentVersionInfo {
  versions?: Array<{
    version: string;
    bundleHash?: string;
  }>;
  latestVersion?: string;
}

interface UpdateOptions {
  registry?: string;
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

function padRight(s: string, len: number): string {
  if (s.length >= len) return s.slice(0, len - 1) + " ";
  return s + " ".repeat(len - s.length);
}

/**
 * Compare two semver strings. Returns:
 *  -1 if a < b, 0 if equal, 1 if a > b
 */
export function compareSemver(a: string, b: string): number {
  const pa = a.replace(/^v/, "").split(".").map(Number);
  const pb = b.replace(/^v/, "").split(".").map(Number);

  for (let i = 0; i < 3; i++) {
    const va = pa[i] ?? 0;
    const vb = pb[i] ?? 0;
    if (va < vb) return -1;
    if (va > vb) return 1;
  }
  return 0;
}

export async function updateCommand(agentId?: string, options?: UpdateOptions): Promise<void> {
  const registry = getRegistryUrl(options?.registry);
  const agentsDir = join(homedir(), ".purfle", "agents");

  // Determine which agents to check
  let agentIds: string[];
  if (agentId) {
    const storePath = agentStorePath(agentId);
    if (!existsSync(storePath)) {
      console.error(`Agent "${agentId}" is not installed.`);
      process.exit(1);
    }
    agentIds = [agentId];
  } else if (!existsSync(agentsDir)) {
    console.log("No agents installed. Nothing to update.");
    return;
  } else {
    try {
      agentIds = readdirSync(agentsDir, { withFileTypes: true })
        .filter((d) => d.isDirectory())
        .map((d) => d.name);
    } catch {
      console.log("No agents installed. Nothing to update.");
      return;
    }
  }

  if (agentIds.length === 0) {
    console.log("No agents installed. Nothing to update.");
    return;
  }

  console.log(`Checking ${agentIds.length} agent(s) for updates...\n`);

  const colAgent = 24;
  const colCurrent = 14;
  const colAvailable = 14;
  const colAction = 20;

  const header =
    padRight("Agent", colAgent) +
    padRight("Current", colCurrent) +
    padRight("Available", colAvailable) +
    "Action";

  console.log(bold(header));
  console.log(dim("-".repeat(colAgent + colCurrent + colAvailable + colAction)));

  let updated = 0;
  let upToDate = 0;
  let errors = 0;

  for (const id of agentIds) {
    const storePath = agentStorePath(id);
    const manifest = readManifest(storePath);
    const currentVersion = manifest?.version ?? "0.0.0";
    const displayName = manifest?.name ?? id;

    try {
      // Fetch agent info from marketplace
      const detailPath = `api/agents/${encodeURIComponent(id)}`;
      const detail = await apiGet<AgentVersionInfo>(registry, detailPath);
      const latestVersion = detail.latestVersion ?? "unknown";

      if (latestVersion === "unknown") {
        console.log(
          padRight(displayName.slice(0, colAgent - 2), colAgent) +
          padRight(currentVersion, colCurrent) +
          padRight("?", colAvailable) +
          dim("not in registry")
        );
        continue;
      }

      const cmp = compareSemver(currentVersion, latestVersion);

      if (cmp >= 0) {
        console.log(
          padRight(displayName.slice(0, colAgent - 2), colAgent) +
          padRight(currentVersion, colCurrent) +
          padRight(latestVersion, colAvailable) +
          green("up to date")
        );
        upToDate++;
        continue;
      }

      // Newer version available — download and install
      console.log(
        padRight(displayName.slice(0, colAgent - 2), colAgent) +
        padRight(currentVersion, colCurrent) +
        padRight(latestVersion, colAvailable) +
        yellow("updating...")
      );

      const bundlePath = `api/agents/${encodeURIComponent(id)}/latest/bundle`;
      const bundleData = await apiDownloadBinary(registry, bundlePath);

      if (!bundleData) {
        console.error(`  Failed to download bundle for ${id}`);
        errors++;
        continue;
      }

      // Verify SHA-256 if available
      const expectedHash = detail.versions?.find((v) => v.version === latestVersion)?.bundleHash;
      if (expectedHash) {
        const actualHash = createHash("sha256").update(bundleData).digest("hex");
        if (actualHash !== expectedHash) {
          console.error(`  SHA-256 integrity check failed for ${id}!`);
          console.error(`    Expected: ${expectedHash}`);
          console.error(`    Actual:   ${actualHash}`);
          errors++;
          continue;
        }
      }

      // Replace the agent directory
      if (existsSync(storePath)) rmSync(storePath, { recursive: true });
      mkdirSync(storePath, { recursive: true });
      extractZip(bundleData, storePath);

      console.log(`  ${green("Updated")} ${displayName} ${currentVersion} -> ${latestVersion}`);
      updated++;
    } catch (err) {
      console.log(
        padRight(displayName.slice(0, colAgent - 2), colAgent) +
        padRight(currentVersion, colCurrent) +
        padRight("-", colAvailable) +
        dim(`error: ${(err as Error).message.slice(0, 40)}`)
      );
      errors++;
    }
  }

  console.log(dim(`\n${updated} updated, ${upToDate} up to date, ${errors} error(s).`));
}
