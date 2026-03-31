# RFC 0001 — Agent Identity Model

**Status:** Draft
**Date:** 2026-03-31
**Author:** Roman Noble

---

## 1. Problem Statement

A Purfle agent manifest is an executable contract: it declares the capabilities an agent may use, the permissions that govern them, and the runtime that will host it. Without an identity layer, any manifest could be tampered with after authoring — silently escalating permissions, changing the LLM model, or substituting a different agent body.

The AIVM must be able to answer three questions at load time:

1. **Who wrote this manifest?** Attribution must be cryptographically bound, not just asserted.
2. **Has it changed since signing?** The entire manifest must be tamper-evident.
3. **Is it still valid?** Expiry and revocation must be checkable without trusting the agent package itself.

---

## 2. Requirements

Agent identity must provide:

| # | Requirement | Rationale |
|---|---|---|
| R1 | **Attributable** | The manifest must be cryptographically bound to a publisher key, not merely claim an author string. |
| R2 | **Tamper-evident** | Any modification to the manifest after signing must be detectable. |
| R3 | **Expirable** | Manifests must carry an expiry timestamp the AIVM can enforce without a network call. |
| R4 | **Revocable** | Publishers must be able to invalidate a signing key, rendering previously signed manifests untrusted. |
| R5 | **Verifiable offline** | The AIVM must be able to complete verification using only locally cached material after the initial key fetch. |
| R6 | **Additive to manifest structure** | Identity must be representable as a first-class block in the manifest JSON; it must not require a separate envelope format. |
| R7 | **Library-available** | Signing and verification libraries must exist and be stable in .NET, TypeScript/Node.js, and common CLI environments. |
| R8 | **DID-migratable** | The identity model must not structurally prevent migration to decentralized identifiers in a future version. |

---

## 3. Options Considered

### 3a. JWS (JSON Web Signatures) with ES256

The manifest body is serialized in canonical JSON form (keys sorted, no whitespace, `signature` field omitted) and signed using ECDSA P-256 / SHA-256 (algorithm identifier: `ES256`). The resulting JWS Compact Serialization is embedded in `identity.signature`. The corresponding public key is registered in a Purfle-operated key store keyed by `identity.key_id`.

**Verification flow:**
1. Fetch the public key from the key store using `key_id` (cached after first fetch)
2. Verify the JWS signature over the canonical manifest body
3. Check `expires_at` against wall clock

**Strengths:**
- Mature, well-understood — JWS (RFC 7515) is widely implemented. Libraries exist in every target language.
- Simple key model — one registry, one lookup, deterministic verification.
- Fast to ship — no infrastructure beyond a key store with a `GET /keys/{id}` endpoint.
- Auditable — the registry is the authority; revocation is a registry delete operation.
- EC P-256 keys are smaller and verification is faster than RSA (RS256).

**Weaknesses:**
- Central registry dependency — if the Purfle registry is unavailable, key verification requires a cached copy.
- Publisher-controlled trust root — publishers must trust Purfle as the registry operator.
- Not self-sovereign — authors cannot rotate keys without registry involvement.

### 3b. DID (Decentralized Identifiers)

Each agent author holds a DID (e.g., `did:key:z6Mk...` or `did:web:example.com`). The manifest is signed with the private key corresponding to a verification method in the author's DID Document. Verifiers resolve the DID to retrieve the public key.

**Strengths:**
- Self-sovereign — authors control identity without Purfle as intermediary.
- Interoperable — compatible with the W3C DID standard and the Verifiable Credentials ecosystem.
- No central registry for `did:key` — resolution is purely local.
- Future-proof — aligns with emerging agent identity standards.

**Weaknesses:**
- Implementation complexity — DID resolution, DID Document parsing, and verification method selection add significant scope.
- Multiple DID methods — `did:key`, `did:web`, `did:ion`, `did:ethr` each have different resolution mechanics; supporting more than one adds combinatorial surface.
- Tooling immaturity — DID libraries are less consistent and less battle-tested than JWT/JWS libraries in .NET and Node.js.
- Revocation is unsolved — each DID method handles revocation differently; `did:key` has none at all.
- Developer experience — generating and managing a DID is unfamiliar to most developers today.

---

## 4. Decision

**Use JWS/ES256 for phase 1. DID migration path reserved for v0.2.**

