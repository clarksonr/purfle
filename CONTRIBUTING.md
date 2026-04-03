# Contributing to Purfle

Thank you for your interest in contributing to Purfle. This document covers everything you need to get started: building, testing, branching, and submitting changes.

---

## 1. Prerequisites

You need the following installed before you begin:

| Tool | Minimum version | Check command |
|---|---|---|
| .NET SDK | 8.0 | `dotnet --version` |
| Node.js | 18.0 | `node --version` |
| npm | 9.0 | `npm --version` |
| Git | 2.30 | `git --version` |

Ensure all four commands return a version at or above the minimum before proceeding.

---

## 2. Clone and Build

Clone the repository:

```bash
git clone https://github.com/clarksonr/purfle.git
cd purfle
```

Build the .NET solution (runtime, desktop app, tests):

```bash
dotnet build src/Purfle.sln
```

Build the SDK and CLI (TypeScript):

```bash
cd sdk
npm install
npm run build
```

Both builds must complete without errors before you submit a pull request.

---

## 3. Running Tests

### .NET unit tests

```bash
dotnet test src/Purfle.sln
```

### .NET integration tests

```bash
dotnet test tests/Purfle.IntegrationTests/
```

### SDK and CLI tests (Jest)

```bash
cd sdk
npm test
```

### Run a specific test by name

.NET:

```bash
dotnet test src/Purfle.sln --filter "FullyQualifiedName~YourTestName"
```

Jest:

```bash
cd sdk
npx jest --testNamePattern "your test name"
```

All tests must pass before you open a pull request.

---

## 4. Running the Demo

The `purfle demo` command starts local MCP servers and runs the dogfood agents:

```bash
purfle demo
```

Then open the desktop app to see agents running in the dashboard.

You need at least one LLM engine API key set as an environment variable. The current developer preference is Gemini:

```bash
# Pick one (or more) depending on which engine your agents use:
export GEMINI_API_KEY=your-key-here
export ANTHROPIC_API_KEY=your-key-here
export OPENAI_API_KEY=your-key-here
# Ollama requires no key — just a running Ollama instance on localhost:11434
```

---

## 5. Branching

- `main` is the stable branch. All pull requests target `main`.
- Create feature branches off `main`. Use descriptive names: `feat/cross-agent-sharing`, `fix/scheduler-crash`, `docs/agent-authoring`.
- Pull request titles must follow conventional commit format (see Section 6).
- Rebase on `main` before requesting review if your branch has fallen behind.

---

## 6. Commit Messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/).

**Format:**

```
type(scope): short description
```

**Types:**

| Type | When to use |
|---|---|
| `feat` | A new feature or capability |
| `fix` | A bug fix |
| `chore` | Maintenance, dependency updates, CI changes |
| `test` | Adding or updating tests |
| `docs` | Documentation changes |
| `refactor` | Code restructuring without behavior change |

**Scopes:**

| Scope | Covers |
|---|---|
| `runtime` | AIVM, agent loader, sandbox, scheduler, adapters |
| `sdk` | TypeScript SDK and CLI |
| `desktop` | .NET MAUI desktop app and UI |
| `spec` | Manifest schema, RFCs, specification documents |
| `docs` | Documentation files |
| `integration` | Integration tests |

**Examples:**

```
feat(runtime): add cross-agent output reader
fix(sdk): correct SHA-256 hash comparison in install
chore(desktop): update MAUI workload to 8.0.40
test(runtime): add scheduler overlap-skip tests
docs(spec): RFC 0003 cross-agent output sharing
refactor(sdk): extract manifest validation into shared module
```

---

## 7. CI Requirements

The CI pipeline runs on every pull request. Your PR must satisfy all of the following:

1. **All .NET tests pass.** Unit tests and integration tests.
2. **All SDK tests pass.** Jest test suite.
3. **No `PLACEHOLDER_` values.** Never hardcode placeholder strings. Use the `PLACEHOLDER_*` string as-is if the real value is not yet available.
4. **No hardcoded engine names.** The runtime is engine-agnostic. Never assume, default, or prefer any specific LLM engine (Gemini, Anthropic, OpenAI, Ollama) in code, tests, or documentation. Always derive the engine from `runtime.engine` in the agent manifest.
5. **Build succeeds.** Both `dotnet build` and `npm run build` must complete without errors.

---

## 8. Creating an Agent

For a complete walkthrough of authoring, building, signing, and publishing a Purfle agent, see:

**[docs/AGENT_AUTHORING.md](docs/AGENT_AUTHORING.md)**

That guide covers the full lifecycle from `purfle init` through `purfle publish`, including manifest structure, engine selection, security scanning, and troubleshooting.

---

## 9. Code of Conduct

- Be respectful and constructive in all interactions.
- Focus feedback on the work, not the person.
- Assume good intent. Ask clarifying questions before drawing conclusions.
- No harassment, discrimination, or personal attacks of any kind.
- If you see a problem, report it. If you can fix it, submit a pull request.

We are building software that runs autonomously on people's machines. That responsibility demands careful, honest, collaborative work. Hold yourself and others to that standard.

---

## License

By contributing to Purfle, you agree that your contributions will be licensed under the MIT License.
