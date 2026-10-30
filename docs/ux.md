# UX

Spec for workflows and keys.
Keyboard and mouse are both fully supported: every action is reachable with either.
Bindings live in the keymap and mouse tables; the other sections give the behaviour the tables cannot.
How decisions came about and which issue built what: `docs/history/ux.md` and the issues.

## Parts

- [ux-workflows.md](ux-workflows.md): the workflows (open a repository, graph, sidebar, diffs, working tree, branches, operations, remotes, worktrees, stashes).
- [ux-keymap.md](ux-keymap.md): the keymap (temporary home until the generated reference of #857).
- [ux-mouse.md](ux-mouse.md): the mouse table and gestures.
- [ux-settings.md](ux-settings.md): the Settings screen.
- [ux-look.md](ux-look.md): look and colours, the config file.

## Principles

- **Modal and discoverable.** Normal mode navigates, actions are short mnemonic chords, `?` shows the keys of the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- **Vim-like motions everywhere:** `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work. Bindings are data (mode, view, key sequence, action id); remapping is done in the config file (see "Config file" in [ux-look.md](ux-look.md)).
- **Layout.** History view: left sidebar (branches, remotes, tags, stashes, worktrees), centre commit graph, bottom detail pane (commit info, files, diff). The working tree is a separate view of the tab (workflow 2), not a graph row.
- **Branches and worktrees are first-class:** always visible in the sidebar, and every branch shows which worktree holds it.
- **Destructive actions confirm,** say what they will do and how to undo it.
- **Mouse is optional but complete.** Click to focus/select, double-click to open, wheel to scroll, clickable tabs and buttons, drag to select ranges and resize panes. Nothing is keyboard-only or mouse-only.

## Workspace and tabs

- The app opens with or without a repository: `gitunia` restores the last session (or shows the repository picker if there is none); `gitunia <path>` opens that repository as a tab, also from outside any repository (a subdirectory resolves to its repository root).
- The tab bar shows each tab's name and a dirty/ahead/behind marker (details in workflow 0).
- The repository picker is a fuzzy search over open, pinned, recent and discovered repositories, and also opens a typed path.
  A pinned repository stays at the top, right after the open tabs and above recents and discovered ones, however long ago it was opened; pins never expire, recents are limited to the last 50. Opening an already-open repository focuses its tab.
- Each tab keeps its own selection, scroll, filters, pane sizes and focused pane. Closing the active tab activates its right neighbour (left if it was last).

## Modes

Normal (navigate, act), Visual (select a range of lines, commits or files), Insert (text input: commit message, filters, prompts), Command (`:` palette), Confirm (y/n dialogs).

**Repeat mode** is a sticky sub-mode of Normal for step actions, so a value can be nudged several times without the popup or the prefix each time.

- **Which actions.** Only steppers: the context lines `+`/`-` in the diffs (not the file content) and the pane resize keys `Ctrl-w >` `Ctrl-w <` `Ctrl-w +` `Ctrl-w -`. Toggles (wrap, whole file, split, whitespace) never enter the mode.
- **Entering.** No extra key: running a step by key, chip or the `view ▾` dropdown starts the mode on the focused pane after that first step. A refused resize (a pane that cannot be resized, a zoomed pane) does not start it.
- **While on.** The step keys work bare (context: `+` `-`; resize: `>` `<` wider/narrower, `+` `-` taller/shorter, `=` reset) and have the meaning of the mode even where the pane binds them otherwise. Any other key ends the mode and then acts as usual; only `Esc` ends it and does nothing else. The bare `>` and `<` exist only here, so they are not in the vim-clash list.
- **Feedback.** The status bar shows one hint line instead of the pane's own and the pane title gets the marker `[repeat]` (placed so a narrow box cuts the name or option markers first). The mode is not saved with the session.
- **Scope.** One tab and one pane: switching tab or moving focus ends it, as does opening any overlay, prompt or search. The keys are the keymap views `ContextRepeat` and `ResizeRepeat`, so remapping follows the keymap.

**Paste.** A terminal paste works in every text input. A bracketed paste is replayed as the keys typing it would have sent; a terminal without bracketed paste sends plain keys, a line break as `Enter` (known limit: a pasted `url` plus newline accepts a prompt there). In a single-line input line breaks become one space and breaks at either end are dropped; message editors keep line breaks; tabs become spaces and other control characters are dropped. Outside Insert and Command mode a paste does nothing.

**Confirm mode** is used by every destructive action. A centred dialog holds a title, what will happen, what cannot be undone or how to undo it (in the warning colour) and the name of `y`.
- Only `y` runs the action, after the dialog has closed. `n` and `Esc` drop it. Every other key is swallowed: never `Enter`, and nothing behind the dialog moves.
- The two answers are rows of the dialog and clickable; a click anywhere else closes it like `n`.
- If the dialog does not fit the screen it is not shown and the action does not start (the status bar says so): an answer nobody can read is no confirmation. A dialog that stops fitting after a resize is closed unanswered, with a warning.
- The input mode before the dialog (Normal, or Visual with a range) is restored when it closes. What the action does is fixed when the dialog opens, not when `y` is pressed.

**Prompt popups** are used by every flow that asks for text or a choice (branch, tag, stash, worktree, reset mode, tab name, remotes).

- A centred box holds a title, what the answer is for, the text field (`> text_`), the candidate list of a filter prompt and the answering keys: `Enter` accepts, `Esc` cancels, `Ctrl-j`/`Ctrl-k` (also `Up`/`Down`, `Tab`) select in a filter. The keys are the same whether the flow started by key, palette or mouse; a click outside cancels like `Esc`.
- **Filter pickers** (remote of a push, tag push or delete, remote management, remote-branch delete, upstream, branch checkout; `FilterPicker<T>`): candidates are always on screen under the field, best match first, at most 8 rows, the highlighted row marked `> `, then the position `(2/5)`; an empty list says what is missing. `Enter` runs the highlighted candidate without typing. Letters always filter, so `j`/`k` are text here. A remote branch in the checkout list is written `origin/foo -> local foo`: it is checked out as a new local branch tracking it.
- The status bar keeps transient messages and the find-as-you-type bars (search `/`, ref jump `<space>f`, quick jump `<space>j`, the diff, reflog and sidebar filters). When the screen is too small for a popup, the prompt shows in the status bar and still answers to its keys.

## Focus model and key style

- **Panes:** sidebar (left), graph (centre), detail pane with Details, Changes and Files tabs (bottom), and the diff (it belongs to the Changes tab and sits beside its file list, and on the Files tab it shows the file content beside the tree; on screens too narrow for both it opens on demand and replaces the detail pane, see 1c).
- **Moving focus.** `Tab` cycles sidebar, graph, detail, diff (skipping closed panes); `Shift-Tab` reverses.
  **Pane rule (owner, #834):** `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l` move between panes everywhere, bare `h` `j` `k` `l` only inside a pane: they never change the focus (in a tree they fold, unfold and move the cursor; in the message box they move the text cursor).
  Going into a pane from a list is `Enter` (a commit to the detail pane, a file to its diff or content), coming back is `Esc`; `Ctrl-l`/`Ctrl-h` do the same by direction.
  `Ctrl-w` + `h` `j` `k` `l` (or `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l`, no prefix, Normal mode only) moves by direction (sidebar and graph on top, detail and diff below; `j` from the sidebar or graph goes to the detail pane, or the diff when the detail pane is hidden; `k` from a bottom pane goes to the graph); `Ctrl-w w` cycles; `Ctrl-w c` closes the tab.
  The focused pane has a highlighted border and its own status hints.
- **Keys.** Frequent actions are single keys per view (`Enter`, `y`, `/`). Global actions use the `<space>` leader (`<space>o` open repository, `<space>p` pin, `<space>z` zoom). `g`, `]`/`[` and `Ctrl-w` start chords; counts prefix motions (`5j`, `3gt`).
- **Chords.** A pending prefix shows in the status bar and, after 250 ms, in a which-key popup (`hints.whichKey` turns the popup off, #837) listing the continuations, plain actions first and `+more` groups after, each part alphabetical by key label with lowercase before uppercase; a group row reads `+more (branch operations)` and so on. `Esc` cancels; there is no chord timeout.
- **Insert mode.** Printable keys are text; only `Esc`, `Enter` and `Ctrl-`/`Alt-` combinations act, so the leader and chords never fire while typing.
- **`Esc` precedence:** cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph).
  In the graph, when nothing else claims it, `Esc` clears the comparison mark (#535), and when there is none it dismisses the status message.
- **`q` always quits,** from any pane, except that it first closes an open overlay or ends a Visual range. `Esc` is back in Detail and Diff; `h` and `l` collapse and expand in the Sidebar and in trees and never change the pane (the diff has no horizontal scroll). `Ctrl-c` is the last resort: it quits from any mode or modal at once and cancels running git work (#856).
- **Notation:** keys are spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `gg`, `<space>o`), never `^d` or `c-d`, in the help overlay, the palette, the which-key popup and the status bar.
- **Help overlay (`?`):** the keys of the focused view, generated from the keymap table and the action titles, never hand-written, in two columns. One tab per source: the view's own bindings, then the inherited `Lists` and `Global` ones, and `Markers` on the graph and the sidebar; the active tab is in brackets and collapses to `[Graph] 1/4` when the labels do not fit. Rows sit under the `ActionCatalog.GroupOf` subheaders (the palette's vocabulary and order; an action without a group lands in `Other`, a test forbids it). `Tab`/`l` and `Shift-Tab`/`h` switch tabs (or a click), `j`/`k`/`Ctrl-d`/`gg` scroll, `Esc`, `q` or `?` close it; every other action is ignored while it is open. Digits are not tab keys: they stay counts.
- **Command palette (`:` or `Ctrl-p`):** typing filters every action fuzzily (title or action id), `Up`/`Down` (or `Ctrl-j`/`Ctrl-k`) move, `Enter` (or `Ctrl-l`) runs the highlighted action, `Esc` closes.
  Each row shows its bindings and actions are grouped under subheaders (see Actions menu), the focused view's own actions first inside a group.
  Modal-only actions (the Confirm answers, the picker's own keys, the palette's own keys, text-input accept and `Esc`) are not listed; actions bound ahead of their feature are listed greyed out.
- **Status bar hints** are key plus short description pairs, keys in the accent colour. They start with `? help  : commands  q quit` (`hints.statusBar`: `minimal` shows only these, `off` none, #837), so a narrow terminal cuts the pane's own hints, never those. Every hint names the action of each of its keys, and a test checks the default keymap binds that key to that action in that pane.
  - **Few hints, view options grouped.** The default line of a pane lists only what matters most there. The diffs fold their view options into one group hint, `<space>v view options`, placed right after `/ search` so a narrow line cuts it last; it opens the which-key popup, and the individual keys keep working and stay in help and the palette. A group hint's test checks the keymap binds something below the prefix.
- **Status messages.** Background work that fails reports to one channel. The newest message shows in the status bar between the position and the key hints until it expires, is dismissed with `<space>x` (or `Esc` in the graph when nothing else claims it) or is replaced.
  - **Look and expiry.** Success or info use the good colour, warnings the warning colour, errors the error colour, bold; for the first 1.5 s a message is a filled chip. Info disappears after 5 s, warnings after 15 s, errors after 30 s (`StatusLifetime`); expired messages stay in the `<space>m` history (last 50, newest first, live). The same text while it still shows is not posted again and does not renew the timer.
  - Tab-scoped messages start with the repository name. A key action's own one-off notice takes the place of the message while it lasts.

