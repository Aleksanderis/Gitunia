# UX

Spec for workflows and keys; the flows are written before the views. Keyboard and mouse are both fully supported: every action is reachable with either.

## Principles

- Modal and discoverable: Normal mode navigates, actions are short mnemonic chords, `?` shows keys for the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- Key bindings are the defaults, and users can remap them or add extra bindings in the config file (planned for M4; the keymap engine is data-driven from M1 so remapping needs no redesign). Vim-like motions everywhere: `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work.
- History layout: left sidebar (branches, remotes, tags, stashes, worktrees), center commit graph, right/bottom detail (commit info, files, diff). The working tree is a separate view of the tab (workflow 2), not a graph row.
- Working-tree layout (staging): a separate view of the tab (owner decision, #28): Unstaged and Staged file lists on the left, the diff on the right, a commit message box below it with SignOff, No-Verify and Amend options. Whole files, hunks or selected lines are staged and unstaged from there. Workflow 2 specifies the keys; it opens with `<space>s` or `gs` and closes with `q`/`Esc`.
- Branches and worktrees are first-class, not a submenu: they are always visible in the sidebar and every branch shows which worktree holds it.
- Destructive actions confirm and say what they will do and how to undo it.
- Mouse is optional but complete: everything doable by keyboard is doable with the mouse (click to focus/select, double-click to open, wheel to scroll, clickable tabs, buttons and menus for actions, drag to select ranges and resize panes). Nothing is keyboard-only or mouse-only; mouse paths are listed in the Mouse section below.

## Workspace and tabs

- The app opens with or without a repository: `gitunia` (restore last session, or repository picker if none), `gitunia <path>` (open that repository as a tab, also from outside any repository; a subdirectory resolves to its repository root).
- Tab bar shows repository name (path on disambiguation), branch and a dirty/ahead/behind marker, and background-activity state.
- Repository picker: fuzzy search over recent, pinned and discovered repositories (configurable scan roots), open by typing a path, close/pin entries. Pinning (#218) keeps a repository at the top of the picker, right after the open tabs and above recents and discovered repositories, however long ago it was opened; pins are saved in the session file and never expire (recents are limited to the last 50). Opening an already-open repository focuses its tab.
- Each tab keeps its own selection, scroll, filters and focused pane.  Closing the active tab activates its right neighbour (left if it was last).

## Modes

Normal (navigate, act), Visual (select a range of lines/commits), Insert (text input: commit message, filters), Command (`:` palette), Confirm (y/n dialogs).

## Focus model and key style

- Panes: sidebar (left), graph (center), detail pane with Details, Changes and Files tabs (right/bottom) and diff (the diff belongs to the Changes tab and sits beside its file list; on screens too narrow for both it opens on demand and replaces the detail pane, see 1c). `Tab` cycles in the order sidebar, graph, detail, diff (skipping panes that are closed); `Shift-Tab` reverses. Directions follow that layout (sidebar and graph on top, detail and diff below them). `Ctrl-w` + `h` `j` `k` `l` moves focus by direction (`j` from the sidebar or graph goes to the detail pane, or the diff when the detail pane is hidden; `k` from a bottom pane goes to the graph), `Ctrl-w w` cycles, `Ctrl-w c` closes the tab. Both exist; the focused pane has a highlighted border and its own status hints.
- Frequent actions are single keys per view (`Enter`, `y`, `/`); global actions use the `<space>` leader (`<space>o` open repository, `<space>p` pin, `<space>z` zoom pane). `g` and `]`/`[` start chords; counts prefix motions (`5j`, `3gt`).
- Chord engine: a pending prefix shows in the status bar and, after 500 ms, a which-key popup listing the continuations; `Esc` cancels; there is no chord timeout. Bindings are data (mode, view, key sequence, action id), so remapping in M4 needs no redesign.
- Help and palette (#20): `?` opens a help overlay with the keys of the focused view (its own bindings, then the inherited `Lists` and `Global` ones), generated from the keymap table and the action titles in `ActionCatalog`, never hand-written; `j`/`k`/`Ctrl-d`/`gg` scroll it and `Esc`, `q` or `?` close it (every other action is ignored while it is open). `:` or `Ctrl-p` opens the command palette in Command mode: typing filters every action fuzzily (title or action id), `Up`/`Down` move, `Enter` runs the highlighted action and `Esc` closes; each row shows its bindings, and the focused view's own actions come first. Modal-only actions (the `confirm.*` answers, the picker's own keys, the palette's `Up`/`Down`, text input accept and `Esc`; `ActionCatalog.IsModalOnly`) are not listed, and actions bound ahead of their feature (`ActionCatalog.Unimplemented`) are listed greyed out (#96). The which-key popup is the same kind of overlay, docked above the status bar.
- Key notation and columns (#242): keys are written spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `gg`, `<space>o`), never as `^d` or `c-d`; the help overlay, the palette, the which-key popup and the status bar hints all use it. The help overlay shows the keys and the description in two columns separated by a vertical line (`Ctrl-d │ Half page down`), and every status bar hint is a key with a short description (`j/k move  gg/G top/end`, keys in the accent colour, `PaneHints` returns `KeyHint` records and a test fails for a hint without a description). Every hint also names the `ActionId` of each of its keys (the description stays short and is not taken from the action titles), and a second test fails for a key that is not bound to that action in the default keymap for that pane (#273, #287). The status messages overlay uses the same two columns (severity, text). The help text for `Alt-1`..`Alt-9` also names the multiplexer-safe `3gt`.
- Status messages (#93): background work that fails (worktree, stash or status reads of a tab, a failed session save, a session file that was reset at start-up) reports through one `StatusChannel` in `Gitunia.Core.Status`; later features post to it the same way. The newest message shows in the status bar between the position and the key hints (errors in the error colour) and stays until dismissed with `<space>x`, or `Esc` in the graph when nothing else claims it, or until a newer one replaces it. `<space>m` opens the history (the last 50, newest first; long messages wrap) as an overlay; a failed graph load is posted there too; it updates live: a message posted while it is open appears at the top without reopening. Tab-scoped messages start with the repository name, and a message equal to the one showing is not posted again. A key action's own one-off notice (`HEAD is not loaded yet`) still takes the place of the message while it lasts. The status bar hints start with `? help  : commands  q quit`, so a narrow terminal cuts the pane's own hints, never those.
- `Esc` precedence: cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph); in the graph, when nothing else claims it, `Esc` dismisses the status message. `q` always quits (#333), from any pane, except that it first closes an open overlay or ends a Visual range; `h` is back in Detail and Diff and collapse in Sidebar, `l` is forward (Detail to the diff, Sidebar expand; Diff has no horizontal scroll).
- In Insert mode (search box, picker input) printable keys are text; only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act, so the leader and chords never fire while typing.

## Workflows

Each later workflow (3-7 at the end of this section) needs a written step-by-step key flow here before it is implemented. Flows 0, 1 and 2 are specified now.

### 0. Open a repository, switch tabs, reopen the session

1. `gitunia` restores the last session (tabs, active tab, per-tab selection/focus). With no session it opens the repository picker. `gitunia <path>` opens that path as a tab (subdirectory resolves to the repository root; a repository already open just gets focused) and keeps the other restored tabs.
2. `Ctrl-t` or `<space>o` opens the repository picker (Insert mode, input focused). It lists open, pinned, recent and discovered repositories, fuzzy-filtered as you type; `Up`/`Down` and `Ctrl-j`/`Ctrl-k` move; `Enter` or `Ctrl-l` opens in a new tab (or focuses the existing tab); `Backspace` deletes a character; `Tab` or `Right` accepts the suggestion (#232: the highlighted repository's path is filled in and its subfolders are listed; in a typed path it completes the folder name); typing and matching ignore case (every segment of a typed path, directory part included, is resolved against the real folder names, so `~/projects/x` lists `~/Projects/x` on Linux; the inserted text keeps the folder's real case); `Esc` cancels (with no tab open it quits instead, since there is nothing to go back to). Discovered repositories come from a scan of the conventional source folders below the home directory (`Projects`, `src`, `code`, `dev`, `repos`, `git`, `work`, ... up to three levels deep, hidden and dependency folders skipped); configurable scan roots arrive with the config file in M4. A typed path is listed off the UI thread: the previous rows stay until the new listing arrives (`scanning...` appears after about 150 ms when there are none), a newer keystroke cancels the older listing, and a listing slower than 5 s ends with a "timed out" notice. Rows show tags (`open`, `pinned`, `recent`, `found`); a click highlights a row and a double-click opens it.
3. Typing something that starts with `/`, `~/`, `./`, `../` or `C:\` is treated as a path (a drive letter needs `C:\` or `C:/`; others need `/`, `~/`, `./` or `../`; anything else is a fuzzy query): the list shows the matching directories (repositories marked), `Tab` completes to the common prefix and then to the highlighted directory, `Enter` opens the resolved path (relative paths are taken from the active repository, or the home directory, never from the process working directory); a non-repository path shows an inline "not a git repository" message and stays open.
4. In the picker `Alt-p` (or the `[pin]` / `[unpin]` button on the highlighted row) pins the highlighted entry to the top of the list or unpins it (a pinned row is tagged `pinned`; `<space>p` does the same for the repository of the active tab) and `Alt-d` removes it from recents (never deletes anything on disk).
5. Switch tabs with `gt` / `gT` (or `3gt`), `Alt-1`..`Alt-9`, or `Ctrl-^` or `<space><Tab>` for the previously active tab. Mouse: click a tab. Tab bar (#219, #220, #221): one row on the bar background (the status bar's, so the screen is bracketed by the two bars), a margin, then every tab as a chip with two columns of padding on each side, ` N name markers `, one blank column between tabs and a ` + ` button after the last tab that opens the picker (#232; `Ctrl-t`). The active tab is the filled accent chip, the others are buttons (#240, `SegmentRole.RepositoryTab`): the same padded chip on a subtle tinted background (the dim selection tint), so they read as clickable; without background colours they are underlined, in ASCII bracketed `[ 2 name ]` (same width). The active one stays clear without colour (it is the only chip in the accent colour; with `NO_COLOR` the chip is reverse video). The name is the repository name, or the name you gave the tab with `Ctrl-w r` (a status bar prompt that starts with the current name; `Enter` saves it, an empty name restores the repository name, `Esc` cancels; the name is saved with the tab in the session file). The branch is not part of the tab (it made tabs very long; the sidebar marks the current branch with `*`); `TabLabelFactory` has a switch for showing it that a future config option can set (#46). Tabs that do not fit are left out of the row (the active one always shows), names are cut at 40 characters. Markers: `*` for a dirty working tree, `↑n` ahead, `↓n` behind (`+n`/`-n` without Unicode) and an ellipsis while the tab loads; two tabs with the same name show their parent directory; when the tabs do not fit the row scrolls to keep the active one visible (`<` marks hidden tabs on the left). Markers follow external git activity (file watching; `r` refreshes manually). `Ctrl-w c` closes the tab; closing the last tab shows the picker.
6. A tab whose repository disappeared (moved or deleted) stays in the bar marked `! name (missing)`; `Enter` on it opens the picker (Locate repository, starting in the folder that held it) and the chosen repository replaces that tab, `Ctrl-w c` closes it.
7. Terminal key caveats, verified against Terminal.Gui 2.5.0 under tmux: `Ctrl-j` (0x0A) arrives as `Ctrl+Enter`, distinct from `Enter` (0x0D), so it works as a picker alias for `Down` (the translator maps it to `Ctrl-j`; a terminal that sends the same byte for both would break the alias, `Down` still works); `Ctrl-^` (0x1E) arrives as `Ctrl+6` and is mapped to `Ctrl-^`, so it works, and `<space><Tab>` remains the fallback for terminals that swallow it; some terminals send `Backspace` as `Ctrl-h`, which the picker treats as `Backspace`.

### 1. Browse the graph, jump, search

1. A repository tab opens with focus on the graph, the newest commit selected and HEAD marked. Commits stream in; the list is usable immediately and the status bar shows loading progress.
   A graph row is a table (#168, #217): branch lanes (the message starts two columns after the last line of the row's own lanes, so with many branches open the rows that only use the first lanes keep their message close to the line; author, date and hash stay in fixed columns and the message takes the room the lanes left free), message (ref decorations then subject), author, date and time (`yyyy-MM-dd HH:mm`, the author's local date and time, #318) and short hash (dimmed). The message column is the flexible one and takes all the width the other columns leave (no cap, #198), so a wide terminal is used in full and nothing else is squeezed; author is 16 columns (ellipsis when longer), 20 when the row has 140 or more columns left after the lanes, date 16 (10, date only, when the time does not fit), hash 7. The columns after the message sit one blank column apart on compact rows; from 100 columns left after the lanes (#244, for example 160x45) the gaps are three columns and the row keeps a two-column right margin, so author, date and hash neither crowd each other nor touch the frame (80x24 is unchanged, except for the time column, #318). The message column gives up 7 columns at that threshold (the wider gaps and the margin), which is intended. Narrow terminals drop columns instead of squeezing the message below 30 columns: hash first, then the time (the date stays, 10 columns), then the date, then author; they return when the terminal widens (`gitunia <repository> --print-frame WxH` shows any width). Ref labels (#214): the symbolic `origin/HEAD` is hidden; a local branch and its remote-tracking branch (same name) on the same commit are one label, `main ↔` (ASCII: `main (r)`); the branch HEAD is on gets a `◆` before it (ASCII: `HEAD -> main`). A remote branch with no local branch of that name, or on another commit, keeps its own `origin/x` label. Example at 120 columns (without the sidebar):
   ```
   ●  Fix parser crash on empty input                Ada Lovelace       2026-10-13 09:00   7a21f84
   ●  (◆ main ↔) Merge branch 'topic'                Alexander the G…   2026-10-12 23:30   ee3f455
   ```
   Current branch (#301): the first-parent chain of the checked-out branch always runs in the leftmost lane, as a straight line from its tip down; branches ahead of it (such as `origin/main`, however many commits ahead) continue it upwards in the same lane, while other branches, merged features and remote tips sit to its right and join it from the right (a detached HEAD has no such lane and keeps the plain leftmost-free-lane layout).

   Straight lines (#238): a commit with one child and one parent on its own lane is drawn as part of the line (`│`, ASCII `|`), not as a node; node dots (`●`, merge `○`; ASCII `*`, `o`) appear only where a lane starts, ends, forks or merges. A commit that carries a ref (branch, tag, remote, HEAD) keeps its node dot even in the middle of a line, so labels never hang on a bare line (#280).
2. Move with `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G`, counts and the mouse wheel. While the log is still streaming in, `G` goes to the last loaded commit and stays there (it does not follow the stream); the status bar says `Still loading: at the last loaded commit` (#194); the hint goes away when the load finishes and a plain `G` only (not `5G`) shows it. `@` jumps to HEAD; `gr` opens a prompt in the status bar (Insert mode) that fuzzy-filters refs (branches, tags, remotes) and also accepts a hash prefix of at least 4 hex digits; the best candidate is shown, `Up`/`Down`/`Tab` cycle through the others, `Enter` selects that commit and `Esc` cancels.
3. `Enter` on a commit moves focus to the detail pane; `Esc` or `h` returns to the graph. The detail pane follows the graph selection while the graph is focused. The pane sits below the graph (hidden on screens under 14 rows) and has three tabs, shown as buttons on its title row (#169, #198): **Details** (hash, author, committer only when different, parents, refs and the full message), **Changes** (the changed files with their status `A` `M` `D` `T`, `R<score> old -> new`, `C<score> old -> new`; merges show the changes against the first parent) and **Files** (the file tree of the commit; only a placeholder, "not implemented yet", for now). A fresh tab opens on Changes. `]` / `[` switch to the next / previous tab (wrapping) and `gd` `gc` `gf` jump to Details, Changes, Files, from the graph or the detail pane; a click on a tab label does the same. The selected tab is part of the repository tab's view: it is kept while the graph selection moves, is saved with the session and each tab remembers its own cursor row for the selected commit. While focused, `j`/`k` scroll through the rows of the tab; on Changes, focusing lands on the first file row, the diff of that file is already shown beside the list and `j`/`k` show the diff of each file as the cursor reaches it, with no `Enter` (1c); `Enter` or `l` moves focus into the diff (on the other tabs they only say to open Changes). When the details fail to load the pane says so and `r` in the detail pane (or the palette) retries. The title row shows the file count (`.. files` until the details of the commit have arrived, instead of a `0 files` that flickers, #223); the rows of the tabs start at the same left edge (the summary line, the labels, the file marks and the placeholder; only the commit message is indented).
4. `/` opens the search box (Insert mode). Plain text matches the commit subject (the message body is not searched yet: graph rows keep only the subject), `author:` matches the author name and `hash:` the start of the hash, all case-insensitive. Matches are highlighted in the rows while typing and the status bar shows the count (`/fix_  3/17`). `Enter` jumps to the first match at or after the selection and returns to Normal mode, `n`/`N` step through matches (counts work, the search wraps), `Esc` in the box cancels, `Esc` in Normal mode clears the highlight. The search scans the loaded commits in small slices between key presses and keeps scanning as the log streams in, so matches beyond the loaded range appear as they load and the status bar shows `searching N%` meanwhile. In the box `Backspace`, `Ctrl-u` and `Ctrl-w` edit the text.
5. `y` copies the selected commit hash (OSC 52); `r` refreshes the repository now.

### 1b. Sidebar navigation

1. `Ctrl-w h` (or `Tab` cycling) focuses the sidebar (sections: branches, remotes, tags, worktrees, stashes). `j`/`k` move, `l`/`Enter` expand a section, `h` collapses it (or moves to the parent).
2. `Enter` on a branch, remote branch or tag selects its commit in the graph and keeps focus in the sidebar; `l` on a leaf moves focus to the graph. Each branch shows the worktree that holds it.
3. `/` filters the sidebar entries by name (Insert mode: type, `Backspace` erases, `Enter` keeps the filter and returns to Normal mode, `Esc` drops it). The filter also matches the worktree shown beside a branch and unfolds every section with a match; in Normal mode `Esc` clears an active filter, and otherwise (like `q`) returns focus to the graph.
4. A section header shows its entry count; Remotes and Tags are folded by default. A branch shows `wt:<directory>` for the worktree that has it checked out (`*` marks the current branch and the current worktree) and `↑n↓m` against its upstream. A stash is not a graph node, so `Enter` on it only says so. The sidebar is hidden below 60 columns.

### 1c. Diff viewing

1. The diff is part of the Changes tab, not a mode of its own (owner decision of 2026-10-13, see `docs/history/ux.md`). While Changes is the selected tab the diff pane is shown beside the file list: it holds the whole commit's unified diff (against the first parent, so merge commits match the file list) with the header of the file under the file cursor at the top. It is there as soon as the tab is, for the first file; moving the file cursor with `j`/`k` (or the mouse, a click on a row) shows that file's diff at once, no `Enter` needed, and selecting another commit in the graph loads that commit's diff at its first file (a load in flight is cancelled when another commit is asked for). `Enter` or `l` on the file list moves focus into the diff to scroll, search and copy; `h` (or `Esc`) comes back to the file list and the diff stays. On the Details and Files tabs the pane is not shown. Narrow or short screens (below the side-by-side width of 72 columns, or without the bottom strip) keep the on-demand behaviour: the diff opens with `Enter` or `l` on a file row, takes focus and replaces the detail pane (screens under 14 rows zoom it), and `Esc` or `h` closes it. The diff pane sits beside the compact file list (a third of the width, 24-60 columns) on screens of 72 columns or more, so both are visible together at 80 columns (a diff opened on demand on a narrow screen that is then widened stays open and also keeps showing a Details or Files tab beside it, following the graph selection). `<space>z` zooms the focused graph, detail or diff pane to the full area between the tab row and the status bar and back (the sidebar does not zoom; moving focus to another pane ends the zoom). While zoomed, `Tab` only reaches panes that are shown.
2. `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G` scroll; `]c` / `[c` jump to the next / previous hunk and `]f` / `[f` to the next / previous file of the commit (counts work; at the last one the status bar says so). The title shows the commit, the file under the cursor and `file n/m  hunk n/m`. `/` opens a search box (Insert mode, case-insensitive substring over every row including headers; matches are highlighted while typing); `Enter` jumps to the first match at or after the cursor, `n`/`N` step through matches and wrap, `Esc` in the box cancels. Mouse: the buttons at the right of the title row (`‹ hunk`, `hunk ›`, `wrap`, `zoom`, drawn as chips like the tabs) are `[c` `]c` `w` and `<space>z`; there are no file buttons, files are chosen in the file list (#310). Quick repeated clicks on a button each count: the toolkit reports the second and third click of a burst as a double and a triple click, which buttons treat as clicks (#310); a drag selects a range of rows (Mouse section).
3. Rows are `old new` line numbers, a `+`/`-`/space marker and the text; text is cut with an ellipsis while word wrap is off (the default); `w` turns wrap on, and a long row then continues on the following screen lines under a blank gutter (`j`/`k` move by screen line, the title shows `[wrap]`; there is no horizontal scroll), tabs expand to 4-column stops and wide characters are never split. Binary files, renames without edits, mode changes and "no newline" markers get a note row; parser warnings (such as invalid UTF-8 in the patch) show as a red `!` row above the first file.
4. Diff options (#312, per tab, not saved with the session): `+`/`-` show 3 more / fewer unchanged lines around each change (title `[ctx N]`), `F` shows whole files (`[file]`), `W` ignores whitespace-only changes (`[-w]`). Each reloads the commit with git's `-U<n>` / `-w` (the cache is dropped, the cursor returns to the same file). Each has a title button (`ctx-`, `ctx+`, `file`, `-w`, `split`, see the mouse table) as well as its key. In whole-file mode `]c` / `[c` still visit each group of changes, grouped the way git's own hunks would be for the current context (changes closer than twice the context lines are one hunk), the cursor landing where that hunk's context starts, and the title's `hunk n/m` counts those groups, so the file is never one hunk (the grouping uses the context set with `+`/`-`, 3 by default, not a `diff.context` from the user's config). The options reset when the tab's repository is reloaded; a user's `diff.context` config applies until the first change. Files whose changes are whitespace-only stay in the file list but have no diff while `[-w]` is on. Visible whitespace characters are not done yet (registered as an issue).
5. Side by side (#315, `S` or the `split` button, per tab, not saved; title `[split]`): the old side on the left with its line numbers, the new side on the right, a vertical rule between; unchanged lines appear on both sides, a run of removed lines is paired line by line with the added lines after it (the shorter side stays blank), file and hunk headers and notes span both sides. It needs a pane of 80 text columns; on a narrower pane the unified rows stay and the title says `[split 80+]` (zoom with `<space>z` to get room). The cursor is one line of the pair: it is the old side's row (the new side's for an added-only line), so `y` copies that row, `Y` still copies the file's rows in diff order and a Visual range copies the rows from the first row of its first line to the last row of its last line, in diff order. Wrap (`w`) wraps each side inside its half and a line takes as many screen lines as its longer side. Search, `]c` `]f` and the diff options work as in the unified view (a match on either side puts the cursor on its line, and `n`/`N` continue from the matched row).
6. `Esc` or `h` goes back: first it clears an active search, then returns focus to the file list (beside the file list the diff stays; an on-demand diff closes). If the tab is changed away from Changes while the diff has focus, focus returns to the detail pane. A diff is capped at 400,000 rows and 100,000 lines per file (the last row, or the file's note row, says it was cut short); `L` loads the same commit again without the caps (the cursor stays on the same file; with nothing cut it only says so). Binary files have no content to load, so `L` leaves them as a note. The diff follows the graph selection: while the pane is shown, moving the graph (or a refresh that moves the selection) loads the newly selected commit once its details have arrived; beside the file list it shows the file under the file cursor (the first one for a new commit), an on-demand diff keeps the cursor on the same file if the new commit touches it; the old diff stays until then. A resize that makes room for the diff loads it at once; one that takes the room away returns focus from the diff to the file list. The status bar shows the keys of the focused pane (sidebar, graph, detail, diff).

### 2. Working-tree view: stage, commit, amend, discard (design for #29-#33, #51, #314)

Owner decision (2026-10-15, #28): the working tree is a separate view of the tab, not a graph pane or pseudo-row. The keys below are planned: they are listed here, not in the Keymap table, because the keymap test requires every row there to be a registered default. The implementing issue moves its rows into the Keymap table together with the `DefaultKeymap` entries, `ActionIds` and hints (views `WorkingUnstaged`, `WorkingStaged`, the two diffs and `WorkingMessage`; they inherit `Lists` and `Global`, and the diff the existing `Diff` navigation rows).

**Layout.** Left column: `Unstaged` list on top (conflicted files first, in their own group, then modified, deleted, renamed and untracked files, each with its status letter and path) and `Staged` list below it, each titled with its count. Right: the diff of the file under the cursor (the working-tree diff for Unstaged, the index diff for Staged; untracked files show their content as additions) and, below the diff, the message box with an option row `[ ] SignOff  [ ] No-Verify  [ ] Amend` and a `Commit` button. The sidebar is hidden in this view (it gets the whole width); the tab bar stays. Terminals under 80 columns show one pane at a time, as the diff does in 1c. Lists and diff follow the pane, selection, zoom (`<space>z`) and diff option rules of workflows 1 and 1c; the diff is the same widget (hunk navigation, wrap, context, split).

**Open and leave.** `<space>s` (global) toggles the view for the active tab; `gs` in the graph opens it, and so do a click on the tab's dirty marker and the palette entry `Working tree`. The view is per tab: a tab keeps its view mode, cursors, draft message and option checkboxes while another tab or the history is shown. Leave with `q`/`Esc` from a list (back to the history view with the same graph selection) or `<space>s` again; `Esc`/`q` in the diff and the message box step back to the lists first (the 1c rule). Opening the view focuses the first non-empty list (Unstaged, else Staged); a clean tree shows "Nothing to commit, working tree clean" and the message box stays usable for Amend.

**Focus.** `Tab`/`Shift-Tab` cycle Unstaged, Staged, Diff, Message; `Ctrl-w h/j/k/l` move by direction (lists left, diff top right, message below it); `Ctrl-w j`/`k` move between Unstaged and Staged (stacked), `Ctrl-w l` from either goes to the diff; `l`/`Enter` on a file moves into its diff, `h`/`Esc` back; `y` copies the file's path. `v` (Visual) is extended to the file lists and the working-tree diff by the implementing issues, with the Mouse drag; in a file list a Visual range works with `s`, `u` and `x` (the Confirm names the count), in the diff `s`, `u`, `x` act on the selected lines (rows marked `(Visual)` when they join the Keymap table).

**Side-specific keys.** Each list and each diff side is its own view for the keymap (`WorkingUnstaged`, `WorkingStaged`, `WorkingUnstagedDiff`, `WorkingStagedDiff`), so help and hints show only what applies: `s`, `a`, `x` exist only on the Unstaged side, `u`, `U` only on the Staged side; the other key there is unbound, not a no-op.

**Stage and unstage.**
1. `s` in Unstaged stages the file under the cursor (`git add -- <path>`; a deletion or rename as git reports it), `u` in Staged unstages it (`git restore --staged`, or `git rm --cached` on an unborn branch). The cursor stays at the same index so the next file is under it and `s` can be repeated. `v` then `s`/`u` applies to a range of files. `a` stages all (Unstaged), `U` unstages all (Staged); both report the count in the status bar and need no confirm (nothing is lost).
2. In the diff, `s` stages the hunk under the cursor when the diff is of an unstaged file, `u` unstages it when it is of a staged file; with a Visual range (`v`, then `j`/`k`) it applies to the selected lines only (patch built by #31, applied with `git apply --cached`, reversed for unstage). Afterwards the diff reloads and the cursor stays on the next hunk, or the next file when the file is done. `s` on the diff of an untracked or binary file stages the whole file (there are no hunks to pick).
3. Conflicted files show a `!` letter; `s` on one marks it resolved (`git add`) after a Confirm that says unresolved conflict markers stay in the file. Resolving conflicts itself is M3.
4. Every action is also a button or a palette entry (Mouse paragraph below). The lists refresh from the tab's `StatusSnapshot` (#313) after each operation and on the file watcher; `r` forces a refresh.

**Discard.** `x` on a file in Unstaged, or on a hunk or Visual lines in the Unstaged diff, opens a Confirm dialog that names what is lost ("Discard changes to 2 files? This cannot be undone"; for untracked files "Delete untracked file X? This cannot be undone"). `y` runs it, `n`/`Esc` cancels. There is no key for discarding staged changes (unstage first). Git keeps no copy of discarded working-tree changes, so there is no reflog hint; #33 may decide to save a dangling stash commit first (recommended default: no, the dialog is the safety).

**Message and commit.**
1. `c` (from a list or the diff) focuses the message box and enters Insert mode (the built-in editor of #314: first line is the subject, a blank line then the body; the subject length is shown with a soft hint at 72 columns, never enforced). `Esc` leaves Insert mode but stays in the box (its Normal mode: `i`/`Enter` back to Insert, `h`/`q`/`Esc` back to the lists). The draft is kept per tab until a commit succeeds. Editor keys (Insert mode, `MessageEditor` in Core, drawn by `MessageEditorRenderer`; not in the Keymap table, like the prompt editing keys): typed text and pastes insert at the cursor (line endings become new lines); `Enter` is a new line; `Backspace` and `Delete` join lines at their ends; `Left`/`Right` wrap across lines, `Up`/`Down` keep the column (counted in characters, not display columns), `Home`/`End` and `Ctrl-a`/`Ctrl-e` go to the line edges; `Ctrl-u` deletes to line start, `Ctrl-k` to line end, `Ctrl-w` the previous word. The cursor never splits a wide character, emoji sequence or combining mark. Lines are not wrapped on screen: the cursor's line scrolls sideways, and the part of the subject past column 72 is drawn as an error-coloured hint.
2. Options toggle with `Alt-s` (SignOff, `-s`), `Alt-n` (No-Verify, `--no-verify`) and `Alt-a` (Amend), from anywhere in the view including Insert mode, or by clicking the checkboxes. All start off; hooks run unless No-Verify is on.
3. Commit: `Alt-Enter` in Insert mode, `cc` in the box's Normal mode, or the `Commit` button. The `Alt-` chords (`Alt-Enter`, `Alt-s`, `Alt-n`, `Alt-a`) arrive as Esc followed by the key in some terminals (and under tmux); #314 verified all four in tmux on Linux (Terminal.Gui 2.5.0): `Alt-s`, `Alt-n` and `Alt-a` arrive as Alt plus the letter, `Alt-Enter` (Esc CR) arrives as `Ctrl-Alt-m` and `KeyTranslator` maps it to `Alt-Enter`; `Ctrl-g` arrives intact and is the commit fallback for terminals that swallow `Alt-Enter` (the options fall back to the checkboxes and palette). Windows and macOS terminals are not verified yet. `ce` in Normal mode commits through `$EDITOR` instead (#51: the TUI suspends and git's own `commit` runs on the staged index). The commit runs `git commit` off the UI thread, cancellable, with a timeout long enough for hooks; the box is read-only while it runs.
4. Refusals, without running git: an empty message (status message, the box keeps focus) and nothing staged with Amend off ("Nothing staged"; an amend with nothing staged is allowed and rewrites only the message). While a merge, cherry-pick or revert is in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`) the nothing-staged check is skipped, because finishing such an operation with an empty tree is valid; the operations themselves are M3. A failing hook (non-zero exit) keeps the draft and the staged state and shows git's output in a scrollable overlay (like the status history, `Esc` closes); files a hook rewrites show as new unstaged changes after the refresh.
5. Amend: toggling it on fills the box with HEAD's message when the draft is empty (the draft is kept aside and restored when Amend is toggled off), shows `Amending <short hash> <subject>` in the box title and runs `git commit --amend`; staged changes are added to the amended commit. If HEAD is already on the upstream branch the status bar warns that a force push will be needed. Undo path: the status message after the commit names the old hash ("Amended; previous commit abc1234, `git reset --soft abc1234` restores it") and the reflog keeps it reachable. While a merge is in progress Amend is disabled and the box is prefilled from `MERGE_MSG`.
6. After a successful commit the draft and Amend are cleared, SignOff and No-Verify keep their state, the lists refresh, the status bar says `Committed abc1234` and focus returns to the lists. The history view picks up the commit through the normal refresh.

