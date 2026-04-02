# Project Notes

## Architecture Decisions

- Agents run in isolated AssemblyLoadContext instances
- All manifests are signed with ES256 (ECDSA P-256)
- The AIVM enforces permissions declared in manifests

## Phase 4 Goals

- Build a working dogfood agent (this one!)
- Complete the marketplace API
- Add publisher verification via DNS TXT records
- Implement attestation service

## Open Questions

- How should cross-agent output sharing work in phase 2?
- What additional attestation types should we support?
