import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { validateManifest, parseManifest } from "./schema.js";
import type { AgentManifest } from "./manifest.js";

function validManifestObj(): Record<string, unknown> {
  return {
    purfle: "0.1",
    id: "00000000-0000-0000-0000-000000000001",
    name: "Test Agent",
    version: "1.0.0",
    description: "A test agent.",
    identity: {
      author: "tester",
      email: "test@example.com",
      key_id: "test-key",
      algorithm: "ES256",
      issued_at: "2025-01-01T00:00:00Z",
      expires_at: "2026-01-01T00:00:00Z",
      signature: "",
    },
    capabilities: [],
    permissions: {},
    lifecycle: { on_error: "terminate" },
    runtime: { requires: "purfle/0.1", engine: "openai-compatible" },
    io: { input: {}, output: {} },
  };
}

describe("validateManifest", () => {
  it("accepts a valid manifest", () => {
    const result = validateManifest(validManifestObj());
    assert.ok(result.valid, `errors: ${result.errors.join(", ")}`);
    assert.equal(result.errors.length, 0);
  });

  it("rejects non-object", () => {
    assert.ok(!validateManifest("not an object").valid);
    assert.ok(!validateManifest(null).valid);
    assert.ok(!validateManifest(42).valid);
  });

  it("rejects missing required fields", () => {
    const result = validateManifest({});
    assert.ok(!result.valid);
    assert.ok(result.errors.length > 0);
  });

  it("rejects bad purfle version format", () => {
    const m = validManifestObj();
    m.purfle = "abc";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects empty name", () => {
    const m = validManifestObj();
    m.name = "";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects bad semver", () => {
    const m = validManifestObj();
    m.version = "not-semver";
    assert.ok(!validateManifest(m).valid);
  });

  it("rejects invalid lifecycle.on_error", () => {
    const m = validManifestObj();
    (m.lifecycle as Record<string, unknown>).on_error = "explode";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("on_error")));
  });

  it("rejects invalid runtime.engine", () => {
    const m = validManifestObj();
    (m.runtime as Record<string, unknown>).engine = "gpt-magic";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("engine")));
  });

  it("accepts all valid engine values", () => {
    for (const engine of ["openai-compatible", "anthropic", "ollama"]) {
      const m = validManifestObj();
      (m.runtime as Record<string, unknown>).engine = engine;
      const result = validateManifest(m);
      assert.ok(result.valid, `engine '${engine}' should be valid`);
    }
  });

  it("accepts all valid on_error values", () => {
    for (const val of ["terminate", "suspend", "retry"]) {
      const m = validManifestObj();
      (m.lifecycle as Record<string, unknown>).on_error = val;
      const result = validateManifest(m);
      assert.ok(result.valid, `on_error '${val}' should be valid`);
    }
  });

  it("rejects additional properties", () => {
    const m = { ...validManifestObj(), extra: "nope" };
    assert.ok(!validateManifest(m).valid);
  });

  it("validates identity block fields", () => {
    const m = validManifestObj();
    (m.identity as Record<string, unknown>).algorithm = "RS256";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("algorithm")));
  });

  it("validates identity email format", () => {
    const m = validManifestObj();
    (m.identity as Record<string, unknown>).email = "not-an-email";
    assert.ok(!validateManifest(m).valid);
  });

  it("validates runtime.requires pattern", () => {
    const m = validManifestObj();
    (m.runtime as Record<string, unknown>).requires = "0.1";
    assert.ok(!validateManifest(m).valid);
  });

  it("validates capability id pattern", () => {
    const m = validManifestObj();
    m.capabilities = [{ id: "Valid-No-Uppercase" }];
    assert.ok(!validateManifest(m).valid);
  });

  it("collects multiple errors", () => {
    const result = validateManifest({});
    assert.ok(!result.valid);
    assert.ok(result.errors.length > 1);
  });
});

describe("parseManifest", () => {
  it("returns typed manifest from valid JSON", () => {
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
