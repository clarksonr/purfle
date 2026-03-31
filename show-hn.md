# Show HN: Purfle — signed manifests and capability enforcement for AI agents

**Link:** https://github.com/clarksonr/purfle

---

OpenClaw shipped 9+ CVEs in its first two months. Not because the code was
careless — because there is no trust model in the current AI agent ecosystem.
Any agent can request any capability. There is no standard way to verify who
authored an agent, whether it has been tampered with since signing, or what it
is actually permitted to do at runtime. NVIDIA shipped NemoClaw as a bolt-on
sandbox after the fact. Bolt-ons don't fix architectural gaps.

Purfle designs the trust model in from the start.

The core idea: every AI agent is defined by a signed manifest — a JSON document
that declares the agent's identity, runtime requirements, capability needs, and
permission scope. A conforming AIVM (AI Virtual Machine) host verifies the
manifest before any agent code runs, enforces the declared capability set for
the agent's entire lifetime, and rejects agents that require capabilities the
host cannot provide.

The analogy that keeps being useful: a JVM loads bytecode, verifies it, enforces
a security model, and executes it. It does not trust the bytecode. The AIVM does
the same for AI agents.

**What's in the repo:**

- `spec/SPEC.md` — the manifest specification (RFC tone, precise)
- `spec/schema/agent.manifest.schema.json` — JSON Schema (Draft 2020-12)
- `spec/rfcs/0001-identity-model.md` — identity model: JWS/ES256, with DID
  migration path documented
- .NET/C# AIVM runtime with 82 passing tests — manifest loader, JWS verifier,
  capability negotiator, sandbox enforcer, scheduler, LLM adapters
- .NET MAUI desktop app — agent cards, scheduler, log viewer, settings
- TypeScript SDK and CLI — `purfle init | build | sign | simulate`

**The capability model in one example:**

```json
{
  "capabilities": ["llm.chat", "network.outbound", "env.read"],
  "permissions": {
    "network.outbound": { "hosts": ["api.anthropic.com"] },
    "env.read":         { "vars":  ["ANTHROPIC_API_KEY"] }
  }
}
```

That agent can reach one host and read one environment variable. Nothing else.
The AIVM enforces it. The agent cannot escalate.

**Identity:**

Manifests are signed with JWS/ES256 at authoring time. The AIVM verifies the
signature at load time, checks key revocation, and enforces the expiry timestamp.
A tampered manifest fails to load. An expired manifest fails to load. A manifest
signed with a revoked key fails to load. None of this is configurable.

**What I'm looking for:**

- Feedback on the spec — especially the capability model and permission schema
- Anyone building on OpenClaw or another agent runtime who has opinions on
  what a trust layer should actually look like in practice
- Runtime adapter implementations — the spec is runtime-agnostic; the reference
  implementation is .NET but adapters for other runtimes are explicitly in scope

MIT licensed. Built by one developer in Rocheport, Missouri.

https://github.com/clarksonr/purfle
