import Ajv from "ajv";
import addFormats from "ajv-formats";
import { AgentManifest } from "./manifest.js";

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

// Lazily compiled validator — built on first call, then reused.
let _validate: ReturnType<Ajv["compile"]> | null = null;

function getValidator(): ReturnType<Ajv["compile"]> {
  if (_validate) return _validate;

  // Inline the identity schema so @purfle/core stays file-system-independent.
  const identitySchema = {
    $id: "agent.identity.schema.json",
    type: "object",
    required: ["author", "email", "key_id", "algorithm", "issued_at", "expires_at", "signature"],
    additionalProperties: false,
    properties: {
      author:     { type: "string", minLength: 1, maxLength: 256 },
      email:      { type: "string", format: "email" },
      key_id:     { type: "string", minLength: 1 },
      algorithm:  { type: "string", enum: ["ES256"] },
      issued_at:  { type: "string", format: "date-time" },
      expires_at: { type: "string", format: "date-time" },
      signature:  { type: "string" },
    },
  };

  const manifestSchema = {
    $id: "agent.manifest.schema.json",
    type: "object",
    required: ["purfle", "id", "name", "version", "description", "identity",
               "capabilities", "permissions", "lifecycle", "runtime", "io"],
    additionalProperties: false,
    properties: {
      purfle:       { type: "string", pattern: "^\\d+\\.\\d+$" },
      id:           { type: "string" },
      name:         { type: "string", minLength: 1 },
      version:      { type: "string", pattern: "^\\d+\\.\\d+\\.\\d+" },
      description:  { type: "string", maxLength: 1024 },
      identity:     { $ref: "agent.identity.schema.json" },
      capabilities: {
        type: "array",
        items: {
          type: "object",
          required: ["id"],
          additionalProperties: false,
          properties: {
            id:          { type: "string", pattern: "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$" },
            description: { type: "string" },
            required:    { type: "boolean" },
          },
        },
      },
      permissions: {
        type: "object",
        additionalProperties: false,
        properties: {
          network: {
            type: "object", additionalProperties: false,
            properties: {
              allow: { type: "array", items: { type: "string" } },
              deny:  { type: "array", items: { type: "string" } },
            },
          },
          filesystem: {
            type: "object", additionalProperties: false,
            properties: {
              read:  { type: "array", items: { type: "string" } },
              write: { type: "array", items: { type: "string" } },
            },
          },
          environment: {
            type: "object", additionalProperties: false,
            properties: {
              allow: { type: "array", items: { type: "string" } },
            },
          },
          tools: {
            type: "object", additionalProperties: false,
            properties: {
              mcp: { type: "array", items: { type: "string" } },
            },
          },
        },
      },
      lifecycle: {
        type: "object",
        required: ["on_error"],
        additionalProperties: false,
        properties: {
          init_timeout_ms: { type: "integer", minimum: 0 },
          max_runtime_ms:  { type: "integer", minimum: 0 },
          on_error:        { type: "string", enum: ["terminate", "suspend", "retry"] },
          restartable:     { type: "boolean" },
        },
      },
      runtime: {
        type: "object",
        required: ["requires", "engine"],
        additionalProperties: false,
        properties: {
          requires: { type: "string", pattern: "^purfle/\\d+\\.\\d+$" },
          engine:   { type: "string", enum: ["openai-compatible", "anthropic", "ollama"] },
          model:    { type: "string" },
          adapter:  { type: "string" },
        },
      },
      io: {
        type: "object",
        required: ["input", "output"],
        additionalProperties: false,
        properties: {
          input:  { type: "object" },
          output: { type: "object" },
        },
      },
    },
  };

  const ajv = new Ajv({ allErrors: true });
  addFormats(ajv);
  ajv.addSchema(identitySchema);
  _validate = ajv.compile(manifestSchema);
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
