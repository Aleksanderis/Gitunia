# UX: Settings screen

Part of the UX spec; start at [ux.md](ux.md).

## Settings screen (design #465; the screen is implemented in #523, the theme in #524, the key reference in #525)

The registry is the single list the screen, the writer and the table below agree on (a test compares the table with it). A category without a setting is hidden.

One screen for every option of the config file, and the only UI for them (the theme picker of #464 is an entry of it, not a second UI). It edits `config.json`; the file stays the source of truth and the screen never keeps a setting the file cannot hold.

**Entry.** `<space>,` or the palette action `settings.open` (`:settings`) opens it as a large overlay (like Help: Esc closes, the tab underneath stays).
`settings.open-theme` (no key, listed in the palette without one: `PaletteEntryBuilder` adds the actions of `ActionCatalog.PaletteOnly`) opens it with the cursor on Appearance, Theme.
Mouse: the `settings.open` entry of the command palette, reached by the ` : commands ` button.

```
╭─ Settings ──────────────────────────────────────────────────────╮
│ / filter                                                         │
│ ▌Appearance  │ Theme                                [auto ▾]   │
│  Keys        │   ● auto                                          │
│  Repositories│   ○ dark                                          │
│              │   ○ light                                         │
│  Tabs        │ Show branch in tab name                  [x]      │
│              ├───────────────────────────────────────────────────┤
│              │ Theme: the colours of the interface. Written to   │
│              │ config.json as "theme". Default: auto.            │
├──────────────┴───────────────────────────────────────────────────┤
│ j/k move  l/Enter edit  h back  / filter  Esc close              │
╰──────────────────────────────────────────────────────────────────╯
```

**Layout.** Categories on the left (Appearance, Keys, Repositories, Git, Tabs, Config file; a category with no setting yet is hidden, Git has none today), the settings of the selected category on the right, a help line under the list (what the setting does, its config key, its default) and a hint row.
Under 80 columns the categories collapse into one list with category headings; under 40x12 the overlay shows a "terminal too small" line and no layout (Esc still closes it).
The set of categories and settings is the table below; the screen is generated from it (a registry of setting id, category, control, config key, default, validation), so docs, screen and config reader cannot drift.

| Category | Setting | Control | Config key | From |
|---|---|---|---|---|
| Appearance | Theme | choice list with live preview | `theme` | #47, #61 |
| Appearance | Branch label caps | choice (none, rounded, angled) | `labelCaps` | #851 |
| Tabs | Show branch in tab name | toggle | `tabs.showBranch` | #453 |
| Repositories | Scan roots | path list | `scanRoots` | #453 |
| Keys | Key reference | read-only list from the keymap | none | #50 |
| Config file | Open config.json in $EDITOR | action | none | #523 |

New options of later issues add a row here in the same change; a number control exists in the screen for them (min, max, step, typed digits).

**Focus and keys.** Two columns, `h`/`l` move between them (`l` or `Enter` on a category enters it, `h` or `Esc` in the settings column goes back, `Esc` in the categories closes).
`j`/`k`, `gg`/`G`, `Ctrl-d`/`Ctrl-u` move in the focused column.
`/` filters settings across all categories by name and help text, and the key reference lines by title, keys and action id (Insert mode, input focused; `Esc` clears; matches show their category and `Enter` jumps to the setting).
Per control:

- Toggle: `Enter` or `x` flips it and saves at once (a bare `<space>` stays the leader prefix).
- Choice list: the cursor moves through the options; `Enter` keeps and saves, `Esc` restores the value from before.
  For the theme (options `auto`, then every theme file: the built-in ones, then the user's from the `themes` folder as found at startup) the cursor move previews it live (the whole UI is repainted with that theme behind the overlay, nothing is written); `Enter` keeps it and writes `theme`, `Esc`, closing the screen or clicking another row restores the previous theme.
  A kept theme stays on screen while its write is pending and is restored if the write is refused or fails.
  `auto` chosen at run time uses the `COLORFGBG` guess: the terminal is asked for its background (OSC 11) only at startup, and only when the file says `auto`.
- Number: `Enter` starts typing (Insert mode), `Enter` saves, `Esc` cancels; `+`/`-` step. Out of range is refused with a message naming the range, nothing is saved.
- Path list: `a` add (prompt, `~` expanded, must be an existing directory), `d` remove (a question inside the overlay naming the path, Confirm mode: `y` removes, `n` or `Esc` keeps, or click its `y`/`n` row; the folder is untouched), `Enter` edit, `J`/`K` reorder. Saved on each change.
- Key reference: read-only.
  `KeyReferenceBuilder` generates it from the key binding table of the running app (the defaults with the `keys` entries of the config file applied, so rebinds show) and the action titles, one line per action and view (`j / Down`, title on the left, keys on the right), under a heading per view; Visual-mode bindings get their own heading (`Lists (Visual)`).
  Insert, Command and Confirm keys are not listed.
  It is a list of rows under the `Key reference` setting, listed Global first, then the other views as bound, then the Visual headings.
  The filter (`/`) also matches the lines' title, keys and action id (`quit`, `gg`, `list.down`) and lists them under their view heading; `Enter` on one clears the filter and lands on the line.
  In the collapsed layout the Key reference is the last category, so the others are reachable without passing its lines (#558).
  `Enter` (or a second click) on a line shows its `keys` entry in the status bar (`{ "view": "Graph", "key": "J", "action": "list.down" }`, with `mode` for Visual); it works on a read-only file too and writes nothing.
  The list shows the keymap read at startup: after editing `keys`, restart.
  Editing a binding in the UI is out of scope (see open choices).

Keys are Normal-mode view keys of the `Settings` view (rows in the keymap table); no new global leader beyond `<space>,`, which also closes the screen.
A path list is always expanded: its folders are rows under the setting row, `a` or `Enter` on the setting row adds one, and a list with no entry in the file shows "default folders" (the first add writes a list that replaces them).
The settings never open over another overlay.
While the screen is open the file is the source of truth: it is read when the screen opens, a write that finds the file changed re-reads it, and the editor row re-reads it when the editor closes (key bindings still need a restart).
Each applied change shows "Saved <name>" in the status bar; refusals and failures appear there too.

**Mouse.** Click a category selects it; click a setting selects it, a second click (or a click on its control) edits it: toggle flips, choice opens the list, click on an option selects and keeps it (a theme option: the first click previews it, a second click on the same option keeps it); wheel scrolls the column under the pointer; click outside the overlay (Backdrop) closes it like `Esc`; a click on a path-list `[+]`/`[x]` adds or removes.
Every mouse gesture maps to the action id a key produces (`DefaultMouseMap` rows added by the implementing issue).

**Persistence.**

- A change is written when it is committed (toggle flip, `Enter` on a choice, number, path edit), never on each cursor move; the theme preview is not written until `Enter`. The write is applied to the running app at once, no restart.
- The write is a minimal text edit: `ConfigKeyLocator` (extended by #522 to return the last match and a result that tells found, absent, invalid file with line, parent not an object, and shadowed duplicate apart) finds the span of the value of the key; only that span is replaced, so comments, order, indentation and trailing commas stay byte-for-byte.
  The file is written to a temporary file in the same directory and renamed over `config.json` (keeping its permissions), so a crash never leaves half a file.
  A symlinked `config.json` is resolved and the temporary file is written next to its target (the link stays).
  The file's BOM, line ending and indent are reused for inserted text.
  Rename-over uses `File.Move(overwrite)` on Windows and `rename` elsewhere; permissions are copied where the OS has them, best effort.
  The window between the stale check and the rename is accepted.
- A key the file lacks is inserted as a new member at the end of the top-level object, one line, indented like the other members, with a comma added after the previous last member unless it already has one.
  An empty file or `{}` gets the object filled in.
  A nested key (`tabs.showBranch`) is inserted into its parent object, which is created the same way if missing; a parent that exists but is not an object (`"tabs": 5`, `null`) is refused with a message, never replaced.
- A file that does not parse (or is over 1 MB, or is not an object) is never written.
  The screen opens read-only with a banner naming the problem and its line, and edits are refused with the same message.
  A missing file is created (with the config directory) on the first change, containing only that key.
- Unknown keys are kept untouched and not shown. Duplicate keys (the same member twice in one object): the screen edits the last one, which is the one the reader honours (the locator returns it), and warns once that the first is shadowed; it does not delete either.
- A value in the file that is invalid (wrong type, out of range) shows the setting with its default and a warning marker with the reason; changing it writes a valid value over the bad one.
- The file changed on disk since it was read (compared by content hash just before the write): the screen re-reads it, shows the new values, and asks to repeat the edit; nothing is overwritten blindly.
  Editing the file by hand while the screen is open is therefore safe; the app does not watch the file otherwise.
- Settings that hold a value the file cannot (read-only key reference) are not editable and show no control.

