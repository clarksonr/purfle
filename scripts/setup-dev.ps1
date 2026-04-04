# Purfle developer setup script (Windows)
# Checks and configures required development tools.

$ErrorActionPreference = "Continue"

$Errors = 0
$Warnings = 0

function Ok($msg)   { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "  ⚠ $msg" -ForegroundColor Yellow; $script:Warnings++ }
function Fail($msg) { Write-Host "  ✗ $msg" -ForegroundColor Red; $script:Errors++ }

Write-Host ""
Write-Host "═══════════════════════════════════════════"
Write-Host "  Purfle Developer Setup"
Write-Host "═══════════════════════════════════════════"
Write-Host ""

# ── 1. .NET SDK ──────────────────────────────

Write-Host "Checking .NET SDK..."
try {
    $dotnetVersion = & dotnet --version 2>$null
    $major = [int]($dotnetVersion -split '\.')[0]
    if ($major -ge 8) {
        Ok ".NET SDK $dotnetVersion"
    } else {
        Fail ".NET SDK $dotnetVersion found, but 8.0+ required"
        Write-Host "       Install from: https://dotnet.microsoft.com/download"
    }
} catch {
    Fail ".NET SDK not found"
    Write-Host "       Install from: https://dotnet.microsoft.com/download"
}

# ── 2. Node.js ───────────────────────────────

Write-Host "Checking Node.js..."
try {
    $nodeVersion = (& node --version 2>$null) -replace '^v', ''
    $major = [int]($nodeVersion -split '\.')[0]
    if ($major -ge 18) {
        Ok "Node.js $nodeVersion"
    } else {
        Fail "Node.js $nodeVersion found, but 18+ required"
        Write-Host "       Install from: https://nodejs.org/"
    }
} catch {
    Fail "Node.js not found"
    Write-Host "       Install from: https://nodejs.org/"
}

# ── 3. npm packages ──────────────────────────

Write-Host "Checking npm packages..."
$RepoRoot = Split-Path -Parent $PSScriptRoot

if (Test-Path "$RepoRoot\sdk\node_modules") {
    Ok "sdk\node_modules exists"
} else {
    Write-Host "  Installing npm packages in sdk\..."
    Push-Location "$RepoRoot\sdk"
    try {
        & npm install 2>$null
        if ($LASTEXITCODE -eq 0) { Ok "npm install completed" }
        else { Fail "npm install failed" }
    } catch {
        Fail "npm install failed: $_"
    }
    Pop-Location
}

# ── 4. Environment variables ─────────────────

Write-Host "Checking environment variables..."

$hasLlmKey = $false
if ($env:GEMINI_API_KEY)    { $hasLlmKey = $true; Ok "GEMINI_API_KEY set" }
if ($env:ANTHROPIC_API_KEY) { $hasLlmKey = $true; Ok "ANTHROPIC_API_KEY set" }
if ($env:OPENAI_API_KEY)    { $hasLlmKey = $true; Ok "OPENAI_API_KEY set" }

if (-not $hasLlmKey) {
    Warn "No LLM API key set (GEMINI_API_KEY, ANTHROPIC_API_KEY, or OPENAI_API_KEY)"
}

if ($env:PURFLE_ADMIN_TOKEN) {
    Ok "PURFLE_ADMIN_TOKEN set"
} else {
    Warn "PURFLE_ADMIN_TOKEN not set (needed for admin routes)"
}

if ($env:PURFLE_REGISTRY_API_KEY) {
    Ok "PURFLE_REGISTRY_API_KEY set"
} else {
    Warn "PURFLE_REGISTRY_API_KEY not set (needed for key registration)"
}

# ── 5. Azure Key Registry ────────────────────

Write-Host "Checking Azure Key Registry..."
$registryUrl = "https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net"
try {
    $response = Invoke-WebRequest -Uri "$registryUrl/api/keys/ping" -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 204) {
        Ok "Key Registry reachable ($registryUrl)"
    } else {
        Warn "Key Registry returned HTTP $($response.StatusCode)"
    }
} catch {
    Warn "Key Registry unreachable (may be cold-starting or network issue)"
}

# ── 6. Signing key ───────────────────────────

Write-Host "Checking signing key..."
$signingKey = Join-Path $HOME ".purfle\signing.key.pem"
if (Test-Path $signingKey) {
    Ok "Signing key found at $signingKey"
} else {
    Warn "No signing key at $signingKey"
    try {
        & purfle --version 2>$null | Out-Null
        Write-Host "       Run: purfle keygen"
    } catch {
        Write-Host "       Build the CLI first, then run: purfle keygen"
    }
}

# ── 7. Build TypeScript SDK ──────────────────

Write-Host "Building TypeScript SDK..."
if ((Get-Command npm -ErrorAction SilentlyContinue) -and (Test-Path "$RepoRoot\sdk\package.json")) {
    Push-Location "$RepoRoot\sdk"
    try {
        & npm run build 2>$null
        if ($LASTEXITCODE -eq 0) { Ok "TypeScript SDK built" }
        else { Fail "TypeScript SDK build failed" }
    } catch {
        Fail "TypeScript SDK build failed: $_"
    }
    Pop-Location
} else {
    Warn "Cannot build SDK — npm or sdk\package.json missing"
}

# ── 8. Restore .NET packages ─────────────────

Write-Host "Restoring .NET packages..."
if ((Get-Command dotnet -ErrorAction SilentlyContinue) -and (Test-Path "$RepoRoot\runtime\Purfle.Runtime.slnx")) {
    Push-Location "$RepoRoot\runtime"
    try {
        & dotnet restore 2>$null
        if ($LASTEXITCODE -eq 0) { Ok ".NET packages restored" }
        else { Fail ".NET restore failed" }
    } catch {
        Fail ".NET restore failed: $_"
    }
    Pop-Location
} else {
    Warn "Cannot restore — dotnet or solution file missing"
}

# ── Summary ──────────────────────────────────

Write-Host ""
Write-Host "═══════════════════════════════════════════"
if ($Errors -eq 0 -and $Warnings -eq 0) {
    Write-Host "  All checks passed!" -ForegroundColor Green
} elseif ($Errors -eq 0) {
    Write-Host "  $Warnings warning(s), no errors" -ForegroundColor Yellow
} else {
    Write-Host "  $Errors error(s), $Warnings warning(s)" -ForegroundColor Red
}
Write-Host "═══════════════════════════════════════════"
Write-Host ""

if ($Errors -gt 0) {
    Write-Host "Next steps:"
    Write-Host "  1. Fix the errors above"
    Write-Host "  2. Re-run this script"
    exit 1
}

if ($Warnings -gt 0) {
    Write-Host "Next steps:"
    Write-Host "  - Address warnings above (optional but recommended)"
    Write-Host "  - Run: .\scripts\start-dev.ps1"
} else {
    Write-Host "Next steps:"
    Write-Host "  - Run: .\scripts\start-dev.ps1"
}
Write-Host ""
