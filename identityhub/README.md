# Purfle IdentityHub

IdentityHub is the trust and registry layer for the Purfle ecosystem,
formerly known as "marketplace". It provides:

- **Agent Registry** -- catalog of published agent manifests with version tracking
- **Key Registry with Revocation** -- ES256 signing key management, including key revocation
- **Publisher Verification** -- domain-based publisher identity verification
- **Attestation Service** -- trust attestations (marketplace-listed, publisher-verified, etc.)
- **Manifest Verification** -- verify JWS signatures on agent manifests against registered keys

## Relationship to Marketplace

The existing `marketplace/` directory contains the original implementation with
full OAuth, OpenIddict, and ASP.NET Identity integration. IdentityHub wraps and
re-exports the core domain services (from `Purfle.Marketplace.Core`) under a
streamlined minimal API surface focused on trust and identity operations.

The marketplace code remains intact and functional. IdentityHub is the
forward-looking brand for the trust infrastructure layer.

## Projects

| Project | Purpose |
|---|---|
| `Purfle.IdentityHub.Core` | Models, service interfaces, and JSON file-backed implementations for registry, revocation, and trust |
| `Purfle.IdentityHub.Api` | ASP.NET Core minimal API exposing all IdentityHub endpoints |

## Endpoints

| Method | Path | Description |
|---|---|---|
| GET | `/agents` | Search/list registered agents |
| POST | `/agents` | Register a new agent |
| GET | `/keys/{id}` | Get a signing key by key ID |
| POST | `/keys` | Register a new signing key |
| DELETE | `/keys/{id}` | Revoke a signing key |
| GET | `/publishers` | List publishers |
| POST | `/publishers` | Register a new publisher |
| GET | `/attestations?agentId={id}` | Get attestations for an agent |
| POST | `/attestations` | Issue an attestation |
| POST | `/verify` | Verify a signed agent manifest |
