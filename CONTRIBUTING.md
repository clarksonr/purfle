# Contributing to Purfle

Purfle is an early-stage open source project. Contributions are welcome — code, spec feedback, bug reports, and documentation improvements all matter at this stage.

---

## What's Most Useful Right Now

The project is in phase 2/3. The areas where outside help has the most impact:

- **Spec review** — read `spec/SPEC.md` and the RFCs; open an issue if anything is ambiguous, underspecified, or wrong
- **Runtime adapter implementations** — the AIVM is .NET/C#; adapters for other runtimes (OpenClaw, Ollama) are stubbed and need implementation
- **TypeScript SDK** — `purfle simulate` and `purfle publish` commands are incomplete; see `sdk/packages/cli/src/commands/`
- **Test coverage** — the runtime has 82 passing tests; gaps are documented in `CLAUDE.md`
- **Documentation** — if something is unclear, a PR to improve it is as valuable as a code PR

---

## Getting Started

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0+ |
| Node.js | 20+ |
| npm | 10+ |

### Clone and build

```bash
git clone https://github.com/clarksonr/purfle.git
cd purfle
```

**Runtime (.NET):**
```bash
cd runtime
dotnet restore
dotnet build
dotnet test
```

**SDK (TypeScript):**
```bash
cd sdk
npm install
npm run build
npm test
```

### Running the CLI

```bash
cd sdk
npm run build
node packages/cli/src/index.js --help
```

To simulate an agent end-to-end (requires an Anthropic API key):

```bash
export ANTHROPIC_API_KEY=your-key-here
node packages/cli/src/index.js simulate ../spec/examples/assistant.agent.json
```

---

## How to Contribute

### Reporting bugs

Open a GitHub issue. Include the manifest JSON if relevant, the error output, and the runtime version (`dotnet --version`, Node version).

### Suggesting spec changes

Spec changes are significant. Open an issue first to discuss before writing code. If the change is substantial, a new RFC in `spec/rfcs/` is the right format — follow the structure of `0001-identity-model.md`.

### Submitting a pull request

1. Fork the repo and create a branch from `main`
2. Make your changes
3. Run the relevant tests — `dotnet test` for runtime changes, `npm test` for SDK changes
4. Open a PR with a clear description of what changed and why

Keep PRs focused. One logical change per PR is easier to review than a batch of unrelated fixes.

---

## Project Structure

```
purfle/
├── spec/        ← The manifest specification — JSON Schema, RFCs, examples
├── runtime/     ← .NET / C# AIVM core — the enforcement host
├── app/         ← .NET MAUI desktop app
├── sdk/         ← TypeScript CLI and core library
├── docs/        ← Architecture and roadmap
```

The spec is the source of truth. Runtime and SDK must conform to it, not the other way around.

---

## Design Principles

A few things that guide decisions in this project:

- **The AIVM is a VM, not a framework.** It enforces boundaries; it does not provide agent behavior.
- **The manifest is the contract.** Everything the AIVM needs to know about an agent is in the manifest. Nothing is inferred at runtime.
- **Deny by default.** The sandbox denies everything not explicitly permitted. This is not configurable.
- **No over-engineering.** Abstractions should address real, current needs — not hypothetical future requirements.
- **MCP is a tool protocol, not the packaging model.** Agents are signed .NET assembly bundles. MCP is how the AIVM wires tools to the LLM. These are different things.

If a proposed change conflicts with these principles, expect pushback.

---

## Code Style

**.NET / C#:** Standard C# conventions. No custom formatter config — just keep it consistent with the surrounding code.

**TypeScript:** The SDK uses strict TypeScript. Run `tsc --noEmit` before submitting.

---

## License

By contributing to Purfle, you agree that your contributions will be licensed under the MIT License.
