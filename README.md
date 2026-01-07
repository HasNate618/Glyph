# Glyph

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