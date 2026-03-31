<#
.SYNOPSIS
    Builds and packages all Purfle sample agents into .purfle bundles.

.DESCRIPTION
    For each agent that has a C# project: compiles the assembly in Release mode,
    then calls Purfle.Packager to zip it into a .purfle bundle under dist/.
    Manifest-only agents (no assembly) are packed directly.

.EXAMPLE
    .\build-agents.ps1
    .\build-agents.ps1 -Clean
#>

param(
    [switch]$Clean   # wipe dist/ before building
)

$ErrorActionPreference = "Stop"
$RepoRoot  = $PSScriptRoot
$DistDir   = Join-Path $RepoRoot "dist"
$Packager  = Join-Path $RepoRoot "tools\Purfle.Packager\Purfle.Packager.csproj"
$AgentsDir = Join-Path $RepoRoot "agents"
$AgentsSrc = Join-Path $AgentsDir "src"

# ── Agents with assemblies ────────────────────────────────────────────────────
# manifest            project dir                       target dll name
$AssemblyAgents = @(
    @{
        Manifest = "chat.agent.json"
        Project  = "Purfle.Agents.Chat\Purfle.Agents.Chat.csproj"
        Dll      = "Purfle.Agents.Chat.dll"
    },
    @{
        Manifest = "file-search.agent.json"
        Project  = "Purfle.Agents.FileSearch\Purfle.Agents.FileSearch.csproj"
        Dll      = "Purfle.Agents.FileSearch.dll"
    },
    @{
        Manifest = "web-research.agent.json"
        Project  = "Purfle.Agents.WebResearch\Purfle.Agents.WebResearch.csproj"
        Dll      = "Purfle.Agents.WebResearch.dll"
    }
)

# ── Manifest-only agents (no assembly) ───────────────────────────────────────
$ManifestOnlyAgents = @(
    "file-summarizer.agent.json"
)

# ── Setup ─────────────────────────────────────────────────────────────────────
if ($Clean -and (Test-Path $DistDir)) {
    Write-Host "Cleaning dist/..." -ForegroundColor Yellow
    Remove-Item $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

function Get-AgentOutputName($manifestPath) {
    $manifest = Get-Content $manifestPath | ConvertFrom-Json
    $name     = $manifest.name -replace '\s+', '-' -replace '[^A-Za-z0-9\-]', ''
    $version  = $manifest.version
    return "$name-$version.purfle"
}

function Invoke-Packager($manifestPath, $outputPath, $assemblyPath = $null) {
    $packArgs = @(
        "run", "--project", $Packager, "--",
        "--manifest", $manifestPath,
        "--output",   $outputPath
    )
    if ($assemblyPath) {
        $packArgs += "--assembly", $assemblyPath
    }
    & dotnet @packArgs
    if ($LASTEXITCODE -ne 0) { throw "Packager failed for $manifestPath" }
}

# ── Build assembly agents ─────────────────────────────────────────────────────
foreach ($agent in $AssemblyAgents) {
    $manifestPath = Join-Path $AgentsDir $agent.Manifest
    $projectPath  = Join-Path $AgentsSrc $agent.Project
    $outputName   = Get-AgentOutputName $manifestPath
    $outputPath   = Join-Path $DistDir $outputName

    Write-Host ""
    Write-Host "==> $($agent.Manifest)" -ForegroundColor Cyan

    Write-Host "    Building assembly..." -ForegroundColor Gray
    & dotnet build $projectPath -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $projectPath" }

    # Locate the compiled DLL
    $projectDir = Split-Path $projectPath
    $dllPath = Join-Path $projectDir "bin\Release\net10.0\$($agent.Dll)"
    if (-not (Test-Path $dllPath)) {
        throw "Expected DLL not found: $dllPath"
    }

    Write-Host "    Packing -> dist\$outputName..." -ForegroundColor Gray
    Invoke-Packager $manifestPath $outputPath $dllPath

    Write-Host "    OK" -ForegroundColor Green
}

# ── Pack manifest-only agents ─────────────────────────────────────────────────
foreach ($manifestFile in $ManifestOnlyAgents) {
    $manifestPath = Join-Path $AgentsDir $manifestFile
    $outputName   = Get-AgentOutputName $manifestPath
    $outputPath   = Join-Path $DistDir $outputName

    Write-Host ""
    Write-Host "==> $manifestFile (manifest-only)" -ForegroundColor Cyan
    Write-Host "    Packing -> dist\$outputName..." -ForegroundColor Gray
    Invoke-Packager $manifestPath $outputPath

    Write-Host "    OK" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Bundles in dist/:" -ForegroundColor Green
Get-ChildItem $DistDir -Filter "*.purfle" | ForEach-Object { Write-Host "  $_" }
