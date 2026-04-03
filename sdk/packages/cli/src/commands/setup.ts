import { readFileSync, existsSync, readdirSync, statSync } from "fs";
import { join, resolve } from "path";
import { execSync } from "child_process";
import { validateManifest, generateSigningKey } from "@purfle/core";
import { writeFileSync } from "fs";

const REGISTRY_URL =
  "https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net";
const GREEN = "\x1b[32m";
const YELLOW = "\x1b[33m";
const RED = "\x1b[31m";
const RESET = "\x1b[0m";
const BOLD = "\x1b[1m";

function ok(msg: string) { console.log(`  ${GREEN}✓${RESET}  ${msg}`); }
function warn(msg: string) { console.log(`  ${YELLOW}!${RESET}  ${msg}`); }
function fail(msg: string) { console.log(`  ${RED}✗${RESET}  ${msg}`); }
function heading(msg: string) { console.log(`\n${BOLD}${msg}${RESET}`); }

function checkCommand(cmd: string): string | null {
  try {
    return execSync(`${cmd} --version`, { encoding: "utf8", stdio: ["pipe", "pipe", "pipe"] }).trim();
  } catch {
    return null;
  }
}

export async function setupCommand(): Promise<void> {
  console.log(`${BOLD}Purfle Setup${RESET}`);
  console.log("Checking your development environment...\n");

  let issues = 0;

  // 1. Check required tools
  heading("1. Required Tools");

  const nodeVersion = checkCommand("node");
  if (nodeVersion) ok(`Node.js: ${nodeVersion}`);
  else { fail("Node.js not found. Install from https://nodejs.org"); issues++; }

  const dotnetVersion = checkCommand("dotnet");
  if (dotnetVersion) ok(`.NET SDK: ${dotnetVersion}`);
  else { warn(".NET SDK not found. Required for runtime development."); }

  const npmVersion = checkCommand("npm");
  if (npmVersion) ok(`npm: ${npmVersion}`);
  else { fail("npm not found."); issues++; }

  // 2. Check API keys
  heading("2. API Keys");

  const anthropicKey = process.env.ANTHROPIC_API_KEY;
  if (anthropicKey) {
    ok(`ANTHROPIC_API_KEY: set (${anthropicKey.slice(0, 8)}...)`);
  } else {
    warn("ANTHROPIC_API_KEY not set in environment.");
    console.log("    Set it with: export ANTHROPIC_API_KEY=sk-ant-...");
    issues++;
  }

  const openaiKey = process.env.OPENAI_API_KEY;
  if (openaiKey) ok(`OPENAI_API_KEY: set`);

  const geminiKey = process.env.GEMINI_API_KEY;
  if (geminiKey) ok(`GEMINI_API_KEY: set`);

  if (!anthropicKey && !openaiKey && !geminiKey) {
    warn("No LLM API keys found. At least one is required for agents to run.");
    issues++;
  }

  // 3. Check key registry reachability
  heading("3. Key Registry");

  try {
    const resp = await fetch(`${REGISTRY_URL}/api/keys/ping`);
    if (resp.ok || resp.status === 404) {
      // 404 is fine — the endpoint exists but may not have /ping
      ok(`Key registry reachable at ${REGISTRY_URL}`);
    } else {
      warn(`Key registry returned HTTP ${resp.status}`);
    }
  } catch (e) {
    warn(`Key registry not reachable: ${(e as Error).message}`);
    console.log(`    URL: ${REGISTRY_URL}`);
  }

  // 4. Check signing key
  heading("4. Signing Key");

  const signingKeyPath = join("temp-agent", "signing.key.pem");
  const signingPubPath = join("temp-agent", "signing.pub.pem");

  if (existsSync(signingKeyPath)) {
    ok(`Signing key found: ${signingKeyPath}`);

    if (existsSync(signingPubPath)) {
      ok(`Public key found: ${signingPubPath}`);

      // Check if registered in registry
      await checkKeyRegistration(signingPubPath);
    } else {
      warn("Public key file not found alongside signing key.");
    }
  } else {
    warn("No signing key found at temp-agent/signing.key.pem");
    console.log("    Generate one with: purfle sign --generate-key");
    console.log("    Or creating a new key pair now...");

    // Offer to generate
    try {
      const keyId = `setup-${Date.now()}`;
      const pair = generateSigningKey(keyId);

      const { mkdirSync } = await import("fs");
      mkdirSync("temp-agent", { recursive: true });
      writeFileSync(signingKeyPath, pair.privateKeyPem, { mode: 0o600 });
      writeFileSync(signingPubPath, pair.publicKeyPem);

      ok(`Generated new key pair:`);
      console.log(`    Private: ${signingKeyPath} (keep secret)`);
      console.log(`    Public:  ${signingPubPath}`);
      console.log(`    Key ID:  ${pair.keyId}`);

      await checkKeyRegistration(signingPubPath);
    } catch (e) {
      fail(`Could not generate key: ${(e as Error).message}`);
      issues++;
    }
  }

  // 5. GitHub token
  heading("5. GitHub Token");

  const githubToken = process.env.GITHUB_TOKEN;
  const githubTokenPath = join(".", "..", ".purfle", "github-token");
  const homeGithubPath = join(process.env.HOME ?? process.env.USERPROFILE ?? "~", ".purfle", "github-token");

  if (githubToken) {
    ok(`GITHUB_TOKEN: set (${githubToken.slice(0, 8)}...)`);
  } else if (existsSync(homeGithubPath)) {
    ok(`GitHub token found at ~/.purfle/github-token`);
  } else {
    warn("GitHub token not configured.");
    console.log("    Set GITHUB_TOKEN env var, or create ~/.purfle/github-token");
    console.log("    Required scope: repo (read PR list, get PR detail, list reviews)");
    console.log("    Generate at: https://github.com/settings/tokens");
  }

  // 6. Validate agent manifests
  heading("6. Agent Manifests");

  const agentsDir = "agents";
  if (existsSync(agentsDir)) {
    const manifestFiles = findManifests(agentsDir);
    if (manifestFiles.length === 0) {
      warn("No agent manifests found in agents/");
    } else {
      let valid = 0;
      let invalid = 0;

      for (const mf of manifestFiles) {
        try {
          const json = readFileSync(mf, "utf8");
          const parsed = JSON.parse(json);
          const result = validateManifest(parsed);
          if (result.valid) {
            ok(`${mf}: valid`);
            valid++;
          } else {
            fail(`${mf}: ${result.errors.length} error(s)`);
            for (const err of result.errors.slice(0, 3)) {
              console.log(`      - ${err}`);
            }
            invalid++;
          }
        } catch (e) {
          fail(`${mf}: ${(e as Error).message}`);
          invalid++;
        }
      }

      console.log(`\n    ${valid} valid, ${invalid} invalid`);
      if (invalid > 0) issues++;
    }
  } else {
    warn("No agents/ directory found.");
  }

  // Summary
  heading("Summary");
  if (issues === 0) {
    console.log(`\n  ${GREEN}${BOLD}Everything looks good!${RESET}`);
    console.log("  Your environment is ready for Purfle development.\n");
    console.log("  Next steps:");
    console.log("    purfle init my-agent     Create a new agent");
    console.log("    purfle build             Validate a manifest");
    console.log("    purfle sign              Sign an agent");
    console.log("    purfle publish           Publish to marketplace");
  } else {
    console.log(`\n  ${YELLOW}${issues} issue(s) found.${RESET} See above for details.\n`);
    console.log("  Fix these issues, then run purfle setup again.");
  }
}

