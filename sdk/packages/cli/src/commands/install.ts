import { mkdirSync, writeFileSync, existsSync, rmSync } from "fs";
import { createHash } from "crypto";
import { join } from "path";
import { getRegistryUrl, apiGet, apiDownloadBinary, agentStorePath } from "../marketplace.js";
import { extractZip } from "../zip.js";

interface InstallOptions {
  registry?: string;
  version?: string;
}

export async function installCommand(agentId: string, options: InstallOptions): Promise<void> {
  const registry = getRegistryUrl(options.registry);
  const versionSuffix = options.version
    ? `versions/${encodeURIComponent(options.version)}`
    : "latest";

  try {
    // Try to download the .purfle bundle first.
    const bundlePath = `api/agents/${encodeURIComponent(agentId)}/${versionSuffix}/bundle`;
    const bundleData = await apiDownloadBinary(registry, bundlePath);

    const storePath = agentStorePath(agentId);

    if (bundleData) {
      // Verify SHA-256 integrity if hash is available from the registry
      try {
        const versionSuffix2 = options.version
          ? `versions/${encodeURIComponent(options.version)}`
          : "latest";
        const detailPath = `api/agents/${encodeURIComponent(agentId)}`;
        const detail = await apiGet<{ versions?: Array<{ version: string; bundleHash?: string }> }>(registry, detailPath);
        const expectedHash = detail?.versions?.find(
          (v) => options.version ? v.version === options.version : true
        )?.bundleHash;

        if (expectedHash) {
          const actualHash = createHash("sha256").update(bundleData).digest("hex");
          if (actualHash !== expectedHash) {
            console.error(`SHA-256 integrity check failed!`);
            console.error(`  Expected: ${expectedHash}`);
            console.error(`  Actual:   ${actualHash}`);
            console.error(`Bundle may have been tampered with. Aborting install.`);
            process.exit(1);
          }
          console.log(`SHA-256 verified: ${actualHash}`);
        }
      } catch {
        // If we can't fetch metadata, proceed without hash verification
      }

      // Bundle found — extract it.
      if (existsSync(storePath)) rmSync(storePath, { recursive: true });
      mkdirSync(storePath, { recursive: true });
      extractZip(bundleData, storePath);

      // Read the manifest to display info.
      const manifestPath = findManifest(storePath);
      const manifestJson = require("fs").readFileSync(manifestPath, "utf8");
      const manifest = JSON.parse(manifestJson);
      const name = manifest.name ?? agentId;
      const version = manifest.version ?? "unknown";

      console.log(`Installed ${name} v${version} (bundle)`);
      console.log(`  Location: ${storePath}`);
      console.log(`\nRun with:  purfle run ${storePath}`);
    } else {
      // No bundle — fall back to manifest-only install.
      const manifestApiPath = `api/agents/${encodeURIComponent(agentId)}/${versionSuffix}`;
      const manifest = await apiGet<Record<string, unknown>>(registry, manifestApiPath);

      const name = (manifest.name as string) ?? agentId;
      const version = (manifest.version as string) ?? "unknown";

      mkdirSync(storePath, { recursive: true });
      const manifestPath = join(storePath, "agent.json");
      writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");

      console.log(`Installed ${name} v${version} (manifest-only)`);
      console.log(`  Location: ${manifestPath}`);
      console.log(`\nRun with:  purfle simulate ${manifestPath}`);
    }
  } catch (err) {
    console.error(`Install failed: ${(err as Error).message}`);
    process.exit(1);
  }
}

/** Find the manifest file in the extracted bundle directory. */
function findManifest(dir: string): string {
  // Check for both naming conventions.
  for (const name of ["agent.manifest.json", "agent.json"]) {
    const p = join(dir, name);
    if (existsSync(p)) return p;
  }
  throw new Error(`No manifest found in extracted bundle at ${dir}`);
}
