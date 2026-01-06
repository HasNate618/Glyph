# Glyph — Technical Implementation Plan (Windows)

## 0) Goals and non-goals

**Goals**
- Global, low-latency leader-key + layered key sequence engine.
- Always-available overlay that is **discoverable**: shows current sequence + valid next keys + descriptions.
- Context-aware layers (global, per-app, per-mode) with deterministic precedence.
- Action execution: launch apps, run scripts, window management, and macro-like sequences.
- Safe, debuggable behavior: logs, dry-run/preview, timeouts, and clear failure reporting.

**Non-goals (initially)**
- Full text expansion (a different problem space).
- Complex screen automation/vision-based workflows.
- Cloud sync (keep configs local-first).

## 1) Target platform + constraints

- **OS**: Windows 10/11.
- **Input capture**: Global low-level keyboard hook.
  - Use `WH_KEYBOARD_LL` via `SetWindowsHookEx`.
  - Must be **fast**: do minimal work in hook callback; enqueue events to a worker.
  - Some keys are special: IME, dead keys, AltGr, injected input.
- **Privileges**:
  - A non-elevated process cannot intercept input destined for elevated windows in all cases.
  - Plan: support running elevated (optional) or keep best-effort behavior with clear docs.
- **UI**: Overlay must never steal focus unless explicitly configured.
  - Use a topmost, click-through window (optional), with controlled focus rules.
- **Performance**: overlay should appear within ~16ms–33ms of leader press; avoid stutters.

## 2) Recommended tech stack (baseline)

This plan assumes a Windows-native stack to reduce friction and maximize reliability.

- **Language/runtime**: C# on **.NET 8**
- **UI**: **WPF** (stable, low overhead, great for transparent topmost overlays)
- **Interop**: P/Invoke for Win32 (hooks, window queries, monitors, etc.)
- **Config format**: JSON or TOML (human-editable). Recommend **TOML** for keymaps.
- **Scripting**: optional PowerShell runner and/or external process runner.
- **Installer**: MSIX (preferred) or MSI (fallback).

(If you prefer Rust/Tauri/WinUI3, the architecture still applies; only the “UI + hook plumbing” layer changes.)

## 3) High-level architecture

Process model: **single desktop app** with an always-running background core and an overlay UI.

### Core subsystems
1. **Input Service**
   - Owns the global keyboard hook.
   - Normalizes keystrokes into a canonical event model.
   - Sends events to the Engine via a lock-free queue.

2. **Sequence Engine (State Machine)**
   - Maintains current command session state: active/inactive, buffer, timer.
   - Resolves next valid keys based on active layers + current prefix.
   - Decides whether to:
     - consume the keystroke (prevent it reaching the active app)
     - pass-through
     - show/update overlay
     - execute action

3. **Context Service**
   - Determines active application/window context:
     - active process name (e.g., `code.exe`)
     - window class/title (optional)
     - virtual desktop (optional)
     - “modes” set by user (e.g., WindowMode, CodeMode)
   - Emits context-changed events to re-resolve layers.

4. **Overlay UI**
   - Renders current prefix and available next keys.
   - Never blocks hook thread.
   - Throttles updates (e.g., max 60 fps).

5. **Action Runtime**
   - Executes actions with cancellation/timeouts.
   - Supports: launch app, open file/url, run PowerShell, send key sequences, window ops.
   - Uses a permission model + allowlist/confirm for risky actions if desired.

6. **Configuration + Reload**
   - Loads keymaps, actions, and settings.
   - Hot reload on file changes (debounced) + validation errors displayed in overlay.

7. **Telemetry/Logging (local)**
   - Structured logs (rolling files).
   - Optional “trace mode” to show resolution decisions.

## 4) Data model

### 4.1 Normalized key event
Represent keystrokes in a consistent, layout-aware way.

- Physical key: scan code + extended flag
- Logical key: virtual-key (VK)
- Modifiers: Ctrl/Alt/Shift/Win
- Flags: keydown/keyup, injected, repeat

Recommendation: use **physical key identity for sequences** (scan code) to reduce layout issues, but display labels using the current layout.

### 4.2 Key sequence and trie
Store bindings in a **prefix tree (trie)** so you can:
- rapidly compute valid next keys for the overlay
- detect when the current prefix is complete
- detect ambiguity

Binding example conceptually:
- `Leader` → enters session
- `Leader` + `r` → “Run” sub-layer
- `Leader` + `r` + `c` → “Run Chrome” action

### 4.3 Layers + precedence
Define layers as ordered sources of bindings:

1. **Mode layers** (explicit modes toggled by the user)
2. **App layers** (active process/window)
3. **Global layer**

Within each layer, bindings are a trie. For a given prefix, resolution merges tries by precedence.

Conflict policy (deterministic):
- If multiple layers define the same full sequence, highest-precedence wins.
- For partial prefixes, union next-keys but keep source attribution for display.

### 4.4 Action schema
Actions are typed, with parameters.

Minimum action types:
- `launch`: exe path, args, working dir
- `open`: file/folder/url
- `powershell`: script path or inline, args
- `sendKeys`: sequence (with safeguards)
- `window`: move/resize/focus/snap

Include:
- `name`, `description`
- `timeoutMs`, `requiresElevation?`
- `when`: optional context constraints

