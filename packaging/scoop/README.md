# Scoop packaging notes (Glyph)

This document explains how to use, test, and publish Scoop manifests/buckets for Glyph.

Quick testing
- Install directly from a local manifest:

```powershell
scoop install C:\path\to\Glyph.json
```

- If you maintain a personal bucket (git repo):

```powershell
scoop bucket add mybucket https://github.com/you/scoop-bucket
scoop install Glyph
```

Manifest essentials
- `version` — package version.
- `architecture` — `64bit` / `arm64` entries with `url` and `hash` (sha256). Use explicit per-arch URLs.
- `bin` — list of executables to create shims for. Prefer using `bin` to rely on Scoop's shim generator.
- `persist` — folders to preserve across upgrades (e.g., `appdata`, `config`).
- `post_install` — PowerShell commands that run after extraction (use sparingly; prefer `bin`).
- `checkver` / `autoupdate` — enable automated update detection.

Example notes (Glyph)
- See `packaging/scoop/Glyph.json` for a working example. It uses a portable zip and sets `Glyph.App.exe` in `bin`.
- If you want a short alias `glyph`, either:
  - rely on the `bin` shim name if it is acceptable, or
  - create a tiny `post_install` script that writes `~\scoop\shims\glyph.cmd`/`glyph.ps1` pointing to the main exe.

Publishing workflow
1. Create a git repo with your manifests (root layout: `glyph/Glyph.json` or top-level manifest files). Add a `README.md`.
2. Push to GitHub (public). Users can `scoop bucket add <repo>`.
3. To add to an established bucket (e.g., `scoop-extras`), fork the bucket, add your manifest, and open a PR against the bucket. Follow their PR template and CI guidance.

PR checklist
- Validate JSON or YAML syntax.
- Confirm SHA256 matches the downloaded asset.
- Include `homepage`, `license`, and `description` fields.
- Add `checkver`/`autoupdate` if you want automated bumps.

Useful commands

```powershell
scoop bucket list
scoop bucket add <name> <git-url>
scoop install <package>
scoop uninstall <package>
scoop update
```

If you want, I can:
- Draft a PR-ready `Glyph.json` with `persist` and `checkver` stubs, or
- Create a minimal GitHub bucket repo (push branch) so you can open a PR to `scoop-extras`.
