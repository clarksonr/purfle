import * as fs from "fs";
import * as path from "path";
import Ajv from "ajv";
import addFormats from "ajv-formats";

// ── Types ────────────────────────────────────────────────────────────────────

interface AgentIdentity {
  id: string;
  name: string;
  version: string;
  publisher?: string;
}

interface AgentLifecycle {
  entrypoint: string;
  runtime?: string;
}

interface AgentManifest {
  specVersion: string;
  identity: AgentIdentity;
  capabilities?: string[];
  permissions?: string[];
  lifecycle: AgentLifecycle;
}

// ── Schema loader ─────────────────────────────────────────────────────────────

function loadSchema(schemaPath: string): object {
  const abs = path.resolve(schemaPath);
  if (!fs.existsSync(abs)) {
    throw new Error(`Schema not found: ${abs}`);
  }
  return JSON.parse(fs.readFileSync(abs, "utf8"));
}

// ── Validation ────────────────────────────────────────────────────────────────

function validateManifest(
  manifest: unknown,
  schemaPath: string
): { valid: boolean; errors: string[] } {
  const ajv = new Ajv({ allErrors: true });
  addFormats(ajv);

  const schema = loadSchema(schemaPath);
  const validate = ajv.compile(schema);
  const valid = validate(manifest) as boolean;

  const errors = valid
    ? []
    : (validate.errors ?? []).map(
        (e) => `  ${e.instancePath || "(root)"} ${e.message}`
      );

  return { valid, errors };
}

// ── Simulate execution ────────────────────────────────────────────────────────

function simulateExecution(manifest: AgentManifest): void {
  const { identity, lifecycle, capabilities = [], permissions = [] } = manifest;

  console.log();
  console.log("┌─ Purfle Simulation ──────────────────────────────────────┐");
  console.log(`│  Agent   : ${identity.name} v${identity.version}`);
  console.log(`│  ID      : ${identity.id}`);
  if (identity.publisher) {
    console.log(`│  Publisher: ${identity.publisher}`);
  }
  console.log(
    `│  Caps    : ${capabilities.length > 0 ? capabilities.join(", ") : "(none)"}`
  );
  console.log(
    `│  Perms   : ${permissions.length > 0 ? permissions.join(", ") : "(none)"}`
  );
  console.log(`│  Entry   : ${lifecycle.entrypoint}`);
  console.log("└──────────────────────────────────────────────────────────┘");
  console.log();

  // Check entrypoint exists (relative to manifest location)
  const entrypointAbs = path.resolve(lifecycle.entrypoint);
  const entrypointExists = fs.existsSync(entrypointAbs);

  console.log(`[purfle] Manifest loaded     ✓`);
  console.log(`[purfle] Identity validated  ✓`);
  console.log(
    `[purfle] Entrypoint found    ${entrypointExists ? "✓" : "✗  (not found — stub run)"}`
  );
  console.log();

  if (entrypointExists) {
    console.log(`[purfle] Invoking: ${lifecycle.entrypoint}`);
    console.log();

    // Dispatch by extension
    const ext = path.extname(lifecycle.entrypoint).toLowerCase();
    switch (ext) {
      case ".js":
      case ".ts":
        runNode(entrypointAbs);
        break;
      default:
        console.log(
          `[purfle] Runtime for '${ext}' not yet wired — stub output below:`
        );
        console.log(`  [${identity.name}] Hello from Purfle.`);
    }
  } else {
    // Stub run — show what would happen
    console.log(`[purfle] Stub run (no entrypoint on disk):`);
    console.log(`  [${identity.name}] Hello from Purfle.`);
  }

  console.log();
  console.log(`[purfle] Simulation complete.`);
}

function runNode(entrypointAbs: string): void {
  // Inline dynamic require so the CLI stays dependency-light
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  require(entrypointAbs);
}

// ── Main export ───────────────────────────────────────────────────────────────

export async function simulate(
  manifestPath: string,
  options: { schema?: string } = {}
): Promise<void> {
  // 1. Resolve paths
  const absManifest = path.resolve(manifestPath);
  const defaultSchemaPath = path.resolve(
    __dirname,
    "../../../../spec/schema/agent.manifest.schema.json"
  );
  const schemaPath = options.schema
    ? path.resolve(options.schema)
    : defaultSchemaPath;

  // 2. Load manifest
  if (!fs.existsSync(absManifest)) {
    console.error(`[purfle] Error: manifest not found — ${absManifest}`);
    process.exit(1);
  }

  let manifest: unknown;
  try {
    manifest = JSON.parse(fs.readFileSync(absManifest, "utf8"));
  } catch (err) {
    console.error(`[purfle] Error: could not parse manifest — ${String(err)}`);
    process.exit(1);
  }

  console.log(`[purfle] Loading manifest: ${absManifest}`);

  // 3. Validate if schema exists
  if (fs.existsSync(schemaPath)) {
    const { valid, errors } = validateManifest(manifest, schemaPath);
    if (!valid) {
      console.error(`[purfle] Manifest validation failed:`);
      errors.forEach((e) => console.error(e));
      process.exit(1);
    }
  } else {
    console.warn(
      `[purfle] Warning: schema not found at ${schemaPath}, skipping validation`
    );
  }

  // 4. Simulate
  simulateExecution(manifest as AgentManifest);
}
