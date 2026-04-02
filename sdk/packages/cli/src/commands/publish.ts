import { readFileSync, existsSync, readdirSync } from "fs";
import { join } from "path";
import { createPublicKey } from "crypto";
import { parseManifest } from "@purfle/core";
import { getRegistryUrl, apiPost, apiUploadBinary, loadCredentials } from "../marketplace.js";

interface PublishOptions {
  registry?: string;
  registerKey?: string;
  bundle?: string;
}

export async function publishCommand(dir: string, options: PublishOptions): Promise<void> {
  const registry = getRegistryUrl(options.registry);

  const manifestPath = join(dir, "agent.json");
  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifestJson = readFileSync(manifestPath, "utf8");
  const manifest = parseManifest(manifestJson);

  if (!manifest.identity.signature) {
    console.error("Manifest is not signed. Run `purfle sign` first.");
    process.exit(1);
  }

  // Check authentication after signature check — no point authenticating an unsigned manifest.
  const creds = loadCredentials();
  if (!creds) {
    console.error("Not authenticated. Run `purfle login` first.");
    process.exit(1);
  }

  // Optionally register the public key first.
  if (options.registerKey) {
    const pubKeyPath = join(dir, options.registerKey);
    if (!existsSync(pubKeyPath)) {
      console.error(`Public key file not found: ${pubKeyPath}`);
      process.exit(1);
    }

    const pubKeyPem = readFileSync(pubKeyPath, "utf8");
    const { x, y } = extractP256Coordinates(pubKeyPem);

    console.log(`Registering key '${manifest.identity.key_id}'...`);
    const keyResult = await apiPost(registry, "api/keys", {
      keyId: manifest.identity.key_id,
      algorithm: "ES256",
      x: x,
      y: y,
    });

    if (keyResult.status === 409) {
      console.log(`  Key '${manifest.identity.key_id}' already registered.`);
    } else if (keyResult.status >= 400) {
      console.error(`  Failed to register key: ${JSON.stringify(keyResult.data)}`);
      process.exit(1);
    } else {
      console.log(`  Key registered.`);
    }
  }

  // Publish the signed manifest.
  console.log(`Publishing ${manifest.name} v${manifest.version}...`);
  const result = await apiPost(registry, "api/agents", manifestJson, "application/json");

  if (result.status >= 400) {
    console.error(`Publish failed: ${JSON.stringify(result.data)}`);
    process.exit(1);
  }

  console.log(`Published ${manifest.name} v${manifest.version} to ${registry}`);
  console.log(`  agent_id: ${manifest.id}`);
  console.log(`  key_id:   ${manifest.identity.key_id}`);

  // Upload bundle if available.
  const bundlePath = options.bundle ?? findBundle(dir, manifest.id, manifest.version);
  if (bundlePath && existsSync(bundlePath)) {
    console.log(`Uploading bundle: ${bundlePath}...`);
    const bundleData = readFileSync(bundlePath);
    const uploadResult = await apiUploadBinary(
      registry,
      `api/agents/${encodeURIComponent(manifest.id)}/versions/${encodeURIComponent(manifest.version)}/bundle`,
      bundleData
    );
    if (uploadResult.status >= 400) {
      console.error(`Bundle upload failed: ${JSON.stringify(uploadResult.data)}`);
      process.exit(1);
    }
    console.log(`  Bundle uploaded.`);
  }
}

/** Look for a .purfle bundle in the agent directory. */
function findBundle(dir: string, agentId: string, version: string): string | null {
  // Check for the canonical name first.
  const canonical = join(dir, `${agentId}-${version}.purfle`);
  if (existsSync(canonical)) return canonical;

  // Fall back to any .purfle file in the directory.
  try {
    const files = readdirSync(dir).filter(f => f.endsWith(".purfle"));
    if (files.length === 1) return join(dir, files[0]);
  } catch { /* ignore */ }

  return null;
}

/**
 * Extracts the raw X and Y coordinates from a PEM-encoded SPKI P-256 public key.
 * Returns Base64-encoded strings (standard, not URL-safe) for the API.
 */
function extractP256Coordinates(pem: string): { x: string; y: string } {
  const key = createPublicKey(pem);
  const jwk = key.export({ format: "jwk" });
  if (!jwk.x || !jwk.y) {
    throw new Error("Failed to extract P-256 coordinates from public key.");
  }
  // JWK uses base64url; the API expects standard base64.
  const x = Buffer.from(jwk.x, "base64url").toString("base64");
  const y = Buffer.from(jwk.y, "base64url").toString("base64");
  return { x, y };
}
