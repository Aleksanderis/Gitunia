---
paths:
  - "src/Gitunia.Tui/**"
  - "src/Gitunia.Core/**"
  - "tests/Gitunia.Tui.Tests/**"
  - "tests/Gitunia.Core.Tests/**"
---

# UI, keys and look

Specs start at `docs/internal/ux.md`, which links the parts; the key reference is generated in `docs/keymap.md` (design rules in `docs/internal/ux-keymap.md`) and the mouse table in `docs/internal/ux-mouse.md`. Checklist for a change that adds or alters what the user sees or presses:

## A new action, key or mouse path

- The key row goes in `src/Gitunia.Core/Keymap/keymap.default.json` in the same change, then run `scripts/gen-keymap-doc.sh` to refresh `docs/keymap.md`; no conflicts within a mode and view (tests check the file and the generated reference). A mouse path gets its row in the mouse table (a test checks it against `DefaultMouseMap`).
- Vim-clash rule (owner, #335): standard vim/neovim keys keep their vim meaning; a Gitunia-specific action never binds a bare vim normal-mode command (`s` `u` `c` `a` `x` `r` `D` `M` `E` ...) but a `<space>` leader chord or a `Ctrl-`/`Alt-` combination; `<space>j` is reserved for the quick jump. Check a new key against vim before binding it (list in `docs/internal/ux-workflows.md`, workflow 2, "Key rule").
- Everything doable by keyboard is doable by mouse and the reverse; nothing is keyboard-only or mouse-only. A mouse gesture maps to an action id and runs the same handler as the key.
- The action has a title in `ActionCatalog` (a test fails otherwise). An action bound ahead of its feature goes in `ActionCatalog.Unimplemented`; modal-only ones in `IsModalOnly`.
- Status bar hints are short, not taken from the catalog titles; each key names its `ActionId` and a test checks the default keymap binds it.
- Help, palette and which-key text is generated from the keymap and catalog, never hand-written.
- Key notation is spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`), never `^d` or `c-d`.
- `Esc` and `q` follow the precedence in `docs/internal/ux.md` ("Focus model and key style"). In Insert mode printable keys are text: only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.
- Destructive actions go through Confirm mode (`y`/`n`/`Esc` only, never `Enter`) and say what they do and how to undo it.

## Rendering and colour

- Terminal.Gui types stay in `Gitunia.Tui` (`Views`, `Input`); `Rendering` and Core know only styled text frames and renderer-agnostic key events.
- Key hints and buttons: the hints of a surface are declared once in `HintSet` (Core; keys, a `HintVerb` label, the action of each key) and read by both the status bar and the in-frame footer (`OverlayBox.Hints` with the frame's click targets); a "key: sentence" row is an `ActionRow` (key from the keymap by action); a clickable chip is a `KeyedChip` with its key from `ChipKeys`. A new hint row or button does not hand-build key and label segments, literal key rows or brackets (`ButtonLabel.Bracketed`), and every hint that has an action is clickable and runs it (`docs/architecture.md`, "UI components"; `UiWordingLintTests` and `FooterHintClickTests` enforce it). A new screen adds its hints to `HintSet` and its row to `FooterHintClickTests`.
- The wheel step is `DefaultMouseMap.WheelLines` (3: panes, long lists, the message box) or `PopupWheelLines` (1: popups); a view never holds its own step.
- A list keeps its selection visible and moves it through `ListViewport` (`Edge` policy in panes, `Centre` in overlays, stops at the ends): no scroll-top or clamp arithmetic of its own. A new list surface joins the registry in `ListViewportContractTests` (`docs/architecture.md`, "UI components").
- A text field is a `TextField` (Core) drawn by `CaretField` with a `FieldRoles` preset: typed text is always the typed-text role of its surface, never a role picked for how it looks; keys are added once in `LineEditKeys`; a multi-line host declares undo, clipboard and Esc in `EditorHosts`; a prompt answer a reducer rejects returns `ReopenPromptEffect` so the typed text is not lost (`docs/architecture.md`, "UI components").
- Wording and values come from `Gitunia.Core/Text`: no-match, nothing, unchanged, empty and loading texts from `UiTexts`, the label of Esc, Enter, j/k, Ctrl-g and Ctrl-s hints from `HintVerb`, counts from `CountText.Plural`, hashes from `ShortHash`, dates, status letters, renames and ahead/behind from `ValueFormat` (`docs/architecture.md`, "UI components"); lint tests fail on a literal.
- A role is chosen by meaning from the table in `docs/internal/ux-look.md` ("Meaning to role"), never because it looks right: `KeyLabel` (`BarKeyLabel` on the bar) only on key text, `Heading` for headings, `DirectoryName` for folders, `Divider` for rules inside a box, `Loading` for the loads of the details, file history and working tree panes. No literal box, arrow, check or cross glyphs outside the glyph tables (`RendererSourceLintTests`).
- Colours are decided in one place: `PaletteTable` and `Palette`. Never hard-code a colour in a view. Contrast is tested (4.5, dividers 3.0).
- A chip that starts a bar line (mode badge, operation banner title) starts at column 0; an in-progress operation uses the warning chip role (`StatusOperation`), not the plain mode chip.
- Every colour tier has a path: 24-bit, 16-colour, `NO_COLOR`/`TERM=dumb`, and the ASCII glyph set. Focus and selection never rely on colour alone.
- Layout is data (`PaneLayout`, `GraphColumnLayout`, `FrameGrid`) and is unit- and snapshot-tested at several terminal sizes, including narrow and tiny ones; nothing may crash on resize.
- Wide characters are never split; text width goes through `TextWidth` (backed by `DisplayWidth` in Core).
- UI PRs with a visual change attach one capture (`screenshots` skill); none for key labels, hints only, docs or logic.
