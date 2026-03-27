import { createSign, createVerify, generateKeyPairSync } from "crypto";
import { AgentManifest } from "./manifest.js";

export interface KeyPair {
  keyId: string;
  privateKeyPem: string;
  publicKeyPem: string;
}

/**
 * Produces the canonical JSON signing payload: all object keys sorted
 * lexicographically, no whitespace, identity.signature field removed.
 * See spec §5.1.
 */
export function canonicalize(manifest: AgentManifest): Buffer {
  const copy = JSON.parse(JSON.stringify(manifest)) as AgentManifest;
  delete (copy.identity as Partial<AgentManifest["identity"]>).signature;
  return Buffer.from(sortedJson(copy), "utf8");
}

function sortedJson(value: unknown): string {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) {
    return "[" + value.map(sortedJson).join(",") + "]";
  }
  const obj = value as Record<string, unknown>;
  const keys = Object.keys(obj).sort();
  const pairs = keys.map((k) => JSON.stringify(k) + ":" + sortedJson(obj[k]));
  return "{" + pairs.join(",") + "}";
}

function base64url(buf: Buffer): string {
  return buf.toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function base64urlDecode(s: string): Buffer {
  const padded = s.replace(/-/g, "+").replace(/_/g, "/").padEnd(s.length + ((4 - (s.length % 4)) % 4), "=");
  return Buffer.from(padded, "base64");
}

/**
 * Signs a manifest using ES256. Returns the manifest with identity.signature populated.
 * The private key must be a PEM-encoded ECDSA P-256 private key.
 */
export function signManifest(manifest: AgentManifest, privateKeyPem: string, keyId: string): AgentManifest {
  const unsigned: AgentManifest = {
    ...manifest,
    identity: { ...manifest.identity, key_id: keyId, signature: "" },
  };

  const payload = canonicalize(unsigned);
  const header = base64url(Buffer.from(JSON.stringify({ alg: "ES256", kid: keyId }), "utf8"));
  const payloadB64 = base64url(payload);
  const signingInput = `${header}.${payloadB64}`;

  const sign = createSign("SHA256");
  sign.update(signingInput);
  // Node.js returns DER; convert to IEEE P1363 (raw R||S) for JWS compliance.
  const derSig = sign.sign({ key: privateKeyPem, dsaEncoding: "ieee-p1363" });
  const signature = `${header}.${payloadB64}.${base64url(derSig)}`;

  return { ...unsigned, identity: { ...unsigned.identity, signature } };
}

/**
 * Verifies an ES256 JWS signature on a manifest.
 * The public key must be a PEM-encoded ECDSA P-256 public key.
 */
export function verifyManifest(manifest: AgentManifest, publicKeyPem: string): boolean {
  const jws = manifest.identity.signature;
  const parts = jws.split(".");
  if (parts.length !== 3) return false;

  const [headerB64, payloadB64, sigB64] = parts;
  const canonical = canonicalize(manifest);
  const expectedPayload = base64url(canonical);

  if (payloadB64 !== expectedPayload) return false;

  const signingInput = `${headerB64}.${payloadB64}`;
  const sig = base64urlDecode(sigB64);

  const verify = createVerify("SHA256");
  verify.update(signingInput);
  return verify.verify({ key: publicKeyPem, dsaEncoding: "ieee-p1363" }, sig);
}

/** Generates a new P-256 key pair for signing. */
export function generateSigningKey(keyId: string): KeyPair {
  const { privateKey, publicKey } = generateKeyPairSync("ec", {
    namedCurve: "P-256",
    privateKeyEncoding: { type: "pkcs8", format: "pem" },
    publicKeyEncoding: { type: "spki", format: "pem" },
  });
  return {
    keyId,
    privateKeyPem: privateKey as string,
    publicKeyPem: publicKey as string,
  };
}
