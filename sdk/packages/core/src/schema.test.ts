import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import { validateManifest, parseManifest } from "./schema.js";
import type { AgentManifest } from "./manifest.js";

// Resolve spec/examples/ relative to the compiled dist/ directory (4 levels up to repo root)
const SPEC_EXAMPLES = path.join(__dirname, "../../../../spec/examples");

function readExample(filename: string): Record<string, unknown> {
  return JSON.parse(readFileSync(path.join(SPEC_EXAMPLES, filename), "utf8"));
}

/** Minimal valid manifest — only required fields. */
function validManifestObj(): Record<string, unknown> {
  return {
    purfle: "0.1",
    id: "11111111-1111-4111-a111-111111111111",
    name: "Test Agent",
    version: "1.0.0",
    identity: {
      author: "tester",
      email: "test@example.com",
      key_id: "test-key",
      algorithm: "ES256",
      issued_at: "2025-01-01T00:00:00Z",
      expires_at: "2026-01-01T00:00:00Z",
    },
    capabilities: [],
    runtime: { requires: "purfle/0.1", engine: "anthropic" },
  };
}

// ─── Example file tests (required by task) ───────────────────────────────────

describe("example file validation", () => {
  it("hello-world.agent.json validates against schema with no errors", () => {
    const manifest = readExample("hello-world.agent.json");
    const result = validateManifest(manifest);
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
    assert.equal(result.errors.length, 0);
  });

  it("assistant.agent.json validates against schema with no errors", () => {
    const manifest = readExample("assistant.agent.json");
    const result = validateManifest(manifest);
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
    assert.equal(result.errors.length, 0);
  });

  it("a manifest with a permissions key that has no matching capability fails validation", () => {
    const m = validManifestObj();
    m.capabilities = [];
    (m as Record<string, unknown>).permissions = {
      "network.outbound": { hosts: ["api.anthropic.com"] },
    };
    const result = validateManifest(m);
    assert.ok(!result.valid, `expected failure but schema accepted it`);
  });

  it("a manifest missing required field 'id' fails validation", () => {
    const m = validManifestObj();
    delete (m as Record<string, unknown>).id;
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("id") || e.includes("required")));
  });
});

// ─── Core validation tests ────────────────────────────────────────────────────

describe("validateManifest", () => {
  it("accepts a minimal valid manifest", () => {
    const result = validateManifest(validManifestObj());
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
    assert.equal(result.errors.length, 0);
  });

  it("accepts a manifest with all optional fields present", () => {
    const m: Record<string, unknown> = {
      ...validManifestObj(),
      description: "Full manifest with all optional fields.",
      capabilities: ["llm.chat", "network.outbound", "env.read"],
      permissions: {
        "network.outbound": { hosts: ["api.anthropic.com"] },
        "env.read": { vars: ["ANTHROPIC_API_KEY"] },
      },
      lifecycle: { on_error: "terminate" },
      tools: [{ name: "search", server: "http://localhost:9000" }],
      io: { anything: "goes" },
    };
    const result = validateManifest(m);
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
  });

  it("rejects non-object values", () => {
    assert.ok(!validateManifest("not an object").valid);
    assert.ok(!validateManifest(null).valid);
    assert.ok(!validateManifest(42).valid);
  });

  it("rejects a manifest missing multiple required fields", () => {
    const result = validateManifest({});
    assert.ok(!result.valid);
    assert.ok(result.errors.length > 1);
  });

  it("rejects a bad purfle version format", () => {
    const m = validManifestObj();
    m.purfle = "abc";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects an invalid UUID for id", () => {
    const m = validManifestObj();
    m.id = "not-a-uuid";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects empty name", () => {
    const m = validManifestObj();
    m.name = "";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects a bad semver", () => {
    const m = validManifestObj();
    m.version = "not-semver";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects an invalid lifecycle.on_error value", () => {
    const m = { ...validManifestObj(), lifecycle: { on_error: "explode" } };
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("on_error")));
  });

  it("accepts all valid on_error values", () => {
    for (const val of ["terminate", "log", "ignore"]) {
      const m = { ...validManifestObj(), lifecycle: { on_error: val } };
      const result = validateManifest(m);
      assert.ok(result.valid, `on_error '${val}' should be valid`);
    }
  });

  it("rejects an invalid runtime.engine value", () => {
    const m = validManifestObj();
    (m.runtime as Record<string, unknown>).engine = "gpt-magic";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("engine")));
  });

  it("accepts all valid engine values", () => {
    for (const engine of ["anthropic", "gemini", "openai-compatible", "openclaw", "ollama"]) {
      const m = validManifestObj();
      (m.runtime as Record<string, unknown>).engine = engine;
      const result = validateManifest(m);
      assert.ok(result.valid, `engine '${engine}' should be valid`);
    }
  });

  it("rejects additional top-level properties", () => {
    const m = { ...validManifestObj(), extra: "nope" };
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects identity.algorithm other than ES256", () => {
    const m = validManifestObj();
    (m.identity as Record<string, unknown>).algorithm = "RS256";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("algorithm")));
  });

  it("rejects an invalid identity email format", () => {
    const m = validManifestObj();
    (m.identity as Record<string, unknown>).email = "not-an-email";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects a runtime.requires without the purfle/ prefix", () => {
    const m = validManifestObj();
    (m.runtime as Record<string, unknown>).requires = "0.1";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects an unknown capability string", () => {
    const m = validManifestObj();
    m.capabilities = ["inference.magic"];
    assert.ok(!validateManifest(m).valid);
  });

  it("accepts a manifest without the optional signature field", () => {
    const m = validManifestObj();
    // identity has no signature — should still be valid
    const result = validateManifest(m);
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
  });

  it("accepts permissions only when all keys have matching capabilities", () => {
    const m = validManifestObj();
    m.capabilities = ["network.outbound", "env.read"];
    (m as Record<string, unknown>).permissions = {
      "network.outbound": { hosts: ["example.com"] },
      "env.read": { vars: ["MY_VAR"] },
    };
    const result = validateManifest(m);
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
  });
});

// ─── parseManifest tests ──────────────────────────────────────────────────────

describe("parseManifest", () => {
  it("returns a typed manifest from valid JSON", () => {
    const json = JSON.stringify(validManifestObj());
    const manifest: AgentManifest = parseManifest(json);
    assert.equal(manifest.name, "Test Agent");
    assert.equal(manifest.purfle, "0.1");
  });

  it("throws on invalid JSON", () => {
    assert.throws(() => parseManifest("{not json}"), /Invalid JSON/);
  });

  it("throws on valid JSON that fails validation", () => {
    assert.throws(() => parseManifest('{"name":""}'), /validation failed/i);
  });
});
