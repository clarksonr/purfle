import { readFileSync, existsSync } from "fs";
import { join, resolve, extname } from "path";
import { spawn, ChildProcess } from "child_process";
import { parseManifest } from "@purfle/core";
import type { AgentManifest } from "@purfle/core";

interface RunOptions {
  timeout?: string;
  verbose?: boolean;
}

/**
 * Resolves the agent entrypoint by looking for known file types in the agent directory.
 * Checks lifecycle.on_load first, then falls back to convention-based discovery.
 */
function resolveEntrypoint(dir: string, manifest: AgentManifest): string | null {
  // 1. Check lifecycle.on_load if present
  if (manifest.lifecycle?.on_load) {
    const explicit = join(dir, manifest.lifecycle.on_load);
    if (existsSync(explicit)) return explicit;
  }

  // 2. Convention-based discovery: look for known entrypoint files
  const candidates = [
    // .NET assembly
    "lib/agent.dll",
    `lib/${manifest.name.replace(/\s+/g, "")}.dll`,
    // Node.js
    "index.js",
    "main.js",
    "lib/index.js",
    // TypeScript (pre-compiled)
    "dist/index.js",
    "dist/main.js",
    // Python
    "main.py",
    "agent.py",
  ];

  for (const candidate of candidates) {
    const full = join(dir, candidate);
    if (existsSync(full)) return full;
  }

  return null;
}

/**
 * Determines the runtime command and args for a given entrypoint.
 */
function getRuntimeCommand(entrypoint: string): { cmd: string; args: string[] } {
  const ext = extname(entrypoint).toLowerCase();
  switch (ext) {
    case ".dll":
      return { cmd: "dotnet", args: [entrypoint] };
    case ".js":
      return { cmd: "node", args: [entrypoint] };
    case ".py":
      return { cmd: "python", args: [entrypoint] };
    default:
      return { cmd: entrypoint, args: [] };
  }
}

export function runCommand(dir: string, options: RunOptions): void {
  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifest = parseManifest(readFileSync(manifestPath, "utf8"));

  console.log(`[purfle] Loading agent: ${manifest.name} v${manifest.version}`);
  console.log(`[purfle]   id:     ${manifest.id}`);
  console.log(`[purfle]   engine: ${manifest.runtime.engine}`);

  if (options.verbose) {
    console.log(`[purfle]   capabilities: ${manifest.capabilities.join(", ") || "(none)"}`);
    if (manifest.tools && manifest.tools.length > 0) {
      console.log(`[purfle]   tools: ${manifest.tools.map((t) => t.name).join(", ")}`);
    }
  }

  // Resolve entrypoint
  const absDir = resolve(dir);
  const entrypoint = resolveEntrypoint(absDir, manifest);

  if (!entrypoint) {
    console.error(`[purfle] No entrypoint found in '${absDir}'.`);
    console.error(`[purfle] Checked: lifecycle.on_load, lib/*.dll, index.js, main.js, main.py`);
    console.error(`[purfle] Create an entrypoint file or set lifecycle.on_load in the manifest.`);
    process.exit(1);
  }

  console.log(`[purfle]   entrypoint: ${entrypoint}`);
  console.log();

  const { cmd, args } = getRuntimeCommand(entrypoint);

  if (options.verbose) {
    console.log(`[purfle] Spawning: ${cmd} ${args.join(" ")}`);
  }

  // Build environment: inherit current env, add PURFLE variables
  const env: Record<string, string> = {
    ...process.env as Record<string, string>,
    PURFLE_AGENT_ID: manifest.id,
    PURFLE_AGENT_NAME: manifest.name,
    PURFLE_AGENT_VERSION: manifest.version,
    PURFLE_ENGINE: manifest.runtime.engine,
    PURFLE_DEV_MODE: "true",
  };

  // Parse timeout
  const timeoutMs = options.timeout ? parseInt(options.timeout, 10) * 1000 : 0;

  let child: ChildProcess;
  try {
    child = spawn(cmd, args, {
      cwd: absDir,
      env,
      stdio: ["pipe", "pipe", "pipe"],
    });
  } catch (e) {
    console.error(`[purfle] Failed to spawn process: ${(e as Error).message}`);
    process.exit(1);
  }

  // Pipe stdout/stderr to terminal with prefix
  child.stdout?.on("data", (data: Buffer) => {
    const lines = data.toString().split("\n");
    for (const line of lines) {
      if (line.trim()) {
        process.stdout.write(`  [${manifest.name}] ${line}\n`);
      }
    }
  });

  child.stderr?.on("data", (data: Buffer) => {
    const lines = data.toString().split("\n");
    for (const line of lines) {
      if (line.trim()) {
        process.stderr.write(`  [${manifest.name}:err] ${line}\n`);
      }
    }
  });

  // Handle Ctrl+C gracefully
  const cleanup = () => {
    console.log();
    console.log(`[purfle] Stopping agent...`);
    child.kill("SIGTERM");
    // Give it a moment to clean up, then force kill
    setTimeout(() => {
      if (!child.killed) {
        child.kill("SIGKILL");
      }
    }, 3000);
  };

  process.on("SIGINT", cleanup);
  process.on("SIGTERM", cleanup);

  // Timeout handling
  let timeoutHandle: NodeJS.Timeout | undefined;
  if (timeoutMs > 0) {
    timeoutHandle = setTimeout(() => {
      console.log(`[purfle] Timeout reached (${options.timeout}s). Stopping agent.`);
      child.kill("SIGTERM");
    }, timeoutMs);
  }

  child.on("close", (code) => {
    if (timeoutHandle) clearTimeout(timeoutHandle);
    process.removeListener("SIGINT", cleanup);
    process.removeListener("SIGTERM", cleanup);

    console.log();
    if (code === 0 || code === null) {
      console.log(`[purfle] Agent exited normally.`);
    } else {
      console.log(`[purfle] Agent exited with code ${code}.`);
    }
  });

  child.on("error", (err) => {
    console.error(`[purfle] Process error: ${err.message}`);
    if (err.message.includes("ENOENT")) {
      console.error(`[purfle] Runtime '${cmd}' not found. Is it installed and on PATH?`);
    }
    process.exit(1);
  });
}
