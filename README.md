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

Important: the YAML loader parses the file before applying changes. If the YAML is malformed the loader will log a parse error and will not overwrite your live bindings — this prevents accidental loss of keymaps when editing.

## Keymaps (YAML)

Keymaps are a tree of bindings. Each node has a `key` and `label`, and then either:

- `action`: built-in action id
- `type`: text to type
- `send`: key chord to send (e.g. `Ctrl+Shift+T`)
- `exec` (+ optional `execArgs`, `execCwd`): launch a program
- `steps`: chain multiple `action`/`type`/`send`/`exec` steps
- `children`: nested bindings (multi-stroke sequences)

- `apps:`: program-specific bindings keyed by process name (applied when the foreground process matches)
- `groups:`: named groups of processes that share the same bindings (useful for browsers, terminals, etc.)

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

### Named key tokens

Multi-letter named keys (for example `Win`, `Enter`, `Left`, or `F5`) can be represented as single logical key steps rather than sequences of characters.

Two ways to declare a named token in a keymap:

- `keyTokens`: an explicit list of token names for a binding (unambiguous).
- Angle-bracket inline tokens inside `key:`: use `<Token>` to embed a named token inside a sequence (for example `p<Win>g`).

Examples:

```yaml
bindings:
  - keyTokens: ["Win"]
    label: Win (single token)
    action: openGlyphGui

  - key: "p<Win>g"
    label: P + Win + g
    action: logForeground

  - key: Win
    label: bare Win convenience (single token)
    action: openLogs
```

Display behavior:

- Named tokens render as single keycaps in the overlay and are shown inline in the sequence text using angle brackets (for example: `<Win>`).
- For a practical test map, see: [src/Glyph.App/Config/example_keymaps_tokens.yaml](src/Glyph.App/Config/example_keymaps_tokens.yaml)

Program-prefix (`p`) and overlay behaviour:

- `p` is a reserved top-level prefix used for program-specific bindings. Program bindings should live under `apps:` (or `groups:`) and are applied when the foreground process matches the `process` name.
- When you open the `p` prefix in the overlay, the UI shows one of three messages depending on context:
  - `No Program Focused` — nothing has focus (desktop/overlay/other)
  - `<ProcessName> Not Configured` — a program is focused but there are no `apps:`/`groups:` bindings for it
  - the configured label (from `apps:`/`groups:`) — when the focused process has program-specific bindings

Example `apps:` and `groups:` usage:

```yaml
bindings:
  - key: p
    label: Program

apps:
  - process: Spotify
    bindings:
      - key: p
        label: Play / Pause
        action: mediaPlayPause

groups:
  - name: Browser
    processes: [ chrome, msedge, firefox ]
    bindings:
      - key: t
        label: New Tab
        send: Ctrl+T
```

## Built-in actions

The built-in action ids live in [src/Glyph.Actions/ActionRuntime.cs](src/Glyph.Actions/ActionRuntime.cs).
Tip: there is a helper action `logForeground` you can bind (for example to a key in your `bindings:`) to log the currently focused process name. Use it to discover the exact `process` string to put in `apps:`.

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