## 5) Input handling details (leader + session)

### 5.1 Leader key
- Configurable leader key (single key or key+modifier).
- When leader is pressed:
  - start a “session” state
  - show overlay immediately
  - optionally **consume** the leader keystroke so it doesn’t reach apps

### 5.2 Session rules
- Maintain a buffer of normalized keys (excluding leader).
- On each keydown:
  - look up current prefix
  - if invalid:
    - optionally beep/flash overlay
    - cancel session and (optionally) replay keys to app (advanced; defer)
  - if valid and complete action:
    - execute action
    - end session (default)
  - if valid prefix:
    - update overlay

### 5.3 Timeouts + cancellation
- `sessionTimeoutMs` (e.g., 1500–3000ms) resets session on inactivity.
- `escapeKey` cancels the session.
- If an action is running, show “running…” and allow cancel.

### 5.4 Don’t break normal typing
Important design choice: only consume keys **after** leader is activated. Outside a session, pass everything through.

## 6) Overlay design (minimal but usable)

Overlay content (exactly what you described):
- Current sequence (leader + typed keys)
- Valid next keys
- Description for each next key path

Implementation notes:
- WPF transparent window, TopMost.
- Optional click-through via WS_EX_TRANSPARENT (config).
- Render list of next keys sorted (stable ordering).
- Update overlay on:
  - session start
  - key press within session
  - context change (active app) while session active

## 7) Context awareness

### 7.1 Active app detection
- Use `GetForegroundWindow` + `GetWindowThreadProcessId` to map to process executable name.
- Cache process metadata.
- Debounce context polling or subscribe via WinEvent hook (`SetWinEventHook`) for foreground changes.

### 7.2 Modes
- Modes are explicit user toggles.
- Provide actions:
  - `mode.toggle("Window")`
  - `mode.set("Code")`
  - `mode.clear()`

Keep modes in memory; persist optionally.

## 8) Action runtime implementation

### 8.1 Launch/open
- Use `ProcessStartInfo`.
- Support env vars expansion.

### 8.2 PowerShell
- Prefer `pwsh` if installed, fallback to `powershell`.
- Run non-interactive by default.
- Capture stdout/stderr to logs; show last line on overlay if failure.

### 8.3 Window management
- Use Win32 APIs:
  - enumerate windows, move/resize (`SetWindowPos`), focus (`SetForegroundWindow` w/ restrictions)
  - snapping can be best-effort (Windows imposes focus rules)

### 8.4 Send keys / macros
- Use `SendInput`.
- Mark injected events so the hook can ignore them (avoid recursion).
- Keep macro language minimal at first (e.g., list of key events + delays).

## 9) Configuration design

### 9.1 Files
- `config.toml` (settings)
- `keymap.toml` (bindings + layer definitions)
- `actions.toml` (action definitions)

### 9.2 Validation
On startup and reload:
- parse + validate schema
- ensure referenced actions exist
- detect unreachable nodes and duplicates
- surface errors in:
  - logs
  - overlay (brief)

### 9.3 Hot reload
- Watch files with `FileSystemWatcher`.
- Debounce 200–500ms.
- Atomic update: load into new model, then swap.

## 10) IPC and future extensibility (optional, but plan for it)

Not required day one, but keep seams:
- Internal interface `IActionHandler`
- Potential future: local named-pipe API for external tools to register commands

## 11) Security considerations

- Running scripts is powerful: treat configs as code.
- Store configs in user profile; avoid executing downloaded configs silently.
- Optional “trusted keymaps” directory.
- Avoid network calls by default.

## 12) Project structure (suggested)

- `src/Glyph.App` (WPF app + overlay)
- `src/Glyph.Core` (engine, trie, config, context, action runtime interfaces)
- `src/Glyph.Win32` (P/Invoke + Windows helpers)
- `src/Glyph.Actions` (built-in action handlers)
- `tests/Glyph.Core.Tests`

## 13) Milestones (incremental delivery)

### Milestone 1 — Input + overlay skeleton
- Global keyboard hook captures leader key
- Session state machine + timeout
- Overlay shows current sequence and valid next keys (from a hardcoded trie)

### Milestone 2 — Configurable bindings
- TOML/JSON config loading
- Trie built from config
- Hot reload + validation output

### Milestone 3 — Actions
- Implement `launch`, `open`, `powershell`
- Error handling and logs

### Milestone 4 — Context-aware layers
- Foreground app detection
- Merge precedence logic across global/app/mode

### Milestone 5 — Window + macro basics
- Window actions
- `sendKeys` with injection guard

### Milestone 6 — Packaging + startup
- Installer
- Auto-start option
- Basic settings UI (leader key, overlay behavior)

## 14) Testing strategy

- Unit tests for:
  - trie building and lookup
  - layer precedence + conflict resolution
  - session state machine (timeouts, cancel)
  - config validation
- Manual/integration tests for:
  - hook stability across apps
  - overlay performance + focus behavior
  - elevated app interactions

## 15) Open decisions (choose early)

- Binding identity: scan code vs VK vs mixed.
- Consume behavior for leader and subsequent keys.
- Overlay focus policy (always click-through vs interactable).
- Config format (TOML vs JSON) and where it lives.
- Elevated-mode support and messaging.