**Planned keys** (not in the Keymap table until implemented):

| View | Key | Action |
|---|---|---|
| Global | `<space>s` | Open / leave the working-tree view of the tab |
| Graph | `gs` | Open the working-tree view |
| WorkingUnstaged | `s` | Stage the file; on a conflicted file mark it resolved |
| WorkingStaged | `u` | Unstage the file |
| WorkingUnstaged | `a` | Stage all |
| WorkingStaged | `U` | Unstage all |
| WorkingUnstaged | `x` | Discard the file's changes (Confirm) |
| WorkingUnstaged / WorkingStaged | `Enter` `l` | Go to the diff of the file |
| WorkingUnstaged / WorkingStaged | `c` | Write the commit message (focus the box, Insert mode) |
| WorkingUnstaged / WorkingStaged | `r` | Refresh the status |
| WorkingUnstaged / WorkingStaged | `q` / `Esc` / `h` | Leave the view |
| WorkingUnstagedDiff | `s` | Stage the hunk, or the Visual-selected lines |
| WorkingStagedDiff | `u` | Unstage the hunk, or the Visual-selected lines |
| WorkingUnstagedDiff | `x` | Discard the hunk or selected lines (Confirm) |
| WorkingUnstagedDiff / WorkingStagedDiff | `c` | Write the commit message |
| WorkingUnstagedDiff / WorkingStagedDiff | `]c` `[c` `]f` `[f` `w` `+` `-` `F` `W` `S` `y` `/` `n` `N` | As the history diff (1c) |
| WorkingUnstagedDiff / WorkingStagedDiff | `q` / `Esc` / `h` | Back to the file list |
| WorkingMessage | `i` / `Enter` | Edit the message (Insert mode) |
| WorkingMessage | `cc` | Commit |
| WorkingMessage | `ce` | Commit with `$EDITOR` |
| WorkingMessage | `q` / `Esc` / `h` | Back to the lists |
| WorkingMessage (Insert) | `Alt-Enter` | Commit |
| Working-tree view (also Insert) | `Alt-s` `Alt-n` `Alt-a` | Toggle SignOff, No-Verify, Amend |

