[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'

$version = $Version
$repoUrl = 'https://github.com/HasNate618/Glyph'
$releaseTag = "v$version"

Write-Host "Downloading Release assets from: $repoUrl/releases/tag/$releaseTag" -ForegroundColor Cyan
Write-Host ''

# Create temp directory for downloads
$tempDir = Join-Path $env:TEMP 'glyph-winget-fix'
if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    # Download x64 ZIP
    Write-Host 'Downloading x64 ZIP...'
    $x64Url = "$repoUrl/releases/download/$releaseTag/Glyph-win-x64-$releaseTag.zip"
    $x64Path = Join-Path $tempDir "Glyph-win-x64-$releaseTag.zip"
    Invoke-WebRequest -Uri $x64Url -OutFile $x64Path -ErrorAction Stop

    # Download arm64 ZIP
    Write-Host 'Downloading arm64 ZIP...'
    $arm64Url = "$repoUrl/releases/download/$releaseTag/Glyph-win-arm64-$releaseTag.zip"
    $arm64Path = Join-Path $tempDir "Glyph-win-arm64-$releaseTag.zip"
    Invoke-WebRequest -Uri $arm64Url -OutFile $arm64Path -ErrorAction Stop
}
catch {
    Write-Host "Error downloading release assets: $_" -ForegroundColor Red
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    exit 1
}

Write-Host ''
Write-Host 'Computing SHA256 hashes...' -ForegroundColor Cyan

# Compute hashes
$x64Hash = (Get-FileHash -Path $x64Path -Algorithm SHA256).Hash.ToUpperInvariant()
$arm64Hash = (Get-FileHash -Path $arm64Path -Algorithm SHA256).Hash.ToUpperInvariant()

Write-Host ''
Write-Host "x64:   $x64Hash"
Write-Host "arm64: $arm64Hash"
Write-Host ''

# Update manifest files
$manifestDir = "packaging\winget\manifests\h\HasNate618\Glyph\$version"
$installerManifest = Join-Path $manifestDir 'HasNate618.Glyph.installer.yaml'

Write-Host "Updating: $installerManifest" -ForegroundColor Cyan

if (!(Test-Path $installerManifest)) {
    Write-Host "Manifest not found: $installerManifest" -ForegroundColor Red
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    exit 1
}

$content = Get-Content $installerManifest -Raw

# Replace InstallerSha256 values for x64 and arm64
$content = $content -replace '(?m)(Architecture:\s*x64\s*\n\s*InstallerUrl:.*\n\s*InstallerSha256:\s*)([A-Fa-f0-9]+)', "${1}$x64Hash"
$content = $content -replace '(?m)(Architecture:\s*arm64\s*\n\s*InstallerUrl:.*\n\s*InstallerSha256:\s*)([A-Fa-f0-9]+)', "${1}$arm64Hash"

Set-Content -Path $installerManifest -Value $content -Encoding utf8 -NoNewline

Write-Host '✓ Manifest updated' -ForegroundColor Green
Write-Host ''

Write-Host 'Validating manifest...' -ForegroundColor Cyan
winget validate --manifest $manifestDir
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host '✗ Validation failed!' -ForegroundColor Red
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    exit 1
}

Write-Host ''
Write-Host '✓ Validation passed!' -ForegroundColor Green
Write-Host ''
Write-Host 'Next steps:' -ForegroundColor Cyan
Write-Host '1. Commit changes:'
Write-Host "   git add packaging/winget/manifests/h/HasNate618/Glyph/$version/"
Write-Host "   git commit -m 'Fix: correct SHA256 hashes for v$version installers'"
Write-Host ''
Write-Host '2. Push to branch:'
Write-Host '   git push origin add-glyph-0-1-0'
Write-Host ''
Write-Host '3. The PR will auto-update with the new commit'

# Cleanup
if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }

