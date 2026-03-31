import { readFileSync, existsSync } from "fs";
import { join } from "path";
import { validateManifest, parseManifest } from "@purfle/core";

export function buildCommand(dir: string): void {
  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const json = readFileSync(manifestPath, "utf8");

  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch (e) {
    console.error(`Parse error: ${(e as Error).message}`);
    process.exit(1);
  }

  const result = validateManifest(parsed);

  if (result.valid) {
    const manifest = parseManifest(json);
    console.log(`✓  ${manifest.name} v${manifest.version}`);
    console.log(`   id:          ${manifest.id}`);
    console.log(`   engine:      ${manifest.runtime.engine}`);
    console.log(`   on_error:    ${manifest.lifecycle?.on_error}`);
    console.log(`   capabilities: ${manifest.capabilities.length} declared`);

    const missingIdentity = !manifest.identity.signature || manifest.identity.key_id === "unsigned";
    if (missingIdentity) {
      console.log();
      console.log("  ! identity block is incomplete (author/email/key_id). Run purfle sign to complete.");
    }
  } else {
    console.error("✗  Validation failed:");
    for (const err of result.errors) {
      console.error(`   - ${err}`);
    }
    process.exit(1);
  }
}
