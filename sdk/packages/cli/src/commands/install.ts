import { mkdirSync, writeFileSync } from "fs";
import { getRegistryUrl, apiGet, agentStorePath } from "../marketplace.js";

interface InstallOptions {
  registry?: string;
  version?: string;
}

export async function installCommand(agentId: string, options: InstallOptions): Promise<void> {
  const registry = getRegistryUrl(options.registry);
  const versionPath = options.version
    ? `api/agents/${encodeURIComponent(agentId)}/versions/${encodeURIComponent(options.version)}`
    : `api/agents/${encodeURIComponent(agentId)}/latest`;

  try {
    const manifest = await apiGet<Record<string, unknown>>(registry, versionPath);

    const name = (manifest.name as string) ?? agentId;
    const version = (manifest.version as string) ?? "unknown";

    // Save to local store.
    const storePath = agentStorePath(agentId);
    mkdirSync(storePath, { recursive: true });
    const manifestPath = `${storePath}/agent.json`;
    writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");

    console.log(`Installed ${name} v${version}`);
    console.log(`  Location: ${manifestPath}`);
    console.log(`\nRun with:  purfle simulate ${manifestPath}`);
  } catch (err) {
    console.error(`Install failed: ${(err as Error).message}`);
    process.exit(1);
  }
}
