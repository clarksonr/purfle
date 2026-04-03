# RFC 0003 — Cross-Agent Output Sharing

**Status:** Accepted
**Date:** 2026-04-03
**Author:** Roman D. Clarkson

---

## 1. Summary

Cross-agent output sharing allows an agent to declare read access to the output directory of one or more other agents, enabling multi-agent workflows where downstream agents consume upstream results. The mechanism is file-based, declarative, and enforced by the AIVM sandbox at load time and runtime.

---

## 2. Motivation

Purfle agents write output to sandboxed local paths under `<app-data>/aivm/output/<agent-id>/`. Each agent is isolated by design — it cannot see or reference files belonging to any other agent. This isolation is correct for single-agent use cases, but it blocks the multi-agent workflows that make an AIVM valuable.

Consider the existing dogfood agents:

- `email-monitor` writes a summary of new emails every 15 minutes.
- `pr-watcher` writes a summary of new pull requests every 30 minutes.
- `report-builder` runs at 07:00 and is supposed to read both summaries and produce a morning briefing.

Without cross-agent output sharing, `report-builder` cannot read the outputs of `email-monitor` or `pr-watcher`. The user must manually copy files or grant the agent unrestricted filesystem access — both of which violate the trust model.

### Why file-based, not a shared database

The existing output model writes files to `<app-data>/aivm/output/<agent-id>/`. Cross-agent sharing extends this model rather than replacing it:

- No new storage abstraction is required. Output remains files on disk.
- The AIVM already enforces per-agent path restrictions. Extending this to scoped read access on other agents' paths is a natural, minimal addition.
- Agents do not need to agree on a schema, a wire format, or a query language. One agent writes a file; another reads it. The LLM interprets the content.
- No database dependency, no connection strings, no schema migrations. This aligns with the project's no-over-engineering principle.

---

## 3. Design

### 3.1 Manifest: `io.reads` schema

An agent that needs to read another agent's output declares the dependency in the `io` block of its manifest:

```json
{
  "io": {
    "reads": [
      {
        "agent_id": "b2f1a3c4-5678-9abc-def0-123456789abc",
        "alias": "email-summary",
        "description": "Email monitor daily summary"
      },
      {
        "agent_id": "c3d2e4f5-6789-abcd-ef01-23456789abcd",
        "alias": "pr-summary",
        "description": "PR watcher latest pull request summary"
      }
    ]
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `agent_id` | `string (UUID)` | Yes | The `id` of the agent whose output this agent reads. |
| `alias` | `string` | Yes | A short, stable name the consuming agent uses to reference this source. Must be unique within the `reads` array. |
| `description` | `string` | No | Human-readable description of what the consuming agent expects to find. |

### 3.2 Capability: `agent.read`

Cross-agent output reading requires the `agent.read` capability in the manifest's `capabilities` array:

```json
{
  "capabilities": ["llm.chat", "agent.read", "fs.write"],
  "io": {
    "reads": [
      { "agent_id": "b2f1a3c4-...", "alias": "email-summary" }
    ]
  }
}
```

If a manifest declares `io.reads` without listing `agent.read` in `capabilities`, the AIVM rejects the manifest at load time with `LoadFailureReason.MissingCapability`.

No `permissions` entry is required for `agent.read`. The `io.reads` array itself serves as the permission scope — each entry explicitly names the agent whose output may be read.

### 3.3 `IAgentOutputReader` interface

The AIVM provides a scoped reader to agents that have declared `agent.read` and valid `io.reads` entries:

```csharp
namespace Purfle.Runtime.CrossAgent;

/// <summary>
/// Provides read-only access to the output directory of a declared
/// cross-agent dependency. The AIVM creates a scoped implementation
/// for each agent, restricted to the agent IDs declared in io.reads.
/// </summary>
public interface IAgentOutputReader
{
    /// <summary>
    /// Lists all output files for the given alias.
    /// </summary>
    /// <param name="alias">The alias declared in io.reads.</param>
    /// <returns>Relative file paths within the source agent's output directory.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the alias is not declared in the consuming agent's io.reads.
    /// </exception>
    Task<IReadOnlyList<string>> ListFilesAsync(string alias);

    /// <summary>
    /// Reads the content of a specific output file from the given alias.
    /// </summary>
    /// <param name="alias">The alias declared in io.reads.</param>
    /// <param name="relativePath">Relative path within the source agent's output directory.</param>
    /// <returns>The file content as a string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the alias is not declared in the consuming agent's io.reads.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the file does not exist in the source agent's output directory.
    /// </exception>
    Task<string> ReadFileAsync(string alias, string relativePath);