The rationale:

- **No resolver dependency.** JWS verification requires only a public key lookup from a trusted store — a single HTTP GET, cacheable. DID resolution requires a resolver implementation for each supported DID method, each with its own semantics and failure modes.
- **Smaller keys, faster verification.** ES256 (ECDSA P-256) produces 64-byte signatures and uses 32-byte private keys. RSA (RS256) requires 256–512 byte keys for equivalent security. Faster key operations reduce latency at agent load time.
- **Sufficient for phase 1 trust model.** Phase 1 is a closed platform. A Purfle-operated key store is an acceptable and auditable trust root. The trade-off (central registry) is known and acceptable at this stage.
- **Faster to ship.** A working identity layer using JWS can be implemented and tested in days. A DID-based system that handles multiple methods, resolution failures, and revocation across DID methods would take weeks and is premature before the platform has external users.

The manifest schema is designed to avoid structural lock-in. See Section 6 (DID Migration Path).

---

## 5. Signing Specification

### What is signed

The AIVM signs and verifies the **canonical JSON** of the manifest. Canonical form is defined as:

- All JSON object keys sorted lexicographically (Unicode code point order), recursively
- No whitespace (no spaces, no newlines)
- `identity.signature` field omitted entirely before serialization

This produces a deterministic byte string that is the JWS payload.

**Example — manifest fragment before canonicalization:**

```json
{
  "identity": {
    "algorithm": "ES256",
    "author": "com.example",
    "email": "author@example.com",
    "expires_at": "2027-01-01T00:00:00Z",
    "issued_at": "2026-01-01T00:00:00Z",
    "key_id": "example-key-001"
  },
  "name": "My Agent",
  "purfle": "0.1"
}
```

### Key format

- Algorithm: **ECDSA P-256** (NIST curve)
- JWA algorithm identifier: **`ES256`**
- Public key representation: JWK (`"kty": "EC"`, `"crv": "P-256"`)
- Private key: never leaves the publisher's machine; never stored in the manifest or key store

### key_id format

`key_id` is an arbitrary string, scoped to the publisher. It must be:

- Unique within the publisher's key space
- Stable for the lifetime of keys signed with it
- Treated as an opaque string by the AIVM (no parsing or structure assumed)

Recommended convention: `<reverse-domain>/<label>` — e.g., `com.example/release-2026`.

A future DID value (e.g., `did:web:example.com#key-1`) is a valid `key_id` string under this definition. See Section 6.

### Signature field

`identity.signature` contains the **JWS Compact Serialization** (RFC 7515 §7.1):

```
BASE64URL(JWS Protected Header) . BASE64URL(JWS Payload) . BASE64URL(JWS Signature)
```

The JWS Protected Header must include:

```json
{ "alg": "ES256" }
```

The JWS Payload is the canonical manifest body (UTF-8 encoded, base64url-encoded per JWS rules).

### Verification flow

The AIVM performs the following steps at manifest load time:

1. Deserialize the manifest JSON.
2. Extract `identity.key_id`.
3. Look up the public key in the AIVM's local trusted key store. If absent, fetch from the Purfle key registry (`GET /keys/{key_id}`), validate the response, and cache.
4. Extract `identity.signature`.
5. Reconstruct the canonical manifest JSON with `identity.signature` omitted.
6. Verify the JWS signature against the canonical body using the retrieved public key.
7. If verification fails → reject with `LoadFailureReason.SignatureInvalid`.
8. Check `identity.expires_at` against the current UTC time.
9. If expired → reject with `LoadFailureReason.ManifestExpired`.
10. Check whether `key_id` appears in the AIVM's revocation list.
11. If revoked → reject with `LoadFailureReason.KeyRevoked`.
12. Proceed to capability enforcement.

---

## 6. DID Migration Path

The manifest structure requires no changes to support DIDs. The migration is purely additive:

1. **Extend `key_id` to accept DID strings.** A `did:key:z6Mk...` or `did:web:example.com#key-1` value is already a valid `key_id` string. The AIVM detects DID values by the `did:` prefix and routes to the DID resolution path.

2. **Extend the key store to proxy DID resolution.** The `GET /keys/{key_id}` registry endpoint can accept DID-format key IDs and resolve them via a DID resolver, returning the same JWK public key response format. The AIVM's verification code remains unchanged.

