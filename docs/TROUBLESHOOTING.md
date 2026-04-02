# Troubleshooting

Common errors when building, signing, loading, and running Purfle agents.

---

## Signature Verification Failed

**Error:** `LoadFailureReason.SignatureInvalid`

**Message:** `"Signature verification failed."` or `"Manifest has no signature."`

### Causes

1. **Manifest modified after signing.** Any change to the manifest JSON — even whitespace — invalidates the signature. The runtime strips the `signature` field, canonicalizes the remaining JSON (sorted keys, no whitespace), and verifies the JWS against that canonical form.

2. **Key mismatch.** The public key fetched from the registry for `identity.key_id` does not match the private key that signed the manifest.

3. **Missing signature.** The `identity.signature` field is absent or empty. Run `purfle sign` to sign the manifest.

### Fix

```bash
# Re-sign after any manifest edit
purfle sign my-agent --key-file signing.key.pem --key-id my-key-id

# Verify it worked
purfle build my-agent
```

If you edited the manifest after signing, you must sign again. There is no way to update a manifest without re-signing.

---

## Key Not Found

**Error:** `LoadFailureReason.KeyNotFound`

**Message:** `"Signing key '<key_id>' not found in registry."`

### Causes

1. **Key not registered.** The public key for `identity.key_id` was never uploaded to the key registry.
2. **Wrong key ID.** The `key_id` in the manifest does not match any registered key.
3. **Registry unreachable.** The `HttpKeyRegistryClient` could not reach the key registry API.

### Fix

```bash
# Register your public key
purfle publish my-agent --register-key signing.pub.pem --registry <url>

# Or register directly via the API
curl -X POST https://purfle-key-registry-<host>/keys \
  -H "Content-Type: application/json" \
  -d '{"id": "my-key-id", "publicKeyPem": "..."}'
```

Check that `identity.key_id` in your manifest matches exactly the ID you registered. Key IDs are case-sensitive.

---

## Key Revoked

**Error:** `LoadFailureReason.KeyRevoked`

**Message:** `"Signing key '<key_id>' has been revoked."`

### Causes

1. **Key actually revoked.** Someone called `DELETE /keys/{id}` on the registry, marking the key as revoked.
2. **Compromised key.** If you revoked the key intentionally because it was compromised, you need to generate a new key pair and re-sign.

### Fix

If the revocation was intentional:
```bash
# Generate a new key pair and re-sign
purfle sign my-agent --generate-key
# Register the new public key
purfle publish my-agent --register-key signing.pub.pem --registry <url>
```

If the revocation was accidental, re-register the key (revocation in the current registry is a soft delete — re-POSTing the key restores it).

---

## Manifest Expired

**Error:** `LoadFailureReason.ManifestExpired`

**Message:** `"Manifest expired at '<expires_at>'."`

### Causes

The current time is past `identity.expires_at`. The signature is no longer valid.

### Fix

Update `identity.expires_at` to a future date and re-sign:

```bash
# Edit the manifest to update expires_at, then re-sign
purfle sign my-agent --key-file signing.key.pem --key-id my-key-id
```

---

## Schema Validation Failed

**Error:** `LoadFailureReason.SchemaValidationFailed`

**Message:** Lists specific field-level errors.

### Common causes

1. **Missing required fields.** The required top-level fields are: `purfle`, `id`, `name`, `version`, `identity`, `capabilities`, `runtime`.

2. **Invalid UUID format.** The `id` field must be a valid UUID: `^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$`

3. **Invalid semver.** The `version` field must match `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$`

4. **Permissions without capability.** If `permissions` contains a key like `"network.outbound"`, then `capabilities` must include `"network.outbound"`.

5. **Unknown capability string.** Only the seven phase 1 capability strings are accepted: `llm.chat`, `llm.completion`, `network.outbound`, `env.read`, `fs.read`, `fs.write`, `mcp.tool`.

6. **Extra properties.** The schema sets `additionalProperties: false` on the root object and most sub-objects. Unknown fields are rejected.

### Fix

```bash
# Validate and see specific errors
purfle build my-agent
```

---

## Capability Missing

**Error:** `LoadFailureReason.CapabilityMissing`

**Message:** `"Agent requires capability '<cap>' but the runtime does not provide it."`

### Causes

The agent declares a capability in `capabilities[]` that the runtime does not support. This is a negotiation failure — the runtime cannot fulfill the agent's requirements.

### Fix

Remove the unsupported capability from `capabilities[]`, or use a runtime that supports it.

---

## Permission Denied (Filesystem)

**Error:** `"Error: permission denied — '<path>' not in filesystem.read allowlist."` or `"...filesystem.write allowlist."`

### Causes

1. **Missing `fs.read` or `fs.write` capability.** The capability is not in `capabilities[]`.
2. **Path not in permissions.** The path is not listed in `permissions.fs.read.paths` or `permissions.fs.write.paths`.
3. **Path traversal.** The resolved path escapes the allowed paths via `..` segments.

### Fix

Add the capability and permission:

```json
{
  "capabilities": ["fs.read", "fs.write"],
  "permissions": {
    "fs.read":  { "paths": ["./data"] },
    "fs.write": { "paths": ["./output"] }
  }
}
```

Common mistakes:
- Using absolute paths when the manifest expects relative paths (or vice versa)
- Using `..` to traverse outside allowed directories — the sandbox resolves paths before checking
- Forgetting that the capability must be in `capabilities[]`, not just `permissions`

---

## Permission Denied (Network)

**Error:** `"Error: permission denied — '<url>' not in network.allow list."`

### Causes

