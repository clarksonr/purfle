namespace Purfle.Runtime.Manifest;

/// <summary>
/// Inline copies of the spec JSON Schema documents.
/// These must be kept in sync with spec/schema/ in the monorepo.
/// </summary>
internal static class EmbeddedSchemas
{
    public const string AgentManifest = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://purfle.dev/schema/0.1/agent.manifest.schema.json",
          "title": "Purfle Agent Manifest",
          "description": "Signed description of an AI agent's identity, runtime requirements, capability dependencies, permission scope, lifecycle constraints, and I/O contract.",
          "type": "object",
          "required": ["purfle", "id", "name", "version", "description", "identity", "capabilities", "permissions", "lifecycle", "runtime", "io"],
          "additionalProperties": false,
          "properties": {
            "purfle": {
              "type": "string",
              "pattern": "^\\d+\\.\\d+$"
            },
            "id": {
              "type": "string",
              "format": "uuid"
            },
            "name": {
              "type": "string",
              "minLength": 1,
              "maxLength": 128
            },
            "version": {
              "type": "string",
              "pattern": "^\\d+\\.\\d+\\.\\d+(-[a-zA-Z0-9.]+)?(\\+[a-zA-Z0-9.]+)?$"
            },
            "description": {
              "type": "string",
              "maxLength": 1024
            },
            "identity": {
              "$ref": "agent.identity.schema.json"
            },
            "capabilities": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["id"],
                "additionalProperties": false,
                "properties": {
                  "id": {
                    "type": "string",
                    "pattern": "^([a-z][a-z0-9-]*|[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+\\.[a-z][a-z0-9-]*)$"
                  },
                  "description": { "type": "string", "maxLength": 512 },
                  "required": { "type": "boolean", "default": false }
                }
              }
            },
            "permissions": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "network": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "allow": { "type": "array", "items": { "type": "string" } },
                    "deny":  { "type": "array", "items": { "type": "string" } }
                  }
                },
                "filesystem": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "read":  { "type": "array", "items": { "type": "string" } },
                    "write": { "type": "array", "items": { "type": "string" } }
                  }
                },
                "environment": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "allow": { "type": "array", "items": { "type": "string" } }
                  }
                },
                "tools": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "mcp": { "type": "array", "items": { "type": "string" } }
                  }
                }
              }
            },
            "lifecycle": {
              "type": "object",
              "required": ["on_error"],
              "additionalProperties": false,
              "properties": {
                "init_timeout_ms": { "type": "integer", "minimum": 0 },
                "max_runtime_ms":  { "type": "integer", "minimum": 0 },
                "on_error": {
                  "type": "string",
                  "enum": ["terminate", "suspend", "retry"]
                },
                "restartable": { "type": "boolean" }
              }
            },
            "runtime": {
              "type": "object",
              "required": ["requires", "engine"],
              "additionalProperties": false,
              "properties": {
                "requires": {
                  "type": "string",
                  "pattern": "^purfle/\\d+\\.\\d+$"
                },
                "engine": {
                  "type": "string",
                  "enum": ["openai-compatible", "anthropic", "ollama"]
                },
                "model":   { "type": "string" },
                "adapter": { "type": "string" }
              }
            },
            "io": {
              "type": "object",
              "required": ["input", "output"],
              "additionalProperties": false,
              "properties": {
                "input":  { "type": "object" },
                "output": { "type": "object" }
              }
            }
          }
        }
        """;

    public const string AgentIdentity = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://purfle.dev/schema/0.1/agent.identity.schema.json",
          "title": "Purfle Agent Identity",
          "type": "object",
          "required": ["author", "email", "key_id", "algorithm", "issued_at", "expires_at", "signature"],
          "additionalProperties": false,
          "properties": {
            "author":    { "type": "string", "minLength": 1, "maxLength": 256 },
            "email":     { "type": "string", "format": "email" },
            "key_id":    { "type": "string", "minLength": 1 },
            "algorithm": { "type": "string", "enum": ["ES256"] },
            "issued_at": { "type": "string", "format": "date-time" },
            "expires_at":{ "type": "string", "format": "date-time" },
            "signature": {
              "type": "string",
              "pattern": "^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]*\\.[A-Za-z0-9_-]+$"
            }
          }
        }
        """;
}
