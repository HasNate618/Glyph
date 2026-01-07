# Glyph

Glyph is a Windows leader-key, discoverable multi-stroke keymap overlay.

This repo contains the app, a YAML-configurable keymap loader, and a small set of built-in actions. The short guide below covers configuration and examples.

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