import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import * as fs from "node:fs";
import * as path from "node:path";
import { glob } from "glob";

const ALLOWED_ROOT = path.resolve(
  process.env.MCP_FILE_ROOT || path.join(process.cwd(), "../../agents/file-assistant/workspace")
);

function resolveSafe(filePath: string): string {
  const resolved = path.resolve(ALLOWED_ROOT, filePath);
  if (!resolved.startsWith(ALLOWED_ROOT)) {
    throw new Error(`Access denied: path is outside allowed directory`);
  }
  return resolved;
}

const server = new McpServer({
  name: "purfle-file-tools",
  version: "1.0.0",
});

server.tool(
  "files/read",
  "Read the contents of a file",
  { path: z.string().describe("Relative path to the file") },
  async ({ path: filePath }) => {
    try {
      const resolved = resolveSafe(filePath);
      const content = fs.readFileSync(resolved, "utf-8");
      return { content: [{ type: "text" as const, text: content }] };
    } catch (err: any) {
      return {
        content: [{ type: "text" as const, text: `Error: ${err.message}` }],
        isError: true,
      };
    }
  }
);

server.tool(
  "files/list",
  "List files in a directory",
  {
    path: z.string().default(".").describe("Relative path to the directory"),
    pattern: z.string().optional().describe("Glob pattern to filter files"),
  },
  async ({ path: dirPath, pattern }) => {
    try {
      const resolved = resolveSafe(dirPath);
      if (!fs.existsSync(resolved) || !fs.statSync(resolved).isDirectory()) {
        return {
          content: [{ type: "text" as const, text: `Error: '${dirPath}' is not a valid directory` }],
          isError: true,
        };
      }

      let entries: string[];
      if (pattern) {
        entries = await glob(pattern, { cwd: resolved });
      } else {
        entries = fs.readdirSync(resolved);
      }

      const listing = entries.map((entry) => {
        const fullPath = path.join(resolved, entry);
        try {
          const stat = fs.statSync(fullPath);
          const type = stat.isDirectory() ? "dir" : "file";
          const size = stat.isFile() ? ` (${stat.size} bytes)` : "";
          return `${type}: ${entry}${size}`;
        } catch {
          return `unknown: ${entry}`;
        }
      });

      return {
        content: [{ type: "text" as const, text: listing.join("\n") || "(empty directory)" }],
      };
    } catch (err: any) {
      return {
        content: [{ type: "text" as const, text: `Error: ${err.message}` }],
        isError: true,
      };
    }
  }
);

server.tool(
  "files/search",
  "Search for files by glob pattern",
  {
    pattern: z.string().describe("Glob pattern (e.g. '**/*.json')"),
    directory: z.string().default(".").describe("Relative directory to search in"),
  },
  async ({ pattern, directory }) => {
    try {
      const resolved = resolveSafe(directory);
      if (!fs.existsSync(resolved) || !fs.statSync(resolved).isDirectory()) {
        return {
          content: [{ type: "text" as const, text: `Error: '${directory}' is not a valid directory` }],
          isError: true,
        };
      }

      const matches = await glob(pattern, { cwd: resolved });
      if (matches.length === 0) {
        return {
          content: [{ type: "text" as const, text: `No files found matching '${pattern}'` }],
        };
      }

      return {
        content: [{ type: "text" as const, text: matches.join("\n") }],
      };
    } catch (err: any) {
      return {
        content: [{ type: "text" as const, text: `Error: ${err.message}` }],
        isError: true,
      };
    }
  }
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(`purfle-file-tools MCP server running (root: ${ALLOWED_ROOT})`);
}

main().catch((err) => {
  console.error("Fatal:", err);
  process.exit(1);
});
