<p align="center">
  <img src="assets\LogoTextWhite.svg" alt="Glyph" />
</p>

---
<p align="center">
  A modern and highly configurable leader-key driven command overlay for Windows.
</p>


Glyph is a small productivity layer for Windows: press the <strong>Glyph key</strong> to open an overlay, then type short sequences to trigger actions.

It’s designed to be:

- Discoverable: the overlay shows available next keys.
- Fast: short, mnemonic sequences (leader-key style).
- Context-aware: global bindings + per-application layers.
- Customizable: edit keymaps in the GUI (YAML optional for power users) and switch JSON themes live.

## Features

- **Leader-key overlay** with label breadcrumbs (e.g., <code>Glyph &gt; Text &gt; Copy</code>).
- **Key sequences** with nested layers (tree of bindings).
- **Per-app bindings** via `apps:` and shared app groups via `groups:`.
- **Actions**:
  - `action`: built-in action id
  - `send`: send a key chord (e.g. `Ctrl+Shift+T`)
  - `type`: type text
  - `exec`: launch a program (`execArgs`, `execCwd` supported)
  - `steps`: chain multiple actions together
- **Named key tokens** as single steps (Tab, Enter, Esc, Space, arrows, function keys, Win, Ctrl/Shift/Alt, left/right variants).
- **Themes**: JSON-based themes in `%APPDATA%\Glyph\themes\` with live selection from the Settings UI.
- **Self-contained releases** for Windows (no separate .NET install required).

## Install

### Winget (recommended)

```powershell
winget install HasNate618.Glyph
```

Then run Glyph from the terminal using `glyph` if you installed via winget (requires admin).
Otherwise run `Glyph.App.exe` from the terminal or launch it from the Start menu.

### Manual install

- Download a release zip (self-contained) from [GitHub Releases](https://github.com/HasNate618/Glyph/releases).
- Unzip anywhere.
- Run `Glyph.App.exe`.

## GUI & Tray

- Glyph runs with a tray icon. Double-click opens the GUI; right-click shows actions (Open GUI, open config/log folders, reload theme, About, Exit).
- The Settings GUI lets you change the theme, redefine the glyph key (default: F12), and open the Keymap Editor to manage bindings visually. Settings are stored in `%APPDATA%\Glyph\config.json`.

## Config files

- Settings: `%APPDATA%\Glyph\config.json`
- Keymaps (managed by the Keymap Editor): `%APPDATA%\Glyph\keymaps.yaml`
- Themes directory: `%APPDATA%\Glyph\themes\` (`*.json`)
- Theme selection: stored in `%APPDATA%\Glyph\config.json` (field `BaseTheme`)

On first run, a default `%APPDATA%\Glyph\keymaps.yaml` is created. While developing from source, the template comes from `src/Glyph.App/Config/default_keymaps.yaml`.

### Themes

- Built-in themes are embedded in the app and extracted to `%APPDATA%\Glyph\themes\` on first run.
- To add your own theme: drop a `*.json` file into `%APPDATA%\Glyph\themes\` and select it in the Settings GUI (selection is saved to `%APPDATA%\Glyph\config.json`).
- For quick switching from keymaps, you can bind the action id `setTheme:<ThemeId>`.

## Keymaps

Open **Settings → Keymaps → Open Keymap Editor** to create and edit bindings visually. The editor writes to `%APPDATA%\Glyph\keymaps.yaml` for you, so you can focus on the structure instead of the file format.

In the editor you can:

- Add new key sequences and nested layers.
- Assign actions like `action`, `type`, `send`, `exec`, or multi-step `steps`.
- Define per-application bindings and shared groups (for browsers, terminals, etc.).
- Use named key tokens (Win, Enter, arrows, function keys) as single steps.
- Reload keymaps at runtime (default binding: glyph → `,` → `r`).

### Notes

- Named key tokens (Win, Enter, arrows, function keys) appear as single keycaps in the overlay.
- Per-app bindings apply when a matching process is focused; shared groups let you reuse bindings across related apps.
- For a practical test map, see: [src/Glyph.App/Config/example_keymaps_tokens.yaml](src/Glyph.App/Config/example_keymaps_tokens.yaml)

## Contributing

- Start here: [docs/README.md](docs/README.md)
- Contribution guidelines: [CONTRIBUTING.md](CONTRIBUTING.md)
- Original vision/notes: [plan.md](plan.md)

## Releases

- CI builds self-contained zips for Windows x64 and Windows arm64.
- Local publish helper: `scripts/publish.ps1`.
- Release notes/instructions: [docs/release.md](docs/release.md)

## Quick start (from source)

```powershell
dotnet build Glyph.sln -c Debug
dotnet run --project src/Glyph.App/Glyph.App.csproj -c Debug
```

## License

MIT. See `LICENSE`.