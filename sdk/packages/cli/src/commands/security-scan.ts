import { readFileSync, existsSync, readdirSync, statSync } from "fs";
import { join, resolve } from "path";
import { parseManifest, verifyManifest } from "@purfle/core";
import type { AgentManifest } from "@purfle/core";

interface ScanOptions {
  publicKey?: string;
  verbose?: boolean;
}

type Severity = "critical" | "high" | "medium" | "low" | "info";

interface Finding {
  severity: Severity;
  category: string;
  message: string;
}

const SEVERITY_ORDER: Record<Severity, number> = {
  critical: 0,
  high: 1,
  medium: 2,
  low: 3,
  info: 4,
};

const SEVERITY_LABELS: Record<Severity, string> = {
  critical: "CRITICAL",
  high:     "HIGH    ",
  medium:   "MEDIUM  ",
  low:      "LOW     ",
  info:     "INFO    ",
};

/**
 * Checks for overly broad permissions in the manifest.
 */
function checkPermissions(manifest: AgentManifest, findings: Finding[]): void {
  const perms = manifest.permissions;
  if (!perms) return;

  // Network: wildcard or overly broad hosts
  const netPerm = perms["network.outbound"];
  if (netPerm) {
    const hosts = netPerm.hosts ?? [];
    for (const host of hosts) {
      if (host === "*" || host === "*.com" || host === "*.net" || host === "*.org") {
        findings.push({
          severity: "critical",
          category: "permissions",
          message: `Overly broad network permission: host '${host}' allows unrestricted outbound access`,
        });
      } else if (host.startsWith("*.")) {
        findings.push({
          severity: "medium",
          category: "permissions",
          message: `Wildcard network host '${host}' — consider restricting to specific hosts`,
        });
      }
    }
    if (hosts.length > 10) {
      findings.push({
        severity: "medium",
        category: "permissions",
        message: `Agent requests access to ${hosts.length} hosts — review if all are necessary`,
      });
    }
  }

  // Filesystem: root or overly broad paths
  const BROAD_PATHS = ["/", "C:\\", "C:/", "~", "%USERPROFILE%", "$HOME", ".."];
  for (const fsKey of ["fs.read", "fs.write"] as const) {
    const fsPerm = perms[fsKey];
    if (!fsPerm) continue;
    const paths = fsPerm.paths ?? [];
    for (const p of paths) {
      if (BROAD_PATHS.includes(p)) {
        findings.push({
          severity: "critical",
          category: "permissions",
          message: `Overly broad ${fsKey} permission: path '${p}' grants access to the entire filesystem`,
        });
      } else if (p.includes("..")) {
        findings.push({
          severity: "high",
          category: "permissions",
          message: `Path traversal in ${fsKey}: '${p}' contains '..'`,
        });
      }
    }
  }

  // Env: sensitive variable names
  const envPerm = perms["env.read"];
  if (envPerm) {
    const vars = envPerm.vars ?? [];
    const sensitivePatterns = ["SECRET", "PASSWORD", "TOKEN", "PRIVATE_KEY", "CREDENTIALS"];
    for (const v of vars) {
      const upper = v.toUpperCase();
      for (const pattern of sensitivePatterns) {
        if (upper.includes(pattern)) {
          findings.push({
            severity: "medium",
            category: "permissions",
            message: `env.read requests access to '${v}' — potentially sensitive variable`,
          });
          break;
        }
      }
    }
  }
}

/**
 * Validates the signature chain.
 */
