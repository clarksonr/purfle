import { describe, it, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, rmSync, readFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";

const CLI = join(__dirname, "..", "dist", "index.js");

function run(args: string[], opts: { cwd?: string } = {}): { stdout: string; stderr: string; code: number } {
  try {
    const stdout = execFileSync("node", [CLI, ...args], {
      cwd: opts.cwd,
      encoding: "utf8",
      timeout: 10_000,
      stdio: ["pipe", "pipe", "pipe"],
    });
    return { stdout, stderr: "", code: 0 };
  } catch (e: unknown) {
    const err = e as { stdout?: string; stderr?: string; status?: number };
    return {
      stdout: err.stdout ?? "",
      stderr: err.stderr ?? "",
      code: err.status ?? 1,
    };
  }
}

// ── init ─────────────────────────────────────────────────────────────────────

describe("purfle init", () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), "purfle-test-"));
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it("scaffolds a new agent project", () => {
    const { stdout, code } = run(["init", "My Agent"], { cwd: tmp });
    assert.equal(code, 0);
    assert.ok(stdout.includes("Created"));

    const manifestPath = join(tmp, "my-agent", "agent.json");
    assert.ok(existsSync(manifestPath));

    const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
    assert.equal(manifest.name, "My Agent");
    assert.equal(manifest.purfle, "0.1");
    assert.equal(manifest.version, "0.1.0");
    assert.ok(manifest.id); // UUID generated
  });

  it("uses --dir to override output directory", () => {
    const { code } = run(["init", "Test", "--dir", "custom-dir"], { cwd: tmp });
    assert.equal(code, 0);
    assert.ok(existsSync(join(tmp, "custom-dir", "agent.json")));
  });

  it("fails if directory already exists", () => {
    run(["init", "Dupe"], { cwd: tmp });
    const { code, stderr } = run(["init", "Dupe"], { cwd: tmp });
    assert.equal(code, 1);
    assert.ok(stderr.includes("already exists"));
  });
});

// ── build ────────────────────────────────────────────────────────────────────

describe("purfle build", () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), "purfle-test-"));
    run(["init", "Build Test"], { cwd: tmp });
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it("validates a scaffolded manifest", () => {
    const agentDir = join(tmp, "build-test");
    const { stdout, code } = run(["build", agentDir]);
    assert.equal(code, 0);
    assert.ok(stdout.includes("Build Test"));
    assert.ok(stdout.includes("v0.1.0"));
  });

  it("reports incomplete identity", () => {
    const agentDir = join(tmp, "build-test");
    const { stdout } = run(["build", agentDir]);
    assert.ok(stdout.includes("identity block is incomplete"));
  });

  it("fails on missing directory", () => {
    const { code, stderr } = run(["build", join(tmp, "nonexistent")]);
    assert.equal(code, 1);
    assert.ok(stderr.includes("No agent.json"));
  });
});

// ── sign ─────────────────────────────────────────────────────────────────────

describe("purfle sign", () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), "purfle-test-"));
    run(["init", "Sign Test"], { cwd: tmp });
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it("generates a key pair and signs the manifest", () => {
    const agentDir = join(tmp, "sign-test");
    const { stdout, code } = run(["sign", agentDir, "--generate-key"]);
    assert.equal(code, 0);
    assert.ok(stdout.includes("Generated key pair"));
    assert.ok(stdout.includes("Signed"));

    // Key files created
    assert.ok(existsSync(join(agentDir, "signing.key.pem")));
    assert.ok(existsSync(join(agentDir, "signing.pub.pem")));

    // Manifest updated with signature
    const manifest = JSON.parse(readFileSync(join(agentDir, "agent.json"), "utf8"));
    assert.ok(manifest.identity.signature.length > 0);
    assert.ok(manifest.identity.key_id.length > 0);
  });

  it("signs with an existing key file", () => {
    const agentDir = join(tmp, "sign-test");
    // First generate a key
    run(["sign", agentDir, "--generate-key"]);
    const keyPath = join(agentDir, "signing.key.pem");

    // Re-sign with the existing key
    const { stdout, code } = run(["sign", agentDir, "--key-file", keyPath, "--key-id", "my-key"]);
    assert.equal(code, 0);
    assert.ok(stdout.includes("Signed"));

    const manifest = JSON.parse(readFileSync(join(agentDir, "agent.json"), "utf8"));
    assert.equal(manifest.identity.key_id, "my-key");
  });

  it("fails without --key-file or --generate-key", () => {
    const agentDir = join(tmp, "sign-test");
    const { code, stderr } = run(["sign", agentDir]);
    assert.equal(code, 1);
    assert.ok(stderr.includes("--key-file") || stderr.includes("--generate-key"));
  });
});

// ── publish ──────────────────────────────────────────────────────────────────

describe("purfle publish", () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), "purfle-test-"));
    run(["init", "Pub Test"], { cwd: tmp });
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it("rejects unsigned manifest", () => {
    const agentDir = join(tmp, "pub-test");
    const { code, stderr } = run(["publish", agentDir]);
    assert.equal(code, 1);
    assert.ok(stderr.includes("not signed"));
  });

  it("shows not-yet-implemented for signed manifest", () => {
    const agentDir = join(tmp, "pub-test");
    run(["sign", agentDir, "--generate-key"]);
    const { stdout, code } = run(["publish", agentDir]);
    assert.equal(code, 1);
    assert.ok(stdout.includes("Not yet implemented"));
  });
});

// ── help / version ───────────────────────────────────────────────────────────

describe("purfle --help / --version", () => {
  it("shows help", () => {
    const { stdout, code } = run(["--help"]);
    assert.equal(code, 0);
    assert.ok(stdout.includes("simulate"));
    assert.ok(stdout.includes("init"));
    assert.ok(stdout.includes("build"));
    assert.ok(stdout.includes("sign"));
    assert.ok(stdout.includes("publish"));
  });

  it("shows version", () => {
    const { stdout, code } = run(["--version"]);
    assert.equal(code, 0);
    assert.ok(stdout.includes("0.1.0"));
  });
});
