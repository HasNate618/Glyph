[CmdletBinding()]
param(
  [string]$VersionFolder,
  [string]$ArtifactDir = 'artifacts',
  [string]$Tag
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($VersionFolder)) {
  Write-Error "VersionFolder parameter required (e.g. 'packaging/winget/manifests/h/HasNate618/Glyph/0.1.0-beta.3')"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
  Write-Error "Tag parameter required (e.g. 'v0.1.0-beta.3')"
}

# Normalize tag (remove v prefix for comparison)
$tagClean = $Tag -replace '^v', ''

# Find zips matching the tag in artifacts folder
$x64Zip = Get-ChildItem -Path $ArtifactDir -Filter "*win-x64*$tagClean*.zip" -ErrorAction SilentlyContinue | Select-Object -First 1
$arm64Zip = Get-ChildItem -Path $ArtifactDir -Filter "*win-arm64*$tagClean*.zip" -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $x64Zip) {
  Write-Error "No x64 zip found matching pattern *win-x64*$tagClean*.zip in $ArtifactDir"
}

if ($null -eq $arm64Zip) {
  Write-Error "No arm64 zip found matching pattern *win-arm64*$tagClean*.zip in $ArtifactDir"
}

Write-Host "Found zips:"
Write-Host "  x64: $($x64Zip.Name)"
Write-Host "  arm64: $($arm64Zip.Name)"

# Compute SHA256
$x64Hash = (Get-FileHash -Path $x64Zip.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
$arm64Hash = (Get-FileHash -Path $arm64Zip.FullName -Algorithm SHA256).Hash.ToUpperInvariant()

Write-Host ""
Write-Host "SHA256 hashes:"
Write-Host "  x64: $x64Hash"
Write-Host "  arm64: $arm64Hash"
Write-Host ""

# Update installer.yaml
# The installer file should be named HasNate618.Glyph.installer.yaml
$installerPath = Join-Path $VersionFolder 'HasNate618.Glyph.installer.yaml'
if (!(Test-Path $installerPath)) {
  Write-Error "Installer manifest not found at $installerPath"
}

$content = Get-Content $installerPath -Raw
$content = $content -replace '<SHA256_X64>', $x64Hash
$content = $content -replace '<SHA256_ARM64>', $arm64Hash

Set-Content -Path $installerPath -Value $content -Encoding utf8 -NoNewline

Write-Host "Updated: $installerPath"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Review the manifests in $VersionFolder"
Write-Host "2. Commit and push to GitHub"
Write-Host "3. Create a PR to https://github.com/microsoft/winget-pkgs"
Write-Host "   (or use wingetcreate if first submission)"
