import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { readFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { join } from "node:path";
import { AgentManifest } from "./manifest.js";

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

// Lazily compiled validator — built on first call, then reused.
let _validate: ReturnType<Ajv2020["compile"]> | null = null;

function getValidator(): ReturnType<Ajv2020["compile"]> {
  if (_validate) return _validate;

  // Load the canonical spec schema (Draft 2020-12).
  // Path resolves from compiled dist/ → repo root → spec/schema/.
  const schemaPath = join(__dirname, "../../../../spec/schema/agent.manifest.schema.json");
  const schema = JSON.parse(readFileSync(schemaPath, "utf8"));

  const ajv = new Ajv2020({ allErrors: true });
  addFormats(ajv);
  _validate = ajv.compile(schema);
  return _validate;
}

/**
 * Validates a parsed manifest object against the Purfle manifest schema.
 * Uses Ajv for full JSON Schema (Draft 2020-12) validation.
 */
export function validateManifest(manifest: unknown): ValidationResult {
  const validate = getValidator();
  const valid = validate(manifest) as boolean;

  const errors = valid
    ? []
    : (validate.errors ?? []).map(
        (e) => `${e.instancePath || "(root)"} ${e.message}`
      );

  return { valid, errors };
}

/**
 * Reads a manifest file from disk, parses, and validates it.
 * Throws if the file cannot be read, parsed, or fails schema validation.
 */
export async function loadManifest(filePath: string): Promise<AgentManifest> {
  const json = await readFile(filePath, "utf8");
  return parseManifest(json);
}

/** Parses manifest JSON and validates it. Returns the typed manifest or throws. */
export function parseManifest(json: string): AgentManifest {
  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch (e) {
    throw new Error(`Invalid JSON: ${(e as Error).message}`);
  }

  const result = validateManifest(parsed);
  if (!result.valid) {
    throw new Error(`Manifest validation failed:\n${result.errors.join("\n")}`);
  }

  return parsed as AgentManifest;
}
