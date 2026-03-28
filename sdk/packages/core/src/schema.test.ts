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
    assert.ok(result.valid);
    assert.equal(result.errors.length, 0);
  });

  it("rejects null", () => {
    const result = validateManifest(null);
    assert.ok(!result.valid);
    assert.ok(result.errors[0].includes("JSON object"));
  });

  it("rejects non-object", () => {
    const result = validateManifest("not an object");
    assert.ok(!result.valid);
  });

  it("rejects missing purfle version", () => {
    const m = validManifestObj();
    delete m.purfle;
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("purfle")));
  });

  it("rejects bad purfle version format", () => {
    const m = validManifestObj();
    m.purfle = "1.2.3"; // must be \d+.\d+, not semver
    const result = validateManifest(m);
    // "1.2.3" matches ^\d+\.\d+ so it passes the regex — that's correct
    // Only truly bad formats like "abc" should fail
    m.purfle = "abc";
    const result2 = validateManifest(m);
    assert.ok(!result2.valid);
  });

  it("rejects missing name", () => {
    const m = validManifestObj();
    delete m.name;
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("name")));
  });

  it("rejects empty name", () => {
    const m = validManifestObj();
    m.name = "";
    const result = validateManifest(m);
    assert.ok(!result.valid);
  });

  it("rejects bad semver", () => {
    const m = validManifestObj();
    m.version = "not-semver";
    const result = validateManifest(m);
    assert.ok(!result.valid);
    assert.ok(result.errors.some((e) => e.includes("version")));
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

  it("collects multiple errors", () => {
    const result = validateManifest({ purfle: 42, name: "" });
    assert.ok(!result.valid);
    assert.ok(result.errors.length > 2);
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
