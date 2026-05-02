<#
.SYNOPSIS
    Single-file Release publish for RAM.UI — produces a distributable .exe + .zip.

.DESCRIPTION
    Runs `dotnet publish` with the flags required for a self-contained single-file
    Windows build, then copies the output into ./release/ with versioned filenames
    and creates a matching .zip archive.

    Flag notes:
      * IncludeNativeLibrariesForSelfExtract=true — REQUIRED because Sodium.Core
        ships a native libsodium.dll. Without this, libsodium ends up next to the
        .exe instead of inside the self-extracting bundle, defeating single-file.
      * EnableCompressionInSingleFile=true — Brotli-compresses embedded assemblies.
        Significantly smaller download; slightly slower first-run extraction.

.PARAMETER Version
    SemVer string burned into the assembly and used in output filenames. Pass
    without the leading 'v' (e.g. 1.0.0, not v1.0.0). Defaults to '0.0.0-dev'
    for local testing; CI passes the real version from the git tag.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Runtime
    Target RID. Defaults to win-x64.

.EXAMPLE
    pwsh scripts/publish.ps1 -Version 1.0.0
    Produces release/RAM-Fork-v1.0.0-x64.exe and release/RAM-Fork-v1.0.0-x64.zip.

.EXAMPLE
    pwsh scripts/publish.ps1
    Local dev build → release/RAM-Fork-v0.0.0-dev-x64.{exe,zip}.
#>
[CmdletBinding()]
param(
    [string]$Version       = '0.0.0-dev',
    [string]$Configuration = 'Release',
    [string]$Runtime       = 'win-x64'
)

$ErrorActionPreference = 'Stop'

# Strip any leading 'v' a caller may have included (release.yml passes the bare
# version, but a human invoking the script may type 'v1.0.0').
$Version = $Version -replace '^[vV]', ''

# Resolve repo root from script location so this works regardless of CWD.
$repoRoot      = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectCsproj = Join-Path $repoRoot 'src/RAM.UI/RAM.UI.csproj'

if (-not (Test-Path $projectCsproj)) {
    throw "Could not find RAM.UI.csproj at $projectCsproj"
}

$publishDir = Join-Path $repoRoot "publish/$Runtime"
$releaseDir = Join-Path $repoRoot 'release'
$basename   = "RAM-Fork-v$Version-x64"
$exePath    = Join-Path $releaseDir "$basename.exe"
$zipPath    = Join-Path $releaseDir "$basename.zip"

Write-Host ''
Write-Host "Repo root  : $repoRoot"
Write-Host "Project    : $projectCsproj"
Write-Host "Version    : $Version"
Write-Host "Config     : $Configuration"
Write-Host "Runtime    : $Runtime"
Write-Host "Publish dir: $publishDir"
Write-Host "Release dir: $releaseDir"
Write-Host ''

# Clean previous publish output for this RID so we don't ship stale files.
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }

# Remove any prior copy of this version's outputs.
if (Test-Path $exePath) { Remove-Item -Force $exePath }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

# ----- dotnet publish -------------------------------------------------------
$dotnetArgs = @(
    'publish', $projectCsproj,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    "-p:Version=$Version",
    '-p:DebugType=embedded',
    '-o', $publishDir
)

Write-Host 'Running: dotnet ' ($dotnetArgs -join ' ')
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# ----- copy → release/<basename>.exe ---------------------------------------
# RAM.UI.csproj overrides AssemblyName to 'RAM', so the single-file output is
# RAM.exe (not RAM.UI.exe).
$publishedExe = Join-Path $publishDir 'RAM.exe'
if (-not (Test-Path $publishedExe)) {
    # Fallback: pick whatever single .exe landed in the publish dir.
    $candidate = Get-ChildItem -LiteralPath $publishDir -Filter '*.exe' | Select-Object -First 1
    if ($null -eq $candidate) {
        throw "No .exe found in $publishDir after publish"
    }
    $publishedExe = $candidate.FullName
}
Copy-Item -LiteralPath $publishedExe -Destination $exePath

# ----- create release/<basename>.zip ---------------------------------------
# Compress the .exe alone (single-file already bundles everything else).
Compress-Archive -LiteralPath $exePath -DestinationPath $zipPath -CompressionLevel Optimal -Force

# ----- size report ---------------------------------------------------------
$exeSizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)

Write-Host ''
Write-Host '=== Release artifacts ==='
[PSCustomObject]@{ File = (Split-Path $exePath -Leaf); 'Size (MB)' = $exeSizeMB } | Format-Table -AutoSize
[PSCustomObject]@{ File = (Split-Path $zipPath -Leaf); 'Size (MB)' = $zipSizeMB } | Format-Table -AutoSize

# SHA256 — useful for the GitHub release notes / users verifying downloads.
$exeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $exePath).Hash
Write-Host "SHA256 ($basename.exe):"
Write-Host "  $exeHash"
Write-Host ''
Write-Host "Release artifacts ready in: $releaseDir"
