namespace Purfle.Runtime.Manifest;

/// <summary>
/// Inline copies of the spec JSON Schema documents, kept in sync with spec/schema/.
/// Used by <see cref="AgentLoader"/> for schema validation during the load sequence.
/// </summary>
internal static class EmbeddedSchemas
{
    public const string AgentManifest = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://purfle.dev/schema/0.1/agent.manifest.schema.json",
          "title": "Purfle Agent Manifest",
          "type": "object",
          "required": ["purfle", "id", "name", "version", "identity", "capabilities", "runtime"],
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
              "items": { "$ref": "#/$defs/capabilityString" },
              "uniqueItems": true
            },
            "permissions": {
              "$ref": "#/$defs/permissionsMap"
            },
            "schedule": {
              "$ref": "#/$defs/scheduleBlock"
            },
            "lifecycle": {
              "$ref": "#/$defs/lifecycleBlock"
            },
            "runtime": {
              "$ref": "#/$defs/runtimeBlock"
            },
            "tools": {
              "type": "array",
              "items": { "$ref": "#/$defs/toolBinding" }
            },
            "io": {
              "type": "object"
            }
          },
          "$defs": {
            "capabilityString": {
              "type": "string",
              "enum": [
                "llm.chat",
                "llm.completion",
                "network.outbound",
                "env.read",
                "fs.read",
                "fs.write",
                "mcp.tool"
              ]
            },
            "permissionsMap": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "llm.chat":         { "type": "object", "additionalProperties": false },
                "llm.completion":   { "type": "object", "additionalProperties": false },
                "network.outbound": { "$ref": "#/$defs/networkOutboundConfig" },
                "env.read":         { "$ref": "#/$defs/envReadConfig" },
                "fs.read":          { "$ref": "#/$defs/fsConfig" },
                "fs.write":         { "$ref": "#/$defs/fsConfig" },
                "mcp.tool":         { "type": "object", "additionalProperties": false }
              }
            },
            "networkOutboundConfig": {
              "type": "object",
              "required": ["hosts"],
              "additionalProperties": false,
              "properties": {
                "hosts": { "type": "array", "items": { "type": "string" }, "minItems": 1 }
              }
            },
            "envReadConfig": {
              "type": "object",
              "required": ["vars"],
              "additionalProperties": false,
              "properties": {
                "vars": { "type": "array", "items": { "type": "string" }, "minItems": 1 }
              }
            },
            "fsConfig": {
              "type": "object",
              "required": ["paths"],
              "additionalProperties": false,
              "properties": {
                "paths": { "type": "array", "items": { "type": "string" }, "minItems": 1 }
              }
            },
            "scheduleBlock": {
              "type": "object",
              "required": ["trigger"],
              "additionalProperties": false,
              "properties": {
                "trigger": { "type": "string", "enum": ["interval", "cron", "startup", "window", "event"] },
                "interval_minutes": { "type": "integer", "minimum": 1 },
                "cron": { "type": "string", "minLength": 1 },
                "window": { "$ref": "#/$defs/windowBlock" },
                "event": { "$ref": "#/$defs/eventBlock" }
              }
            },
            "windowBlock": {
              "type": "object",
              "required": ["start", "end", "run_at"],
              "additionalProperties": false,
              "properties": {
                "start": { "type": "string", "minLength": 1 },
                "end": { "type": "string", "minLength": 1 },
                "run_at": { "type": "string", "enum": ["window_open", "window_close", "interval_within"] },
                "timezone": { "type": "string" }
              }
            },
            "eventBlock": {
              "type": "object",
              "required": ["source", "topic"],
              "additionalProperties": false,
              "properties": {
                "source": { "type": "string", "format": "uri" },
                "topic": { "type": "string", "minLength": 1 }
              }
            },
            "lifecycleBlock": {
              "type": "object",
              "required": ["on_error"],
              "additionalProperties": false,
              "properties": {
                "on_load":   { "type": "string" },
                "on_unload": { "type": "string" },
                "on_error":  { "type": "string", "enum": ["terminate", "log", "ignore"] }
              }
            },
            "runtimeBlock": {
              "type": "object",
              "required": ["requires", "engine"],
              "additionalProperties": false,
              "properties": {
                "requires": { "type": "string", "pattern": "^purfle/\\d+\\.\\d+$" },
                "engine": {
                  "type": "string",
                  "enum": ["anthropic", "openai-compatible", "ollama", "gemini"]
                },
                "model":      { "type": "string" },
                "max_tokens": { "type": "integer", "minimum": 1 }
              }
            },
            "toolBinding": {
              "type": "object",
              "required": ["name", "server"],
              "additionalProperties": false,
              "properties": {
                "name":        { "type": "string", "minLength": 1 },
                "server":      { "type": "string" },
                "description": { "type": "string" }
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
          "required": ["author", "email", "key_id", "algorithm", "issued_at", "expires_at"],
          "additionalProperties": false,
          "properties": {
            "author":     { "type": "string", "minLength": 1, "maxLength": 256 },
            "email":      { "type": "string", "format": "email" },
            "key_id":     { "type": "string", "minLength": 1 },
            "algorithm":  { "type": "string", "enum": ["ES256"] },
            "issued_at":  { "type": "string", "format": "date-time" },
            "expires_at": { "type": "string", "format": "date-time" },
            "signature": {
              "type": "string",
              "pattern": "^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]*\\.[A-Za-z0-9_-]+$"
            }
          }
        }
        """;
}
