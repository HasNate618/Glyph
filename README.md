<p align="center">
  <img src="assets/LogoText.svg" alt="Glyph" />
</p>

---
<p align="center">
  A Windows-first, highly configurable leader-key driven command overlay
</p>


Glyph is a small productivity layer for Windows: press the <strong>glyph key</strong> to open an overlay, then type short sequences to trigger actions.

- Discoverable: the overlay shows available next keys.
- Context-aware: supports global bindings and per-application layers.
- Customizable: bindings are defined and easily configured in YAML.

## Quick start (from source)

```powershell
dotnet build Glyph.sln -c Debug
dotnet run --project src/Glyph.App/Glyph.App.csproj -c Debug
```

## GUI & Tray

- Glyph runs with a tray icon. Double-click opens the GUI; right-click shows actions (Open GUI, open config/log folders, reload theme, About, Exit).
- The Settings GUI lets you change the theme and redefine the glyph key (default: F12). Settings are stored in `%APPDATA%\Glyph\config.json`.

## Config files

- Settings: `%APPDATA%\Glyph\config.json`
- Keymaps (YAML): `%APPDATA%\Glyph\keymaps.yaml`
- Themes: `%APPDATA%\Glyph\theme.base` (base), `%APPDATA%\Glyph\theme.xaml` (overrides)

On first run, a default `%APPDATA%\Glyph\keymaps.yaml` is created. While developing from source, the template comes from `src/Glyph.App/Config/default_keymaps.yaml`.

## Keymaps (YAML)

Keymaps are a tree of bindings. Each node has a `key` and `label`, and then either:

- `action`: built-in action id
- `type`: text to type
- `send`: key chord to send (e.g. `Ctrl+Shift+T`)
- `exec` (+ optional `execArgs`, `execCwd`): launch a program
- `steps`: chain multiple `action`/`type`/`send`/`exec` steps
- `children`: nested bindings (multi-stroke sequences)

Reloading keymaps at runtime: the default keymap binds `reloadKeymaps` to glyph → `,` → `r`.

### Examples

Chain steps (preferred):

```yaml
bindings:
  - key: ",g"
    label: "Git commit"
    steps:
      - type: git commit ""
      - send: Left
```

Vim-like `dd` (delete line):

```yaml
bindings:
  - key: t
    label: Text
    children:
      - key: dd
        label: Delete Line
        steps:
          - send: Home
          - send: Shift+End
          - send: Delete
```

Per-app binding:

```yaml
apps:
  - process: code
    bindings:
      - key: f
        label: Format file
        action: formatDocument
```

## Built-in actions

The built-in action ids live in [src/Glyph.Actions/ActionRuntime.cs](src/Glyph.Actions/ActionRuntime.cs).

## Troubleshooting

- If `send`/`type` land in the wrong window: ensure the overlay has fully hidden and the target window is focused.
- If an `exec` doesn’t launch: try the full GUI `.exe` path (not a CLI shim).
- If an `action` does nothing: it may be unknown; check the built-in list in [src/Glyph.Actions/ActionRuntime.cs](src/Glyph.Actions/ActionRuntime.cs).

## Contributing

- Start here: [docs/README.md](docs/README.md)
- Contribution guidelines: [CONTRIBUTING.md](CONTRIBUTING.md)
- Original vision/notes: [plan.md](plan.md)

## License

MIT. See `LICENSE`.