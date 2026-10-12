# UX

Spec for workflows and keys; the flows are written before the views. Keyboard and mouse are both fully supported: every action is reachable with either.

## Principles

- Modal and discoverable: Normal mode navigates, actions are short mnemonic chords, `?` shows keys for the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- Key bindings are the defaults, and users can remap them or add extra bindings in the config file (planned for M4; the keymap engine is data-driven from M1 so remapping needs no redesign). Vim-like motions everywhere: `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work.
- History layout: left sidebar (branches, remotes, tags, stashes, worktrees), center commit graph, right/bottom detail (commit info, files, diff). Working tree status is a pseudo-row at the top of the graph.
- Working-tree layout (staging): selecting the status row (or a key from anywhere) switches the center and detail panes to unstaged files, staged files and a diff/hunk view. Whole files, hunks or selected lines are staged and unstaged from the diff itself, then the commit message is written and committed from the same screen. Details of this flow are specified as workflow 2 below, before M2.
- Branches and worktrees are first-class, not a submenu: they are always visible in the sidebar and every branch shows which worktree holds it.
- Destructive actions confirm and say what they will do and how to undo it.
- Mouse is optional but complete: everything doable by keyboard is doable with the mouse (click to focus/select, double-click to open, wheel to scroll, clickable tabs, buttons and menus for actions, drag to select ranges and resize panes). Nothing is keyboard-only or mouse-only; mouse paths are listed in the Mouse section below.

## Workspace and tabs

- The app opens with or without a repo: `gitunia` (restore last session, or repo picker if none), `gitunia <path>` (open that repo as a tab, also from outside any repo; a subdirectory resolves to its repo root).
- Tab bar shows repo name (path on disambiguation), branch and a dirty/ahead/behind marker, and background-activity state.
- Repo picker: fuzzy search over recent, pinned and discovered repos (configurable scan roots), open by typing a path, close/pin entries. Opening an already-open repo focuses its tab.
- Each tab keeps its own selection, scroll, filters and focused pane.  Closing the active tab activates its right neighbour (left if it was last).

## Modes

Normal (navigate, act), Visual (select a range of lines/commits), Insert (text input: commit message, filters), Command (`:` palette), Confirm (y/n dialogs).

## Focus model and key style

- Panes: sidebar (left), graph (center), detail file list (right/bottom) and diff (beside or below the detail pane, replacing the commit info when open). `Tab` cycles in the order sidebar, graph, detail, diff (skipping panes that are closed); `Shift-Tab` reverses. Directions follow that layout (sidebar and graph on top, detail and diff below them). `Ctrl-w` + `h` `j` `k` `l` moves focus by direction (`j` from the sidebar or graph goes to the detail pane, or the diff when the detail pane is hidden; `k` from a bottom pane goes to the graph), `Ctrl-w w` cycles, `Ctrl-w c` closes the tab. Both exist; the focused pane has a highlighted border and its own status hints.
- Frequent actions are single keys per view (`Enter`, `y`, `/`); global actions use the `<space>` leader (`<space>o` open repo, `<space>p` pin, `<space>z` zoom pane). `g` and `]`/`[` start chords; counts prefix motions (`5j`, `3gt`).
- Chord engine: a pending prefix shows in the status bar and, after 500 ms, a which-key popup listing the continuations; `Esc` cancels; there is no chord timeout. Bindings are data (mode, view, key sequence, action id), so remapping in M4 needs no redesign.
- Help and palette (#20): `?` opens a help overlay with the keys of the focused view (its own bindings, then the inherited `Lists` and `Global` ones), generated from the keymap table and the action titles in `ActionCatalog`, never hand-written; `j`/`k`/`Ctrl-d`/`gg` scroll it and `Esc`, `q` or `?` close it (every other action is ignored while it is open). `:` or `Ctrl-p` opens the command palette in Command mode: typing filters every action fuzzily (title or action id), `Up`/`Down` move, `Enter` runs the highlighted action and `Esc` closes; each row shows its bindings, and the focused view's own actions come first. The which-key popup is the same kind of overlay, docked above the status bar.
- Status messages (#93): background work that fails (worktree, stash or status reads of a tab, a failed session save, a session file that was reset at start-up) reports through one `StatusChannel` in `Gitunia.Core.Status`; later features post to it the same way. The newest message shows in the status bar between the position and the key hints (errors in the error colour) and stays until dismissed with `<space>x`, or `Esc` in the graph when nothing else claims it, or until a newer one replaces it. `<space>m` opens the history (the last 50, newest first; long messages wrap) as an overlay; a failed graph load is posted there too; it is a snapshot taken on open, so reopen it to see newer messages. Tab-scoped messages start with the repository name, and a message equal to the one showing is not posted again. A key action's own one-off notice (`HEAD is not loaded yet`) still takes the place of the message while it lasts. The status bar hints end with `? :` (help and palette).
- `Esc` precedence: cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph); in the graph, when nothing else claims it, `Esc` dismisses the status message. `q` goes back one level the same way and quits only from the graph with nothing pending; `h` is back in Detail and Diff and collapse in Sidebar (Diff has no horizontal scroll).
- In Insert mode (search box, picker input) printable keys are text; only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act, so the leader and chords never fire while typing.

## Workflows

Each later workflow (2-7 at the end of this section) needs a written step-by-step key flow here before it is implemented. Flows 0 and 1 are specified now.

### 0. Open a repo, switch tabs, reopen the session

1. `gitunia` restores the last session (tabs, active tab, per-tab selection/focus). With no session it opens the repo picker. `gitunia <path>` opens that path as a tab (subdirectory resolves to the repo root; a repo already open just gets focused) and keeps the other restored tabs.
2. `Ctrl-t` or `<space>o` opens the repo picker (Insert mode, input focused). It lists open, pinned, recent and discovered repos, fuzzy-filtered as you type; `Up`/`Down` and `Ctrl-j`/`Ctrl-k` move; `Enter` opens in a new tab (or focuses the existing tab); `Backspace` deletes a character; `Esc` cancels (with no tab open it quits instead, since there is nothing to go back to). Discovered repositories come from a scan of the conventional source folders below the home directory (`Projects`, `src`, `code`, `dev`, `repos`, `git`, `work`, ... up to three levels deep, hidden and dependency folders skipped); configurable scan roots arrive with the config file in M4. A typed path is listed off the UI thread: the previous rows stay until the new listing arrives (`scanning...` appears after about 150 ms when there are none), a newer keystroke cancels the older listing, and a listing slower than 5 s ends with a "timed out" notice. Rows show tags (`open`, `pinned`, `recent`, `found`); a click highlights a row and a double-click opens it.
3. Typing something that starts with `/`, `~/`, `./`, `../` or `C:\` is treated as a path (a drive letter needs `C:\` or `C:/`; others need `/`, `~/`, `./` or `../`; anything else is a fuzzy query): the list shows the matching directories (repositories marked), `Tab` completes to the common prefix and then to the highlighted directory, `Enter` opens the resolved path (relative paths are taken from the active repository, or the home directory, never from the process working directory); a non-repo path shows an inline "not a git repository" message and stays open.
4. In the picker `Alt-p` pins or unpins the highlighted entry and `Alt-d` removes it from recents (never deletes anything on disk).
5. Switch tabs with `gt` / `gT` (or `3gt`), `Alt-1`..`Alt-9`, or `Ctrl-^` or `<space><Tab>` for the previously active tab. Mouse: click a tab. Tab bar format: ` N name  branch markers `, with `*` for a dirty working tree, `↑n` ahead, `↓n` behind (`+n`/`-n` without Unicode) and an ellipsis while the tab loads; two tabs with the same name show their parent directory; when the tabs do not fit the row scrolls to keep the active one visible (`<` marks hidden tabs on the left). Markers follow external git activity (file watching; `r` refreshes manually). `Ctrl-w c` closes the tab; closing the last tab shows the picker.
6. A tab whose repo disappeared (moved or deleted) stays in the bar marked `! name (missing)`; `Enter` on it opens the picker (Locate repository, starting in the folder that held it) and the chosen repository replaces that tab, `Ctrl-w c` closes it.
7. Terminal key caveats, verified against Terminal.Gui 2.5.0 under tmux: `Ctrl-j` (0x0A) arrives as `Ctrl+Enter`, distinct from `Enter` (0x0D), so it works as a picker alias for `Down` (the translator maps it to `Ctrl-j`; a terminal that sends the same byte for both would break the alias, `Down` still works); `Ctrl-^` (0x1E) arrives as `Ctrl+6` and is mapped to `Ctrl-^`, so it works, and `<space><Tab>` remains the fallback for terminals that swallow it; some terminals send `Backspace` as `Ctrl-h`, which the picker treats as `Backspace`.

### 1. Browse the graph, jump, search

1. A repo tab opens with focus on the graph, the newest commit selected and HEAD marked. Commits stream in; the list is usable immediately and the status bar shows loading progress.
2. Move with `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G`, counts and the mouse wheel. `@` jumps to HEAD; `gr` opens a prompt in the status bar (Insert mode) that fuzzy-filters refs (branches, tags, remotes) and also accepts a hash prefix of at least 4 hex digits; the best candidate is shown, `Up`/`Down`/`Tab` cycle through the others, `Enter` selects that commit and `Esc` cancels.
3. `Enter` on a commit moves focus to the detail pane (commit info and changed files); `Esc` or `h` returns to the graph. The detail pane also follows the graph selection while the graph is focused. The pane sits below the graph (hidden on screens under 14 rows) and lists, from the top: hash, author, committer (only when different), parents, refs, the full message and the changed files with their status (`A` `M` `D` `T`, `R<score> old -> new`, `C<score> old -> new`; merges show the changes against the first parent). While focused, `j`/`k` scroll through all rows and focusing lands on the first file row; `Enter` on a file row opens its diff (1c). When the details fail to load the pane says so and `r` in the detail pane (or the palette) retries.
4. `/` opens the search box (Insert mode). Plain text matches the commit subject (the message body is not searched yet: graph rows keep only the subject), `author:` matches the author name and `hash:` the start of the hash, all case-insensitive. Matches are highlighted in the rows while typing and the status bar shows the count (`/fix_  3/17`). `Enter` jumps to the first match at or after the selection and returns to Normal mode, `n`/`N` step through matches (counts work, the search wraps), `Esc` in the box cancels, `Esc` in Normal mode clears the highlight. The search scans the loaded commits in small slices between key presses and keeps scanning as the log streams in, so matches beyond the loaded range appear as they load and the status bar shows `searching N%` meanwhile. In the box `Backspace`, `Ctrl-u` and `Ctrl-w` edit the text.
5. `y` copies the selected commit hash (OSC 52); `r` refreshes the repo now.

### 1b. Sidebar navigation

1. `Ctrl-w h` (or `Tab` cycling) focuses the sidebar (sections: branches, remotes, tags, worktrees, stashes). `j`/`k` move, `l`/`Enter` expand a section, `h` collapses it (or moves to the parent).
2. `Enter` on a branch, remote branch or tag selects its commit in the graph and keeps focus in the sidebar; `l` on a leaf moves focus to the graph. Each branch shows the worktree that holds it.
3. `/` filters the sidebar entries by name (Insert mode: type, `Backspace` erases, `Enter` keeps the filter and returns to Normal mode, `Esc` drops it). The filter also matches the worktree shown beside a branch and unfolds every section with a match; in Normal mode `Esc` clears an active filter, and otherwise (like `q`) returns focus to the graph.
4. A section header shows its entry count; Remotes and Tags are folded by default. A branch shows `wt:<directory>` for the worktree that has it checked out (`*` marks the current branch and the current worktree) and `↑n↓m` against its upstream. A stash is not a graph node, so `Enter` on it only says so. The sidebar is hidden below 60 columns.

### 1c. Diff viewing

1. In the detail pane select a file with `j`/`k` and press `Enter`: the diff pane opens with the whole commit's unified diff (against the first parent, so merge commits match the file list), puts the chosen file's header at the top and takes focus. The diff pane sits beside a compact detail pane (the file list on the left, a third of the width, 24-60 columns) on screens of 70 columns or more, so the file list and the diff are visible together at 80 columns, and replaces the detail pane only below 70 columns (`Tab` reaches the detail pane again once the diff is closed); screens under 14 rows open it zoomed. `<space>z` zooms the focused graph, detail or diff pane to the full area between the tab row and the status bar and back (the sidebar does not zoom; moving focus to another pane ends the zoom). While zoomed, `Tab` only reaches panes that are shown.
2. `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G` scroll; `]c` / `[c` jump to the next / previous hunk and `]f` / `[f` to the next / previous file of the commit (counts work; at the last one the status bar says so). The title shows the commit, the file under the cursor and `file n/m  hunk n/m`. `/` opens a search box (Insert mode, case-insensitive substring over every row including headers; matches are highlighted while typing); `Enter` jumps to the first match at or after the cursor, `n`/`N` step through matches and wrap, `Esc` in the box cancels. Mouse: the buttons at the right of the title row (`[<f] [f>] [<h] [h>] [w] [z] [x]`) are `[f` `]f` `[c` `]c` `w` `<space>z` and `q`; a drag selects a range of rows (Mouse section).
3. Rows are `old new` line numbers, a `+`/`-`/space marker and the text; text is cut with an ellipsis while word wrap is off (the default); `w` turns wrap on, and a long row then continues on the following screen lines under a blank gutter (`j`/`k` move by screen line, the title shows `[wrap]`; there is no horizontal scroll), tabs expand to 4-column stops and wide characters are never split. Binary files, renames without edits, mode changes and "no newline" markers get a note row; parser warnings (such as invalid UTF-8 in the patch) show as a red `!` row above the first file.
4. `q`, `Esc` or `h` goes back: first it clears an active search, then closes the diff pane and returns focus to the file list. A diff is capped at 400,000 rows and 100,000 lines per file (the last row, or the file's note row, says it was cut short); `L` loads the same commit again without the caps (the cursor stays on the same file; with nothing cut it only says so). Binary files have no content to load, so `L` leaves them as a note. The diff follows the graph selection: while the pane is open, moving the graph (or a refresh that moves the selection) loads the newly selected commit once its details have arrived and keeps the cursor on the same file if the new commit touches it; the old diff stays until then. The status bar shows the keys of the focused pane (sidebar, graph, detail, diff).

Still to design before their milestones:

2. Working-tree layout: review unstaged changes, stage whole files, hunks or selected lines in the built-in diff view, unstage, write the message, commit, amend.
3. Create/switch/rename/delete branch; set upstream; see ahead/behind.
4. Worktrees: add (new branch or existing), switch the app's active worktree, remove, prune, lock; see dirty state across all worktrees at a glance.
5. Merge, rebase onto, cherry-pick, revert from the graph.
6. Stash push/apply/pop/drop.
7. Fetch/pull/push with visible progress and cancel.

## Keymap

The single source of truth for bindings. Add a row in the same change that adds a binding; no conflicts within a mode/view. A test parses this table (columns `Mode/View | Key | Action`; keys in backticks, `Picker` rows are Insert mode, `Palette` rows Command mode, the rest Normal) and checks every key is a registered default in `DefaultKeymap` with no conflicts. Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `<lt>` a literal `<`, `:name` is an ex command and not a key. Modes: rows apply to Normal; Visual repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action.

| Mode/View | Key | Action |
|---|---|---|
| Global | `?` | Show keys for current view |
| Global | `:` / `Ctrl-p` | Command palette |
| Global | `q` | Back / quit from root view |
| Global | `Tab` / `Shift-Tab` | Next / previous pane |
| Lists | `j` `k` / `gg` `G` | Move / top / bottom |
| Lists | `/` `n` `N` | Search, next, previous (graph search prefixes: `author:`, `hash:`) |
| Global | `gt` / `gT` | Next / previous tab (counts: `3gt` goes to tab 3) |
| Global | `Alt-1`..`Alt-9` | Jump to tab N |
| Global | `Ctrl-t` / `<space>o` | Open repo picker in a new tab |
| Global | `Ctrl-w c` / `:tabclose` | Close tab |
| Global | `<space>p` | Pin/unpin repo |
| Global | `Ctrl-w h` `Ctrl-w j` `Ctrl-w k` `Ctrl-w l` | Focus pane left / down / up / right |
| Global | `Ctrl-w w` | Next pane |
| Global | `Ctrl-^` / `<space><Tab>` | Previously active tab (second binding for terminals that drop `Ctrl-^`) |
| Global | `<space>z` | Zoom focused pane |
| Global | `Ctrl-w >` `Ctrl-w <lt>` | Focused pane wider / narrower: moves the divider beside it by 2 columns (a count multiplies; dragging a divider with the mouse does the same) |
| Global | `Ctrl-w +` `Ctrl-w -` | Focused pane taller / shorter: moves the edge between the graph and the bottom panes by 1 row |
| Global | `Ctrl-w =` | Reset the pane sizes to automatic |
| Global | `<space>x` | Dismiss the status message |
| Global | `<space>m` | Show the status message history (overlay; `j`/`k` scroll, `Esc` closes) |
| Global | `Esc` | Cancel pending chord, clear search highlight, leave Insert mode; in the graph also dismiss the status message |
| Lists | `Ctrl-d` `Ctrl-u` | Half page down / up (mouse wheel scrolls) |
| Lists | `Down` `Up` `PageDown` `PageUp` | Move / page (same as `j` `k` `Ctrl-d` `Ctrl-u`) |
| Lists | `v` | Start / end range selection (Visual mode: motions move the far end, `Esc` / `q` / `v` end it; graph, detail and diff only) |
| Graph | `@` | Jump to HEAD |
| Graph | `gr` | Jump to ref or hash (fuzzy) |
| Graph | `Enter` | Focus commit details |
| Graph | `y` | Copy commit hash |
| Graph | `r` | Refresh repo |
| Detail | `Enter` | Open file diff |
| Detail | `y` | Copy the path of the selected file |
| Detail | `r` | Retry loading the commit details after a failure |
| Detail | `h` / `Esc` | Back to graph |
| Sidebar | `l` | Expand section; on a leaf, focus graph |
| Sidebar | `Enter` | Expand section; on a ref, select it in graph (focus stays) |
| Sidebar | `h` | Collapse section / parent |
| Sidebar | `/` | Filter entries |
| Sidebar | `y` | Copy the branch, tag or stash name, or the worktree path (OSC 52) |
| Diff | `]c` `[c` | Next / previous hunk |
| Diff | `]f` `[f` | Next / previous file |
| Diff | `q` / `Esc` / `h` | Back to file list |
| Diff | `/` `n` `N` | Search in diff, next, previous |
| Diff | `w` | Toggle word wrap (off by default; the cursor moves by screen line while on) |
| Diff | `y` | Copy the cursor line (with its `+`, `-` or space prefix) |
| Diff | `Y` | Copy the displayed rows of the file under the cursor (not a pasteable patch) |
| Diff | `L` | Load anyway: reload the commit without the row and per-file limits |
| Picker | `Up` `Down` | Move highlight |
| Picker | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases; terminals report `Ctrl-j` distinct from `Enter`) |
| Picker | `Enter` | Open repo (new tab or focus existing) |
| Picker | `Tab` | Complete path |
| Picker | `Alt-p` | Pin / unpin entry |
| Picker | `Alt-d` | Remove from recents |
| Palette | `Up` `Down` | Move highlight (typing filters; `Enter` runs the action, `Esc` closes) |

Mouse paths are listed in the next section.

## Mouse

Mouse input is optional and goes through the same action path as keys: the mouse map (`DefaultMouseMap`) turns a gesture on a view into the action id a key would produce, and the same handler runs it. A test parses the table below and checks it against the defaults, so add a row in the same change that adds a mouse path. Views inherit like in the keymap: `Lists` covers Graph, Detail, Sidebar and Diff. `Header`, `Tabs`, `Picker`, the overlays (`Help`, `Messages`, `Palette`, and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `PickerButton.*`, one view per button) are views of their own.

| View | Gesture | Action | Effect |
|---|---|---|---|
| Lists | Wheel up | `list.up` | Moves the selection up 3 rows (like `3k`) in the pane under the pointer, which gets focus |
| Lists | Wheel down | `list.down` | Moves the selection down 3 rows (like `3j`) |
| Lists | Drag start | `visual.toggle` | Pressing a row and moving the pointer starts Visual mode (range selection, like `v`) at the pressed row; graph, detail and diff only |
| Lists | Drag up | `list.up` | While dragging, the cursor follows the pointer: moves up by as many rows as the pointer is above it |
| Lists | Drag down | `list.down` | The same downwards |
| Graph | Double-click | `graph.open-details` | Same as `Enter`: focus the commit details |
| Detail | Double-click | `detail.open-diff` | Same as `Enter`: open the file diff |
| Sidebar | Click | `sidebar.activate` | Same as `Enter`: expand or collapse a section, select the ref's commit in the graph |
| Tabs | Click | `tab.next` | Switch to the clicked tab (same as `3gt` with the tab's number); closes an open picker |
| Header | Click | `palette.open` | Open the command palette: the menu of the actions of the focused pane and the global ones, each with its keys |
| Backdrop | Click | `cancel` | While the help, palette or status history is open, a click anywhere outside it closes it (like `Esc`) |
| Palette | Click | `command.execute` | Run the clicked entry (it is highlighted first) |
| Palette | Wheel up | `palette.previous` | Move the highlight up one row |
| Palette | Wheel down | `palette.next` | Move the highlight down one row |
| Help | Wheel up | `list.up` | Scroll the help up 3 lines (wherever the pointer is while the overlay is open) |
| Help | Wheel down | `list.down` | Scroll the help down 3 lines |
| Messages | Wheel up | `list.up` | Scroll the status history up 3 lines |
| Messages | Wheel down | `list.down` | Scroll the status history down 3 lines |
| DiffButton.PreviousFile | Click | `diff.previous-file` | Button `[<f]` in the diff title row, same as `[f` |
| DiffButton.NextFile | Click | `diff.next-file` | Button `[f>]`, same as `]f` |
| DiffButton.PreviousHunk | Click | `diff.previous-hunk` | Button `[<h]`, same as `[c` |
| DiffButton.NextHunk | Click | `diff.next-hunk` | Button `[h>]`, same as `]c` |
| DiffButton.Wrap | Click | `diff.toggle-wrap` | Button `[w]`, same as `w` |
| DiffButton.Zoom | Click | `pane.zoom` | Button `[z]`, same as `<space>z` |
| DiffButton.Close | Click | `navigate.back` | Button `[x]`, same as `q`: clears a search, then closes the diff |
| Picker | Double-click | `picker.accept` | Open the repository of the row |
| Picker | Wheel up | `picker.previous` | Move the highlight up one row |
| Picker | Wheel down | `picker.next` | Move the highlight down one row |
| PickerButton.Pin | Click | `picker.pin` | Button `[pin]` / `[unpin]` on the highlighted row, same as `Alt-p` |
| PickerButton.Remove | Click | `picker.remove-recent` | Button `[remove]` on a highlighted recent row, same as `Alt-d` |

Pointer-position behaviour that has no action of its own: a click on a pane focuses it, a click on a list row selects it (graph, detail, sidebar, diff; the details pane follows the graph selection as with keys), a click on a picker row highlights it. A click on empty space does nothing. The wheel moves the selection, not just the view (the keyboard has no pure scroll either), so the details pane follows it as it does for `j`/`k`. A mouse action does not touch a pending count or chord typed before it. While the help, palette, status history or which-key overlay is open, panes and the picker ignore every mouse gesture (no focus change, no selection), except that the wheel scrolls the help or history (or moves the palette highlight) wherever the pointer is; a click outside it closes it (`Backdrop` row), like `Esc`.

Drag to select: press a row of the graph, detail or diff pane and move the pointer; the pane takes focus, the pressed row becomes the anchor, Visual mode starts (status bar `VISUAL`) and the cursor follows the pointer, also above or below the pane while the button is held (no auto-scroll while the pointer rests: move it to keep going, #137). The range is drawn like the selected row. The release leaves the range active; a click on a row, `Esc`, `q`, `v` or moving focus to another pane or tab ends it. Nothing acts on a range yet: staging lines (#31) and copying text (#109) will. The sidebar has no ranges (a drag there only moves its selection).

Drag to resize: the divider between the sidebar and the graph, the divider between the detail and diff panes, and the title rows of the detail and diff panes (the edge between the graph and the bottom panes) follow the pointer while the button is held. Sizes are clamped to what fits (at least 12 columns for the sidebar and detail, 20 for the graph and diff, 3 rows for the bottom panes, 4 for the graph), belong to the tab and are not saved with the session (#138); `Ctrl-w =` (or `:`, then "Reset the pane sizes") returns to automatic, and `Ctrl-w >` `Ctrl-w <lt>` `Ctrl-w +` `Ctrl-w -` are the keyboard path. A zoomed pane has no dividers to drag.

Clickable titles and buttons: a click on the title row of the detail or diff pane focuses that pane. The diff title row ends in buttons (see the table) when the pane is at least 30 columns wider than the buttons need; narrower panes show only the rightmost ones (`[x]`, `[z]`, `[w]` first), the keys stay available. The highlighted picker row shows `[pin]`/`[unpin]` and, for a recent repository, `[remove]` at its right edge (screens of 60 columns or more). Clicking the header bar (first row) opens the command palette, which lists the actions of the focused pane and the global ones, so every action without a button (for example `L` load anyway, the search keys, `r` refresh) can be run with the mouse; typing a search or a filter text needs the keyboard, as nothing else can enter text.

Terminal.Gui turns on mouse reporting (SGR, mode 1006) at startup and turns it off on exit; there is no switch to disable it yet. Verified by writing SGR mouse sequences into the pane of a tmux 3.7c session running the Linux build (#64 and #100): clicks, double-clicks, the wheel, press-drag-release (reported as a press, position reports with the button held, a release and then a click at the release position, which the drag logic swallows), divider and title drags, header, palette and button clicks all arrive; a double-click is delivered as a click followed by a double-click event (so a double-click on a sidebar section toggles it once), and a sideways wheel (Ctrl or Shift plus wheel) is ignored (unit-tested only). Not yet verified with a physical mouse, over ssh, or in kitty, Windows Terminal and macOS terminals (tracked in #49). Terminals or ssh paths that drop mouse sequences leave the keyboard path unaffected.