Conflict check: `s`, `u`, `a`, `c`, `x`, `U` are unbound in Graph, Detail, Diff and Sidebar; `S`, `W`, `F` keep their display meaning in the working-tree diff; `Alt-p` and `Alt-d` are Picker-only and `Alt-1`..`Alt-9` jump tabs, so `Alt-s`, `Alt-n`, `Alt-a` are free (they act only in this view). `c` in the message box is a chord prefix (`cc`, `ce`), which is why it is a different view from the lists where `c` is a single key.

**Mouse** (same action path as keys): a click on a file row selects it and a double-click stages or unstages it (a deliberate deviation from "double-click opens": the diff already follows the selection); the diff's title row gets `stage` / `unstage` and `discard` buttons next to `‹ hunk` `hunk ›`; the list titles get `stage all` / `unstage all`; clicking the `SignOff`, `No-Verify` and `Amend` checkboxes toggles them and `Commit` commits; a drag over diff rows starts a Visual range as in 1c. The implementing issues add these rows to the Mouse table.

Still to design before their milestones:

3. Create/switch/rename/delete branch; set upstream; see ahead/behind.
4. Worktrees: add (new branch or existing), switch the app's active worktree, remove, prune, lock; see dirty state across all worktrees at a glance.
5. Merge, rebase onto, cherry-pick, revert from the graph.
6. Stash push/apply/pop/drop.
7. Fetch/pull/push with visible progress and cancel.