function checkSignature(
  manifest: AgentManifest,
  dir: string,
  publicKeyPath: string | undefined,
  findings: Finding[]
): void {
  if (!manifest.identity.signature) {
    findings.push({
      severity: "high",
      category: "signature",
      message: "Manifest is not signed — cannot verify publisher identity",
    });
    return;
  }

  // Validate JWS structure
  const parts = manifest.identity.signature.split(".");
  if (parts.length !== 3) {
    findings.push({
      severity: "critical",
      category: "signature",
      message: "Malformed JWS signature — expected 3 dot-separated parts",
    });
    return;
  }

  // Verify key_id is present and non-placeholder
  if (!manifest.identity.key_id || manifest.identity.key_id === "unsigned") {
    findings.push({
      severity: "high",
      category: "signature",
      message: "key_id is missing or set to 'unsigned' — signature cannot be traced to a publisher",
    });
  }

  // Check expiry
  const expires = Date.parse(manifest.identity.expires_at);
  if (!isNaN(expires) && expires < Date.now()) {
    findings.push({
      severity: "high",
      category: "signature",
      message: `Signature expired on ${manifest.identity.expires_at}`,
    });
  }

  // If a public key is provided, attempt verification
  if (publicKeyPath) {
    const fullKeyPath = resolve(publicKeyPath);
    if (!existsSync(fullKeyPath)) {
      findings.push({
        severity: "medium",
        category: "signature",
        message: `Public key file not found: ${fullKeyPath} — cannot verify signature`,
      });
    } else {
      const publicKeyPem = readFileSync(fullKeyPath, "utf8");
      try {
        const valid = verifyManifest(manifest, publicKeyPem);
        if (valid) {
          findings.push({
            severity: "info",
            category: "signature",
            message: "Signature verification succeeded",
          });
        } else {
          findings.push({
            severity: "critical",
            category: "signature",
            message: "Signature verification FAILED — manifest may have been tampered with",
          });
        }
      } catch (e) {
        findings.push({
          severity: "high",
          category: "signature",
          message: `Signature verification error: ${(e as Error).message}`,
        });
      }
    }
  } else {
    findings.push({
      severity: "low",
      category: "signature",
      message: "No public key provided (--public-key) — signature present but not verified",
    });
  }
}

/**
 * Scans for known vulnerable or suspicious dependency patterns.
 * This is a heuristic check — real dependency auditing requires a registry.
 */
function checkDependencies(dir: string, findings: Finding[]): void {
  // Check for package.json (Node.js agent)
  const packageJsonPath = join(dir, "package.json");
  if (existsSync(packageJsonPath)) {
    try {
      const pkg = JSON.parse(readFileSync(packageJsonPath, "utf8"));
      const allDeps = {
        ...(pkg.dependencies ?? {}),
        ...(pkg.devDependencies ?? {}),
      } as Record<string, string>;

      // Known vulnerable packages (mock check — in production this would query an advisory DB)
      const KNOWN_VULNERABLE: Record<string, string> = {
        "event-stream": "Known supply-chain attack (flatmap-stream incident)",
        "ua-parser-js": "Known malicious version published (2021)",
        "coa": "Known hijacked package (2021)",
        "rc": "Known hijacked package (2021)",
      };

      for (const dep of Object.keys(allDeps)) {
        if (dep in KNOWN_VULNERABLE) {
          findings.push({
            severity: "critical",
            category: "dependencies",
            message: `Vulnerable dependency '${dep}': ${KNOWN_VULNERABLE[dep]}`,
          });
        }
      }

      // Check for wildcard or git dependencies
      for (const [dep, version] of Object.entries(allDeps)) {
        if (version === "*" || version === "latest") {
          findings.push({
            severity: "high",
            category: "dependencies",
            message: `Unpinned dependency '${dep}@${version}' — use a specific version`,
          });
        } else if (
          typeof version === "string" &&
          (version.startsWith("git") || version.startsWith("http"))
        ) {
          findings.push({
            severity: "medium",
            category: "dependencies",
            message: `Git/URL dependency '${dep}' — not verifiable through registry`,
          });
        }
      }
    } catch {
      // Ignore parse errors — not a Node.js agent
    }
  }

  // Check for .csproj (NuGet dependencies, .NET agent)
  const csprojFiles = findFiles(dir, ".csproj");
  for (const csproj of csprojFiles) {
    const content = readFileSync(csproj, "utf8");
    // Simple heuristic: check for PackageReference with wildcard versions
    const wildcardRefs = content.match(/PackageReference.*Version="\*"/g);
    if (wildcardRefs) {
      findings.push({
        severity: "high",
        category: "dependencies",
        message: `Unpinned NuGet reference in ${csproj} — use specific versions`,
      });
    }
  }
}

/**
 * Checks for suspicious files in the agent directory.
 */
