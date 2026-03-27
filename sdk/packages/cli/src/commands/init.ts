import { writeFileSync, mkdirSync, existsSync } from "fs";
import { join } from "path";
import { randomUUID } from "crypto";
import type { AgentManifest } from "@purfle/core";

export function initCommand(name: string, options: { dir?: string }): void {
  const dir = options.dir ?? name.toLowerCase().replace(/\s+/g, "-");

  if (existsSync(dir)) {
    console.error(`Directory '${dir}' already exists.`);
    process.exit(1);
  }

  mkdirSync(dir, { recursive: true });

  const manifest: AgentManifest = {
    purfle: "0.1",
    id: randomUUID(),
    name,
    version: "0.1.0",
    description: `${name} agent.`,
    identity: {
      author: "",
      email: "",
      key_id: "",
      algorithm: "ES256",
      issued_at: new Date().toISOString(),
      expires_at: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(),
      signature: "",
    },
    capabilities: [],
    permissions: {},
    lifecycle: {
      on_error: "terminate",
    },
    runtime: {
      requires: "purfle/0.1",
      engine: "openai-compatible",
    },
    io: {
      input: { type: "object", properties: {}, required: [] },
      output: { type: "object", properties: {}, required: [] },
    },
  };

  const manifestPath = join(dir, "agent.json");
  writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");

  console.log(`Created ${manifestPath}`);
  console.log();
  console.log("Next steps:");
  console.log(`  1. Edit ${manifestPath} — fill in author, email, capabilities`);
  console.log(`  2. purfle build ${dir}   — validate the manifest`);
  console.log(`  3. purfle sign ${dir}    — sign with your key`);
}
