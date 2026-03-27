# Purfle — Monorepo Setup
*Paste this into Claude Code CLI to create and scaffold the monorepo.*

---

## Step 1 — Create the repo

```bash
gh repo create clarksonr/purfle \
  --private \
  --description "Purfle — AI agent identity and trust platform"

gh repo clone clarksonr/purfle
cd purfle
```

## Step 2 — Scaffold the structure

```bash
# Spec
mkdir -p spec/schema spec/examples spec/rfcs

# Runtime
mkdir -p runtime/src/Purfle.Runtime/Manifest
mkdir -p runtime/src/Purfle.Runtime/Identity
mkdir -p runtime/src/Purfle.Runtime/Sandbox
mkdir -p runtime/src/Purfle.Runtime/Lifecycle
mkdir -p runtime/src/Purfle.Runtime.OpenClaw
mkdir -p runtime/tests/Purfle.Runtime.Tests

# SDK
mkdir -p sdk/packages/cli/src/commands
mkdir -p sdk/packages/core/src

# Marketplace stub
mkdir -p marketplace

# Docs
mkdir -p docs
```

## Step 3 — Drop in CLAUDE.md

Copy the CLAUDE.md file into the repo root.

## Step 4 — Initialize runtime and SDK

```bash
# .NET solution
cd runtime
dotnet new sln -n Purfle.Runtime
dotnet new classlib -n Purfle.Runtime -o src/Purfle.Runtime
dotnet new classlib -n Purfle.Runtime.OpenClaw -o src/Purfle.Runtime.OpenClaw
dotnet new xunit -n Purfle.Runtime.Tests -o tests/Purfle.Runtime.Tests
dotnet sln add src/Purfle.Runtime
dotnet sln add src/Purfle.Runtime.OpenClaw
dotnet sln add tests/Purfle.Runtime.Tests
cd ..

# TypeScript monorepo
cd sdk
cat > package.json << 'EOF'
{
  "name": "purfle-sdk",
  "private": true,
  "workspaces": ["packages/*"],
  "scripts": {
    "build": "npm run build --workspaces",
    "test": "npm run test --workspaces"
  }
}
EOF
cat > tsconfig.json << 'EOF'
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "strict": true,
    "esModuleInterop": true
  }
}
EOF
cd ..
```

## Step 5 — Initial commit

```bash
git add .
git commit -m "chore: initial monorepo scaffold"
git push origin main
```

## Step 6 — Now build the spec

With the structure in place, build out these files in order:

1. `spec/SPEC.md` — human-readable spec, RFC tone
2. `spec/schema/agent.manifest.schema.json` — the core JSON Schema
3. `spec/schema/agent.identity.schema.json` — identity block schema
4. `spec/rfcs/0001-identity-model.md` — JWS vs DID, recommend JWS
5. `spec/examples/hello-world.agent.json` — minimal valid manifest
6. `docs/ARCHITECTURE.md`
7. `docs/ROADMAP.md`
8. `README.md`
