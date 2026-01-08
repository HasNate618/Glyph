# Glyph

Glyph is a keyboard-first, leader-key driven command system for Windows. It provides a layered, discoverable, and highly-customizable command language that runs on top of the OS and let users define sequences that map to actions, macros, and workflows.

**Quick summary**
- Leader-key activation opens a discoverable overlay showing the current sequence and available next keys.
- Supports global and per-application layers, groupings, and hierarchical bindings.
- Actions include typing text, sending key chords, launching programs, and running predefined action IDs.

Why Glyph exists
- Replaces flat, inconsistent shortcuts with a scalable, learnable command language.
- Makes macros discoverable and safe to explore via an overlay.
- Lets users map intentful sequences to workflows across apps.

Getting started — config & defaults

- Config file location (Windows): `%APPDATA%\\Glyph\\keymaps.yaml`
- Repository default (dev): `src/Glyph.App/Config/default_keymaps.yaml` — when present and running from source, the loader will copy this into `%APPDATA%\\Glyph\\keymaps.yaml` on first run so maintainers can edit defaults easily. If the repo default is absent, the loader logs and does not create a default.

Key YAML features
- `bindings` — global key bindings
- `apps` / `groups` — per-application and grouped bindings
- `type:` — text to type
- `send:` — send a chord or key sequence (send-spec syntax)
- `steps:` — preferred ordered list of steps to execute sequentially. Each step may contain `action`, `type`, `send`, or `exec`.
- `then:` — deprecated shorthand for simple two-step chains; use `steps:` for clarity and arbitrary chains.

Behavior notes
- Typing no longer auto-sends Enter. If you want Enter after typed text, include an explicit step such as `- send: Enter` in a `steps:` array.
- `steps:` is the recommended way to chain actions; `then:` remains supported as a legacy two-step helper but may be removed in the future.
- The loader resets built-in bindings before applying the YAML; removing entries from your YAML will remove them from runtime discovery after reload.
- Runtime reload: an action `reloadKeymaps` is provided and bound in the glyph layer to `,r` (leader + `,` then `r`). Trigger it to reapply `%APPDATA%\\Glyph\\keymaps.yaml` without restarting the app.

Examples

Global chaining example (preferred `steps:` form):

```yaml
bindings:
	- key: ",g"
		label: "Git commit"
		steps:
			- type: git commit ""
			- send: Left
```

Per-app example (placed under `apps`):

```yaml
apps:
	- process: code
		bindings:
			- key: "f"
				label: "Format file"
				action: formatDocument
```

Runtime tips
- Edit `%APPDATA%\\Glyph\\keymaps.yaml` and then press leader → `,` → `r` to pick up changes. Deletions in the YAML will be removed from discovery on reload.
- To use or update the repo default while developing, keep `src/Glyph.App/Config/default_keymaps.yaml` in the repository.

Files of interest
- Loader: [src/Glyph.App/Config/KeymapYamlLoader.cs](src/Glyph.App/Config/KeymapYamlLoader.cs)
- Plan & motivation: [plan.md](plan.md)


Glyph is a Windows leader-key, discoverable multi-stroke keymap overlay.

This repo contains the app, a YAML-configurable keymap loader, and a small set of built-in actions. The short guide below covers configuration and examples.

## What is Glyph — and why use it?

Glyph is a tiny, focused productivity layer for power users who want to work faster without leaving the keyboard.

- What: Glyph is a discoverable, leader-key driven overlay that lets you trigger actions, launch apps, send complex key chords, and type snippets using short, memorable sequences.
- Why: It reduces context switching and menu hunting — trigger commonly used workflows (open project terminals, control media, navigate the browser, trigger IDE shortcuts) with a two‑keystroke flow.

Benefits at a glance:

- Speed: perform repetitive tasks with minimal keystrokes.
- Discoverability: overlay shows possible next keys so sequences are learnable and self-documenting.
- Customizable: YAML keymaps let you tailor bindings per-app or share groups across browsers and editors.
- Safe automation: `exec`/`send`/`type` provide flexible actions while staying local and auditable in YAML.

Who it's for:

- Developers who want fast editor/terminal navigation and project commands.
- Power users who prefer keyboard-driven workflows.
- Anyone who wants a lightweight, extensible hotkey layer without heavyweight global macro tools.

Key use-cases:

- Quick launcher: fuzzy-launch projects, files, or applications.
- App-specific helpers: per-app layers (e.g., VS Code shortcuts under `p`) so the same leader can do different things per app.
- Clipboard & snippets: paste common text templates, addresses, or code snippets with a leader binding.
- Window/layout control: tile, move, and restore window layouts across monitors.

