import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  canonicalize,
  signManifest,
  verifyManifest,
  generateSigningKey,
} from "./identity.js";
import type { AgentManifest } from "./manifest.js";

function makeManifest(overrides?: Partial<AgentManifest>): AgentManifest {
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
    runtime: { requires: "purfle/0.1", engine: "anthropic" },
    ...overrides,
  };
}

describe("canonicalize", () => {
  it("strips the signature field", () => {
    const m = makeManifest();
    m.identity.signature = "should-be-removed";
    const json = canonicalize(m).toString("utf8");
    assert.ok(!json.includes("should-be-removed"));
    assert.ok(!json.includes('"signature"'));
  });

  it("sorts keys lexicographically", () => {
    const m = makeManifest();
    const json = canonicalize(m).toString("utf8");
    const parsed = JSON.parse(json);
    const keys = Object.keys(parsed);
    assert.deepStrictEqual(keys, [...keys].sort());
  });

  it("produces deterministic output", () => {
    const a = canonicalize(makeManifest());
    const b = canonicalize(makeManifest());
    assert.deepStrictEqual(a, b);
  });

  it("contains no whitespace", () => {
    const json = canonicalize(makeManifest()).toString("utf8");
    // No spaces/newlines/tabs outside of string values
    const withoutStrings = json.replace(/"[^"]*"/g, '""');
    assert.ok(!/\s/.test(withoutStrings));
  });
});

describe("generateSigningKey", () => {
  it("returns a key pair with the given keyId", () => {
    const pair = generateSigningKey("my-key");
    assert.equal(pair.keyId, "my-key");
    assert.ok(pair.privateKeyPem.includes("BEGIN PRIVATE KEY"));
    assert.ok(pair.publicKeyPem.includes("BEGIN PUBLIC KEY"));
  });
});

describe("signManifest / verifyManifest", () => {
  it("sign then verify round-trips", () => {
    const pair = generateSigningKey("round-trip-key");
    const m = makeManifest();
    const signed = signManifest(m, pair.privateKeyPem, pair.keyId);

    assert.ok((signed.identity.signature ?? "").length > 0);
    assert.equal(signed.identity.key_id, "round-trip-key");
    assert.ok(verifyManifest(signed, pair.publicKeyPem));
  });

  it("verification fails with wrong public key", () => {
    const pair1 = generateSigningKey("key-1");
    const pair2 = generateSigningKey("key-2");
    const signed = signManifest(makeManifest(), pair1.privateKeyPem, pair1.keyId);

    assert.ok(!verifyManifest(signed, pair2.publicKeyPem));
  });

  it("verification fails if manifest is tampered", () => {
    const pair = generateSigningKey("tamper-key");
    const signed = signManifest(makeManifest(), pair.privateKeyPem, pair.keyId);
    const tampered = { ...signed, name: "Evil Agent" };

    assert.ok(!verifyManifest(tampered, pair.publicKeyPem));
  });

  it("verification fails on malformed signature", () => {
    const pair = generateSigningKey("bad-sig-key");
    const signed = signManifest(makeManifest(), pair.privateKeyPem, pair.keyId);
    const broken = {
      ...signed,
      identity: { ...signed.identity, signature: "not.a.valid-jws" },
    };

    assert.ok(!verifyManifest(broken, pair.publicKeyPem));
  });

  it("JWS header contains alg and kid", () => {
    const pair = generateSigningKey("header-key");
    const signed = signManifest(makeManifest(), pair.privateKeyPem, pair.keyId);
    const headerB64 = signed.identity.signature!.split(".")[0];
    const header = JSON.parse(
      Buffer.from(headerB64.replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8")
    );
    assert.equal(header.alg, "ES256");
    assert.equal(header.kid, "header-key");
  });
});
