import { createSign, createVerify, generateKeyPairSync } from "node:crypto";
import { canonicalize } from "./canonical.js";
import { AgentManifest } from "./manifest.js";

export interface KeyPair {
  keyId: string;
  privateKeyPem: string;
  publicKeyPem: string;
}

/** Minimal JWK representation sufficient for EC P-256 keys. */
export interface JsonWebKey {
  kty: string;
  crv?: string;
  x?: string;
  y?: string;
  d?: string;
  use?: string;
  key_ops?: string[];
  alg?: string;
  kid?: string;
  [key: string]: unknown;
}

export interface JwkKeyPair {
  publicKey: JsonWebKey;
  privateKey: JsonWebKey;
}

/**
 * Produces the canonical JSON signing payload: all object keys sorted
 * lexicographically, no whitespace, identity.signature field removed.
 * See spec §5.1.
 */
export function canonicalizeManifest(manifest: AgentManifest): Buffer {
  const copy = JSON.parse(JSON.stringify(manifest)) as AgentManifest;
  delete (copy.identity as Partial<AgentManifest["identity"]>).signature;
  return Buffer.from(canonicalize(copy), "utf8");
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

  const payload = canonicalizeManifest(unsigned);
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
  if (!jws) return false;
  const parts = jws.split(".");
  if (parts.length !== 3) return false;

  const [headerB64, payloadB64, sigB64] = parts;
  const canonical = canonicalizeManifest(manifest);
  const expectedPayload = base64url(canonical);

  if (payloadB64 !== expectedPayload) return false;

  const signingInput = `${headerB64}.${payloadB64}`;
  const sig = base64urlDecode(sigB64);

  const verify = createVerify("SHA256");
  verify.update(signingInput);
  return verify.verify({ key: publicKeyPem, dsaEncoding: "ieee-p1363" }, sig);
}

/** Generates a new P-256 key pair for signing, returned as PEM strings. */
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

/**
 * Generates a new P-256 key pair and returns the keys as JWK (JSON Web Key) objects.
 * @param algorithm - must be "ES256"
 */
export function generateKeyPair(algorithm: "ES256"): JwkKeyPair {
  if (algorithm !== "ES256") throw new Error(`Unsupported algorithm: ${algorithm}`);
  const { privateKey: privObj, publicKey: pubObj } = generateKeyPairSync("ec", {
    namedCurve: "P-256",
  });
  return {
    publicKey: pubObj.export({ format: "jwk" }) as unknown as JsonWebKey,
    privateKey: privObj.export({ format: "jwk" }) as unknown as JsonWebKey,
  };
}