## Quick start
- Build: `dotnet build Glyph.sln`
- Run: `dotnet run --project src/Glyph.App/Glyph.App.csproj` (or use `run.bat` for a detached run)

## Config locations
- Settings: `%APPDATA%\Glyph\config.json`
- Keymaps (YAML): `%APPDATA%\Glyph\keymaps.yaml` (created automatically on first run)
- Themes: base theme in `%APPDATA%\Glyph\theme.base`, overrides in `%APPDATA%\Glyph\theme.xaml`

## Keymaps (YAML) — overview
Glyph reads a simple YAML tree of `bindings`. Each node maps a short `key` to an action, typed text, a chord to send, or an executable to launch. Bindings can be global (`bindings`), per-application (`apps`), or applied to groups of processes (`groups`).

Top-level schema:

- `bindings`: list of global binding nodes
- `apps`: list of `{ process, bindings }` for per-process bindings (applied when that process is foreground)
- `groups`: list of named `{ name, processes, bindings }` to share the same bindings across multiple process names

Node fields (for each binding):

- `key` (required): short string (single or multi-char, e.g., `v` or `rs`)
- `label` (required): user-visible label shown in the overlay
- `action` (optional): a built-in action id
- `type` (optional): literal text to type (Enter is sent after typing)
- `send` (optional): chord spec to send, e.g. `Ctrl+T` or `Ctrl+Shift+T`
- `exec` (optional): executable/command to run (path or program name)
- `execArgs` (optional): arguments to pass to the `exec`
- `execCwd` (optional): working directory for `exec`
- `children` (optional): list of child nodes for multi-stroke sequences

Notes:

- Loader precedence: `action` → `type` → `send` → `exec` (the loader uses the first populated field).
- Keys must not contain whitespace.
- For `exec`, prefer the GUI exe (e.g., `Code.exe`) instead of a CLI shim (e.g., `code`) to avoid console flashes.

## Built-in action ids

Use `action: <id>` with one of these known ids:

- App & launch: `launchChrome`, `openTerminal`, `openExplorer`, `openTaskManager`, `openBrowser`
- Media: `mediaPlayPause`, `mediaNext`, `mediaPrev`, `volumeMute`, `openSpotify`, `muteMic`, `mediaShuffle`
- Window: `windowMinimize`, `windowMaximize`, `windowRestore`, `windowClose`, `windowTopmost`
- Misc: `typeNvimDot`, `quitGlyph`

## Examples

Global binding (uses built-in action):

```yaml
bindings:
	- key: o
		label: Open
		children:
			- key: t
				label: Terminal
				action: openTerminal
```

Type text and press Enter:

```yaml
	- key: g
		label: Git status
		type: git status
```

Browser group (send chord):

```yaml
groups:
	- name: Browser
		processes: [ chrome, msedge, firefox ]
		bindings:
			- key: t
				label: New Tab
				send: Ctrl+T
```

Launch an application (exec):

```yaml
bindings:
	- key: v
		label: VSCode
		exec: "C:\\Program Files\\Microsoft VS Code\\Code.exe"

	- key: rs
		label: Steam
		exec: 'C:\\Program Files (x86)\\Steam\\steam.exe'
```

The loader supports `execArgs` and `execCwd` for more complex launches. Glyph launches using `UseShellExecute = true` and hides the launcher window to avoid short console flashes.

## Troubleshooting

- Unknown `action` ids are ignored (the prefix label remains). Check `src/Glyph.Actions/ActionRuntime.cs` for known ids.
- For `exec` failures, try the full GUI exe path and check permissions.
- If `type`/`send` don't appear in the target app, ensure the overlay has hidden and the target window is focused.

## Where defaults live

- Default keymap template: `src/Glyph.App/Config/KeymapYamlLoader.cs`


## Selling points / Elevator pitch

Glyph gives teams and individuals an instant productivity boost: a tiny, discoverable keyboard layer that replaces mouse-driven menus with short, memorable sequences. It's simple to configure (YAML), respects application context, and is designed for low-latency, reliable global input on Windows.

Install it, add a few app-specific bindings, and you’ll find repeated tasks drop from 6+ keystrokes and mouse moves to 1–2 quick taps.

## Contributing / Roadmap

Contributions and feature ideas are welcome. See `docs/possibilities.md` for a long list of ideas and potential next steps (command palette, clipboard history, snippets, macros, plugin API).

If you want to add a feature or help polish UX, open an issue or create a PR against this repo.

---

Updated README: `README.md`