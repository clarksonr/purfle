<#
.SYNOPSIS
    Signs all sample agent manifests and publishes them to the local marketplace.

.DESCRIPTION
    One-time setup script. Requires:
      - Marketplace API running at $Registry (dotnet run --project marketplace/src/Purfle.Marketplace.Api)
      - Node.js installed
      - SDK built (cd sdk && npm install && npm run build)

.PARAMETER Registry
    Marketplace API base URL. Defaults to http://localhost:5000

.PARAMETER Email
    Publisher account email.

.PARAMETER Password
    Publisher account password (min 8 chars).

.PARAMETER DisplayName
    Publisher display name shown in the marketplace.

.EXAMPLE
    .\seed-marketplace.ps1
    .\seed-marketplace.ps1 -Registry http://localhost:5000 -Email "roman@purfle.dev" -Password "MyPass123!"
#>

param(
    [string]$Registry    = "http://localhost:5000",
    [string]$Email       = "roman@purfle.dev",
    [string]$Password    = "Purfle123!",  # dev script only - not for production use
    [string]$DisplayName = "Roman Noble"
)

$ErrorActionPreference = "Stop"

trap {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

$RepoRoot  = $PSScriptRoot
$AgentsDir = Join-Path $RepoRoot "agents"
$KeyDir    = Join-Path $RepoRoot ".signing"
$PrivKey   = Join-Path $KeyDir "signing.key.pem"
$PubKey    = Join-Path $KeyDir "signing.pub.pem"
$CliPath   = Join-Path $RepoRoot "sdk\packages\cli\dist\index.js"

$Manifests = @(
    "chat.agent.json",
    "file-search.agent.json",
    "web-research.agent.json",
    "file-summarizer.agent.json"
)

# --- Preflight ---------------------------------------------------------------
Write-Host "Checking node..." -ForegroundColor Cyan
$ErrorActionPreference = "Continue"
& node --version
$ErrorActionPreference = "Stop"

if (-not (Test-Path $CliPath)) {
    throw "CLI not found at $CliPath`nRun: cd sdk && npm install && npm run build"
}
Write-Host "  CLI: $CliPath" -ForegroundColor Green

Write-Host "Checking marketplace at $Registry..." -ForegroundColor Cyan
try {
    $null = Invoke-RestMethod "$Registry/api/agents" -TimeoutSec 5
    Write-Host "  Marketplace is up." -ForegroundColor Green
} catch {
    throw "Cannot reach $Registry`nRun: dotnet run --project marketplace/src/Purfle.Marketplace.Api"
}

# --- Register publisher account ----------------------------------------------
Write-Host ""
Write-Host "Registering publisher account '$Email'..." -ForegroundColor Cyan
try {
    $body = @{ email = $Email; password = $Password; displayName = $DisplayName } | ConvertTo-Json
    $null = Invoke-RestMethod "$Registry/api/auth/register" `
        -Method Post -Body $body -ContentType "application/json"
    Write-Host "  Account created." -ForegroundColor Green
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 400) {
        Write-Host "  Account already exists - continuing." -ForegroundColor Yellow
    } else {
        throw
    }
}

# --- Sign manifests -----------------------------------------------------------
Write-Host ""
Write-Host "Signing agent manifests..." -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $KeyDir | Out-Null

foreach ($manifestFile in $Manifests) {
    $sourcePath = Join-Path $AgentsDir $manifestFile

    if (-not (Test-Path $sourcePath)) {
        Write-Host "  Skipping $manifestFile (not found)" -ForegroundColor Yellow
        continue
    }

    $tmpDir = Join-Path $env:TEMP "purfle-sign"
    Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
    Copy-Item $sourcePath (Join-Path $tmpDir "agent.json") -Force

    if (-not (Test-Path $PrivKey)) {
        Write-Host "  Generating shared signing key..." -ForegroundColor Gray
        $ErrorActionPreference = "Continue"
        & node $CliPath sign $tmpDir --generate-key --key-id "purfle-samples-key"
        $signExit = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        if ($signExit -ne 0) { throw "purfle sign failed for $manifestFile (exit $signExit)" }

        Copy-Item (Join-Path $tmpDir "signing.key.pem") $PrivKey -Force
        Copy-Item (Join-Path $tmpDir "signing.pub.pem") $PubKey -Force
    } else {
        $ErrorActionPreference = "Continue"
        & node $CliPath sign $tmpDir --key-file $PrivKey --key-id "purfle-samples-key"
        $signExit = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        if ($signExit -ne 0) { throw "purfle sign failed for $manifestFile (exit $signExit)" }
    }

    Copy-Item (Join-Path $tmpDir "agent.json") $sourcePath -Force
    Write-Host "  Signed: $manifestFile" -ForegroundColor Green
    Remove-Item $tmpDir -Recurse -Force
}

Write-Host "  Signing key saved to .signing\  (private key - do not commit)" -ForegroundColor Yellow

# --- Login (password grant - no browser needed) ------------------------------
Write-Host ""
Write-Host "Logging in to $Registry..." -ForegroundColor Cyan
$tokenBody = "grant_type=password&username=$([Uri]::EscapeDataString($Email))&password=$([Uri]::EscapeDataString($Password))&client_id=purfle-cli&scope=openid+email+profile"
$tokenResponse = Invoke-RestMethod "$Registry/connect/token" `
    -Method Post `
    -Body $tokenBody `
    -ContentType "application/x-www-form-urlencoded"
$accessToken = $tokenResponse.access_token

$credsDir = Join-Path $env:USERPROFILE ".purfle"
New-Item -ItemType Directory -Force -Path $credsDir | Out-Null
@{ access_token = $accessToken } | ConvertTo-Json | Set-Content (Join-Path $credsDir "credentials.json")
Write-Host "  Logged in." -ForegroundColor Green

# --- Register signing key with marketplace -----------------------------------
Write-Host ""
Write-Host "Registering signing key..." -ForegroundColor Cyan

# Extract P-256 X/Y coordinates from the PEM using node (same logic as CLI publish command).
$extractScript = Join-Path $env:TEMP "purfle-extract-key.mjs"
@'
import { createPublicKey } from "crypto";
import { readFileSync } from "fs";
const pem = readFileSync(process.argv[2], "utf8");
const key = createPublicKey(pem);
const jwk = key.export({ format: "jwk" });
const x = Buffer.from(jwk.x, "base64url").toString("base64");
const y = Buffer.from(jwk.y, "base64url").toString("base64");
console.log(JSON.stringify({ x, y }));
'@ | Set-Content $extractScript

$ErrorActionPreference = "Continue"
$coordsJson = & node $extractScript $PubKey 2>&1
$ErrorActionPreference = "Stop"
$coords = $coordsJson | ConvertFrom-Json
$keyX = $coords.x
$keyY = $coords.y

$authHeaders = @{ Authorization = "Bearer $accessToken" }
$keyBody = @{ keyId = "purfle-samples-key"; algorithm = "ES256"; x = $keyX; y = $keyY } | ConvertTo-Json
try {
    $null = Invoke-RestMethod "$Registry/api/keys" -Method Post -Body $keyBody `
        -ContentType "application/json" -Headers $authHeaders
    Write-Host "  Key registered." -ForegroundColor Green
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 409) {
        Write-Host "  Key already registered." -ForegroundColor Yellow
    } else {
        throw "Key registration failed: $_"
    }
}

# --- Publish manifests -------------------------------------------------------
Write-Host ""
Write-Host "Publishing agents..." -ForegroundColor Cyan

foreach ($manifestFile in $Manifests) {
    $sourcePath = Join-Path $AgentsDir $manifestFile

    if (-not (Test-Path $sourcePath)) {
        Write-Host "  Skipping $manifestFile (not found)" -ForegroundColor Yellow
        continue
    }

    $manifestBytes = [System.IO.File]::ReadAllBytes($sourcePath)
    try {
        $null = Invoke-WebRequest "$Registry/api/agents" -Method Post -Body $manifestBytes `
            -ContentType "application/json" -Headers $authHeaders
        Write-Host "  Published: $manifestFile" -ForegroundColor Green
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        $body   = $reader.ReadToEnd()
        $reader.Close()
        if ($status -eq 409) {
            Write-Host "  Already published: $manifestFile" -ForegroundColor Yellow
        } else {
            Write-Host "  Failed ($status): $manifestFile" -ForegroundColor Red
            Write-Host "  $body" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "Done. Check the marketplace:" -ForegroundColor Green
Write-Host "  $Registry/api/agents"