1. **Missing `network.outbound` capability.**
2. **Host not in permissions.** The URL's hostname is not in `permissions.network.outbound.hosts`.

### Fix

```json
{
  "capabilities": ["network.outbound"],
  "permissions": {
    "network.outbound": { "hosts": ["api.github.com"] }
  }
}
```

The sandbox checks the hostname, not the full URL. Include only the hostname (no protocol or path).

---

## Permission Denied (Environment Variables)

**Error:** Blocked when reading an environment variable.

### Causes

1. **Missing `env.read` capability.**
2. **Variable not in permissions.** The variable name is not in `permissions.env.read.vars`.

### Fix

```json
{
  "capabilities": ["env.read"],
  "permissions": {
    "env.read": { "vars": ["MY_API_KEY"] }
  }
}
```

**Note:** The runtime's own API key (`ANTHROPIC_API_KEY`, `GEMINI_API_KEY`) is read by the runtime infrastructure, not the agent. Agents do not need `env.read` for the inference adapter's API key.

---

## Inference Adapter Not Available

**Error:** `LoadFailureReason.EngineNotSupported`

**Message:** `"Engine '<engine>' is not supported by this runtime."`

### Causes

1. **Wrong engine value.** Valid engines: `anthropic`, `gemini`, `openai-compatible`, `openclaw`, `ollama`.
2. **API key not set.** The runtime cannot create the adapter because the required environment variable is missing.
3. **Wrong provider prefix.** Using `"engine": "claude"` instead of `"engine": "anthropic"`, or `"engine": "google"` instead of `"engine": "gemini"`.

### Fix

Check `runtime.engine` in your manifest:

```json
{
  "runtime": {
    "requires": "purfle/0.1",
    "engine": "anthropic",
    "model": "claude-sonnet-4-20250514"
  }
}
```

Set the required API key:

```bash
# For Anthropic
export ANTHROPIC_API_KEY="sk-ant-..."

# For Gemini
export GEMINI_API_KEY="..."
```

---

## MCP Tool Call Blocked

**Error:** Tool call blocked at sandbox layer.

### Causes

1. **Missing `mcp.tool` capability.** The agent does not declare `mcp.tool` in `capabilities[]`.
2. **Tool not declared.** The tool server is not listed in the `tools[]` array in the manifest.
3. **Server mismatch.** The MCP server name or URL does not match what the AIVM expects.

### Fix

Declare the tool binding and capability:

```json
{
  "capabilities": ["mcp.tool"],
  "tools": [
    {
      "name": "filesystem",
      "server": "npx @modelcontextprotocol/server-filesystem",
      "description": "Read and write files"
    }
  ]
}
```

The `name` field in `tools[]` is what the LLM sees. The `server` field is what the AIVM connects to.

---

## Assembly Load Failed

**Error:** `LoadFailureReason.AssemblyLoadFailed`

**Message:** `"Failed to load agent assembly: <details>"`

### Causes

1. **DLL not found.** The `lifecycle.on_load` type references an assembly that does not exist in the agent package's `lib/` directory.
2. **Wrong type name.** The assembly-qualified type name is malformed or the type does not exist.
3. **Missing dependencies.** The agent assembly depends on a library not included in the package.

### Fix

Verify the type string format: `"Namespace.ClassName, AssemblyName"`

```json
{
  "lifecycle": {
    "on_load": "Purfle.Agents.Chat.ChatAgent, Purfle.Agents.Chat"
  }
}
```

Ensure the DLL exists at `<agent-dir>/lib/Purfle.Agents.Chat.dll`.

---

## Assembly Entry Point Missing

**Error:** `LoadFailureReason.AssemblyEntryPointMissing`

### Causes

The type specified in `lifecycle.on_load` was found but does not implement the expected agent interface or is not a public class.

### Fix

Ensure your agent class is `public` and implements the required interface:

```csharp
public class ChatAgent : IAgentEntryPoint
{
    public Task RunAsync(AgentContext context, CancellationToken ct) { ... }
}
```

---

## Malformed JSON

**Error:** `LoadFailureReason.MalformedJson`

**Message:** `"Manifest root must be a JSON object."` or a JSON parse error.

### Causes

1. **Invalid JSON syntax.** Trailing commas, unquoted keys, single quotes, comments.
2. **Root is not an object.** The manifest must be a JSON object `{}`, not an array or primitive.
3. **BOM or encoding issues.** The file contains a byte-order mark or is not UTF-8.

### Fix

Validate your JSON:

```bash
# Quick syntax check
npx jsonlint my-agent/agent.json

# Full schema validation
purfle build my-agent
```

---

## Common Mistakes

### Using `..` in paths

```json
"fs.read": { "paths": ["../../etc/passwd"] }
```

The sandbox resolves all paths to their canonical form before checking. Path traversal with `..` does not bypass restrictions — it is resolved and checked against the allowlist.

### Wrong algorithm

```json
"algorithm": "RS256"
```

Only `ES256` is accepted. This is a locked decision — the schema enforces it with a `const` constraint.

### Absolute vs relative paths

Permissions paths are checked as declared. If your manifest says `"./data"` but the tool passes `"C:/Users/roman/data"`, the check may fail depending on how the sandbox resolves them. Be consistent — use the same path format your tools will use at runtime.

### Duplicate capabilities

```json
"capabilities": ["llm.chat", "llm.chat"]
```

The schema requires `uniqueItems: true`. Duplicate capability strings cause validation failure.

### Forgetting to re-sign

Any edit to the manifest — even changing the description — invalidates the signature. Always run `purfle sign` after editing.
