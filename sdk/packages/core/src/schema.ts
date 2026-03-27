import { AgentManifest } from "./manifest.js";

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

/**
 * Validates a parsed manifest object against the Purfle manifest schema.
 *
 * v0.1: structural validation only (required fields, enum values, pattern checks).
 * Full JSON Schema (Draft 2020-12) validation via Ajv will be wired in once the
 * dependency is added.
 */
export function validateManifest(manifest: unknown): ValidationResult {
  const errors: string[] = [];

  if (typeof manifest !== "object" || manifest === null) {
    return { valid: false, errors: ["Manifest must be a JSON object."] };
  }

  const m = manifest as Record<string, unknown>;

  if (typeof m.purfle !== "string" || !/^\d+\.\d+$/.test(m.purfle)) {
    errors.push("purfle: must be a version string matching \\d+.\\d+");
  }
  if (typeof m.id !== "string") {
    errors.push("id: required string (UUID v4)");
  }
  if (typeof m.name !== "string" || m.name.length === 0) {
    errors.push("name: required non-empty string");
  }
  if (typeof m.version !== "string" || !/^\d+\.\d+\.\d+/.test(m.version)) {
    errors.push("version: must be a semver string");
  }
  if (typeof m.description !== "string") {
    errors.push("description: required string");
  }
  if (typeof m.identity !== "object" || m.identity === null) {
    errors.push("identity: required object");
  }
  if (!Array.isArray(m.capabilities)) {
    errors.push("capabilities: required array");
  }
  if (typeof m.permissions !== "object" || m.permissions === null) {
    errors.push("permissions: required object");
  }
  if (typeof m.lifecycle !== "object" || m.lifecycle === null) {
    errors.push("lifecycle: required object");
  } else {
    const lc = m.lifecycle as Record<string, unknown>;
    if (!["terminate", "suspend", "retry"].includes(lc.on_error as string)) {
      errors.push("lifecycle.on_error: must be terminate | suspend | retry");
    }
  }
  if (typeof m.runtime !== "object" || m.runtime === null) {
    errors.push("runtime: required object");
  } else {
    const rt = m.runtime as Record<string, unknown>;
    if (!["openai-compatible", "anthropic", "ollama"].includes(rt.engine as string)) {
      errors.push("runtime.engine: must be openai-compatible | anthropic | ollama");
    }
  }
  if (typeof m.io !== "object" || m.io === null) {
    errors.push("io: required object");
  }

  return { valid: errors.length === 0, errors };
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
