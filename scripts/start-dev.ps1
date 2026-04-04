# Purfle dev startup script (Windows)
# Starts all services needed for development.

$ErrorActionPreference = "Continue"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Jobs = @()

function Start-Service($Name, $ScriptBlock) {
    $job = Start-Job -Name $Name -ScriptBlock $ScriptBlock
    $script:Jobs += $job
    Write-Host "  Started $Name (job $($job.Id))"
}

Write-Host ""
Write-Host "==========================================="
Write-Host "  Purfle Dev Environment"
Write-Host "==========================================="
Write-Host ""

# ── 1. IdentityHub.Web ──────────────────────

$identityHubProject = Join-Path $RepoRoot "identityhub\src\Purfle.IdentityHub.Web"
if (Test-Path $identityHubProject) {
    Write-Host "Starting IdentityHub.Web on :5200..."
    Start-Service "IdentityHub.Web" {
        Set-Location $using:identityHubProject
        & dotnet run --urls "http://localhost:5200" 2>&1
    }
} else {
    Write-Host "  IdentityHub.Web not found, skipping."
}

# ── 2. Marketplace API ──────────────────────

$marketplaceProject = Join-Path $RepoRoot "marketplace\src\Purfle.Marketplace.Api"
if (Test-Path $marketplaceProject) {
    Write-Host "Starting Marketplace API on :5100..."
    Start-Service "Marketplace.Api" {
        Set-Location $using:marketplaceProject
        & dotnet run --urls "http://localhost:5100" 2>&1
    }
} else {
    Write-Host "  Marketplace API not found, skipping."
}

# ── 3. MCP Servers ───────────────────────────

function Start-McpServer($Name, $Dir, $Port) {
    if (Test-Path "$Dir\package.json") {
        Write-Host "Starting $Name on :$Port..."
        Start-Service $Name {
            Set-Location $using:Dir
            if (-not (Test-Path "dist")) { & npm run build 2>$null }
            & node dist/index.js 2>&1
        }
    } else {
        Write-Host "  $Name not found, skipping."
    }
}

Start-McpServer "mcp-file-server" (Join-Path $RepoRoot "tools\mcp-file-server") "8100"
Start-McpServer "mcp-gmail"       (Join-Path $RepoRoot "tools\mcp-gmail")       "8102"
Start-McpServer "mcp-github"      (Join-Path $RepoRoot "tools\mcp-github")      "8111"

# ── 4. Desktop App ──────────────────────────

Start-Sleep -Seconds 2

$appProject = Join-Path $RepoRoot "app\src\Purfle.App"
if (Test-Path $appProject) {
    Write-Host "Starting MAUI desktop app..."
    Start-Service "Desktop" {
        Set-Location $using:appProject
        & dotnet run 2>&1
    }
} else {
    Write-Host "  MAUI desktop app not found."
}

# ── 5. Summary ───────────────────────────────

Start-Sleep -Seconds 1
Write-Host ""
Write-Host "==========================================="
Write-Host "  Purfle dev environment running:"
Write-Host "    IdentityHub.Web  -> http://localhost:5200"
Write-Host "    Marketplace API  -> http://localhost:5100"
Write-Host "    MCP servers      -> :8100, :8102, :8111"
Write-Host "    Desktop app      -> running"
Write-Host ""
Write-Host "  Press Ctrl+C to stop all."
Write-Host "==========================================="

# ── Ctrl+C handler ───────────────────────────

try {
    while ($true) {
        foreach ($job in $Jobs) {
            if ($job.State -ne "Running") {
                Write-Host "  [$($job.Name)] stopped ($($job.State))"
                Receive-Job -Job $job -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  [$($job.Name)] $_" }
            }
        }
        $running = $Jobs | Where-Object { $_.State -eq "Running" }
        if ($running.Count -eq 0) {
            Write-Host "All services stopped."
            break
        }
        Start-Sleep -Seconds 5
    }
} finally {
    Write-Host ""
    Write-Host "Stopping all services..."
    $Jobs | Stop-Job -ErrorAction SilentlyContinue
    $Jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped."
}
