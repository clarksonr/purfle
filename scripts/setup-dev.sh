#!/usr/bin/env bash
# Purfle developer setup script (macOS / Linux)
# Checks and configures required development tools.

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

ok()   { echo -e "  ${GREEN}✓${NC} $1"; }
warn() { echo -e "  ${YELLOW}⚠${NC} $1"; }
fail() { echo -e "  ${RED}✗${NC} $1"; }

ERRORS=0
WARNINGS=0

echo ""
echo "═══════════════════════════════════════════"
echo "  Purfle Developer Setup"
echo "═══════════════════════════════════════════"
echo ""

# ── 1. .NET SDK ──────────────────────────────

echo "Checking .NET SDK..."
if command -v dotnet &>/dev/null; then
    DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "0")
    MAJOR=$(echo "$DOTNET_VERSION" | cut -d. -f1)
    if [ "$MAJOR" -ge 8 ] 2>/dev/null; then
        ok ".NET SDK $DOTNET_VERSION"
    else
        fail ".NET SDK $DOTNET_VERSION found, but 8.0+ required"
        echo "       Install from: https://dotnet.microsoft.com/download"
        ERRORS=$((ERRORS + 1))
    fi
else
    fail ".NET SDK not found"
    echo "       Install from: https://dotnet.microsoft.com/download"
    ERRORS=$((ERRORS + 1))
fi

# ── 2. Node.js ───────────────────────────────

echo "Checking Node.js..."
if command -v node &>/dev/null; then
    NODE_VERSION=$(node --version | sed 's/v//')
    MAJOR=$(echo "$NODE_VERSION" | cut -d. -f1)
    if [ "$MAJOR" -ge 18 ] 2>/dev/null; then
        ok "Node.js $NODE_VERSION"
    else
        fail "Node.js $NODE_VERSION found, but 18+ required"
        echo "       Install from: https://nodejs.org/"
        ERRORS=$((ERRORS + 1))
    fi
else
    fail "Node.js not found"
    echo "       Install from: https://nodejs.org/"
    ERRORS=$((ERRORS + 1))
fi

# ── 3. npm packages ──────────────────────────

echo "Checking npm packages..."
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [ -d "$REPO_ROOT/sdk/node_modules" ]; then
    ok "sdk/node_modules exists"
else
    echo "  Installing npm packages in sdk/..."
    if (cd "$REPO_ROOT/sdk" && npm install 2>/dev/null); then
        ok "npm install completed"
    else
        fail "npm install failed"
        ERRORS=$((ERRORS + 1))
    fi
fi

# ── 4. Environment variables ─────────────────

echo "Checking environment variables..."

HAS_LLM_KEY=false
[ -n "${GEMINI_API_KEY:-}" ]    && HAS_LLM_KEY=true && ok "GEMINI_API_KEY set"
[ -n "${ANTHROPIC_API_KEY:-}" ] && HAS_LLM_KEY=true && ok "ANTHROPIC_API_KEY set"
[ -n "${OPENAI_API_KEY:-}" ]    && HAS_LLM_KEY=true && ok "OPENAI_API_KEY set"

if [ "$HAS_LLM_KEY" = false ]; then
    warn "No LLM API key set (GEMINI_API_KEY, ANTHROPIC_API_KEY, or OPENAI_API_KEY)"
    WARNINGS=$((WARNINGS + 1))
fi

if [ -n "${PURFLE_ADMIN_TOKEN:-}" ]; then
    ok "PURFLE_ADMIN_TOKEN set"
else
    warn "PURFLE_ADMIN_TOKEN not set (needed for admin routes)"
    WARNINGS=$((WARNINGS + 1))
fi

if [ -n "${PURFLE_REGISTRY_API_KEY:-}" ]; then
    ok "PURFLE_REGISTRY_API_KEY set"
else
    warn "PURFLE_REGISTRY_API_KEY not set (needed for key registration)"
    WARNINGS=$((WARNINGS + 1))
fi

# ── 5. Azure Key Registry ────────────────────

echo "Checking Azure Key Registry..."
REGISTRY_URL="https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net"
if command -v curl &>/dev/null; then
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$REGISTRY_URL/api/keys/ping" 2>/dev/null || echo "000")
    if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "204" ]; then
        ok "Key Registry reachable ($REGISTRY_URL)"
    else
        warn "Key Registry returned HTTP $HTTP_CODE (may be cold-starting or unreachable)"
        WARNINGS=$((WARNINGS + 1))
    fi
else
    warn "curl not found — cannot check Key Registry"
    WARNINGS=$((WARNINGS + 1))
fi

# ── 6. Signing key ───────────────────────────

echo "Checking signing key..."
SIGNING_KEY="$HOME/.purfle/signing.key.pem"
if [ -f "$SIGNING_KEY" ]; then
    ok "Signing key found at $SIGNING_KEY"
else
    warn "No signing key at $SIGNING_KEY"
    if command -v purfle &>/dev/null; then
        echo "       Run: purfle keygen"
    else
        echo "       Build the CLI first, then run: purfle keygen"
    fi
    WARNINGS=$((WARNINGS + 1))
fi

# ── 7. Build TypeScript SDK ──────────────────

echo "Building TypeScript SDK..."
if command -v npm &>/dev/null && [ -f "$REPO_ROOT/sdk/package.json" ]; then
    if (cd "$REPO_ROOT/sdk" && npm run build 2>/dev/null); then
        ok "TypeScript SDK built"
    else
        fail "TypeScript SDK build failed"
        ERRORS=$((ERRORS + 1))
    fi
else
    warn "Cannot build SDK — npm or sdk/package.json missing"
    WARNINGS=$((WARNINGS + 1))
fi

# ── 8. Restore .NET packages ─────────────────

echo "Restoring .NET packages..."
if command -v dotnet &>/dev/null && [ -f "$REPO_ROOT/runtime/Purfle.Runtime.slnx" ]; then
    if (cd "$REPO_ROOT/runtime" && dotnet restore 2>/dev/null); then
        ok ".NET packages restored"
    else
        fail ".NET restore failed"
        ERRORS=$((ERRORS + 1))
    fi
else
    warn "Cannot restore — dotnet or solution file missing"
    WARNINGS=$((WARNINGS + 1))
fi

# ── Summary ──────────────────────────────────

echo ""
echo "═══════════════════════════════════════════"
if [ "$ERRORS" -eq 0 ] && [ "$WARNINGS" -eq 0 ]; then
    echo -e "  ${GREEN}All checks passed!${NC}"
elif [ "$ERRORS" -eq 0 ]; then
    echo -e "  ${YELLOW}$WARNINGS warning(s), no errors${NC}"
else
    echo -e "  ${RED}$ERRORS error(s), $WARNINGS warning(s)${NC}"
fi
echo "═══════════════════════════════════════════"
echo ""

if [ "$ERRORS" -gt 0 ]; then
    echo "Next steps:"
    echo "  1. Fix the errors above"
    echo "  2. Re-run this script"
    exit 1
fi

if [ "$WARNINGS" -gt 0 ]; then
    echo "Next steps:"
    echo "  - Address warnings above (optional but recommended)"
    echo "  - Run: ./scripts/start-dev.sh"
else
    echo "Next steps:"
    echo "  - Run: ./scripts/start-dev.sh"
fi
echo ""
