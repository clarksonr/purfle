<#
.SYNOPSIS
    Diagnostic tests for the agent signing and marketplace publish flow.
    Run this to identify exactly where the pipeline breaks.
#>

$RepoRoot = $PSScriptRoot
$Pass = 0
$Fail = 0

function Test-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "[ ] $Name" -ForegroundColor Cyan -NoNewline
    try {
        $result = & $Block
        Write-Host "`r[+] $Name" -ForegroundColor Green
        if ($result) { Write-Host "    $result" -ForegroundColor DarkGray }
        $script:Pass++
        return $true
    } catch {
        Write-Host "`r[-] $Name" -ForegroundColor Red
        Write-Host "    $_" -ForegroundColor Yellow
        $script:Fail++
        return $false
    }
}

Write-Host ""
Write-Host "Purfle Seed Diagnostics" -ForegroundColor White
Write-Host "=======================" -ForegroundColor White

# --- 1. node is in PATH -------------------------------------------------------
$nodeOk = Test-Step "node is in PATH" {
    $v = & node --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "node not found in PATH" }
    "node $v"
}

# --- 2. CLI dist exists -------------------------------------------------------
$cliPath = Join-Path $RepoRoot "sdk\packages\cli\dist\index.js"
$cliOk = Test-Step "CLI dist exists at sdk\packages\cli\dist\index.js" {
    if (-not (Test-Path $cliPath)) {
        throw "Not found. Run: cd sdk && npm install && npm run build"
    }
    $cliPath
}

# --- 3. purfle CLI responds ---------------------------------------------------
$purfleOk = $false
if ($nodeOk -and $cliOk) {
    $purfleOk = Test-Step "purfle CLI responds (node index.js --version)" {
        $ErrorActionPreference = "Continue"
        $out = & node $cliPath --version 2>&1
        $exit = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        "output: $out  exit: $exit"
    }
}

# --- 4. purfle sign on a temp copy of chat.agent.json ------------------------
if ($nodeOk -and $cliOk) {
    Test-Step "purfle sign chat.agent.json" {
        $tmpDir = Join-Path $env:TEMP "purfle-test-sign"
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

        $src = Join-Path $RepoRoot "agents\chat.agent.json"
        Copy-Item $src (Join-Path $tmpDir "agent.json") -Force

        Write-Host ""
        Write-Host "    tmpDir  : $tmpDir" -ForegroundColor DarkGray
        Write-Host "    agent.json exists: $(Test-Path (Join-Path $tmpDir 'agent.json'))" -ForegroundColor DarkGray
        Write-Host "    running : node $cliPath sign $tmpDir --generate-key --key-id test-key" -ForegroundColor DarkGray
        Write-Host ""

        $ErrorActionPreference = "Continue"
        & node $cliPath sign $tmpDir --generate-key --key-id "test-key"
        $exit = $LASTEXITCODE
        $ErrorActionPreference = "Stop"

        if ($exit -ne 0) { throw "exit code $exit" }
        "signed OK, exit 0"
    }
}

# --- 5. marketplace reachable -------------------------------------------------
$marketplaceOk = Test-Step "marketplace reachable at http://localhost:5000" {
    $r = Invoke-RestMethod "http://localhost:5000/api/agents" -TimeoutSec 5
    "responded OK"
}

# --- Summary ------------------------------------------------------------------
Write-Host ""
Write-Host "===============================" -ForegroundColor White
Write-Host "  Passed: $Pass   Failed: $Fail" -ForegroundColor $(if ($Fail -eq 0) { "Green" } else { "Yellow" })
Write-Host "===============================" -ForegroundColor White
Write-Host ""

if ($Fail -gt 0) {
    Read-Host "Press Enter to exit"
}