function checkFiles(dir: string, findings: Finding[]): void {
  const SUSPICIOUS_FILES = [
    ".env",
    "credentials.json",
    "secrets.json",
    "private.key",
    "signing.key.pem",
    ".npmrc",  // may contain auth tokens
  ];

  for (const filename of SUSPICIOUS_FILES) {
    const filepath = join(dir, filename);
    if (existsSync(filepath)) {
      findings.push({
        severity: "high",
        category: "files",
        message: `Sensitive file found: '${filename}' — should not be included in agent package`,
      });
    }
  }
}

/**
 * Checks the capability set for suspicious combinations.
 */
function checkCapabilities(manifest: AgentManifest, findings: Finding[]): void {
  const caps = new Set(manifest.capabilities);

  // Network + filesystem = potential data exfiltration
  if (caps.has("network.outbound") && (caps.has("fs.read") || caps.has("fs.write"))) {
    findings.push({
      severity: "medium",
      category: "capabilities",
      message: "Agent has both network and filesystem access — review for data exfiltration risk",
    });
  }

  // All capabilities requested
  if (manifest.capabilities.length >= 6) {
    findings.push({
      severity: "medium",
      category: "capabilities",
      message: `Agent requests ${manifest.capabilities.length} capabilities — high privilege. Apply least-privilege principle.`,
    });
  }
}

/** Simple recursive file finder by extension. */
function findFiles(dir: string, ext: string, maxDepth: number = 3, depth: number = 0): string[] {
  if (depth >= maxDepth) return [];
  const results: string[] = [];
  try {
    const entries = readdirSync(dir);
    for (const entry of entries) {
      if (entry === "node_modules" || entry === ".git") continue;
      const full = join(dir, entry);
      try {
        const stat = statSync(full);
        if (stat.isDirectory()) {
          results.push(...findFiles(full, ext, maxDepth, depth + 1));
        } else if (entry.endsWith(ext)) {
          results.push(full);
        }
      } catch {
        // Skip inaccessible files
      }
    }
  } catch {
    // Skip inaccessible directories
  }
  return results;
}

export function securityScanCommand(dir: string, options: ScanOptions): void {
  const manifestPath = join(dir, "agent.json");

  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifest = parseManifest(readFileSync(manifestPath, "utf8"));
  const absDir = resolve(dir);

  console.log("┌─ Purfle Security Scan ─────────────────────────────────────┐");
  console.log(`│  Agent   : ${manifest.name} v${manifest.version}`);
  console.log(`│  ID      : ${manifest.id}`);
  console.log(`│  Engine  : ${manifest.runtime.engine}`);
  console.log("└────────────────────────────────────────────────────────────┘");
  console.log();

  const findings: Finding[] = [];

  // Run all checks
  checkPermissions(manifest, findings);
  checkSignature(manifest, absDir, options.publicKey, findings);
  checkDependencies(absDir, findings);
  checkFiles(absDir, findings);
  checkCapabilities(manifest, findings);

  // Sort by severity
  findings.sort((a, b) => SEVERITY_ORDER[a.severity] - SEVERITY_ORDER[b.severity]);

  // Group by category for display
  if (findings.length === 0) {
    console.log("No issues found.");
    console.log();
    return;
  }

  // Display findings
  const categories = [...new Set(findings.map((f) => f.category))];
  for (const category of categories) {
    const catFindings = findings.filter((f) => f.category === category);
    if (catFindings.length === 0) continue;

    console.log(`[${category}]`);
    for (const f of catFindings) {
      console.log(`  ${SEVERITY_LABELS[f.severity]}  ${f.message}`);
    }
    console.log();
  }

  // Summary
  const counts: Record<Severity, number> = { critical: 0, high: 0, medium: 0, low: 0, info: 0 };
  for (const f of findings) {
    counts[f.severity]++;
  }

  const parts: string[] = [];
  if (counts.critical > 0) parts.push(`${counts.critical} critical`);
  if (counts.high > 0) parts.push(`${counts.high} high`);
  if (counts.medium > 0) parts.push(`${counts.medium} medium`);
  if (counts.low > 0) parts.push(`${counts.low} low`);
  if (counts.info > 0) parts.push(`${counts.info} info`);

  console.log(`Summary: ${parts.join(", ")}`);

  if (counts.critical > 0 || counts.high > 0) {
    console.log();
    console.log("Action required: resolve critical and high severity issues before publishing.");
    process.exit(1);
  }
}
