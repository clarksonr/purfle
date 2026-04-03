import { spawn, ChildProcess, execSync } from "child_process";
import { existsSync, readFileSync } from "fs";
import { resolve } from "path";

interface ServerDef {
  name: string;
  dir: string;
  port: number;
  label: string;
  capability: string;
}

// ANSI color helpers
const green  = (s: string) => `\x1b[32m${s}\x1b[0m`;
const red    = (s: string) => `\x1b[31m${s}\x1b[0m`;
const yellow = (s: string) => `\x1b[33m${s}\x1b[0m`;
const bold   = (s: string) => `\x1b[1m${s}\x1b[0m`;
const dim    = (s: string) => `\x1b[2m${s}\x1b[0m`;
const cyan   = (s: string) => `\x1b[36m${s}\x1b[0m`;

export async function demoCommand(): Promise<void> {
  // Resolve tools/ relative to the CLI package root (sdk/packages/cli/dist → repo root)
  const cliRoot = resolve(__dirname, "..", "..");
  const repoRoot = resolve(cliRoot, "..", "..", "..");
  const toolsDir = resolve(repoRoot, "tools");

  // Read version from package.json
  let version = "0.1.0";
  try {
    const pkg = JSON.parse(readFileSync(resolve(cliRoot, "package.json"), "utf-8"));
    version = pkg.version ?? version;
  } catch { /* use default */ }

  // Print banner
  console.log("");
  console.log(cyan("  ╔══════════════════════════════════════════════════════╗"));
  console.log(cyan("  ║") + bold("   Purfle Demo — AIVM Multi-Agent Desktop Runtime   ") + cyan("║"));
  console.log(cyan("  ║") + dim(`                    v${version}                          `.slice(0, 52)) + cyan("║"));
  console.log(cyan("  ╚══════════════════════════════════════════════════════╝"));
  console.log("");

  const servers: ServerDef[] = [
    { name: "mcp-file-server", dir: resolve(toolsDir, "mcp-file-server"), port: 8100, label: "file",   capability: "fs.read / fs.write" },
    { name: "mcp-gmail",       dir: resolve(toolsDir, "mcp-gmail"),       port: 8102, label: "gmail",  capability: "Email read/send (OAuth)" },
    { name: "mcp-github",      dir: resolve(toolsDir, "mcp-github"),      port: 8111, label: "github", capability: "GitHub REST API" },
  ];

  // Check API keys
  if (!process.env.GEMINI_API_KEY && !process.env.ANTHROPIC_API_KEY && !process.env.OPENAI_API_KEY) {
    console.warn(yellow("  ⚠ No LLM API key set (GEMINI_API_KEY, ANTHROPIC_API_KEY, or OPENAI_API_KEY)."));
    console.warn(yellow("    Agent inference will not work without at least one key.\n"));
  }

  // Check purfle:// URI scheme (best-effort on Windows via registry, macOS via lsregister)
  checkUriScheme();

  // Verify server directories exist
  for (const s of servers) {
    if (!existsSync(s.dir)) {
      console.error(red(`  ✗ MCP server directory not found: ${s.dir}`));
      console.error(dim("    Run from the repo root, or use --verbose for details."));
      process.exit(1);
    }
  }

  const children: ChildProcess[] = [];
  const serverStatus: Map<string, "ok" | "error"> = new Map();

  // Clean shutdown on Ctrl+C
  const cleanup = () => {
    console.log(dim("\n  Shutting down MCP servers..."));
    for (const child of children) {
      child.kill("SIGTERM");
    }
    process.exit(0);
  };
  process.on("SIGINT", cleanup);
  process.on("SIGTERM", cleanup);

  console.log(dim("  Starting MCP servers...\n"));

  // Start each server
  for (const s of servers) {
    try {
      const child = spawn("npm", ["start"], {
        cwd: s.dir,
        stdio: "ignore",
        shell: true,
        env: { ...process.env, PORT: String(s.port) },
      });

      child.on("error", (err) => {
        serverStatus.set(s.name, "error");
        console.error(red(`  ✗ ${s.name} failed to start: ${err.message}`));
      });

      child.on("exit", (code) => {
        if (code !== null && code !== 0) {
          serverStatus.set(s.name, "error");
        }
      });

      children.push(child);
      serverStatus.set(s.name, "ok");
      console.log(green(`  ✓ ${s.name}`) + dim(` running on :${s.port}`));
    } catch (err: any) {
      serverStatus.set(s.name, "error");
      console.error(red(`  ✗ ${s.name} failed: ${err.message}`));
    }
  }

  console.log("");

  // Summary table
  const colName = 20;
  const colPort = 8;
  const colStatus = 10;
  const colCap = 28;

  const header = `  ${"Server".padEnd(colName)}${"Port".padEnd(colPort)}${"Status".padEnd(colStatus)}${"Capability".padEnd(colCap)}`;
  const line = "  " + "─".repeat(colName + colPort + colStatus + colCap);
  console.log(bold(header));
  console.log(dim(line));

  for (const s of servers) {
    const status = serverStatus.get(s.name) === "ok" ? green("running") : red("error");
    const statusPad = serverStatus.get(s.name) === "ok" ? "running".length : "error".length;
    console.log(
      `  ${s.name.padEnd(colName)}${(":"+s.port).padEnd(colPort)}${status}${" ".repeat(colStatus - statusPad)}${dim(s.capability)}`
    );
  }

  const hasErrors = [...serverStatus.values()].some(v => v === "error");
  if (hasErrors) {
    console.log("");
    console.error(yellow("  Some servers failed to start. Try: purfle demo --verbose"));
  }

  // Next steps
  console.log("");
  console.log(bold("  Next steps:"));
  console.log(dim("  ─────────────────────────────────────────"));
  console.log(`  1. Open the ${bold("Purfle desktop app")} to see agent cards`);
  console.log(`  2. Run ${cyan("purfle simulate --trigger startup")} to test an agent`);
  console.log(`  3. Run ${cyan("purfle simulate --trigger event")} to test event triggers`);
  console.log("");
  console.log(dim("  Press Ctrl+C to stop all servers."));
  console.log("");
}

function checkUriScheme(): void {
  try {
    if (process.platform === "win32") {
      const result = execSync("reg query HKCU\\Software\\Classes\\purfle 2>nul", { encoding: "utf-8" });
      if (!result.includes("purfle")) {
        console.warn(yellow("  ⚠ purfle:// URI scheme does not appear to be registered.\n"));
      }
    }
    // macOS check is best-effort — skip if lsregister not available
  } catch {
    // Silently ignore — non-critical
  }
}
