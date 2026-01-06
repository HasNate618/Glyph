
# Glyph: Leader-Key, Layered Command System for Windows

## Overview

Glyph is a keyboard-first interaction system for Windows. It lets users:
- Define their own leader key
- Create layered key sequences
- Map those sequences to actions (launch apps, run scripts, macros, context commands)

Inspired by Vim’s leader key and modal philosophy, Glyph replaces scattered shortcuts and opaque macros with a structured, discoverable, and highly customizable command language—one that adapts to the user.

Users design how they interact with their computer, using intentional key sequences supported by a visual interface that shows available actions in real time.


---

## What Glyph Does

### User-Defined Command Language

Users define a leader key to activate a command layer. After pressing the leader:
- Additional keys refine intent (open, run, manage, switch)
- Completing a valid sequence executes the mapped action

This creates a semantic interaction model, where key sequences reflect meaning rather than arbitrary shortcuts.


### Visual, Discoverable Interaction

When the leader key is pressed, a minimal overlay appears showing:
- The current key sequence
- All valid next keys
- A description of each path

Benefits:
- Learnable without memorization
- Self-documenting
- Safe to explore


### Layered and Context-Aware

Commands can exist in:
- Global layer (available everywhere)
- Application-specific layers (change behavior based on active program)
- Mode-specific layers (window management, coding, navigation, etc.)

The same sequence can perform different actions depending on context, allowing users to maintain consistent muscle memory while gaining expressive power.


### Actions Beyond Launching Apps

Key sequences can:
- Run scripts
- Execute multi-step macros
- Control windows and layouts
- Trigger workflows
- Interact with external tools or devices

The keyboard becomes a general-purpose control surface, not just a shortcut launcher.


---

## Why Glyph Is Useful

### Shortcuts Don’t Scale

Traditional keyboard shortcuts are:
- Limited in number
- Inconsistent across applications
- Hard to remember
- Easy to conflict

Glyph solves this by:
- Using hierarchical sequences instead of flat combinations
- Eliminating shortcut collisions
- Allowing unlimited expansion without cognitive overload


### Macros Are Powerful but Invisible

Existing macro tools often suffer from:
- Poor discoverability
- No feedback while typing
- Fragile or opaque behavior
- High learning barriers

Glyph keeps macro power but adds:
- Visual guidance
- Structure
- Intentional design
- Clear boundaries between commands


### Search-Based Launchers Are Inefficient for Repetition

Search-first tools require users to:
- Recall names
- Type repeatedly
- Mentally context-switch

Glyph is muscle-memory-first, not search-first. Once learned, actions become instantaneous and consistent.


---

## Who Glyph Is For

### Power Users
- Developers, engineers, designers, researchers
- Faster workflows, fewer context switches, complete control

### Keyboard-Centric Users
- Minimal mouse usage
- Consistent input patterns
- High efficiency

### Accessibility and Ergonomics
- Fewer complex key chords
- Predictable interactions
- Reduced repetitive strain

### Builders and Tinkerers
- Define workflows instead of adapting to them
- Share keymaps
- Treat input as programmable


---

## Why Glyph Is Better Than Existing Solutions

| Existing Tool Type | Limitation |
|--------------------|------------|
| Shortcut systems   | Too rigid, not scalable |
| Macro tools        | Powerful but opaque    |
| Launchers          | Search-based, not semantic |
| OS shortcuts       | Fixed and inconsistent |

Glyph:
- Is input-first, not UI-first
- Is structured, not ad-hoc
- Is discoverable, not memorization-based
- Is user-defined, not app-defined
- Introduces a coherent interaction model, not isolated features


---

## Design Philosophy

- Intent over memorization
- Structure over shortcuts
- Discoverability over opacity
- User control over defaults
- Consistency over convenience

The system should feel like:
> “I am speaking a language my computer understands.”


---

## Why This Project Matters

Modern computers are powerful, but their interaction models are still fragmented and shallow. Users are forced to adapt to:
- Inconsistent shortcuts
- Fixed UI assumptions
- Application-specific logic

Glyph flips that relationship.
It treats keyboard input as a programmable interface, giving users the ability to define how actions are organized, named, and executed—across the entire system.


---

## In One Sentence

Glyph allows users to design a structured, discoverable, and context-aware command language for interacting with their computer using only the keyboard.

---


---

## Technical Implementation Plan

See technical architecture, components, and milestones in [technical-implementation-plan.md](technical-implementation-plan.md).