    /// <summary>
    /// Returns the most recently modified output file for the given alias.
    /// </summary>
    /// <param name="alias">The alias declared in io.reads.</param>
    /// <returns>
    /// The relative path and content of the most recent file,
    /// or null if the source agent has no output files.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the alias is not declared in the consuming agent's io.reads.
    /// </exception>
    Task<(string RelativePath, string Content)?> ReadLatestAsync(string alias);
}
```

### 3.4 `AgentSandboxedOutputReader` enforcement rules

The concrete implementation, `AgentSandboxedOutputReader`, enforces the following rules:

1. **Alias validation.** Every method call checks that the provided `alias` exists in the consuming agent's `io.reads` array. If it does not, the method throws `InvalidOperationException`. There is no fallback and no discovery.

2. **Path resolution.** The reader resolves the alias to a source agent ID, then maps that ID to the source agent's output directory: `<app-data>/aivm/output/<source-agent-id>/`. The consuming agent never sees the raw path.

3. **Read-only access.** The reader provides no write, delete, rename, or move operations. The consuming agent cannot modify another agent's output.

4. **No directory traversal.** The `relativePath` parameter is validated to ensure it does not contain `..`, absolute paths, or symlink escapes. Any attempt to traverse outside the source agent's output directory throws `InvalidOperationException`.

5. **No recursive discovery.** The reader only accesses files in the declared source agent's output directory. It cannot enumerate other agents' directories or discover agent IDs not listed in `io.reads`.

6. **Missing source agent.** If the source agent has never run (its output directory does not exist), `ListFilesAsync` returns an empty list, `ReadFileAsync` throws `FileNotFoundException`, and `ReadLatestAsync` returns `null`. The consuming agent must handle the absence gracefully.

### 3.5 Load-time validation

The AIVM validates cross-agent references at manifest load time:

1. Parse `io.reads` entries from the manifest.
2. For each entry, verify that an agent with the declared `agent_id` is known to the AIVM (either currently loaded or previously registered).
3. If any `agent_id` references an unknown agent, reject the manifest with `LoadFailureReason.InvalidCrossAgentReference`.
4. Verify that `agent.read` is present in `capabilities`. If missing, reject with `LoadFailureReason.MissingCapability`.
5. Verify that all `alias` values are unique within the `reads` array. If duplicates exist, reject with `LoadFailureReason.ManifestInvalid`.

### 3.6 AIVM passes scoped reader, not global reader

The AIVM constructs an `AgentSandboxedOutputReader` instance for each agent at load time. The reader is scoped: it is initialized with only the `io.reads` entries from that agent's manifest and can resolve only those aliases. The agent never receives a global reader that could access arbitrary agent outputs.

This follows the same pattern as the existing sandbox model: the agent receives a capability-scoped interface, not a privileged one. The AIVM constructs the scope; the agent operates within it.

---

## 4. Security Considerations

### What this protects against

| Threat | Mitigation |
|---|---|
| **Undeclared cross-agent read** — an agent attempts to read output from an agent not listed in `io.reads` | `AgentSandboxedOutputReader` only resolves aliases present in the agent's declared `io.reads`. Unknown aliases throw immediately. |
| **Agent ID discovery** — a compromised agent tries to enumerate other agents' IDs or output directories | The scoped reader has no enumeration API. It only knows the aliases and agent IDs declared in the consuming agent's manifest. The output directory path is never exposed to the agent. |
| **Directory traversal** — a compromised agent passes `../../other-agent/secrets.txt` as a relative path | Path validation rejects `..`, absolute paths, and symlinks before any filesystem access occurs. |
| **Output tampering** — a consuming agent modifies the source agent's output files | The reader interface is strictly read-only. No write, delete, or rename methods exist. |
| **Capability escalation** — a manifest declares `io.reads` without the `agent.read` capability | The AIVM rejects the manifest at load time with `LoadFailureReason.MissingCapability`. |
| **Stale or missing upstream output** — the source agent has not run yet or has been uninstalled | The reader handles this gracefully: empty file lists, `FileNotFoundException`, or `null` from `ReadLatestAsync`. The consuming agent must be written to handle absence. |

### What this does NOT protect against

| Limitation | Notes |
|---|---|
| **Malicious upstream output** — the source agent writes intentionally misleading data | The consuming agent trusts the source agent's output. Content integrity is the source agent's responsibility. Manifest signing ensures the source agent is authentic, but not that its output is correct. |
| **Timing races** — the consuming agent reads output while the source agent is mid-write | File-level atomicity is not guaranteed. Agents that produce multi-file outputs should write to a temporary location and rename atomically. This is a best practice, not enforced by the reader. |

---

## 5. Open Questions

None. All design questions have been resolved:

- **Sharing model:** File-based, read-only, declared in manifest. Resolved.
- **Capability gating:** `agent.read` capability required. Resolved.
- **Discovery:** No runtime discovery. All references declared at authoring time. Resolved.
- **Scope:** AIVM constructs per-agent scoped reader. No global reader. Resolved.
- **Missing upstream:** Graceful degradation (empty lists, nulls, exceptions). Resolved.

---

## 6. Implementation

Source files are located in `runtime/src/Purfle.Runtime/CrossAgent/`:

| File | Purpose |
|---|---|
| `IAgentOutputReader.cs` | Interface definition (Section 3.3) |
| `AgentSandboxedOutputReader.cs` | Scoped, sandboxed implementation with enforcement rules (Section 3.4) |
| `CrossAgentReference.cs` | Model class for `io.reads` entries (agent_id, alias, description) |
| `CrossAgentValidator.cs` | Load-time validation logic (Section 3.5) |

The `AgentLoader` integrates cross-agent validation as an additional step after capability negotiation and before agent start. The scoped reader is injected into the agent's execution context alongside the existing sandboxed filesystem and network interfaces.

---

*Filed: 2026-04-03*
