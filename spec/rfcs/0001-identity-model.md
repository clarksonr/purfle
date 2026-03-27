# RFC 0001 — Agent Identity Model

**Status:** Accepted
**Date:** 2026-03-27
**Author:** Roman Noble

---

## Abstract

This RFC evaluates two candidate identity models for Purfle — JSON Web Signatures (JWS) and Decentralized Identifiers (DIDs) — and recommends JWS for v0.1 with an explicit migration path to DIDs in a future release.

---

## 1. Problem Statement

An AI agent manifest must be:

1. **Attributable** — we know who created it
2. **Tamper-evident** — we know it hasn't changed since signing
3. **Revocable** — the publisher can invalidate it
4. **Verifiable offline** — a runtime can check validity without a live network call in the hot path (key fetch excepted)

The identity model is the mechanism that satisfies these properties.

---

## 2. Option A — JWS (JSON Web Signatures)

### How it works

The manifest body is serialized in canonical JSON form and signed using ECDSA P-256 / SHA-256 (ES256). The resulting JWS Compact Serialization is embedded in `identity.signature`. The corresponding public key is registered in a Purfle-operated key registry keyed by `identity.key_id`.

Verification:
1. Fetch the public key from the registry using `key_id`
2. Verify the JWS signature over the canonical manifest body
3. Check `expires_at`

### Strengths

- **Mature, well-understood** — JWS (RFC 7515) is widely implemented. Libraries exist in every language.
- **Simple key model** — one registry, one lookup, deterministic verification.
- **Fast to ship** — no infrastructure beyond a key registry with a `GET /keys/{id}` endpoint.
- **Audit trail** — the registry is the authority; revocation is a registry operation.

### Weaknesses

- **Central registry dependency** — if the Purfle registry is unavailable, offline key verification requires a cached copy.
- **Publisher-controlled** — the registry operator (Purfle) is a trust root. Publishers must trust us.
- **Not self-sovereign** — authors cannot rotate keys without registry involvement.

---

## 3. Option B — DIDs (Decentralized Identifiers)

### How it works

Each agent author holds a DID (e.g., `did:key:z6Mk...` or `did:web:example.com`). The manifest is signed with the private key corresponding to a verification method in the author's DID Document. Verifiers resolve the DID to retrieve the public key.

### Strengths

- **Self-sovereign** — authors control their identity without Purfle as intermediary.
- **Interoperable** — compatible with W3C DID standard and VC ecosystem.
- **No central registry** — `did:key` requires no network; `did:web` uses the author's own domain.
- **Future-proof** — aligns with emerging agent identity standards.

### Weaknesses

- **Implementation complexity** — DID resolution, DID Document parsing, and verification method selection add significant scope.
- **Multiple DID methods** — `did:key`, `did:web`, `did:ion`, `did:ethr` each have different resolution mechanics. Supporting more than one adds combinatorial surface.
- **Tooling immaturity** — DID libraries are less consistent and less battle-tested than JWT/JWS libraries.
- **User experience** — generating and managing a DID is unfamiliar to most developers today.
- **Revocation is unsolved** — each DID method handles revocation differently. `did:key` has no revocation at all.

---

## 4. Recommendation

**Use JWS for v0.1.**

The goal of v0.1 is to validate the manifest format and the identity layer concept with real users and conforming runtimes. JWS gives us a working, auditable identity model in days, not weeks. The central registry is acceptable for a private platform at this stage — it is a known trade-off, not a mistake.

DIDs become compelling when:
- Third parties want to sign agents without trusting Purfle as the registry operator
- The platform is public and self-sovereign identity is a selling point
- DID tooling stabilizes enough to be a normal developer dependency

---

## 5. Migration Path to DIDs

The manifest schema is designed to avoid lock-in:

- `identity.key_id` is a string, not a URL or registry-specific identifier. A `did:key` or `did:web` value is a valid future `key_id`.
- `identity.algorithm` is an enum today (`ES256`). Adding `EdDSA` (common in DID ecosystems) is additive.
- The registry `GET /keys/{id}` endpoint can proxy DID resolution in a future version, letting the runtime verification code remain unchanged.

When the platform is ready, the migration is:

1. Extend `key_id` to accept DID strings
2. Extend the registry to resolve DIDs and return the same public key response format
3. Add `EdDSA` to the `algorithm` enum
4. Update the CLI `sign` command to accept a DID + private key instead of a registry key ID
5. Version bump to `purfle/0.2`

No breaking changes to the manifest format are required.

---

## 6. Decision

| Criterion | JWS | DID |
|---|---|---|
| Implementation speed | Fast | Slow |
| Spec maturity | High | Medium |
| Library availability | Excellent | Variable |
| Revocation | Registry op | Method-dependent |
| Self-sovereignty | No | Yes |
| Migration path | N/A | Additive in 0.2 |

**Accepted: JWS for v0.1. DID migration path documented and reserved for v0.2.**
