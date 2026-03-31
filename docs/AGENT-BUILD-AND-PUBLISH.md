# Building and Publishing Sample Agents

This guide covers building agent packages (`.purfle` bundles) and publishing
them to the local marketplace. Two PowerShell scripts in the repo root handle
all of this.

---

## Overview

There are two separate operations:

| Script | What it does |
|---|---|
| `build-agents.ps1` | Compiles agent assemblies and packs `.purfle` bundles into `dist/` |
| `seed-marketplace.ps1` | Signs manifests, creates a publisher account, and publishes agents to the marketplace API |

Building and publishing are independent. You can build without a running
marketplace, and you can re-publish without rebuilding.

---

## Part 1 — Build Agent Packages

### Prerequisites

- .NET 10 SDK installed
- Solution builds cleanly: `dotnet build Purfle.slnx`

### Run the build script

```powershell
.\build-agents.ps1
```

Add `-Clean` to wipe `dist/` first:

```powershell
.\build-agents.ps1 -Clean
```

### What it produces

Each agent is compiled and packed into a `.purfle` bundle under `dist/`:

```
dist/
  Purfle-Chat-1.0.0.purfle
  Purfle-File-Search-1.0.0.purfle
  Purfle-Web-Research-1.0.0.purfle
  File-Summarizer-1.0.0.purfle
```

A `.purfle` file is a zip archive containing the signed manifest, compiled
assembly, and build metadata. The `dist/` folder is gitignored.

### Adding a new agent

- **Agent with a C# assembly** — add an entry to `$AssemblyAgents` in the script.
- **Manifest-only agent** (no custom code) — add the filename to `$ManifestOnlyAgents`.

---

## Part 2 — Publish to the Marketplace

### Prerequisites

**One-time: build the CLI**

```powershell
cd sdk
npm install
npm run build
npm link        # makes `purfle` available in your shell
cd ..
```

**Every session: start the marketplace API**

Leave this running in a separate terminal:

```powershell
dotnet run --project marketplace/src/Purfle.Marketplace.Api
```

The API runs at `http://localhost:5000` by default.

### Run the seed script

```powershell
.\seed-marketplace.ps1
```

With custom credentials:

```powershell
.\seed-marketplace.ps1 -Email "you@example.com" -Password "MyPass123!" -DisplayName "Your Name"
```

Default credentials if you just press Enter: `roman@purfle.dev` / `Purfle123!`

### What the script does

1. **Checks** the marketplace API is reachable at `$Registry`
2. **Registers** a publisher account (skips silently if already exists)
3. **Signs** each agent manifest with a shared P-256 key:
   - Generates the key pair on first run, saves it to `.signing/`
   - Re-uses the same key for all agents
   - Writes the real JWS signature back into each `agents/*.agent.json` file
4. **Opens your browser** for OAuth2 login — log in with the account from step 2
5. **Publishes** each agent manifest to the marketplace
   - Registers the signing public key with the marketplace on the first publish

### After publishing

Verify agents are listed:

```powershell
Invoke-RestMethod http://localhost:5000/api/agents | ConvertTo-Json -Depth 5
```

Or in a browser: `http://localhost:5000/api/agents`

### Signing keys

The signing key pair is saved to `.signing/` in the repo root. This folder is
gitignored. **Do not commit the private key** (`signing.key.pem`).

If you need to re-seed from scratch (e.g. after wiping the marketplace data):

```powershell
Remove-Item .signing -Recurse -Force   # forces a new key pair to be generated
Remove-Item marketplace/src/Purfle.Marketplace.Api/data -Recurse -Force
.\seed-marketplace.ps1
```

---

## Running Both Scripts Together

To go from a clean checkout to a fully seeded local marketplace:

```powershell
# Terminal 1 — start the marketplace and leave it running
dotnet run --project marketplace/src/Purfle.Marketplace.Api

# Terminal 2 — build bundles, then seed
.\build-agents.ps1
.\seed-marketplace.ps1
```

The MAUI desktop app (`Purfle.App`) will then show agents when browsing the
marketplace, since it points at `http://localhost:5000` in development.
