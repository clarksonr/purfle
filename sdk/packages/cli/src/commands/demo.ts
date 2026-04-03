import { spawn, ChildProcess, execSync } from "child_process";
import { existsSync } from "fs";
import { resolve } from "path";

interface ServerDef {
  name: string;
  dir: string;
  port: number;
  label: string;
}

export async function demoCommand(): Promise<void> {
  // Resolve tools/ relative to the CLI package root (sdk/packages/cli/dist → repo root)
  const cliRoot = resolve(__dirname, "..", "..");
  const repoRoot = resolve(cliRoot, "..", "..", "..");
  const toolsDir = resolve(repoRoot, "tools");

  const servers: ServerDef[] = [
    { name: "mcp-file-server", dir: resolve(toolsDir, "mcp-file-server"), port: 8100, label: "file" },
    { name: "mcp-gmail",       dir: resolve(toolsDir, "mcp-gmail"),       port: 8102, label: "gmail" },
    { name: "mcp-github",      dir: resolve(toolsDir, "mcp-github"),      port: 8111, label: "github" },
  ];

  // Check ANTHROPIC_API_KEY
  if (!process.env.ANTHROPIC_API_KEY) {
    console.warn("\x1b[33m⚠ ANTHROPIC_API_KEY is not set. LLM inference will not work.\x1b[0m");
  }

  // Check purfle:// URI scheme (best-effort on Windows via registry, macOS via lsregister)
  checkUriScheme();

  // Verify server directories exist
  for (const s of servers) {
    if (!existsSync(s.dir)) {
      console.error(`Error: MCP server directory not found: ${s.dir}`);
      process.exit(1);
    }
  }

  const children: ChildProcess[] = [];

  // Clean shutdown on Ctrl+C
  const cleanup = () => {
    console.log("\nShutting down MCP servers...");
    for (const child of children) {
      child.kill("SIGTERM");
    }
    process.exit(0);
  };
  process.on("SIGINT", cleanup);
  process.on("SIGTERM", cleanup);

  // Start each server
  for (const s of servers) {
    const child = spawn("npm", ["start"], {
      cwd: s.dir,
      stdio: "ignore",
      shell: true,
      env: { ...process.env, PORT: String(s.port) },
    });

    child.on("error", (err) => {
      console.error(`Failed to start ${s.name}: ${err.message}`);
    });

    child.on("exit", (code) => {
      if (code !== null && code !== 0) {
        console.error(`${s.name} exited with code ${code}`);
      }
    });

    children.push(child);
  }

  console.log("");
  console.log("Purfle demo environment ready.");
  console.log(`MCP servers: ${servers.map(s => `${s.label}(:${s.port})`).join("  ")}`);
  console.log("Open the Purfle app to begin.");
  console.log("Press Ctrl+C to stop.");
  console.log("");
}

function checkUriScheme(): void {
  try {
    if (process.platform === "win32") {
      const result = execSync("reg query HKCU\\Software\\Classes\\purfle 2>nul", { encoding: "utf-8" });
      if (!result.includes("purfle")) {
        console.warn("\x1b[33m⚠ purfle:// URI scheme does not appear to be registered.\x1b[0m");
      }
    }
    // macOS check is best-effort — skip if lsregister not available
  } catch {
    console.warn("\x1b[33m⚠ Could not verify purfle:// URI scheme registration.\x1b[0m");
  }
}
