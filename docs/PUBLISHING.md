# Publishing Agents to the Purfle Marketplace

This guide covers how to register as a publisher, verify your domain, and publish agents.

## 1. Register as a Publisher

```bash
# Register with the marketplace
curl -X POST http://localhost:5000/api/publishers/register \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Your Name",
    "domain": "yourdomain.com",
    "email": "you@yourdomain.com",
    "password": "your-password"
  }'
```

This returns a verification challenge and instructions.

## 2. Domain Verification

After registration, you receive a challenge string. To verify domain ownership:

1. Create a file at `https://yourdomain.com/.well-known/purfle-verify.txt`
2. Add a single line: `purfle-verify=<your-challenge-string>`
3. Call the verify endpoint:

```bash
curl -X POST http://localhost:5000/api/publishers/verify \
  -H "Authorization: Bearer <your-token>" \
  -H "Content-Type: application/json" \
  -d '{ "domain": "yourdomain.com" }'
```

Verified publishers receive automatic `publisher-verified` attestations on their agents.

## 3. Create Your Agent

```bash
# Scaffold a new agent
purfle init my-agent

# Edit the manifest
# Edit prompts/system.md with your agent's instructions
```

See [MANIFEST_REFERENCE.md](MANIFEST_REFERENCE.md) for field-by-field details.

## 4. Build and Sign

```bash
# Validate manifest against schema
purfle build my-agent

# Generate a signing key and sign
purfle sign my-agent --generate-key

# Or sign with existing key
purfle sign my-agent --key-file path/to/private.pem
```

## 5. Authenticate with the Marketplace

```bash
# Login (opens browser for OAuth PKCE flow)
purfle login
```

## 6. Publish

```bash
# Publish to marketplace
purfle publish my-agent

# Optionally register your public key first
purfle publish my-agent --register-key
```

On publish:
- Manifest signature is verified server-side
- `marketplace-listed` attestation is auto-issued
- If you're a verified publisher, `publisher-verified` attestation is also issued

## 7. Verify Your Listing

```bash
# Search for your agent
purfle search my-agent

# Check attestations
curl http://localhost:5000/api/attestations/<agent-id>
```

## Attestation Levels

| Attestation | Meaning | Requirement |
|---|---|---|
| `marketplace-listed` | Agent is published to the marketplace | Published via API |
| `publisher-verified` | Publisher has verified domain ownership | Domain verification complete |

## Installing Published Agents

```bash
# Search the marketplace
purfle search "file assistant"

# Install an agent
purfle install <agent-id>

# Install a specific version
purfle install <agent-id> --version 1.0.0
```

Installed agents are stored at `~/.purfle/agents/<agent-id>/`.
