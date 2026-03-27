# Purfle

Purfle is an AI Virtual Machine (AIVM) — a runtime that loads AI agents the way a JVM loads bytecode: verify first, enforce boundaries always, execute only what was declared. An agent's signed manifest defines its identity, capability requirements, and permission sandbox; the AIVM host enforces all of it before a single line of agent code runs. An agent cannot exceed the scope it declared, and it cannot load on a runtime that cannot satisfy its requirements.

→ [Specification](spec/SPEC.md) · [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md)

---

## Status

Phase 1 (spec) and phase 2 (AIVM runtime core) are complete. Phase 3 (SDK + CLI) is in progress.

## License

TBD — private repository while concept is validated.
