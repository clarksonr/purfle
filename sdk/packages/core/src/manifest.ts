/**
 * TypeScript types for the Purfle Agent Manifest.
 * Mirror the JSON Schema in spec/schema/agent.manifest.schema.json.
 */

export interface AgentManifest {
  purfle: string;
  id: string;
  name: string;
  version: string;
  description: string;
  identity: AgentIdentity;
  capabilities: AgentCapability[];
  permissions: AgentPermissions;
  lifecycle: AgentLifecycle;
  runtime: AgentRuntime;
  io: AgentIo;
}

export interface AgentIdentity {
  author: string;
  email: string;
  key_id: string;
  algorithm: "ES256";
  issued_at: string;   // ISO 8601
  expires_at: string;  // ISO 8601
  signature: string;   // JWS compact serialization
}

export interface AgentCapability {
  id: string;
  description?: string;
  required?: boolean;
}

export interface AgentPermissions {
  network?: {
    allow?: string[];
    deny?: string[];
  };
  filesystem?: {
    read?: string[];
    write?: string[];
  };
  environment?: {
    allow?: string[];
  };
  tools?: {
    mcp?: string[];
  };
}

export interface AgentLifecycle {
  init_timeout_ms?: number;
  max_runtime_ms?: number;
  on_error: "terminate" | "suspend" | "retry";
  restartable?: boolean;
}

export interface AgentRuntime {
  requires: string;
  engine: "openai-compatible" | "anthropic" | "ollama";
  model?: string;
  adapter?: string;
}

export interface AgentIo {
  input: Record<string, unknown>;   // JSON Schema fragment
  output: Record<string, unknown>;  // JSON Schema fragment
}

/** Well-known capability IDs defined by the Purfle registry. */
export const WellKnownCapabilities = {
  Inference:     "inference",
  WebSearch:     "web-search",
  Filesystem:    "filesystem",
  McpTools:      "mcp-tools",
  CodeExecution: "code-execution",
  TextToSpeech:  "text-to-speech",
  SpeechToText:  "speech-to-text",
} as const;
