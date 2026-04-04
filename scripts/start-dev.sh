#!/usr/bin/env bash
# Purfle dev startup script (macOS / Linux)
# Starts all services needed for development.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PIDS=()

cleanup() {
    echo ""
    echo "Stopping all services..."
    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    wait 2>/dev/null || true
    echo "Stopped."
}
trap cleanup INT TERM EXIT

echo ""
echo "═══════════════════════════════════════════"
echo "  Purfle Dev Environment"
echo "═══════════════════════════════════════════"
echo ""

# ── 1. IdentityHub.Web ──────────────────────

IDENTITYHUB_PROJECT="$REPO_ROOT/identityhub/src/Purfle.IdentityHub.Web"
if [ -d "$IDENTITYHUB_PROJECT" ]; then
    echo "Starting IdentityHub.Web on :5200..."
    (cd "$IDENTITYHUB_PROJECT" && dotnet run --urls "http://localhost:5200" 2>&1 | sed 's/^/  [identityhub] /') &
    PIDS+=($!)
else
    echo "  IdentityHub.Web not found, skipping."
fi

# ── 2. Marketplace API ──────────────────────

MARKETPLACE_PROJECT="$REPO_ROOT/marketplace/src/Purfle.Marketplace.Api"
if [ -d "$MARKETPLACE_PROJECT" ]; then
    echo "Starting Marketplace API on :5100..."
    (cd "$MARKETPLACE_PROJECT" && dotnet run --urls "http://localhost:5100" 2>&1 | sed 's/^/  [marketplace] /') &
    PIDS+=($!)
else
    echo "  Marketplace API not found, skipping."
fi

# ── 3. MCP Servers ───────────────────────────

start_mcp() {
    local name="$1"
    local dir="$2"
    local port="$3"

    if [ -d "$dir" ] && [ -f "$dir/package.json" ]; then
        echo "Starting $name on :$port..."
        if [ -d "$dir/dist" ]; then
            (cd "$dir" && node dist/index.js 2>&1 | sed "s/^/  [$name] /") &
        else
            echo "  Building $name first..."
            (cd "$dir" && npm run build 2>/dev/null && node dist/index.js 2>&1 | sed "s/^/  [$name] /") &
        fi
        PIDS+=($!)
    else
        echo "  $name not found, skipping."
    fi
}

start_mcp "mcp-file-server" "$REPO_ROOT/tools/mcp-file-server" "8100"
start_mcp "mcp-gmail"       "$REPO_ROOT/tools/mcp-gmail"       "8102"
start_mcp "mcp-github"      "$REPO_ROOT/tools/mcp-github"      "8111"

# ── 4. Desktop App ──────────────────────────

sleep 2  # Let services start first

OS="$(uname -s)"
case "$OS" in
    Linux*)
        DESKTOP_PROJECT="$REPO_ROOT/runtime/src/Purfle.Desktop.Avalonia"
        if [ -d "$DESKTOP_PROJECT" ]; then
            echo "Starting Avalonia desktop app..."
            (cd "$DESKTOP_PROJECT" && dotnet run 2>&1 | sed 's/^/  [desktop] /') &
            PIDS+=($!)
        else
            echo "  Avalonia desktop app not found."
        fi
        ;;
    *)
        APP_PROJECT="$REPO_ROOT/app/src/Purfle.App"
        if [ -d "$APP_PROJECT" ]; then
            echo "Starting MAUI desktop app..."
            (cd "$APP_PROJECT" && dotnet run 2>&1 | sed 's/^/  [desktop] /') &
            PIDS+=($!)
        else
            echo "  MAUI desktop app not found."
        fi
        ;;
esac

# ── 5. Summary ───────────────────────────────

sleep 1
echo ""
echo "═══════════════════════════════════════════"
echo "  Purfle dev environment running:"
echo "    IdentityHub.Web  → http://localhost:5200"
echo "    Marketplace API  → http://localhost:5100"
echo "    MCP servers      → :8100, :8102, :8111"
echo "    Desktop app      → running"
echo ""
echo "  Press Ctrl+C to stop all."
echo "═══════════════════════════════════════════"

# Wait for all background processes
wait
