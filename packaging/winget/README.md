# Winget (portable ZIP)

This folder contains **templates** for publishing Glyph via **Windows Package Manager (winget)** using a **portable ZIP** installer.

Winget manifests are submitted to the community repository:
- https://github.com/microsoft/winget-pkgs

## Why portable ZIP?

- Minimal packaging work (reuses existing self-contained release ZIPs)
- Clean upgrade path via winget
- No MSIX/signing required to get started

Downside: this does **not** provide the same Windows integration as MSIX (App identity, automatic Start Menu tiles, etc.).

## What you publish

For each release tag (example `v0.1.0`), CI produces:
- `Glyph-win-x64-v0.1.0.zip`
- `Glyph-win-arm64-v0.1.0.zip`
- `SHA256SUMS.txt`

The ZIP contains `Glyph.App.exe` at the root.

## Creating a New Version

For each release, create a version folder with updated manifests and SHA256 hashes.

### Quick Start: Using the Helper Script

After publishing release artifacts:

```powershell
# Create a new version folder
mkdir "manifests/h/HasNate618/Glyph/X.Y.Z"

# Copy manifests from previous version and run the update script
.\update-manifest.ps1 -VersionFolder "manifests/h/HasNate618/Glyph/X.Y.Z" -Tag "vX.Y.Z"
```

The script will:
1. Locate artifacts matching the tag (e.g., `Glyph-win-x64-vX.Y.Z.zip`)
2. Compute SHA256 hashes
3. Update the installer manifest automatically

### Manual Process

1. Create a new version folder:
   ```powershell
   mkdir "manifests/h/HasNate618/Glyph/X.Y.Z"
   Copy-Item "manifests/h/HasNate618/Glyph/0.1.0-beta.3/*" "manifests/h/HasNate618/Glyph/X.Y.Z/"
   ```

2. Update all three YAML files with:
   - `PackageVersion: X.Y.Z`
   - `InstallerUrl` with correct release tag
   - `InstallerSha256` (computed from artifacts)

3. Compute SHA256 hashes:
   ```powershell
   Get-FileHash "Glyph-win-x64-vX.Y.Z.zip" -Algorithm SHA256
   Get-FileHash "Glyph-win-arm64-vX.Y.Z.zip" -Algorithm SHA256
   ```

### Using wingetcreate (Alternative)

Microsoft's tool can auto-detect hashes:

```powershell
wingetcreate update HasNate618.Glyph `
  --urls "https://github.com/HasNate618/Glyph/releases/download/vX.Y.Z/Glyph-win-x64-vX.Y.Z.zip" `
  "https://github.com/HasNate618/Glyph/releases/download/vX.Y.Z/Glyph-win-arm64-vX.Y.Z.zip" `
  --version "X.Y.Z"
```

## Submitting to microsoft/winget-pkgs

### First Submission (Your First Release)

1. Create a GitHub account and fork https://github.com/microsoft/winget-pkgs
2. Clone your fork
3. Copy the `manifests/h/HasNate618/` folder to your fork's `manifests/` directory
4. Create a branch and push:
   ```powershell
   git checkout -b glyph-0-1-0-beta-3
   git add manifests/h/HasNate618/
   git commit -m "Add HasNate618.Glyph 0.1.0-beta.3"
   git push origin glyph-0-1-0-beta-3
   ```
5. Create a PR on https://github.com/microsoft/winget-pkgs

The winget team will review and merge within a few days. Typical feedback focuses on metadata accuracy and file integrity.

### Subsequent Releases (Package Already Listed)

Just submit a PR with the new version folder and updated YAML files:

```powershell
mkdir "manifests/h/HasNate618/Glyph/X.Y.Z"
# ... create/update manifests ...
git add manifests/h/HasNate618/Glyph/X.Y.Z/
git commit -m "New version: HasNate618.Glyph X.Y.Z"
git push
# Create PR
```

## File Structure

Each version folder contains three YAML manifests:

```
manifests/h/HasNate618/Glyph/
├── 0.1.0-beta.3/
│   ├── HasNate618.Glyph.yaml                 (version & defaults)
│   ├── HasNate618.Glyph.locale.en-US.yaml    (metadata & icon)
│   └── HasNate618.Glyph.installer.yaml       (installer URL + SHA256)
├── 0.1.0/
│   ├── ...
└── X.Y.Z/
    └── ...
```

Winget parses all three files; keep them in sync per version.

## Local testing

You can test a manifest locally before opening a PR:

```powershell
winget validate --manifest .\packaging\winget\manifests\h\HasNate618\Glyph\0.1.0-beta.3
winget install --manifest .\packaging\winget\manifests\h\HasNate618\Glyph\0.1.0-beta.3
```

Note: local manifest install still downloads from `InstallerUrl`, so the URLs must exist and be publicly accessible (no auth).

## Installing

After the manifest is merged into winget community, users can install using:

```powershell
winget install HasNate618.Glyph
```

Users can also try `winget install glyph` (winget treats this as a search query), but it may prompt to disambiguate if multiple packages match.

## Notes

- Winget community generally prefers **stable** versions. Pre-releases like `0.1.0-beta.3` may be rejected.
- The portable installer declares a command alias `glyph` pointing to `Glyph.App.exe`.

## Troubleshooting

### Manual validation: missing `Glyph.App.dll`

If winget PR manual validation shows an error like:

`The application to execute does not exist: ...\Glyph.App.dll`

this is typically because the app is not published as a true standalone executable. With portable installs, winget may execute the alias from the WinGet Links directory, and a .NET app host can fail if it expects the adjacent `Glyph.App.dll`.

Fix (recommended): publish as a single-file self-contained executable, then cut a new versioned release (e.g., `v0.1.1`) and update the winget manifest hashes for that version.
