import { readFileSync, existsSync, statSync, openSync, readSync, closeSync } from "fs";
import { join } from "path";
import { homedir } from "os";
import { agentStorePath } from "../marketplace.js";

const dim = (s: string) => `\x1b[2m${s}\x1b[0m`;

function getOutputDir(): string {
  if (process.platform === "win32") {
    return join(process.env.LOCALAPPDATA ?? join(homedir(), "AppData", "Local"), "aivm", "output");
  }
  return join(homedir(), ".local", "share", "aivm", "output");
}

function getRunLogPath(agentId: string): string {
  return join(getOutputDir(), agentId, "run.log");
}

function hasTimestamp(line: string): boolean {
  // Matches ISO 8601 or common log timestamp patterns at the start of a line
  return /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}/.test(line) ||
         /^\[\d{4}-\d{2}-\d{2}/.test(line);
}

function prependTimestamp(line: string): string {
  if (!line.trim()) return line;
  if (hasTimestamp(line)) return line;
  return dim(new Date().toISOString()) + " " + line;
}

function tailLines(content: string, n: number): string[] {
  const lines = content.split("\n");
  // Remove trailing empty line from final newline
  if (lines.length > 0 && lines[lines.length - 1] === "") {
    lines.pop();
  }
  return lines.slice(-n);
}

interface LogsOptions {
  tail?: string;
  follow?: boolean;
}

export async function logsCommand(agentId: string, options: LogsOptions): Promise<void> {
  // Verify agent is installed
  const storePath = agentStorePath(agentId);
  if (!existsSync(storePath)) {
    console.error(`Agent "${agentId}" is not installed.`);
    console.error(`Install it with: purfle install ${agentId}`);
    process.exit(1);
  }

  const logPath = getRunLogPath(agentId);
  if (!existsSync(logPath)) {
    console.log(`No logs yet for ${agentId}. Has it run?`);
    return;
  }

  const tailCount = options.tail ? parseInt(options.tail, 10) : 50;
  if (isNaN(tailCount) || tailCount < 1) {
    console.error("--tail must be a positive integer.");
    process.exit(1);
  }

  // Read and display tail lines
  const content = readFileSync(logPath, "utf8");
  const lines = tailLines(content, tailCount);

  for (const line of lines) {
    console.log(prependTimestamp(line));
  }

  if (!options.follow) return;

  // Follow mode: poll for new content every 500ms
  let lastSize = statSync(logPath).size;

  const poll = setInterval(() => {
    try {
      if (!existsSync(logPath)) return;
      const currentSize = statSync(logPath).size;
      if (currentSize <= lastSize) {
        if (currentSize < lastSize) {
          // File was truncated — reset
          lastSize = 0;
        }
        return;
      }

      const bytesToRead = currentSize - lastSize;
      const buf = Buffer.alloc(bytesToRead);
      const fd = openSync(logPath, "r");
      readSync(fd, buf, 0, bytesToRead, lastSize);
      closeSync(fd);

      lastSize = currentSize;

      const newContent = buf.toString("utf8");
      const newLines = newContent.split("\n");
      // The last element might be empty if content ends with newline
      for (const line of newLines) {
        if (line) {
          console.log(prependTimestamp(line));
        }
      }
    } catch {
      // File may have been removed — ignore
    }
  }, 500);

  // Clean shutdown on Ctrl+C
  const cleanup = () => {
    clearInterval(poll);
    process.exit(0);
  };
  process.on("SIGINT", cleanup);
  process.on("SIGTERM", cleanup);

  // Keep process alive
  await new Promise<void>(() => {
    // Never resolves — process stays alive until signal
  });
}
