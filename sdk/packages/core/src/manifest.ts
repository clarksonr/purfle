/**
 * TypeScript types for the Purfle Agent Manifest.
 * Mirror the JSON Schema in spec/schema/agent.manifest.schema.json.
 */

/** Phase-1 capability identifiers. */
export type CapabilityString =
  | "llm.chat"
  | "llm.completion"
  | "network.outbound"
  | "env.read"
  | "fs.read"
  | "fs.write"
  | "mcp.tool";

export interface AgentManifest {
  purfle: string;
  id: string;
  name: string;
  version: string;
  description?: string;
  identity: AgentIdentity;
  capabilities: CapabilityString[];
  permissions?: AgentPermissions;
  lifecycle?: AgentLifecycle;
  runtime: AgentRuntime;
  tools?: ToolBinding[];
  /** Optional input/output schema hints. No enforcement in phase 1. */
  io?: Record<string, unknown>;
}

export interface AgentIdentity {
  author: string;
  email: string;
  key_id: string;
  algorithm: "ES256";
  issued_at: string;   // ISO 8601
  expires_at: string;  // ISO 8601
  /** JWS compact serialization. Absent at authoring time; added by the SDK on publish. */
  signature?: string;
}

export interface NetworkOutboundConfig { hosts: string[]; }
export interface EnvReadConfig { vars: string[]; }
export interface FsConfig { paths: string[]; }
export type EmptyPermConfig = Record<string, never>;

export interface AgentPermissions {
  "llm.chat"?:        EmptyPermConfig;
  "llm.completion"?:  EmptyPermConfig;
  "network.outbound"?: NetworkOutboundConfig;
  "env.read"?:        EnvReadConfig;
  "fs.read"?:         FsConfig;
  "fs.write"?:        FsConfig;
  "mcp.tool"?:        EmptyPermConfig;
}

export interface AgentLifecycle {
  on_load?: string;
  on_unload?: string;
  on_error: "terminate" | "log" | "ignore";
}

export interface AgentRuntime {
  requires: string;
  engine: "anthropic" | "gemini" | "openai-compatible" | "openclaw" | "ollama";
  model?: string;
  max_tokens?: number;
}

export interface ToolBinding {
  name: string;
  server: string;
  description?: string;
}