3. **Add `EdDSA` to the `algorithm` enum.** `Ed25519` (JWA: `EdDSA`) is common in DID ecosystems (`did:key` with `z6Mk` prefix). Adding it to the allowed set is a backwards-compatible schema change.

4. **Update the CLI `sign` command** to accept a DID + private key path in addition to a registry key ID.

5. **Version bump to `purfle/0.2`.** The `purfle` field in the manifest signals the runtime version requirement; the AIVM rejects manifests requiring a higher version than it implements.

No existing signed manifests are invalidated by this migration. A v0.1 JWS-signed manifest continues to verify under a v0.2 runtime.

---

## 7. Key Management

### Publisher responsibilities

1. Generate an EC P-256 keypair using standard tooling (OpenSSL, the Purfle CLI, or any PKCS#11-compatible tool).
2. Keep the private key on the publisher's machine. It must not be committed to source control, embedded in the agent package, or transmitted to Purfle.
3. Register the public key with the Purfle key store (phase 4: marketplace; phase 1: out-of-band).
4. Use the private key to sign manifests at authoring time via the Purfle CLI.
5. Rotate keys by generating a new keypair and re-registering. Previously signed manifests remain valid until their `expires_at`.

### AIVM responsibilities

1. Maintain a local trusted key store — a persistent cache of `key_id → JWK public key` mappings.
2. Fetch unknown keys from the Purfle key registry on first use and cache the result.
3. Maintain a revocation list — a set of `key_id` values that are no longer trusted. The AIVM polls the registry for updates on a configurable interval.
4. Enforce `expires_at` using the system clock. Clock skew tolerance: ±5 minutes.

### Phase 1 key distribution

In phase 1 (pre-marketplace), public key distribution is out-of-band:

- Developers register keys by submitting a JWK public key to a Purfle-operated endpoint.
- The AIVM is seeded with a local key store file for development and testing.
- No public marketplace key lookup is required for the runtime integration tests.

---

## 8. Threat Model

### What this protects against

| Threat | Mitigation |
|---|---|
| **Manifest tampering** — an attacker modifies the manifest after signing (e.g., escalating capabilities, changing the model, altering `expires_at`) | Signature verification over canonical JSON detects any change. Modified manifests fail to load. |
| **Unsigned manifests** — a manifest with no `signature` field is presented to the AIVM | The AIVM rejects manifests with a missing or empty `signature` field before attempting verification. |
| **Expired manifests** — an old valid manifest is replayed after its intended lifetime | `expires_at` is part of the signed body; it cannot be altered. The AIVM enforces it at load time. |
| **Key compromise** — a publisher's private key is stolen | The publisher revokes the `key_id` in the registry. The AIVM's revocation list check blocks further loads of manifests signed with that key. |
| **Unsigned key impersonation** — an attacker claims a `key_id` they don't own | The AIVM only accepts keys fetched from the trusted Purfle registry over HTTPS. The registry enforces publisher authentication at registration time. |

### What this does NOT protect against

| Limitation | Notes |
|---|---|
| **Malicious-but-validly-signed manifests** | A publisher can sign a manifest that declares benign capabilities but includes malicious logic in the DLL. Code review or marketplace policy is required for this, not cryptographic identity. |
| **Compromised AIVM** — the runtime itself is modified | If the AIVM process is compromised, it may skip verification entirely. OS-level integrity (e.g., signed binaries, UAC) is out of scope for this RFC. |
| **Clock manipulation** — the system clock is set backwards to bypass `expires_at` | The AIVM can optionally compare against a trusted time source; this is deferred to a future RFC. |
| **Registry unavailability** — the Purfle key registry is unreachable and the key is not cached | The AIVM fails closed: if a key cannot be resolved, the manifest is rejected. This is a deliberate safety choice. |
| **Revocation lag** — the AIVM's revocation list is stale between poll intervals | Revocation is eventually consistent, not immediate. The poll interval is configurable; the default is 1 hour. |
| **Supply chain attacks on DLLs** — the agent package contains DLLs not covered by the manifest signature in phase 1 | The manifest signature covers the manifest only. DLL integrity checking (hash manifest over `lib/`) is deferred to a future phase. |
