# Glyph — Feature Possibilities

This document enumerates ideas, features, and extensions that pair well with Glyph. Use this as a brainstorming catalogue or roadmap seed. Items are grouped by theme and include short notes about UI, data model, and implementation pointers.

---

## Table of contents

- Core UX
- Input & Sequences
- Text & Clipboard
- Window & Workspace
- Automation & Macros
- Discovery & Onboarding
- Integrations & Extensibility
- Profiles, Sync & Sharing
- Accessibility, Security & Privacy
- Developer & Testing Tools
- Telemetry & Analytics
- Miscellaneous Ideas

---

## Core UX

- Command Palette
  - Fuzzy search across actions, bindings, apps, and snippets.
  - UI: single-line search; results grouped by category.
  - Implementation: index actions+labels at startup; incremental search.

- Searchable Overlay
  - Allow typing to filter overlay keys in addition to showing next-key suggestions.
  - UI: overlay shows a small input for filtering.

- Contextual Action Hints
  - Display actions relevant to the foreground app or active window title.
  - Data: per-app rules; dynamic matching on window title regex.

- Quick Launcher
  - Launch apps/files/projects with fuzzy input and history.
  - Support `exec` bindings, pinned favorites, and project folders.

- Repeat/Undo Last Action
  - Repeat the last triggered action with a single binding; undo where meaningful.
  - Data: record last `ActionRequest` and provide `repeatAction`/`undoAction` handlers.

---

## Input & Sequences

- Multi-key leader variants
  - Support configurable multi-stroke leaders with optional timeout behavior.
  - Option to start immediately on key-down or on key-up for modifiers.

- Chord/N-key rollover sending
  - Allow sending chords and complex virtual-key sequences reliably.
  - Support sequences like `Ctrl+Alt+Shift+K` and media keys.

- Arrow & Special Keys in YAML
  - Document tokens for arrow keys, function keys, page up/down, home/end.
  - Already-supported: `Left`, `Right`, `Up`, `Down`, `F1..F12`, `Enter`, `Tab`.

- Dynamic Layers
  - Allow bindings to create temporary layers (e.g., entering a `resize` layer).
  - Layers can have expiry or be modal until explicitly closed.

---

## Text & Clipboard

- Clipboard History
  - Maintain a history of clipboard entries, access via leader or palette.
  - Optional paste-in-place and insert-with-formatting options.
  - Data: ring buffer, per-item preview, persistent storage.

- Snippets / Text Expansion
  - Named snippets that expand into text or templates with placeholders.
  - Support tab stops, date/time, and snippet variables.

- Type-from-Template
  - Bindings that type multi-line templates, filling fields from prompts.

- Clipboard to App
  - Quick commands to send clipboard contents to app-specific inputs (e.g., search bar).

---

## Window & Workspace

- Window Tiling / Layouts
  - Snap, tile, and remember layouts per monitor + workspace.
  - Commands: tile-left, tile-right, move-to-monitor, save-layout, restore-layout.

- App-specific Window Actions
  - Predefined sequences for developer apps (e.g., open terminal in project folder).

- Switcher & Most-Recent
  - Fast app/window switching with MRU order and fuzzy matching.

---

## Automation & Macros

- Macro Recorder / Player
  - Record key events and actions; replay as macro with repeat count and speed control.
  - Save macros as named actions in keymaps.

- Conditional Actions
  - Actions that branch based on active app, OS state, or clipboard contents.
  - Example: if VSCode active -> open terminal, else open system terminal.

- Scheduled Actions
  - Run actions at times or intervals (e.g., nightly backups, open daily tools).

- Composite Actions
  - Combine `send`, `type`, and `exec` steps with optional delays and error handling.

---

## Discovery & Onboarding

- Interactive Onboarding
  - Show leader, overlay, and how to add the first app in a guided flow.

- Discover Mode
  - Press leader-hold to see a walkthrough of the current overlay with examples.

- Hints & Tips
  - Small, contextual tips for new users (e.g., "Use `o` → `v` to open VSCode").

---

## Integrations & Extensibility

- Plugin / Scripting API
  - Allow external scripts (PowerShell, Node, .NET) to register actions.
  - Secure sandboxing and an API surface for registering/triggering actions.

- HTTP/Webhook Actions
  - Trigger webhooks, call REST APIs, or receive remote commands (auth required).

- IDE Integrations
  - Deeper integrations for VS Code, JetBrains IDEs to trigger editor actions.

- Git / Repo Shortcuts
  - Quick bindings for `git status`, `git pull`, create branch, open repo in editor.

---

## Profiles, Sync & Sharing

- Profiles
  - Multiple named keymap/theme profiles (Work, Gaming, Presentation).

- Export / Import Keymaps
  - Share YAML snippets or full profiles; support diff and merge tools.

- Cloud Sync (optional)
  - Encrypt and sync settings across machines (e.g., using user-provided S3 or Git).

---

## Accessibility, Security & Privacy

- High-Contrast, Scaling & Keyboard Navigation
  - Ensure overlay supports high-contrast modes, large fonts, and full keyboard control.

- Permissions & Secure Exec
  - Confirm/expose security implications of `exec` and remote actions. Provide allow-lists.

- Privacy Mode
  - Turn off telemetry and deep integrations; run fully local-only.

---

## Developer & Testing Tools

- Developer Console
  - Show logs, session traces, and active key buffer for debugging.

- Test Harness
  - Automated tests that simulate leader sequences, per-app behaviors, and SendInput results.

- Hot Reloading of YAML
  - Watch keymaps file and apply changes live; show reload errors in UI.

---

## Telemetry & Analytics (optional)

- Usage Stats
  - Local-only counters for most-used bindings and sessions to help users optimize.

- Opt-in Telemetry
  - Aggregate anonymous usage for product improvement (opt-in required).

---

## Miscellaneous Ideas

- Actions Marketplace
  - Curated community-built keymaps and snippets for popular apps.

- Voice Trigger
  - (Optional) Allow actions to be triggered by voice commands (with privacy safeguards).

- Mobile Companion App
  - Use phone as a remote to trigger Glyph actions or view overlays.

- Game Mode
  - Suspend overlay when particular fullscreen processes are active.

---

## Implementation notes and priorities

- Low-effort, high-value
  - Clipboard history, snippets, quick launcher, hot-reload YAML, command palette.

- Mid-effort
  - Window layouts, contextual suggestions, profiles & sync.

- High-effort / Longer-term
  - Plugin API, marketplace, cloud sync, mobile companion, voice integration.

---

## Example YAML additions (quick reference)

- Send arrow with modifiers:
```yaml
bindings:
  - key: k
    label: Browser nav
    children:
      - key: b
        label: Back
        send: Alt+Left
      - key: f
        label: Forward
        send: Alt+Right
```

- Snippet exec example (type template):
```yaml
bindings:
  - key: s
    label: Snippets
    children:
      - key: a
        label: Address
        type: "123 Main St\nCity, State ZIP"
```

---

If you want, I can:

- Convert this into a prioritized roadmap with estimated effort.
- Create example UI mockups for the Command Palette or Clipboard History.
- Implement one feature (pick one) end-to-end.

Pick what to do next and I’ll continue.
