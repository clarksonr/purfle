import { existsSync, readFileSync, rmSync } from "fs";
import { join } from "path";
import { homedir } from "os";
import { createInterface } from "readline";
import { agentStorePath } from "../marketplace.js";

function getOutputDir(agentId: string): string {
  if (process.platform === "win32") {
    return join(
      process.env.LOCALAPPDATA ?? join(homedir(), "AppData", "Local"),
      "aivm", "output", agentId
    );
  }
  return join(homedir(), ".local", "share", "aivm", "output", agentId);
}

function readAgentName(agentDir: string): string {
  for (const name of ["agent.manifest.json", "agent.json"]) {
    const p = join(agentDir, name);
    if (existsSync(p)) {
      try {
        const manifest = JSON.parse(readFileSync(p, "utf8"));
        return manifest.name ?? "unknown";
      } catch {
        return "unknown";
      }
    }
  }
  return "unknown";
}

function promptConfirm(message: string): Promise<boolean> {
  return new Promise((resolve) => {
    const rl = createInterface({ input: process.stdin, output: process.stdout });
    rl.question(message, (answer) => {
      rl.close();
      resolve(answer.toLowerCase() === "y" || answer.toLowerCase() === "yes");
    });
  });
}

interface UninstallOptions {
  keepOutput?: boolean;
  yes?: boolean;
}

export async function uninstallCommand(agentId: string, options: UninstallOptions): Promise<void> {
  const storePath = agentStorePath(agentId);

  if (!existsSync(storePath)) {
    console.error(`Agent "${agentId}" is not installed.`);
    process.exit(1);
  }

  const agentName = readAgentName(storePath);

  if (!options.yes) {
    const confirmed = await promptConfirm(
      `This will remove ${agentName} (${agentId}). Continue? [y/N] `
    );
    if (!confirmed) {
      console.log("Cancelled.");
      return;
    }
  }

  // Remove agent directory
  rmSync(storePath, { recursive: true, force: true });

  // Remove output directory unless --keep-output
  if (!options.keepOutput) {
    const outputDir = getOutputDir(agentId);
    if (existsSync(outputDir)) {
      rmSync(outputDir, { recursive: true, force: true });
    }
  }

  console.log(`Uninstalled ${agentName}.`);
}
