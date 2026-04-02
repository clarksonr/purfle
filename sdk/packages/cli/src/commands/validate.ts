import { readFileSync, existsSync } from "fs";
import { join } from "path";
import { validateManifest, parseManifest } from "@purfle/core";
import type { AgentManifest, CapabilityString } from "@purfle/core";

interface ValidateOptions {
  strict?: boolean;
}

interface Finding {
  severity: "error" | "warning" | "info";
  message: string;
}

const VALID_CAPABILITIES: CapabilityString[] = [
  "llm.chat",
  "llm.completion",
  "network.outbound",
  "env.read",
  "fs.read",
  "fs.write",
  "mcp.tool",
];

const CAPABILITIES_REQUIRING_PERMISSIONS: Record<string, string[]> = {
  "network.outbound": ["hosts"],
  "env.read": ["vars"],
  "fs.read": ["paths"],
  "fs.write": ["paths"],
};

export function validateCommand(dir: string, options: ValidateOptions): void {
  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const json = readFileSync(manifestPath, "utf8");

  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch (e) {
    console.error(`Parse error: ${(e as Error).message}`);
    process.exit(1);
  }

  console.log(`[purfle] Validating: ${manifestPath}`);
  console.log();

  const findings: Finding[] = [];

  // ── 1. Schema compliance ────────────────────────────────────────────────────
  const schemaResult = validateManifest(parsed);
  if (!schemaResult.valid) {
    for (const err of schemaResult.errors) {
      findings.push({ severity: "error", message: `Schema: ${err}` });
    }
  }

  // If schema validation fails hard, we can still try deeper checks on the raw object.
  const manifest = parsed as Record<string, unknown>;

  // ── 2. Required fields presence ─────────────────────────────────────────────
  const REQUIRED_TOP = ["purfle", "id", "name", "version", "identity", "capabilities", "runtime"];
  for (const field of REQUIRED_TOP) {
    if (!(field in manifest)) {
      findings.push({ severity: "error", message: `Missing required field: '${field}'` });
    }
  }

  // ── 3. Identity block structure ─────────────────────────────────────────────
  const identity = manifest.identity as Record<string, unknown> | undefined;
  if (identity && typeof identity === "object") {
    const REQUIRED_IDENTITY = ["author", "email", "key_id", "algorithm", "issued_at", "expires_at"];
    for (const field of REQUIRED_IDENTITY) {
      if (!(field in identity)) {
        findings.push({ severity: "error", message: `Identity: missing required field '${field}'` });
      }
    }

    if (identity.algorithm && identity.algorithm !== "ES256") {
      findings.push({ severity: "error", message: `Identity: algorithm must be 'ES256', got '${identity.algorithm}'` });
    }

    if (typeof identity.issued_at === "string") {
      const issued = Date.parse(identity.issued_at);
      if (isNaN(issued)) {
        findings.push({ severity: "error", message: `Identity: 'issued_at' is not a valid ISO 8601 date` });
      }
    }

    if (typeof identity.expires_at === "string") {
      const expires = Date.parse(identity.expires_at);
      if (isNaN(expires)) {
        findings.push({ severity: "error", message: `Identity: 'expires_at' is not a valid ISO 8601 date` });
      } else if (expires < Date.now()) {
        findings.push({ severity: "warning", message: `Identity: 'expires_at' is in the past (${identity.expires_at})` });
      }
    }

    if (typeof identity.issued_at === "string" && typeof identity.expires_at === "string") {
      const issued = Date.parse(identity.issued_at);
      const expires = Date.parse(identity.expires_at);
      if (!isNaN(issued) && !isNaN(expires) && expires <= issued) {
        findings.push({ severity: "error", message: `Identity: 'expires_at' must be after 'issued_at'` });
      }
    }

    if (!identity.signature || identity.signature === "") {
      findings.push({ severity: "warning", message: `Identity: manifest is not signed (no signature). Run 'purfle sign' before publishing.` });
    }

    if (identity.author === "unsigned" || identity.key_id === "unsigned") {
      findings.push({ severity: "warning", message: `Identity: placeholder values detected (author or key_id is 'unsigned')` });
    }
  }

  // ── 4. Capability/permission consistency ────────────────────────────────────
  const capabilities = manifest.capabilities as string[] | undefined;
  const permissions = manifest.permissions as Record<string, unknown> | undefined;

  if (Array.isArray(capabilities)) {
    // Check for unknown capabilities
    for (const cap of capabilities) {
      if (!VALID_CAPABILITIES.includes(cap as CapabilityString)) {
        findings.push({ severity: "error", message: `Capability: unknown capability '${cap}'` });
      }
    }

    // Check for duplicate capabilities
    const seen = new Set<string>();
    for (const cap of capabilities) {
      if (seen.has(cap)) {
        findings.push({ severity: "warning", message: `Capability: duplicate capability '${cap}'` });
      }
      seen.add(cap);
    }
  }

  if (permissions && typeof permissions === "object") {
    // Every permission key must have a matching capability
    for (const permKey of Object.keys(permissions)) {
      if (!Array.isArray(capabilities) || !capabilities.includes(permKey)) {
        findings.push({
          severity: "error",
          message: `Permission '${permKey}' declared without matching capability`,
        });
      }
    }
  }

  // Capabilities that need permissions should have them
  if (Array.isArray(capabilities)) {
    for (const cap of capabilities) {
      if (cap in CAPABILITIES_REQUIRING_PERMISSIONS) {
        if (!permissions || !(cap in permissions)) {
          findings.push({
            severity: "warning",
            message: `Capability '${cap}' declared without permissions config (no scope constraints)`,
          });
        }
      }
    }
  }

  // ── 5. Schedule block validity ──────────────────────────────────────────────
  const schedule = manifest.schedule as Record<string, unknown> | undefined;
  if (schedule && typeof schedule === "object") {
    const trigger = schedule.trigger;
    if (!trigger) {
      findings.push({ severity: "error", message: `Schedule: missing 'trigger' field` });
    } else if (!["interval", "cron", "startup"].includes(trigger as string)) {
      findings.push({ severity: "error", message: `Schedule: invalid trigger '${trigger}' (must be interval, cron, or startup)` });
    }

    if (trigger === "interval") {
      const minutes = schedule.interval_minutes;
      if (minutes === undefined || minutes === null) {
        findings.push({ severity: "error", message: `Schedule: 'interval_minutes' is required when trigger is 'interval'` });
      } else if (typeof minutes !== "number" || minutes < 1 || !Number.isInteger(minutes)) {
        findings.push({ severity: "error", message: `Schedule: 'interval_minutes' must be a positive integer` });
      }
    }

    if (trigger === "cron") {
      const cron = schedule.cron;
      if (!cron || typeof cron !== "string") {
        findings.push({ severity: "error", message: `Schedule: 'cron' expression is required when trigger is 'cron'` });
      } else {
        // Basic cron format check: 5 fields separated by spaces
        const parts = cron.trim().split(/\s+/);
        if (parts.length !== 5) {
          findings.push({ severity: "error", message: `Schedule: cron expression must have 5 fields, got ${parts.length}` });
        }
      }
    }
  }

  // ── 6. Runtime block checks ─────────────────────────────────────────────────
  const runtime = manifest.runtime as Record<string, unknown> | undefined;
  if (runtime && typeof runtime === "object") {
    if (runtime.max_tokens !== undefined) {
      const tokens = runtime.max_tokens as number;
      if (typeof tokens !== "number" || tokens < 1) {
        findings.push({ severity: "error", message: `Runtime: 'max_tokens' must be a positive integer` });
      }
    }
  }

  // ── Report findings ─────────────────────────────────────────────────────────
  const errors = findings.filter((f) => f.severity === "error");
  const warnings = findings.filter((f) => f.severity === "warning");
  const infos = findings.filter((f) => f.severity === "info");

  if (errors.length > 0) {
    console.log("Errors:");
    for (const f of errors) {
      console.log(`  [ERROR]   ${f.message}`);
    }
  }

  if (warnings.length > 0) {
    if (errors.length > 0) console.log();
    console.log("Warnings:");
    for (const f of warnings) {
      console.log(`  [WARN]    ${f.message}`);
    }
  }

  if (infos.length > 0) {
    if (errors.length > 0 || warnings.length > 0) console.log();
    console.log("Info:");
    for (const f of infos) {
      console.log(`  [INFO]    ${f.message}`);
    }
  }

  console.log();

  if (errors.length === 0) {
    const name = (manifest as Record<string, unknown>).name ?? "agent";
    const version = (manifest as Record<string, unknown>).version ?? "?";
    console.log(`Validation passed: ${name} v${version}`);
    if (warnings.length > 0) {
      console.log(`  ${warnings.length} warning(s) — review before publishing.`);
    }
  } else {
    console.log(`Validation failed: ${errors.length} error(s), ${warnings.length} warning(s).`);
    process.exit(1);
  }
}
