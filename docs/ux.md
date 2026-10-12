# UX

Spec for workflows and keys; the flows are written before the views. Keyboard and mouse are both fully supported: every action is reachable with either.

## Principles

- Modal and discoverable: Normal mode navigates, actions are short mnemonic chords, `?` shows keys for the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- Key bindings are the defaults, and users can remap them or add extra bindings in the config file (planned for M4; the keymap engine is data-driven from M1 so remapping needs no redesign). Vim-like motions everywhere: `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work.
- History layout: left sidebar (branches, remotes, tags, stashes, worktrees), center commit graph, right/bottom detail (commit info, files, diff). Working tree status is a pseudo-row at the top of the graph.
- Working-tree layout (staging): selecting the status row (or a key from anywhere) switches the center and detail panes to unstaged files, staged files and a diff/hunk view. Whole files, hunks or selected lines are staged and unstaged from the diff itself, then the commit message is written and committed from the same screen. Details of this flow are specified as workflow 2 below, before M2.
- Branches and worktrees are first-class, not a submenu: they are always visible in the sidebar and every branch shows which worktree holds it.
- Destructive actions confirm and say what they will do and how to undo it.
- Mouse is optional but complete: everything doable by keyboard is doable with the mouse (click to focus/select, double-click to open, wheel to scroll, clickable tabs, buttons and menus for actions, drag to select ranges and resize panes). Nothing is keyboard-only or mouse-only; mouse paths are listed in the keymap notes below and in each flow.

## Workspace and tabs

- The app opens with or without a repo: `gitunia` (restore last session, or repo picker if none), `gitunia <path>` (open that repo as a tab, also from outside any repo; a subdirectory resolves to its repo root).
- Tab bar shows repo name (path on disambiguation), branch and a dirty/ahead/behind marker, and background-activity state.
- Repo picker: fuzzy search over recent, pinned and discovered repos (configurable scan roots), open by typing a path, close/pin entries. Opening an already-open repo focuses its tab.
- Each tab keeps its own selection, scroll, filters and focused pane.  Closing the active tab activates its right neighbour (left if it was last).

## Modes

Normal (navigate, act), Visual (select a range of lines/commits), Insert (text input: commit message, filters), Command (`:` palette), Confirm (y/n dialogs).

## Focus model and key style

- Panes: sidebar (left), graph (center), detail file list (right/bottom) and diff (beside or below the detail pane, replacing the commit info when open). `Tab` cycles in the order sidebar, graph, detail, diff (skipping panes that are closed); `Shift-Tab` reverses. Directions follow that layout. `Ctrl-w` + `h` `j` `k` `l` moves focus by direction, `Ctrl-w w` cycles, `Ctrl-w c` closes the tab. Both exist; the focused pane has a highlighted border and its own status hints.
- Frequent actions are single keys per view (`Enter`, `y`, `/`); global actions use the `<space>` leader (`<space>o` open repo, `<space>p` pin, `<space>z` zoom pane). `g` and `]`/`[` start chords; counts prefix motions (`5j`, `3gt`).
- Chord engine: a pending prefix shows in the status bar and, after 500 ms, a which-key popup listing the continuations; `Esc` cancels; there is no chord timeout. Bindings are data (mode, view, key sequence, action id), so remapping in M4 needs no redesign.
- Help and palette (#20): `?` opens a help overlay with the keys of the focused view (its own bindings, then the inherited `Lists` and `Global` ones), generated from the keymap table and the action titles in `ActionCatalog`, never hand-written; `j`/`k`/`Ctrl-d`/`gg` scroll it and `Esc`, `q` or `?` close it (every other action is ignored while it is open). `:` or `Ctrl-p` opens the command palette in Command mode: typing filters every action fuzzily (title or action id), `Up`/`Down` move, `Enter` runs the highlighted action and `Esc` closes; each row shows its bindings, and the focused view's own actions come first. The which-key popup is the same kind of overlay, docked above the status bar.
- `Esc` precedence: cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph). `q` goes back one level the same way and quits only from the graph with nothing pending; `h` is back in Detail and Diff and collapse in Sidebar (Diff has no horizontal scroll).
- In Insert mode (search box, picker input) printable keys are text; only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act, so the leader and chords never fire while typing.

## Workflows

Each later workflow (2-7 at the end of this section) needs a written step-by-step key flow here before it is implemented. Flows 0 and 1 are specified now.

### 0. Open a repo, switch tabs, reopen the session

1. `gitunia` restores the last session (tabs, active tab, per-tab selection/focus). With no session it opens the repo picker. `gitunia <path>` opens that path as a tab (subdirectory resolves to the repo root; a repo already open just gets focused) and keeps the other restored tabs.
2. `Ctrl-t` or `<space>o` opens the repo picker (Insert mode, input focused). It lists open, pinned, recent and discovered repos, fuzzy-filtered as you type; `Up`/`Down` and `Ctrl-j`/`Ctrl-k` move; `Enter` opens in a new tab (or focuses the existing tab); `Backspace` deletes a character; `Esc` cancels (with no tab open it quits instead, since there is nothing to go back to). Discovered repositories come from a scan of the conventional source folders below the home directory (`Projects`, `src`, `code`, `dev`, `repos`, `git`, `work`, ... up to three levels deep, hidden and dependency folders skipped); configurable scan roots arrive with the config file in M4. Rows show tags (`open`, `pinned`, `recent`, `found`); a click highlights a row and a double-click opens it.
3. Typing something that starts with `/`, `~/`, `./`, `../` or `C:\` is treated as a path (a drive letter needs `C:\` or `C:/`; others need `/`, `~/`, `./` or `../`; anything else is a fuzzy query): the list shows the matching directories (repositories marked), `Tab` completes to the common prefix and then to the highlighted directory, `Enter` opens the resolved path (relative paths are taken from the active repository, or the home directory, never from the process working directory); a non-repo path shows an inline "not a git repository" message and stays open.
4. In the picker `Alt-p` pins or unpins the highlighted entry and `Alt-d` removes it from recents (never deletes anything on disk).
5. Switch tabs with `gt` / `gT` (or `3gt`), `Alt-1`..`Alt-9`, or `Ctrl-^` or `<space><Tab>` for the previously active tab. Mouse: click a tab. Tab bar format: ` N name  branch markers `, with `*` for a dirty working tree, `↑n` ahead, `↓n` behind (`+n`/`-n` without Unicode) and an ellipsis while the tab loads; two tabs with the same name show their parent directory; when the tabs do not fit the row scrolls to keep the active one visible (`<` marks hidden tabs on the left). Markers reflect the state when the tab was loaded (live refresh comes with file watching). `Ctrl-w c` closes the tab; closing the last tab shows the picker.
6. A tab whose repo disappeared (moved or deleted) stays in the bar marked `! name (missing)`; `Enter` on it opens the picker (Locate repository, starting in the folder that held it) and the chosen repository replaces that tab, `Ctrl-w c` closes it.
7. Terminal key caveats, verified against Terminal.Gui 2.5.0 under tmux: `Ctrl-j` (0x0A) arrives as `Ctrl+Enter`, distinct from `Enter` (0x0D), so it works as a picker alias for `Down` (the translator maps it to `Ctrl-j`; a terminal that sends the same byte for both would break the alias, `Down` still works); `Ctrl-^` (0x1E) arrives as `Ctrl+6` and is mapped to `Ctrl-^`, so it works, and `<space><Tab>` remains the fallback for terminals that swallow it; some terminals send `Backspace` as `Ctrl-h`, which the picker treats as `Backspace`.

### 1. Browse the graph, jump, search

1. A repo tab opens with focus on the graph, the newest commit selected and HEAD marked. Commits stream in; the list is usable immediately and the status bar shows loading progress.
2. Move with `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G`, counts and the mouse wheel. `@` jumps to HEAD; `gr` opens a fuzzy ref list (branches, tags, remotes) that also accepts a hash prefix, and `Enter` selects that commit.
3. `Enter` on a commit moves focus to the detail pane (commit info and changed files); `Esc` or `h` returns to the graph. The detail pane also follows the graph selection while the graph is focused. The pane sits below the graph (hidden on screens under 14 rows) and lists, from the top: hash, author, committer (only when different), parents, refs, the full message and the changed files with their status (`A` `M` `D` `T`, `R<score> old -> new`, `C<score> old -> new`; merges show the changes against the first parent). While focused, `j`/`k` scroll through all rows and focusing lands on the first file row; opening a file's diff arrives with #25.
4. `/` opens the search box (Insert mode). Plain text matches the subject and body; `author:` and `hash:` prefixes match the author or hash prefix. `Enter` jumps to the first match and returns to Normal mode, `n`/`N` step through matches, `Esc` in Normal mode clears the highlight. Matches outside the loaded range load on demand and show progress.
5. `y` copies the selected commit hash (OSC 52); `r` refreshes the repo now.

### 1b. Sidebar navigation

1. `Ctrl-w h` (or `Tab` cycling) focuses the sidebar (sections: branches, remotes, tags, worktrees, stashes). `j`/`k` move, `l`/`Enter` expand a section, `h` collapses it (or moves to the parent).
2. `Enter` on a branch, remote branch or tag selects its commit in the graph and keeps focus in the sidebar; `l` on a leaf moves focus to the graph. Each branch shows the worktree that holds it.
3. `/` filters the sidebar entries by name; `Esc` clears the filter.

### 1c. Diff viewing

1. In the detail pane select a file with `j`/`k` and press `Enter`: the diff pane shows that file's unified diff and takes focus. `<space>z` zooms the focused pane to full screen and back.
2. `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G` scroll; `]c` / `[c` jump to the next / previous hunk and `]f` / `[f` to the next / previous file of the commit. `/` and `n`/`N` search within the diff.
3. `q`, `Esc` or `h` returns focus to the file list. Binary and oversized files show a placeholder with an explicit key to load them anyway.

Still to design before their milestones:

2. Working-tree layout: review unstaged changes, stage whole files, hunks or selected lines in the built-in diff view, unstage, write the message, commit, amend.
3. Create/switch/rename/delete branch; set upstream; see ahead/behind.
4. Worktrees: add (new branch or existing), switch the app's active worktree, remove, prune, lock; see dirty state across all worktrees at a glance.
5. Merge, rebase onto, cherry-pick, revert from the graph.
6. Stash push/apply/pop/drop.
7. Fetch/pull/push with visible progress and cancel.

## Keymap

The single source of truth for bindings. Add a row in the same change that adds a binding; no conflicts within a mode/view. A test parses this table (columns `Mode/View | Key | Action`; keys in backticks, `Picker` rows are Insert mode, `Palette` rows Command mode, the rest Normal) and checks every key is a registered default in `DefaultKeymap` with no conflicts. Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `:name` is an ex command and not a key. Modes: rows apply to Normal; Visual repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action.

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
| Global | `Esc` | Cancel pending chord, clear search highlight, leave Insert mode |
| Lists | `Ctrl-d` `Ctrl-u` | Half page down / up (mouse wheel scrolls) |
| Lists | `Down` `Up` `PageDown` `PageUp` | Move / page (same as `j` `k` `Ctrl-d` `Ctrl-u`) |
| Lists | `v` | Start / end range selection (Visual mode) |
| Graph | `@` | Jump to HEAD |
| Graph | `gr` | Jump to ref or hash (fuzzy) |
| Graph | `Enter` | Focus commit details |
| Graph | `y` | Copy commit hash |
| Graph | `r` | Refresh repo |
| Detail | `Enter` | Open file diff |
| Detail | `h` / `Esc` | Back to graph |
| Sidebar | `l` | Expand section; on a leaf, focus graph |
| Sidebar | `Enter` | Expand section; on a ref, select it in graph (focus stays) |
| Sidebar | `h` | Collapse section / parent |
| Sidebar | `/` | Filter entries |
| Diff | `]c` `[c` | Next / previous hunk |
| Diff | `]f` `[f` | Next / previous file |
| Diff | `q` / `Esc` / `h` | Back to file list |
| Diff | `/` `n` `N` | Search in diff, next, previous |
| Picker | `Up` `Down` | Move highlight |
| Picker | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases; terminals report `Ctrl-j` distinct from `Enter`) |
| Picker | `Enter` | Open repo (new tab or focus existing) |
| Picker | `Tab` | Complete path |
| Picker | `Alt-p` | Pin / unpin entry |
| Picker | `Alt-d` | Remove from recents |
| Palette | `Up` `Down` | Move highlight (typing filters; `Enter` runs the action, `Esc` closes) |

Mouse paths (all actions): click focuses a pane or selects a row, double-click opens (commit details, file diff, repo in picker), wheel scrolls, click a tab switches, click a sidebar ref selects it, click a hunk/file marker or use the pane header buttons to jump and zoom, picker rows open on double-click with pin/remove buttons per row. (More rows come with workflows 2-7.)