## Look and colours

Colours are decided in one place: `PaletteTable` (`Gitunia.Tui.Views`) holds the colours of a scheme and `Palette` says which role uses which colour, so a theme (#46, #47, #61) later swaps the table and nothing else. There is no config file yet.

**Frame (#197).** Every pane is a box and neighbouring boxes share their edges, so the whole screen is one frame split into panes (`FrameGrid`, drawn the same way by the UI and by `--print-frame`):

```
 1 gitunia                                                   <- tab row
╭─ gitunia ────────────┬─ History ──────────────────────────────╮
│  ▾ Branches 2        │ ○─╮  (main) Merge feature x    Ada ... │
│     feature/x        │ ●  │  Fix parser                       │
├─ Details ─ Changes ─ Files ─ 64d72f2 ─ 3 files ┬─ Diff ... ──┤
│ commit  64d72f2...                             │ @@ -1 +1 @@  │
╰────────────────────────────────────────────────┴─────────────╯
 NORMAL  1/4  j/k gg G ^d ^u @ ? : q                          <- status bar
```

- Rounded outer corners and plain T and cross junctions; the ASCII tier uses `+`, `-` and `|`. Where two boxes meet there is one line, not two.
- Titles sit in the top edge of each box as chips after one stretch of line: the sidebar shows the repository name, the graph `History`, the bottom pane its tabs, the commit and the file count, the diff its title (the file under the cursor and its position, no commit hash: the detail title already shows it) and buttons. The title row of the bottom pane is the line between the graph and the bottom panes (so it costs no extra row).
- From 100 columns of screen width the History list and the bottom detail and diff panes are padded by two columns (`PanePadding.SpaciousColumns`, #244), so text and rows clear the frame lines; the sidebar and screens under 100 columns keep one. Text is padded by one column inside each box otherwise (rows keep their selection tint over the padding); the first and last column and the top and bottom edge row belong to the frame, so the frame costs two columns and one row more than the old divider layout (the old header row is gone, #221).
- Focus: the focused pane's whole box is drawn in the accent colour (bold), every other line in the quiet border colour, and its title chip is the filled accent chip (the other titles are dim chips). Focus never relies on colour alone: the chips differ in text style, and with `NO_COLOR` the focused box is bold.
- Layout numbers: the sidebar (clamped 20-36 columns, shown from 60 columns) and the detail/diff split are widths including the padding; drag and the `Ctrl-w` size keys move the shared line.

**Selection and focus.** The selected row is a background tint, never inverse video, so the row keeps its own colours (branch, hash, diff colours stay readable). The focused pane's selection uses the full tint (blue), a pane without focus keeps a quieter grey tint, so the cursor stays findable. This holds for the graph, sidebar, detail, diff, picker rows, overlay rows and the Visual range. Focus is also shown by the frame (above): the focused pane's box is accent, its title is a filled accent chip and the other titles are dim chips. The active tab, the status mode (`NORMAL`, `INSERT`...) and an overlay's title bar are the same accent chip; the tab row and the status bar sit on a slightly lighter bar. An overlay's frame is drawn in the accent colour.

**Roles to colours** (accent blue, green, red, yellow, cyan, purple, muted grey):

| Role | Colour |
|---|---|
| Section titles, buttons, diff file header, the focused pane's frame | accent, bold |
| Graph lanes | a cycle of 12 colours (6 in 16-colour mode) |
| Graph commit marks | the lane's colour |
| Local branch / current branch (HEAD) / remote branch / tag | green / cyan / purple / yellow, bold (remote branches are not red: red means an error or a deletion, #222; blue on a light terminal in the 16-colour scheme, where yellow and purple are taken) |
| Hash, date, line numbers, hints, counts, punctuation | muted |
| Added line or file / removed / modified | green / red / yellow; hunk header cyan |
| Errors | red, bold (in the status bar on its own bar) |
| Search match | dark text on yellow (kept on a selected row) |
| Overlay key column | yellow, bold |

**Colour depth.** The scheme follows the terminal. 24-bit (`COLORTERM=truecolor`, also used for 256-colour terminals because Terminal.Gui has no 256-colour output of its own): the Catppuccin palette (Mocha on a dark terminal, Latte on a light one; #216), checked by a test for a contrast ratio of at least 4.5 on a typical dark and light terminal background (3.0 for dividers), also on selected rows. 16-colour (`TERM` without `256color`, or a Terminal.Gui driver that cannot do 24-bit; the driver is then forced to 16 colours): only the 16 named colours, so the terminal's own theme decides the exact look; the focused selection is bright black (bold), the unfocused one underlined. `NO_COLOR` or `TERM=dumb`: text styles only (reverse video for the selection, underline when unfocused, bold underline for search matches, underline for the current branch). Light backgrounds are recognised from `COLORFGBG` (background 7 or 15) and get darker accents and light tints; a terminal that does not set it is assumed dark. A terminal whose `TERM` has no `256color` and that sets no `COLORTERM` (for example over ssh without `COLORTERM` forwarded) gets the 16-colour scheme; set `COLORTERM=truecolor` to force the 24-bit one.

**Looking at it without the UI.** `gitunia <repository> --print-frame WxH [--diff] --colors truecolor|16|none [--light]` writes the frame with ANSI colours (the graph is focused, the sidebar cursor shows the dim tint). The overlays and the picker are not part of the printed frame; open them with `?`, `:` and `Ctrl-t`.

## Keymap

The single source of truth for bindings. Add a row in the same change that adds a binding; no conflicts within a mode/view. A test parses this table (columns `Mode/View | Key | Action`; keys in backticks, `Picker` rows are Insert mode, `Palette` rows Command mode, the rest Normal) and checks every key is a registered default in `DefaultKeymap` with no conflicts. Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `<lt>` a literal `<`, `:name` is an ex command and not a key. Modes: rows apply to Normal, except those whose view says `(Visual)` (Visual mode only); Visual repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action.

| Mode/View | Key | Action |
|---|---|---|
| Global | `?` | Show keys for current view |
| Global | `:` / `Ctrl-p` | Command palette |
| Global | `q` | Quit (closes an overlay or ends a Visual range first) |
| Global | `Tab` / `Shift-Tab` | Next / previous pane |
| Lists | `j` `k` / `gg` `G` | Move / top / bottom |
| Lists | `/` `n` `N` | Search, next, previous (graph search prefixes: `author:`, `hash:`) |
| Global | `gt` / `gT` | Next / previous tab (counts: `3gt` goes to tab 3) |
| Global | `Alt-1`..`Alt-9` | Jump to tab N (terminal multiplexers may take `Alt-N` for themselves; `3gt`, typing the number and then `gt`, does the same and never conflicts; configurable keys arrive with #46) |
| Global | `Ctrl-t` / `<space>o` | Open repository picker in a new tab |
| Global | `Ctrl-w c` / `:tabclose` | Close tab |
| Global | `<space>p` | Pin the repository to the top of the picker / unpin it |
| Global | `Ctrl-w r` | Rename the tab (status bar prompt, `Enter` saves, an empty name restores the repository name) |
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
| Lists (Visual) | `y` | Copy the selected range (OSC 52) and end it when the copy worked (a refused or failed copy keeps the range): graph one hash per line, detail the rows' text, diff the rows with their `+`/`-`/space prefix and no gutter (a wrapped row once; notes skipped); text too large for OSC 52 (about 75 KB) is refused with a warning |
| Graph | `@` | Jump to HEAD |
| Graph | `gr` | Jump to ref or hash (fuzzy) |
| Graph | `Enter` | Focus commit details |
| Graph | `y` | Copy commit hash |
| Graph | `r` | Refresh repository |
| Graph | `D` | Open the selected commit (against its first parent) in the configured difftool |
| Graph | `M` | Resolve conflicts in the configured mergetool |
| Graph | `E` | Commit the staged changes with `$EDITOR` (git's own editor; the working-tree view's `ce` does the same) |
| Graph | `]` `[` | Next / previous tab of the bottom detail pane (Details, Changes, Files; wraps) |
| Graph | `gd` `gc` `gf` | Show the Details / Changes / Files tab of the bottom detail pane |
| Detail | `Enter` `l` | Go to the diff of the selected file (Changes tab): focus it; on narrow screens open it on demand |
| Detail | `y` | Copy the path of the selected file |
| Detail | `r` | Retry loading the commit details after a failure |
| Detail | `]` `[` | Next / previous tab of the detail pane (Details, Changes, Files; wraps) |
| Detail | `gd` `gc` `gf` | Show the Details / Changes / Files tab |
| Detail | `h` / `Esc` | Back to graph |
| Sidebar | `l` | Expand section; on a leaf, focus graph |
| Sidebar | `Enter` | Expand section; on a ref, select it in graph (focus stays) |
| Sidebar | `h` | Collapse section / parent |
| Sidebar | `/` | Filter entries |
| Sidebar | `y` | Copy the branch, tag or stash name, or the worktree path (OSC 52) |
| Diff | `]c` `[c` | Next / previous hunk |
| Diff | `]f` `[f` | Next / previous file |
| Diff | `Esc` / `h` | Back to the file list (closes an on-demand diff) |
| Diff | `/` `n` `N` | Search in diff, next, previous |
| Diff | `w` | Toggle word wrap (off by default; the cursor moves by screen line while on) |
| Diff | `y` | Copy the cursor line (with its `+`, `-` or space prefix) |
| Diff | `Y` | Copy the displayed rows of the file under the cursor (not a pasteable patch) |
| Diff | `+` `-` | Show more / fewer unchanged lines around the changes (3 at a time, default 3; the title shows `[ctx N]`) |
| Diff | `F` | Toggle showing whole files instead of hunks (title `[file]`) |
| Diff | `W` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| Diff | `S` | Toggle the side-by-side view (title `[split]`; needs a pane of 80 text columns) |
| Diff | `L` | Load anyway: reload the commit without the row and per-file limits |
| Picker | `Up` `Down` | Move highlight |
| Picker | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases; terminals report `Ctrl-j` distinct from `Enter`) |
| Picker | `Enter` `Ctrl-l` | Open repository (new tab or focus existing); `Ctrl-l` is the home-row alias of `Enter` (#241), `Ctrl-w l` is a chord and unaffected |
| Picker | `Tab` `Right` | Accept the suggestion: with a typed name, fill in the highlighted repository's path (then its subfolders are listed); in a path, complete the directory name (to the common prefix of the candidates, or to the highlighted one). Matching ignores case on every platform, the inserted text keeps the real case of the directory |
| Picker | `Alt-p` | Pin the entry to the top of the list / unpin it |
| Picker | `Alt-d` | Remove from recents |
| Palette | `Up` `Down` | Move highlight (typing filters; `Enter` runs the action, `Esc` closes) |

Mouse paths are listed in the next section.

## Mouse

Mouse input is optional and goes through the same action path as keys: the mouse map (`DefaultMouseMap`) turns a gesture on a view into the action id a key would produce, and the same handler runs it. A test parses the table below and checks it against the defaults, so add a row in the same change that adds a mouse path. Views inherit like in the keymap: `Lists` covers Graph, Detail, Sidebar and Diff. `CommandsButton`, `NewTabButton`, `Tabs`, `Picker`, the overlays (`Help`, `Messages`, `Palette`, and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `DetailTab.*`, `PickerButton.*`, one view per button or label) are views of their own.

| View | Gesture | Action | Effect |
|---|---|---|---|
| Lists | Wheel up | `list.up` | Moves the selection up 3 rows (like `3k`) in the pane under the pointer, which gets focus |
| Lists | Wheel down | `list.down` | Moves the selection down 3 rows (like `3j`) |
| Lists | Drag start | `visual.toggle` | Pressing a row and moving the pointer starts Visual mode (range selection, like `v`) at the pressed row; graph, detail and diff only |
| Lists | Drag up | `list.up` | While dragging, the cursor follows the pointer: moves up by as many rows as the pointer is above it |
| Lists | Drag down | `list.down` | The same downwards |
| Graph | Double-click | `graph.open-details` | Same as `Enter`: focus the commit details |
| Detail | Double-click | `detail.open-diff` | Same as `Enter`: go to the file diff |
| Sidebar | Click | `sidebar.activate` | Same as `Enter`: expand or collapse a section, select the ref's commit in the graph |
| Tabs | Click | `tab.next` | Switch to the clicked tab (same as `3gt` with the tab's number); closes an open picker |
| NewTabButton | Click | `picker.open` | The ` + ` button right after the last tab (#232): open the repository picker for a new tab, same as `Ctrl-t` |
| CommandsButton | Click | `palette.open` | The ` : commands ` button at the right end of the tab row (rows of 44 columns or more; there is no header bar any more, #221): open the command palette, the menu of the actions of the focused pane and the global ones, each with its keys |
| Backdrop | Click | `cancel` | While the help, palette or status history is open, a click on a pane or the tab row outside it closes it (like `Esc`); the double-click that follows is ignored |
| Palette | Click | `command.execute` | Run the clicked entry (it is highlighted first) |
| Palette | Wheel up | `palette.previous` | Move the highlight up one row |
| Palette | Wheel down | `palette.next` | Move the highlight down one row |
| Help | Wheel up | `list.up` | Scroll the help up 3 lines (wherever the pointer is while the overlay is open) |
| Help | Wheel down | `list.down` | Scroll the help down 3 lines |
| Messages | Wheel up | `list.up` | Scroll the status history up 3 lines |
| Messages | Wheel down | `list.down` | Scroll the status history down 3 lines |
| DiffButton.PreviousHunk | Click | `diff.previous-hunk` | Button `‹ hunk`, same as `[c` |
| DiffButton.NextHunk | Click | `diff.next-hunk` | Button `hunk ›`, same as `]c` |
| DiffButton.Wrap | Click | `diff.toggle-wrap` | Button `wrap`, same as `w` |
| DiffButton.Zoom | Click | `pane.zoom` | Button `zoom`, same as `<space>z` |
| DiffButton.MoreContext | Click | `diff.more-context` | Button `ctx+`, same as `+` |
| DiffButton.LessContext | Click | `diff.less-context` | Button `ctx-`, same as `-` |
| DiffButton.WholeFile | Click | `diff.toggle-whole-file` | Button `file`, same as `F` |
| DiffButton.IgnoreWhitespace | Click | `diff.toggle-ignore-whitespace` | Button `-w`, same as `W` |
| DiffButton.SideBySide | Click | `diff.toggle-side-by-side` | Button `split`, same as `S` |
| DetailTab.Details | Click | `detail.tab-details` | The `Details` label in the detail pane's title row, same as `gd` (also focuses the pane) |
| DetailTab.Changes | Click | `detail.tab-changes` | The `Changes` label, same as `gc` |
| DetailTab.Files | Click | `detail.tab-files` | The `Files` label, same as `gf` |
| Picker | Double-click | `picker.accept` | Open the repository of the row |
| Picker | Wheel up | `picker.previous` | Move the highlight up one row |
| Picker | Wheel down | `picker.next` | Move the highlight down one row |
| PickerButton.Pin | Click | `picker.pin` | Button `[pin]` / `[unpin]` on the highlighted row, same as `Alt-p` |
| PickerButton.Remove | Click | `picker.remove-recent` | Button `[remove]` on a highlighted recent row, same as `Alt-d` |

Pointer-position behaviour that has no action of its own: a click on a pane focuses it, a click on a list row selects it (graph, detail, sidebar, diff; the details pane follows the graph selection as with keys), a click on a picker row highlights it. A click on empty space does nothing. The wheel moves the selection, not just the view (the keyboard has no pure scroll either), so the details pane follows it as it does for `j`/`k`. A mouse action does not touch a pending count or chord typed before it. While the help, palette or status history is open, panes and the picker ignore every mouse gesture (no focus change, no selection), except that the wheel scrolls the help or history (or moves the palette highlight) when the pointer is over a pane or the overlay, and a click on a pane, the tab row or the header closes it (`Backdrop` row), like `Esc`; the which-key popup does not block the mouse.

Drag to select: press a row of the graph, detail or diff pane and move the pointer; the pane takes focus, the pressed row becomes the anchor, Visual mode starts (status bar `VISUAL`) and the cursor follows the pointer, also above or below the pane while the button is held: while the pointer rests outside the pane the cursor keeps moving every 80 ms, 1 row just outside, 1 more per 2 rows of distance, at most 6, until the pointer is back inside, the list ends or the button is released (#137). The range is drawn like the selected row. The release leaves the range active; a click on a row, `Esc`, `q`, `v` or moving focus to another pane or tab ends it. `y` copies the range (the drag plus `y`, or `visual.copy` from the palette) and ends it; staging lines (#31) will act on it too. The sidebar has no ranges (a drag there only moves its selection); a drag also does not start a range while a prompt (search, filter, ref jump) or the picker is open.

Drag to resize: the line between the sidebar and the graph, the line between the detail and diff panes, and the title rows of the detail and diff panes (the line between the graph and the bottom panes) follow the pointer while the button is held. Sizes are clamped to what fits (at least 12 columns for the sidebar and detail, 20 for the graph and diff, 3 rows for the bottom panes, 4 for the graph), belong to the tab and are saved with the session (#138); `Ctrl-w =` (or `:`, then "Reset the pane sizes") returns to automatic, and `Ctrl-w >` `Ctrl-w <lt>` `Ctrl-w +` `Ctrl-w -` are the keyboard path. A zoomed pane has no dividers to drag.

Clickable titles and buttons: a click on the title row of the detail or diff pane focuses that pane. The detail title row starts with the three tab labels (`Details`, `Changes`, `Files`), drawn as buttons: the selected one is the accent chip (quieter when the pane has no focus), the others look exactly like the inactive repository tabs of the tab row (a chip on a subtle tinted background; underlined where the terminal tier has no background colours, `[Name]` in the ASCII tier from 29 columns, one padding column per side; #300). The selected tab of an unfocused pane keeps the chip but in bold text (bold and underlined in the colourless tiers) so it still differs, one column of line between them; below 29 columns the padding around the labels goes. The content of the detail pane sits on a panel background, the bar colour (24-bit schemes only; the 16-colour and `NO_COLOR` tiers rely on the frame line and the buttons), so the pane reads as a separate area. A click on a label selects the tab. The diff title row ends in buttons (see the table), chips like the inactive repository tabs with one column of line between them and `<`/`>` arrows and brackets in the ASCII tier (#302), when the pane is at least 30 columns wider than the buttons need; narrower panes drop buttons in this order, last to go first: `zoom`, `wrap`, the two hunk buttons, `split`, `-w`, `file`, `ctx+`, `ctx-`, the keys stay available. The highlighted picker row shows `[pin]`/`[unpin]` and, for a recent repository, `[remove]` at its right edge (rows of 60 columns or more inside the frame, so screens of 62 columns or more). Clicking the commands button at the right end of the tab row opens the command palette, which lists the actions of the focused pane and the global ones, so every action without a button (for example `L` load anyway, the search keys, `r` refresh) can be run with the mouse; typing a search or a filter text needs the keyboard, as nothing else can enter text.

Terminal.Gui turns on mouse reporting (SGR, mode 1006) at startup and turns it off on exit; there is no switch to disable it yet. Verified by writing SGR mouse sequences into the pane of a tmux 3.7c session running the Linux build (#64 and #100): clicks, double-clicks, the wheel, press-drag-release (reported as a press, position reports with the button held, a release and then a click at the release position, which the drag logic swallows), divider and title drags, header, palette and button clicks all arrive; a double-click is delivered as a click followed by a double-click event (so a double-click on a sidebar section toggles it once), and a sideways wheel (Ctrl or Shift plus wheel) is ignored (unit-tested only). Not yet verified with a physical mouse, over ssh, or in kitty, Windows Terminal and macOS terminals (tracked in #49). Terminals or ssh paths that drop mouse sequences leave the keyboard path unaffected.

Design history (why things are the way they are, measurements, rejected options): `docs/history/ux.md`. Not needed for routine work.