async function checkKeyRegistration(pubKeyPath: string): Promise<void> {
  const registryApiKey = process.env.PURFLE_REGISTRY_API_KEY;

  // Read the public key to extract key_id (from the PEM comment or derive one)
  const pubKeyPem = readFileSync(pubKeyPath, "utf8");

  if (!registryApiKey) {
    warn("PURFLE_REGISTRY_API_KEY not set — cannot check/register key with registry.");
    console.log("    Set it to register your public key: export PURFLE_REGISTRY_API_KEY=...");
    return;
  }

  // Try to register the key
  try {
    const keyId = "com.clarksonr/release-2026";
    const encodedKeyId = keyId.replace(/\//g, "__");
    const resp = await fetch(`${REGISTRY_URL}/api/keys/${encodedKeyId}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-api-key": registryApiKey,
      },
      body: JSON.stringify({
        key_id: keyId,
        algorithm: "ES256",
        public_key_pem: pubKeyPem,
      }),
    });

    if (resp.ok || resp.status === 409) {
      ok(`Public key registered (or already exists) as '${keyId}'`);
    } else {
      const body = await resp.text();
      warn(`Key registration returned HTTP ${resp.status}: ${body.slice(0, 200)}`);
    }
  } catch (e) {
    warn(`Could not register key: ${(e as Error).message}`);
  }
}

function findManifests(dir: string): string[] {
  const results: string[] = [];
  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      // Check for agent.manifest.json or agent.json in subdirectories
      const manifestPath = join(full, "agent.manifest.json");
      const agentJsonPath = join(full, "agent.json");
      if (existsSync(manifestPath)) results.push(manifestPath);
      else if (existsSync(agentJsonPath)) results.push(agentJsonPath);

      // Don't recurse deeper than one level
    } else if (entry.name.endsWith(".agent.json")) {
      results.push(full);
    }
  }

  return results;
}
