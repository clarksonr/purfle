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

  // Inline identity schema so @purfle/core stays file-system-independent.
  const identitySchema = {
    $id: "agent.identity.schema.json",
    type: "object",
    required: ["author", "email", "key_id", "algorithm", "issued_at", "expires_at"],
    additionalProperties: false,
    properties: {
      author:     { type: "string", minLength: 1, maxLength: 256 },
      email:      { type: "string", format: "email" },
      key_id:     { type: "string", minLength: 1 },
      algorithm:  { const: "ES256" },
      issued_at:  { type: "string", format: "date-time" },
      expires_at: { type: "string", format: "date-time" },
      // signature is optional — omitted at authoring time
      signature:  { type: "string" },
    },
  };

  // Shared permission config shapes
  const networkOutboundConfig = {
    type: "object", required: ["hosts"], additionalProperties: false,
    properties: { hosts: { type: "array", items: { type: "string" }, minItems: 1 } },
  };
  const envReadConfig = {
    type: "object", required: ["vars"], additionalProperties: false,
    properties: { vars: { type: "array", items: { type: "string" }, minItems: 1 } },
  };
  const fsConfig = {
    type: "object", required: ["paths"], additionalProperties: false,
    properties: { paths: { type: "array", items: { type: "string" }, minItems: 1 } },
  };
  const emptyPermConfig = { type: "object", additionalProperties: false };

  // Cross-field: each permissions key must appear in capabilities[]
  const capabilityPermChecks = (
    ["llm.chat", "llm.completion", "network.outbound", "env.read", "fs.read", "fs.write", "mcp.tool"] as const
  ).map((cap) => ({
    if: {
      required: ["permissions"],
      properties: { permissions: { type: "object", required: [cap] } },
    },
    then: {
      properties: { capabilities: { type: "array", contains: { const: cap } } },
    },
  }));

  const manifestSchema = {
    $id: "agent.manifest.schema.json",
    type: "object",
    required: ["purfle", "id", "name", "version", "identity", "capabilities", "runtime"],
    additionalProperties: false,
    properties: {
      purfle:   { type: "string", pattern: "^\\d+\\.\\d+$" },
      id:       { type: "string", pattern: "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" },
      name:     { type: "string", minLength: 1, maxLength: 128 },
      version:  { type: "string", pattern: "^\\d+\\.\\d+\\.\\d+(-[0-9A-Za-z.-]+)?(\\+[0-9A-Za-z.-]+)?$" },
      description: { type: "string", maxLength: 1024 },
      identity: { $ref: "agent.identity.schema.json" },
      capabilities: {
        type: "array",
        items: {
          type: "string",
          enum: ["llm.chat", "llm.completion", "network.outbound", "env.read", "fs.read", "fs.write", "mcp.tool"],
        },
        minItems: 0,
        uniqueItems: true,
      },
      permissions: {
        type: "object",
        additionalProperties: false,
        properties: {
          "llm.chat":         emptyPermConfig,
          "llm.completion":   emptyPermConfig,
          "network.outbound": networkOutboundConfig,
          "env.read":         envReadConfig,
          "fs.read":          fsConfig,
          "fs.write":         fsConfig,
          "mcp.tool":         emptyPermConfig,
        },
      },
      lifecycle: {
        type: "object",
        required: ["on_error"],
        additionalProperties: false,
        properties: {
          on_load:   { type: "string" },
          on_unload: { type: "string" },
          on_error:  { type: "string", enum: ["terminate", "log", "ignore"] },
        },
      },
      runtime: {
        type: "object",
        required: ["requires", "engine"],
        additionalProperties: false,
        properties: {
          requires:   { type: "string", pattern: "^purfle/\\d+\\.\\d+$" },
          engine:     { type: "string", enum: ["anthropic", "gemini", "openai-compatible", "openclaw", "ollama"] },
          model:      { type: "string" },
          max_tokens: { type: "integer", minimum: 1 },
        },
      },
      tools: {
        type: "array",
        items: {
          type: "object",
          required: ["name", "server"],
          additionalProperties: false,
          properties: {
            name:        { type: "string", minLength: 1 },
            server:      { type: "string" },
            description: { type: "string" },
          },
        },
      },
      io: { type: "object" },
    },
    allOf: capabilityPermChecks,
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
