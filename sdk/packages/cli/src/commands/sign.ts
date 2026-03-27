import { readFileSync, writeFileSync, existsSync } from "fs";
import { join } from "path";
import { parseManifest, signManifest, generateSigningKey } from "@purfle/core";

interface SignOptions {
  keyFile?: string;
  keyId?: string;
  generateKey?: boolean;
}

export function signCommand(dir: string, options: SignOptions): void {
  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifest = parseManifest(readFileSync(manifestPath, "utf8"));

  let privateKeyPem!: string;
  let keyId!: string;

  if (options.generateKey) {
    // Generate a new key and write it alongside the manifest.
    // In production, the private key is stored securely outside the agent directory.
    const keyId_ = options.keyId ?? `${manifest.id.slice(0, 8)}-key`;
    const pair = generateSigningKey(keyId_);
    const keyPath = join(dir, "signing.key.pem");
    const pubPath = join(dir, "signing.pub.pem");
    writeFileSync(keyPath, pair.privateKeyPem, { mode: 0o600 });
    writeFileSync(pubPath, pair.publicKeyPem);
    console.log(`Generated key pair:`);
    console.log(`  private: ${keyPath}  (keep this secret)`);
    console.log(`  public:  ${pubPath}`);
    privateKeyPem = pair.privateKeyPem;
    keyId = pair.keyId;
  } else if (options.keyFile) {
    if (!existsSync(options.keyFile)) {
      console.error(`Key file not found: ${options.keyFile}`);
      process.exit(1);
    }
    privateKeyPem = readFileSync(options.keyFile, "utf8");
    keyId = options.keyId ?? "unknown-key";
  } else {
    console.error("Provide --key-file <path> or --generate-key.");
    process.exit(1);
  }

  const signed = signManifest(manifest, privateKeyPem, keyId);
  writeFileSync(manifestPath, JSON.stringify(signed, null, 2) + "\n");

  console.log(`✓  Signed: ${manifestPath}`);
  console.log(`   key_id: ${signed.identity.key_id}`);
  console.log(`   expires: ${signed.identity.expires_at}`);
  console.log();
  console.log("Register your public key with the Purfle key registry before publishing:");
  console.log("  purfle publish --register-key signing.pub.pem");
}
