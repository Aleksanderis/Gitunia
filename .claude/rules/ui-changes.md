---
paths:
  - "src/Gitunia.Tui/**"
  - "src/Gitunia.Core/**"
  - "tests/Gitunia.Tui.Tests/**"
  - "tests/Gitunia.Core.Tests/**"
---

# UI, keys and look

Specs, the keymap table and the mouse table are in `docs/ux.md`. Checklist for a change that adds or alters what the user sees or presses:

## A new action, key or mouse path

- The key row goes in the keymap table in `docs/ux.md` in the same change; no conflicts within a mode and view (a test parses the table against `DefaultKeymap`). A mouse path gets its row in the mouse table (a test checks it against `DefaultMouseMap`).
- Vim-clash rule (owner, #335): standard vim/neovim keys keep their vim meaning; a Gitunia-specific action never binds a bare vim normal-mode command (`s` `u` `c` `a` `x` `r` `D` `M` `E` ...) but a `<space>` leader chord or a `Ctrl-`/`Alt-` combination; `<space>j` is reserved for the quick jump. Check a new key against vim before binding it (list in `docs/ux.md`, workflow 2, "Key rule").
- Everything doable by keyboard is doable by mouse and the reverse; nothing is keyboard-only or mouse-only. A mouse gesture maps to an action id and runs the same handler as the key.
- The action has a title in `ActionCatalog` (a test fails otherwise). An action bound ahead of its feature goes in `ActionCatalog.Unimplemented`; modal-only ones in `IsModalOnly`.
- Status bar hints are short, not taken from the catalog titles; each key names its `ActionId` and a test checks the default keymap binds it.
- Help, palette and which-key text is generated from the keymap and catalog, never hand-written.
- Key notation is spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`), never `^d` or `c-d`.
- `Esc` and `q` follow the precedence in `docs/ux.md` ("Focus model and key style"). In Insert mode printable keys are text: only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.
- Destructive actions go through Confirm mode (`y`/`n`/`Esc` only, never `Enter`) and say what they do and how to undo it.

## Rendering and colour

- Terminal.Gui types stay in `Gitunia.Tui` (`Views`, `Input`); `Rendering` and Core know only styled text frames and renderer-agnostic key events.
- Colours are decided in one place: `PaletteTable` and `Palette`. Never hard-code a colour in a view. Contrast is tested (4.5, dividers 3.0).
- Every colour tier has a path: 24-bit, 16-colour, `NO_COLOR`/`TERM=dumb`, and the ASCII glyph set. Focus and selection never rely on colour alone.
- Layout is data (`PaneLayout`, `GraphColumnLayout`, `FrameGrid`) and is unit- and snapshot-tested at several terminal sizes, including narrow and tiny ones; nothing may crash on resize.
- Wide characters are never split; text width goes through `TextWidth`.
- UI PRs attach screenshots (`screenshots` skill).
