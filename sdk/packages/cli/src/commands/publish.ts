import { readFileSync, existsSync } from "fs";
import { join } from "path";
import { parseManifest } from "@purfle/core";

interface PublishOptions {
  registry?: string;
  registerKey?: string;
}

export function publishCommand(dir: string, options: PublishOptions): void {
  // TODO (phase 3): implement registry API client
  // - POST /keys  to register public key
  // - POST /agents to publish signed manifest
  // - Return registry URL for the published agent

  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifest = parseManifest(readFileSync(manifestPath, "utf8"));

  if (!manifest.identity.signature) {
    console.error("Manifest is not signed. Run purfle sign first.");
    process.exit(1);
  }

  console.log(`[publish] Not yet implemented.`);
  console.log(`  Would publish: ${manifest.name} v${manifest.version}`);
  console.log(`  key_id: ${manifest.identity.key_id}`);
  console.log(`  registry: ${options.registry ?? "https://registry.purfle.dev (not live yet)"}`);
  process.exit(1);
}
