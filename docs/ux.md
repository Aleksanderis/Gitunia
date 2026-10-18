# UX

Spec for workflows and keys. Keyboard and mouse are both fully supported: every action is reachable with either. Bindings live in the Keymap and Mouse tables near the end; the sections before them give the behaviour the tables cannot. How decisions came about and which issue built what: `docs/history/ux.md` and the issues.

## Principles

- **Modal and discoverable.** Normal mode navigates, actions are short mnemonic chords, `?` shows the keys of the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- **Vim-like motions everywhere:** `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work. Bindings are data (mode, view, key sequence, action id); remapping in the config file is planned for M4.
- **Layout.** History view: left sidebar (branches, remotes, tags, stashes, worktrees), centre commit graph, bottom detail pane (commit info, files, diff). The working tree is a separate view of the tab (workflow 2), not a graph row.
- **Branches and worktrees are first-class:** always visible in the sidebar, and every branch shows which worktree holds it.
- **Destructive actions confirm,** say what they will do and how to undo it.
- **Mouse is optional but complete.** Click to focus/select, double-click to open, wheel to scroll, clickable tabs and buttons, drag to select ranges and resize panes. Nothing is keyboard-only or mouse-only.

## Workspace and tabs

- The app opens with or without a repository: `gitunia` restores the last session (or shows the repository picker if there is none); `gitunia <path>` opens that repository as a tab, also from outside any repository (a subdirectory resolves to its repository root).
- The tab bar shows each tab's name and a dirty/ahead/behind marker (details in workflow 0).
- The repository picker is a fuzzy search over open, pinned, recent and discovered repositories, and also opens a typed path. A pinned repository stays at the top, right after the open tabs and above recents and discovered ones, however long ago it was opened; pins never expire, recents are limited to the last 50. Opening an already-open repository focuses its tab.
- Each tab keeps its own selection, scroll, filters, pane sizes and focused pane. Closing the active tab activates its right neighbour (left if it was last).

## Modes

Normal (navigate, act), Visual (select a range of lines, commits or files), Insert (text input: commit message, filters, prompts), Command (`:` palette), Confirm (y/n dialogs).

**Confirm mode** is used by every destructive action. A centred dialog holds a title, what will happen, what cannot be undone or how to undo it (in the warning colour) and the name of `y`.
- Only `y` runs the action, after the dialog has closed. `n` and `Esc` drop it. Every other key is swallowed: never `Enter`, and nothing behind the dialog moves.
- The two answers are rows of the dialog and clickable; a click anywhere else closes it like `n`.
- If the dialog does not fit the screen it is not shown and the action does not start (the status bar says so): an answer nobody can read is no confirmation. A dialog that stops fitting after a resize is closed unanswered, with a warning.
- The input mode before the dialog (Normal, or Visual with a range) is restored when it closes. What the action does is fixed when the dialog opens, not when `y` is pressed.

## Focus model and key style

- **Panes:** sidebar (left), graph (centre), detail pane with Details, Changes and Files tabs (bottom), and the diff (it belongs to the Changes tab and sits beside its file list; on screens too narrow for both it opens on demand and replaces the detail pane, see 1c).
- **Moving focus.** `Tab` cycles sidebar, graph, detail, diff (skipping closed panes); `Shift-Tab` reverses. `Ctrl-w` + `h` `j` `k` `l` moves by direction (sidebar and graph on top, detail and diff below; `j` from the sidebar or graph goes to the detail pane, or the diff when the detail pane is hidden; `k` from a bottom pane goes to the graph); `Ctrl-w w` cycles; `Ctrl-w c` closes the tab. The focused pane has a highlighted border and its own status hints.
- **Keys.** Frequent actions are single keys per view (`Enter`, `y`, `/`). Global actions use the `<space>` leader (`<space>o` open repository, `<space>p` pin, `<space>z` zoom). `g`, `]`/`[` and `Ctrl-w` start chords; counts prefix motions (`5j`, `3gt`).
- **Chords.** A pending prefix shows in the status bar and, after 500 ms, in a which-key popup listing the continuations; a row that only opens a longer chord reads `+more` plus what the group holds where it is a named leader group (`<space>b` `+more (branch operations)`, also commit, tag, worktree, stash operations, external tools, diff view options; #443). `Esc` cancels; there is no chord timeout.
- **Insert mode.** Printable keys are text; only `Esc`, `Enter` and `Ctrl-`/`Alt-` combinations act, so the leader and chords never fire while typing.
- **`Esc` precedence:** cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph). In the graph, when nothing else claims it, `Esc` dismisses the status message.
- **`q` always quits,** from any pane, except that it first closes an open overlay or ends a Visual range. `h` is back in Detail and Diff and collapse in the Sidebar; `l` is forward (Detail to the diff, Sidebar expand; the diff has no horizontal scroll).
- **Notation:** keys are spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `gg`, `<space>o`), never `^d` or `c-d`, in the help overlay, the palette, the which-key popup and the status bar.
- **Help overlay (`?`):** the keys of the focused view (its own bindings, then the inherited `Lists` and `Global` ones), generated from the keymap table and the action titles, never hand-written, in two columns separated by a vertical line (`Ctrl-d │ Half page down`). `j`/`k`/`Ctrl-d`/`gg` scroll it; `Esc`, `q` or `?` close it; every other action is ignored while it is open. The `Alt-1`..`Alt-9` entry also names the multiplexer-safe `3gt`.
- **Command palette (`:` or `Ctrl-p`):** typing filters every action fuzzily (title or action id), `Up`/`Down` (or `Ctrl-j`/`Ctrl-k`) move, `Enter` (or `Ctrl-l`) runs the highlighted action, `Esc` closes. Each row shows its bindings and the focused view's own actions come first. Modal-only actions (the Confirm answers, the picker's own keys, the palette's own keys, text-input accept and `Esc`) are not listed; actions bound ahead of their feature are listed greyed out.
- **Status bar hints** are key plus short description pairs (`j/k move  gg/G top/end`), keys in the accent colour. They start with `? help  : commands  q quit`, so a narrow terminal cuts the pane's own hints, never those. Every hint names the action of each of its keys, and a test checks the default keymap binds that key to that action in that pane.
- **Status messages.** Background work that fails (a tab's worktree, stash or status read, a failed session save, a reset session file, a failed graph load) reports to one channel; later features post to it the same way.
  - The newest message shows in the status bar between the position and the key hints (errors in the error colour) until dismissed with `<space>x`, or `Esc` in the graph when nothing else claims it, or replaced by a newer one.
  - `<space>m` opens the history (last 50, newest first, long messages wrap) as an overlay that updates live.
  - Tab-scoped messages start with the repository name; a message equal to the one showing is not posted again. A key action's own one-off notice (`HEAD is not loaded yet`) takes the place of the message while it lasts.

## Workflows

### 0. Open a repository, switch tabs, reopen the session

1. **Start.** `gitunia` restores the last session (tabs, active tab, per-tab selection and focus); with no session it opens the picker. `gitunia <path>` opens that path as a tab (a repository already open just gets focused) and keeps the other restored tabs.
2. **Picker.** `Ctrl-t` or `<space>o` opens it (Insert mode, input focused). It lists open, pinned, recent and discovered repositories, fuzzy-filtered as you type, with a tag per row (`open`, `pinned`, `recent`, `found`).
   - `Up`/`Down` and `Ctrl-j`/`Ctrl-k` move; `Enter` or `Ctrl-l` opens in a new tab (or focuses the existing one); `Backspace` deletes a character; `Esc` cancels (with no tab open it quits, since there is nothing to go back to).
   - `Tab` or `Right` accepts the suggestion: the highlighted repository's path is filled in and its subfolders are listed; in a typed path it completes the folder name.
   - Typing and matching ignore case: every segment of a typed path is resolved against the real folder names (`~/projects/x` lists `~/Projects/x` on Linux) and the inserted text keeps the real case.
   - Discovered repositories come from a scan of the conventional source folders below the home directory (`Projects`, `src`, `code`, `dev`, `repos`, `git`, `work`, ... up to three levels deep, hidden and dependency folders skipped). Configurable scan roots arrive with the config file in M4.
   - A typed path is listed off the UI thread: the previous rows stay until the new listing arrives, `scanning...` appears after about 150 ms when there are none, a newer keystroke cancels the older listing, and a listing slower than 5 s ends with a "timed out" notice.
   - A click highlights a row, a double-click opens it.
3. **Typed paths.** Text starting with `/`, `~/`, `./`, `../` or a drive (`C:\` or `C:/`) is a path; anything else is a fuzzy query. For a path the list shows the matching directories (repositories marked), `Tab` completes to the common prefix and then to the highlighted directory, and `Enter` opens the resolved path. Relative paths are taken from the active repository, or the home directory, never from the process working directory. A non-repository path shows an inline "not a git repository" message and the picker stays open.
4. **Pin and remove.** In the picker `Alt-p` (or the `[pin]`/`[unpin]` button on the highlighted row) pins or unpins the entry; `Alt-d` removes it from recents (never deletes anything on disk). `<space>p` pins the repository of the active tab.
5. **Switch tabs** with `gt`/`gT` (or `3gt`), `Alt-1`..`Alt-9`, or `Ctrl-^` / `<space><Tab>` for the previously active tab. Mouse: click a tab; a click on a tab's `*` also opens its working-tree view. `Ctrl-w c` closes the tab; closing the last tab shows the picker.
6. **Tab bar.** One row on the bar background (the status bar's, so the bars bracket the screen), a margin, then every tab as a chip with two columns of padding per side, ` N name markers `, one blank column between tabs, and a ` + ` button after the last tab that opens the picker.
   - The active tab is the filled accent chip. The others are buttons: the same chip on a subtle tinted background so they read as clickable; underlined without background colours, bracketed (`[ 2 name ]`, same width) in ASCII. With `NO_COLOR` the active chip is reverse video.
   - The name is the repository name, or the name given with `Ctrl-w r` (a status bar prompt that starts with the current name: `Enter` saves it with the tab in the session file, an empty name restores the repository name, `Esc` cancels). The branch is not part of the tab (the sidebar marks the current branch with `*`).
   - Markers: `!` an operation (merge, rebase, cherry-pick, revert) is in progress (workflow 4), `*` dirty working tree, `↑n` ahead, `↓n` behind (`+n`/`-n` without Unicode), an ellipsis while the tab loads. They follow external git activity through file watching; `<space>r` refreshes manually.
   - Two tabs with the same name show their parent directory. Names are cut at 40 characters. When the tabs do not fit, the row scrolls to keep the active one visible and `<` marks hidden tabs on the left.
7. **Missing repository.** A tab whose repository was moved or deleted stays in the bar as `! name (missing)`. `Enter` on it opens the picker (Locate repository, starting in the folder that held it) and the chosen repository replaces the tab; `Ctrl-w c` closes it.
8. **Unsent message.** Closing a tab that holds an unsent commit message (typed or edited, not committed) does not close it at once: the status bar names the tab, and a second `Ctrl-w c` right after (nothing else pressed in between) closes it and drops the message.
9. **Terminal key caveats** (Terminal.Gui 2.5.0 under tmux). `Ctrl-j` (0x0A) arrives as `Ctrl+Enter`, distinct from `Enter`, so the translator maps it back to `Ctrl-j` and it works as a picker alias for `Down` (a terminal that sends one byte for both would break the alias; `Down` still works). `Ctrl-^` (0x1E) arrives as `Ctrl+6` and is mapped back; `<space><Tab>` is the fallback for terminals that swallow it. Some terminals send `Backspace` as `Ctrl-h`, which the picker treats as `Backspace`.

### 1. Browse the graph, jump, search

1. **Open.** A repository tab opens with focus on the graph, the newest commit selected and HEAD marked. Commits stream in; the list is usable immediately and the status bar shows loading progress.
2. **Graph row.** A table: branch lanes, message (ref decorations then subject), author, date and time (`yyyy-MM-dd HH:mm`, the author's local time) and short hash (dimmed).
   - The message starts two columns after the last line of the row's own lanes, so with many branches open, rows that only use the first lanes keep their message close to the line. The message column is the flexible one and takes all width the other columns leave (no cap).
   - Fixed columns: author 16 (ellipsis when longer; 20 when 140 or more columns are left after the lanes), date 16 (10, date only, when the time does not fit), hash 7. The columns after the message sit one blank column apart; from 100 columns left after the lanes (for example 160x45) the gaps are three columns and the row keeps a two-column right margin (the message column gives up 7 columns for that, intended).
   - Narrow terminals drop columns instead of squeezing the message below 30 columns: hash first, then the time (the date stays), then the date, then author. They return when the terminal widens. `gitunia <repository> --print-frame WxH` shows any width.
   - Example at 120 columns (without the sidebar):
     ```
     ●  Fix parser crash on empty input                Ada Lovelace       2026-10-13 09:00   7a21f84
     ●  (◆ main ↔) Merge branch 'topic'                Alexander the G…   2026-10-12 23:30   ee3f455
     ```
3. **Ref labels.** The symbolic `origin/HEAD` is hidden. A local branch and its remote-tracking branch of the same name on the same commit are one label, `main ↔` (ASCII: `main (r)`). The branch HEAD is on gets a `◆` before it (ASCII: `HEAD -> main`). A remote branch with no local branch of that name, or on another commit, keeps its own `origin/x` label.
4. **Lanes.** The first-parent chain of the checked-out branch always runs in the leftmost lane, as a straight line from its tip down. Branches ahead of it (such as `origin/main`, however many commits ahead) continue it upwards in the same lane; other branches, merged features and remote tips sit to its right and join it from the right. A detached HEAD has no such lane and keeps the plain leftmost-free-lane layout.
5. **Straight lines.** A commit with one child and one parent on its own lane is drawn as part of the line (`│`, ASCII `|`), not as a node. Node dots (`●`, merge `○`; ASCII `*`, `o`) appear only where a lane starts, ends, forks or merges, and on every commit that carries a ref (branch, tag, remote, HEAD), so labels never hang on a bare line.
6. **Move** with `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G`, counts and the mouse wheel. While the log is still streaming, `G` goes to the last loaded commit and stays there (it does not follow the stream); the status bar says `Still loading: at the last loaded commit` until the load finishes (a plain `G` only, not `5G`).
7. **Jump.** `<space>h` jumps to HEAD. `<space>f` opens a status bar prompt (Insert mode) that fuzzy-filters refs (branches, tags, remotes) and also accepts a hash prefix of at least 4 hex digits; the best candidate is shown, `Up`/`Down`/`Tab` cycle through the others, `Enter` selects that commit, `Esc` cancels.
8. **Details.** `Enter` on a commit moves focus to the detail pane; `Esc` or `h` returns. The pane follows the graph selection while the graph is focused. It sits below the graph (hidden on screens under 14 rows) and has three tabs, shown as buttons on its title row:
   - **Details:** hash, author, committer (only when different), parents, refs and the full message.
   - **Changes:** the changed files with status `A` `M` `D` `T`, `R<score> old -> new`, `C<score> old -> new`; merges show changes against the first parent.
   - **Files:** the file tree of the commit (a "not implemented yet" placeholder for now).
   - A fresh tab opens on Changes. `]`/`[` switch to the next/previous tab (wrapping), `gd` `gc` `gf` jump to Details, Changes, Files from the graph or the detail pane; a click on a label does the same. The selected tab is part of the repository tab's view: kept while the graph selection moves, saved with the session, and each tab remembers its own cursor row for the selected commit.
   - `j`/`k` scroll the rows of the tab. On Changes, focus lands on the first file row, that file's diff is already shown beside the list and `j`/`k` show the diff of each file as the cursor reaches it, no `Enter` (see 1c). `Enter` or `l` moves focus into the diff (on the other tabs they only say to open Changes).
   - When the details fail to load the pane says so and `<space>r` in the detail pane (or the palette) retries. The title row shows the file count (`.. files` until the details have arrived). The rows of all tabs start at the same left edge; only the commit message is indented.
9. **Search.** `/` opens the search box (Insert mode). Plain text matches the commit subject (message bodies are not searched yet), `author:` matches the author name and `hash:` the start of the hash, all case-insensitive. Matches are highlighted while typing and the status bar shows the count (`/fix_  3/17`). `Enter` jumps to the first match at or after the selection and returns to Normal mode, `n`/`N` step through matches (counts work, the search wraps), `Esc` in the box cancels, `Esc` in Normal mode clears the highlight. In the box `Backspace`, `Ctrl-u` and `Ctrl-w` edit the text. The search scans the loaded commits in small slices between key presses and keeps scanning as the log streams in; the status bar shows `searching N%` meanwhile.
10. **Copy and refresh.** `y` copies the selected commit hash (OSC 52); `<space>r` refreshes the repository now.
11. **Quick jump** (`<space>j`, flash/easymotion style). It opens a prompt in Insert mode (`jump _` in the status bar). Type text and every row in view that contains it (case-insensitive) gets a one-letter label drawn over the character after the match, in a chip so it shows without colour. Pressing a label selects that row and closes the prompt; `Enter` selects the nearest match; `Esc` cancels.
    - Only the rows in view are looked at, so the cost does not depend on the log size. Labels go to the matches nearest the cursor first, home-row letters first. A letter that would continue a match in view is never a label, so typing more text and pressing a label never clash (any other key edits the text). With more matches than letters the farthest stay unlabelled: type more to narrow them.
    - A match whose label would fall past the right edge of its row takes no letter, so no label is ever hidden. A wide character under a label leaves a blank so the rest of the row does not shift.
    - It works in the graph, the diffs (history and working tree; it matches the row text, not line numbers or the `+`/`-` marker) and the Unstaged and Staged lists (file paths); the jump moves that list's cursor and the diff beside it follows. In the side-by-side diff both sides are labelled (a label sits within its half; an unchanged line is labelled on its left side). The detail pane has it too (rows in view, file paths on Changes); not yet the sidebar. The prompt closes when focus moves to another pane or tab. Mouse: a click selects any row, as always.

### 1b. Sidebar navigation

1. `Ctrl-w h` (or `Tab` cycling) focuses the sidebar (sections: branches, remotes, tags, worktrees, stashes). `j`/`k` move, `l`/`Enter` expand a section, `h` collapses it (or moves to the parent).
2. `Enter` on a branch, remote branch or tag selects its commit in the graph and keeps focus in the sidebar; `l` on a leaf moves focus to the graph. A stash is not a graph node, so `Enter` on it shows the stash's diff in the detail pane (workflow 7).
3. `/` filters the entries by name (Insert mode: type, `Backspace` erases, `Enter` keeps the filter and returns to Normal mode, `Esc` drops it). The filter also matches the worktree shown beside a branch and unfolds every section with a match. In Normal mode `Esc` clears an active filter, and otherwise returns focus to the graph.
4. A section header shows its entry count. Remotes and Tags are folded by default. A branch shows `wt:<directory>` for the worktree that has it checked out (`*` marks the current branch and the current worktree) and `↑n↓m` against its upstream. The sidebar is hidden below 60 columns.

### 1c. Diff viewing

1. **Where it is.** The diff is part of the Changes tab, not a mode of its own. While Changes is selected, the diff pane is shown beside the file list. It holds the whole commit's unified diff (against the first parent, so merge commits match the file list) with the header of the file under the file cursor at the top.
   - It is there as soon as the tab is, for the first file. Moving the file cursor with `j`/`k` (or a click on a row) shows that file's diff at once. Selecting another commit in the graph loads that commit's diff at its first file (a load in flight is cancelled when another commit is asked for).
   - `Enter` or `l` on the file list moves focus into the diff to scroll, search and copy; `h` or `Esc` comes back and the diff stays. On the Details and Files tabs the pane is not shown.
   - The diff sits beside the compact file list (a third of the width, 24-60 columns) on screens of 72 columns or more, so both are visible together at 80 columns. Narrow or short screens (under 72 columns, or without the bottom strip) keep the on-demand behaviour: the diff opens with `Enter` or `l` on a file row, takes focus and replaces the detail pane (screens under 14 rows zoom it), and `Esc` or `h` closes it. An on-demand diff on a screen that is then widened stays open and also keeps showing a Details or Files tab beside it, following the graph selection.
   - `<space>z` zooms the focused graph, detail or diff pane to the full area between the tab row and the status bar and back (the sidebar does not zoom; moving focus to another pane ends the zoom). While zoomed, `Tab` only reaches panes that are shown.
2. **Navigate.** `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G` scroll; `]c`/`[c` jump to the next/previous hunk and `]f`/`[f` to the next/previous file of the commit (counts work; at the last one the status bar says so). The title shows the commit, the file under the cursor and `file n/m  hunk n/m`.
3. **Search.** `/` opens a search box (Insert mode, case-insensitive substring over every row including headers, highlighted while typing); `Enter` jumps to the first match at or after the cursor, `n`/`N` step through matches and wrap, `Esc` in the box cancels.
4. **Buttons.** The right end of the title row has chips (`‹ hunk`, `hunk ›`, `wrap`, `zoom` and the option buttons below); each is the mouse path of a key (Mouse table). There are no file buttons: files are chosen in the file list. The working-tree diff also puts its staging chips on the first line of the current change group. Narrower panes drop buttons (see Mouse). Quick repeated clicks on a button each count (the toolkit reports the second and third click of a burst as double and triple click, which buttons treat as clicks). A drag selects a range of rows.
5. **Rows.** `old new` line numbers, a `+`/`-`/space marker and the text. Text is cut with an ellipsis while word wrap is off (the default). `<space>vw` turns wrap on: a long row continues on the following screen lines under a blank gutter, `j`/`k` move by screen line, the title shows `[wrap]`, there is no horizontal scroll. Tabs expand to 4-column stops and wide characters are never split. Binary files, renames without edits, mode changes and "no newline" markers get a note row; parser warnings (such as invalid UTF-8 in the patch) show as a red `!` row above the first file.
6. **Options** (per tab, not saved with the session): `+`/`-` show 3 more/fewer unchanged lines around each change (title `[ctx N]`), `<space>vf` shows whole files (`[file]`), `<space>vi` ignores whitespace-only changes (`[-w]`). Each reloads the commit with git's `-U<n>` / `-w` (the cache is dropped, the cursor returns to the same file).
   - In whole-file mode `]c`/`[c` still visit each group of changes, grouped as git's own hunks would be for the current context (changes closer than twice the context lines are one hunk), and the title's `hunk n/m` counts those groups. The grouping uses the context set with `+`/`-` (3 by default), not a `diff.context` from the user's config.
   - The options reset when the tab's repository is reloaded; a user's `diff.context` config applies until the first change. Files whose changes are whitespace-only stay in the file list but have no diff while `[-w]` is on. Showing whitespace characters is not done yet.
7. **Side by side** (`<space>vs` or the `split` button; per tab, not saved; title `[split]`). The old side on the left with its line numbers, the new side on the right, a vertical rule between. Unchanged lines appear on both sides; a run of removed lines is paired line by line with the added lines after it (the shorter side stays blank); file and hunk headers and notes span both sides.
   - It needs a pane of 80 text columns; on a narrower pane the unified rows stay and the title says `[split 80+]` (zoom to get room).
   - The cursor is one line of the pair: the old side's row (the new side's for an added-only line). `y` copies that row, `Y` still copies the file's rows in diff order, and a Visual range copies from the first row of its first line to the last row of its last line, in diff order.
   - Wrap wraps each side inside its half and a line takes as many screen lines as its longer side. Search, `]c`, `]f` and the options work as in the unified view (a match on either side puts the cursor on its line).
8. **Back.** `Esc` or `h` first clears an active search, then returns focus to the file list (beside the file list the diff stays; an on-demand diff closes). If the tab is changed away from Changes while the diff has focus, focus returns to the detail pane.
9. **Limits.** A diff is capped at 400,000 rows and 100,000 lines per file (the last row, or the file's note row, says it was cut short). `<space>vl` loads the same commit again without the caps (the cursor stays on the same file; with nothing cut it only says so; binary files have no content to load and stay a note).
10. **Following the graph.** While the pane is shown, moving the graph (or a refresh that moves the selection) loads the newly selected commit once its details have arrived; the old diff stays until then. Beside the file list it shows the file under the file cursor (the first one for a new commit); an on-demand diff keeps the cursor on the same file if the new commit touches it. A resize that makes room for the diff loads it at once; one that takes the room away returns focus from the diff to the file list. The status bar shows the keys of the focused pane.

### 2. Working-tree view: stage, commit, amend, discard

The working tree is a separate view of the tab, not a graph pane or pseudo-row. It has its own views for the keymap (`WorkingUnstaged`, `WorkingStaged`, `WorkingUnstagedDiff`, `WorkingStagedDiff`, `WorkingMessage`); they inherit `Lists` and `Global`, and the diffs the `Diff` navigation rows. Everything specified here is built except the narrow-screen mouse path from a list to its diff.

**Layout.** Left column: the `Unstaged` list on top (conflicted files first in their own group, then modified, deleted, renamed and untracked files, each with its status letter and path) and the `Staged` list below it, each titled with its count. Right: the diff of the file under the cursor and, below it, the message box with an option row `[ ] SignOff  [ ] No-Verify  [ ] Amend` and a `Commit` button. The sidebar is hidden (the view gets the whole width); the tab bar stays.
- Status letters: `M` `A` `D` `R` `C` `T`, `?` untracked, `!` conflicted (with the kind of conflict). A rename or copy shows as `old -> new` in Staged, an edit of the renamed file under its new path in Unstaged. A path that is not valid UTF-8 shows its stray bytes as replacement characters.
- Ignored files are in no list. List order follows git's, except that conflicted files come first.
- Terminals under 80 columns show one pane at a time (the focused list shows, `Tab` switches; the focused diff replaces the list, as in 1c). Under 7 rows of content the two lists do not stack and the focused one takes the left column. Under 12 rows the message box shows only while it has focus, in place of the diff area.
- Lists and diff follow the pane, selection, zoom and diff option rules of workflows 1 and 1c.

**Open and leave.** `<space>s` toggles the view for the active tab; so do a click on the tab's dirty marker and the palette entry `Open or leave the working tree`. The history status bar hints `<space>s working tree` right after `j/k move` in the graph, sidebar and detail panes.
- The view is per tab: a tab keeps its view mode, cursors, draft message and option checkboxes while another tab or the history is shown. The view mode is not saved with the session (a restart opens on the history); the draft is.
- Leave with `h`/`Esc` from a list (back to the history with the same graph selection) or `<space>s` again. `Esc` in the diff and the message box steps back to the lists first (the 1c rule). `q` quits from every pane.
- Opening focuses the first non-empty list (Unstaged, else Staged). A clean tree shows "Nothing to commit, working tree clean"; the message box stays usable for Amend.

**Focus.** `Tab`/`Shift-Tab` cycle Unstaged, Staged, Diff, Message. `Ctrl-w h/j/k/l` move by direction (lists left, diff top right, message below it); `Ctrl-w j`/`k` move between the stacked lists, `Ctrl-w l` from either goes to the diff. `l`/`Enter` on a file moves into its diff, `h`/`Esc` back. `y` copies the file's path. `<space>1`..`<space>4` jump straight to Unstaged, Staged, Diff (of the file under the cursor) and Message from any pane, including the message box in Normal mode; not in Insert mode, where space is text (`Esc` first).

**Side-specific keys.** Each list and each diff side is its own view for the keymap, so help and hints show only what applies: `<space>a`, `<space>A`, `<space>d` exist only on the Unstaged side, `<space>u`, `<space>U` only on the Staged side; the other key there is unbound, not a no-op.

**The diff.** The right box shows the diff of the file under the cursor of the focused list, read off the UI thread with the history diff's limits: the working-tree diff for Unstaged, the index diff for Staged (both paths of a rename), an untracked file as additions, a binary file as a note, and a conflicted file as the combined diff (one file; each line added, removed or context as in the result, with the result's line numbers; the per-parent columns are not shown). It is the same widget as the history diff (hunk jumps, wrap, context, whole file, ignore whitespace, split, search, `y`/`Y`, load anyway). The title reads `Diff  unstaged|staged  change n/m  <path>`. Here a unit is a change group, not a hunk of the patch: every run of consecutive added/removed lines (separated by unchanged lines) is its own unit, even when the patch merged several into one hunk (changes less than twice the context apart). The group under the cursor (the cursor on unchanged lines belongs to the group above) is highlighted: a bar in the left margin of each of its rows (`│`, `|` in the ASCII set) and, in the 24-bit schemes, a quiet tint behind them (`GroupBackground`, below the selection tint); the 16-colour and `NO_COLOR` tiers have the bar alone. The mark is off while a Visual range is open, since the range is then what the keys act on. The staging buttons in the title row, and the same chips on the group's first line, act on this group. `]c`/`[c`, the title count and the stage, unstage and discard keys all use these groups, always (the history diff keeps the patch's own hunks: only the working tree stages, decided in #386).
- It follows the cursor and reloads after every status read, also when the file's status letters did not change (a second edit), keeping the cursor row. A path that is not valid UTF-8 shows its diff too, with replacement characters.
- Zoom (`<space>z` or the `zoom` button) gives the diff the whole view (lists and message box hidden) until focus leaves the diff or zoom is used again.

**Stage and unstage.** All run off the UI thread, one operation per tab at a time (a key pressed meanwhile says "busy"). When one is done the tab re-reads its status and the cursor lands on the same index, so the keys repeat. Failures show git's message in the status bar. Paths are always passed literally.
1. **Files.** `<space>a` in Unstaged stages the file under the cursor (a deletion or rename as git reports it); `<space>u` in Staged unstages it (a rename unstages both paths). `<space>A` stages all (conflicted files skipped, the count reported), `<space>U` unstages all. Neither needs a confirm, since nothing is lost. A double-click on a row does the same as the key.
2. **Ranges of files.** `v` or a mouse drag in a list starts a Visual range (rows drawn like the selected row); the motions move its far end, `<space>a`/`<space>u` act on every file of the range in one git call (conflicted files skipped and counted, as in stage all), and `y` copies the paths one per line. The range ends when the operation starts, or on a click, `Esc`, `q`, `v`, a change of focus, or a refresh that changes the list. The title rows have `stage all` / `unstage all` buttons at the right end (left out when there is no room).
3. **Hunks and lines.** In the diff of an Unstaged file `<space>a` stages the change group under the cursor (the cursor on unchanged lines belongs to the group above); in the diff of a Staged file `<space>u` unstages it. The patch of a group is built like a Visual range patch, so the other groups of the same hunk stay as they were. `v` (or a drag) starts a Visual range in the diff and `<space>a`/`<space>u` then act on the added and removed lines inside it only (context, header and note rows are ignored; a range with no change is refused). A double-click on a diff row acts on its change group.
   - The patch is never taken from the rows on screen: the file's diff is read again with git's default context (so the context, whole-file and whitespace options do not matter), the picked lines are found in it by line number and text, and the resulting patch is applied to the index. Git refuses a patch that does not apply and leaves the index alone. If a picked line is no longer in the file the operation stops with "changed since the diff was read" and the diff reloads.
   - Lines of an untracked text file are staged like any other lines (the file is recorded as intent-to-add first; the rest stays an unstaged addition). A mode change is never part of a partial patch.
   - Files that cannot be split (binary, staged-added, deleted, renamed without edits, untracked binary or symlink, a name that is not valid UTF-8) go whole when the key acts on the change group, and the status bar says so. Lines picked in a Visual range are never widened to the whole file: the status bar warns "cannot be split into lines" and nothing changes. A binary file has no hunks, so the key stages it whole.
   - The keys are refused while the diff is loading, for a conflicted file, and while the diff is cut short (`<space>vl` loads all of it first). Afterwards the status is read again and the cursor goes to the change group now at the same position (the next group when it was staged), or to the next file when the file is done. A renamed file with edits can be split: the rename header travels with the patch.
4. **Conflicted files** show `!`. `<space>a` (or a double-click) on one opens a Confirm, "Mark X as resolved?", saying that conflict markers still in the file are not removed or checked and that `git checkout --merge <file>` re-creates the conflict (overwriting the file's current content). `y` runs `git add` on it (status: "Marked X resolved"). Stage all and a Visual range skip conflicted files and count them; a range of only conflicted files is refused. The banner with continue, abort and skip and the mergetool key are built (workflow 4); a conflict editor stays post-MVP.
5. Every action is also a palette entry. The lists refresh from the tab's status after each operation and on the file watcher; `<space>r` forces a refresh.

**Discard.** Nothing discarded can be undone: git keeps no copy, so there is no reflog hint and no dangling stash; the Confirm dialog is the safety. `<space>d` on a file in Unstaged, or on a change group or Visual lines in the Unstaged diff, opens a Confirm that names what is lost; `y` runs it. There is no key for discarding staged changes (unstage first) and no "discard all".
- **Dialog texts:** "Discard all unstaged changes to X?" (plus "The deleted file is restored from the index." for a file deleted on disk and "Its staged changes are kept." when the file also has staged changes), "Discard this change (N lines) of X?", "Discard the N selected lines of X?", and for an untracked file "Delete the untracked file X?" (an untracked directory: "...and everything in it").
- **What happens:** a tracked file is put back as the index has it (staged changes stay). An untracked file is deleted from disk, but never a tracked or an ignored path. Change groups and lines are cut from the file's unstaged diff, read again with git's default context (the same patches staging uses), and applied backwards to the working tree only; the index is never touched. Git refuses a patch that does not apply and changes nothing; a line no longer in the file stops with "changed since the diff was read".
- **Whole file behind a hunk key:** a file with no hunks to pick from (binary, deleted on disk, untracked) is discarded whole when the key acts on the hunk, and the dialog says so. A file that shows hunks but cannot be cut (text that is not valid UTF-8, a renamed file with a quoted path) is refused with "cannot be split into hunks or lines ... nothing was discarded; discard the whole file from the list". Lines picked in a Visual range are never widened to the whole file.
- **Ranges of files:** one Confirm, "Discard changes to N files?", names the untracked files, which are deleted (the first five, then a count), and says it cannot be undone. The range ends when `y` is answered; `n`/`Esc` keep it. Conflicted files, submodules and untracked names that are not valid UTF-8 are left out and counted in the dialog. Files go in batches (one restore for the tracked files, then one clean for the untracked, in chunks of 64 paths); if one fails the status bar says so and that the tracked files may already be restored.
- **Refused before the dialog:** a conflicted file, a submodule, and an untracked name that is not valid UTF-8 on an OS without a byte-exact delete (Windows, macOS; Linux deletes it by its exact bytes behind the same dialog).
- Same busy rule as staging. The discard buttons and mouse path are in the Mouse section.

**Message and commit.**
1. **Writing.** `<space>c` (from a list or the diff) focuses the message box and enters Insert mode. The built-in editor: the first line is the subject, a blank line then the body; the title counts the subject width as `12/72` and the part past column 72 is an error-coloured hint, never enforced. `Esc` leaves Insert mode but stays in the box (its Normal mode: `i`/`Enter` back to Insert, `h`/`Esc` back to the lists). A click in the box places the cursor at the pointer before it starts Insert mode.
   - Editor keys (Insert mode; not rebindable rows in the Keymap table): typed text and pastes insert at the cursor (line endings become new lines); `Enter` is a new line; `Backspace`/`Delete` join lines at their ends; `Left`/`Right` wrap across lines; `Up`/`Down` keep the display column (a wide character takes two, a shorter line clamps the cursor and the wanted column returns on the next longer line); `Tab` inserts spaces up to the next multiple of four (no tab character; `Shift-Tab` does nothing); `Home`/`End` and `Ctrl-a`/`Ctrl-e` go to the line edges; `Ctrl-u` deletes to line start, `Ctrl-k` to line end, `Ctrl-w` the previous word. The cursor never splits a wide character, emoji sequence or combining mark.
   - Lines are not wrapped on screen: the cursor's line scrolls sideways, and the box scrolls vertically with the cursor line.
2. **Draft.** The message, SignOff and No-Verify (not Amend) are kept per tab until a commit succeeds, and saved in the session file (debounced after each edit and on exit) so they survive restarts. Quitting never loses a message: `q` quits at once; only when the final save fails while a message is unsent does the first `q` warn and the next one leave. Locating a missing tab moves its draft to the replacing tab (when that tab holds a message of its own, both stay and a notice says so). Closing a tab with an unsent message needs a second `Ctrl-w c` (workflow 0).
3. **Options.** `Alt-s` (SignOff, `-s`), `Alt-n` (No-Verify, `--no-verify`) and `Alt-a` (Amend) toggle from anywhere in the view including Insert mode, or by clicking the checkboxes. All start off; hooks run unless No-Verify is on.
4. **Commit.** `Alt-Enter` or `Ctrl-g` in Insert mode, `<space>c` in the box's Normal mode, or the `Commit` button. `Ctrl-g` is the fallback for terminals that swallow `Alt-Enter`; the other options fall back to the checkboxes and the palette. (Verified in tmux on Linux with Terminal.Gui 2.5.0: `Alt-s`/`-n`/`-a` arrive as Alt plus the letter; `Alt-Enter`, Esc then CR, arrives as `Ctrl-Alt-m` and is mapped to `Alt-Enter`. Windows and macOS terminals are not verified.)
   - The message goes to `git commit -F -` on stdin, so comment lines (`#`) are kept, unlike a commit through `$EDITOR`. Hooks, signing and the author come from the user's git config. The commit runs off the UI thread with a 10 minute timeout so hooks can run; the box is read-only meanwhile, and `Esc` cancels it (git is killed, the draft stays; the notice says to check the history, because git may have finished first).
   - `<space>te` (also in the lists and the diff) commits through `$EDITOR` instead: the TUI suspends and git's own `commit` runs on the staged index. The draft is kept; git uses its own template.
   - **Nothing staged.** With nothing staged but unstaged or untracked changes present (Amend off, no merge, cherry-pick or revert in progress), the commit action opens a Confirm, `Nothing is staged. Stage all changes and commit?`. `y` stages everything as `<space>A` does (untracked included, conflicted skipped), waits for the status re-read and commits; `n`/`Esc` leaves the draft and the index untouched. The message and options are fixed when `y` is pressed.
5. **Refusals,** without running git, with the reason in the status bar and focus kept in the box: an empty message, and nothing staged with Amend off and nothing to stage ("Nothing staged"). An amend with nothing staged is allowed and rewrites only the message. While a merge, cherry-pick or revert is in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`) the nothing-staged check is skipped, because finishing such an operation with an empty tree is valid; the operations themselves are built (workflows 3 and 4). Amend is refused when there is no commit yet and during a merge.
6. **Amend.** Toggling it on fills the box with HEAD's message when the draft is empty (the draft is kept aside and restored when Amend is toggled off), shows `Amending <short hash> <subject>` in the box title and runs `git commit --amend`; staged changes are added to the amended commit. If HEAD is already on the upstream branch, the status bar warns that a force push will be needed. Undo path: the status message after the commit names the old hash ("Amended to abc1234; previous commit 9f8e7d6, `git reset --soft 9f8e7d6` restores it") and the reflog keeps it reachable.
7. **Finishing a merge.** The box starts with git's `MERGE_MSG` (comment lines dropped); untouched, it does not count as an unsent message.
8. **After a commit.** The draft and Amend are cleared, SignOff and No-Verify keep their state, the lists refresh, the status bar says `Committed abc1234` and focus returns to the lists. The history view picks up the commit through the normal refresh.
9. **Failure.** A failing hook (non-zero exit), a prompt, or a signing program that needs a passphrase fails the commit, keeps the draft and the staged state, and shows git's and the hooks' output (stderr, then stdout such as `nothing to commit`) in a scrollable overlay (title `Commit failed`, `Esc` closes it; the status bar carries the first line). Under the output a hint says that no keyboard input reaches git and its hooks (stdin is closed) and that `<space>te` runs `git commit` in the real terminal where it can prompt. Files a hook rewrites show as new unstaged changes after the refresh. A commit that fails on a tab that is not active leaves the status bar notice and keeps its output; the overlay opens once that tab is shown.

**Key rule.** Standard vim/neovim keys keep their vim meaning (`j` `k` `h` `l` `gg` `G` `/` `n` `N` `v` `y` `i` `Ctrl-d` `Ctrl-u` ...). A Gitunia-specific action never takes a bare vim command (`s` substitute, `u` undo, `c` change, `a` append, `x` delete, `r` replace, `D` `M` `E` motions and so on) but a leader chord (`<space>` then a key) or a `Ctrl-`/`Alt-` combination. Check this before adding a binding.
- **Leader keys in use:** `<space>o` `p` `x` `m` `s` `z` `<Tab>` (global); `<space>a` `A` `u` `U` `d` `c` `r` (working tree and refresh; `<space>c` is a single key in each of the lists, the diff and the box, each its own view); `<space>t` + `d` `m` `e` (external tools); `<space>h` (jump to HEAD) and `<space>f` (find a ref) in the graph; `<space>v` + `w` `i` `f` `s` `l` (diff view options); `<space>j` (quick jump); `<space>1`..`<space>4` (working-tree pane jumps).
- **`Alt-`:** `Alt-p` and `Alt-d` are Picker-only, `Alt-1`..`Alt-9` jump tabs, `Alt-s` `Alt-n` `Alt-a` act only in the working-tree view.
- **Documented exceptions:** a few bare keys keep a non-vim meaning on purpose, because the panes are read-only or the key is navigation: `q` (back/quit; vim records a macro), `h` `l` `]` `[` (navigation), `?` (help; vim searches backwards, same as `less` and lazygit), `Y` (copy the file's rows; close to vim's yank line), `+` `-` (diff context) and `gd` `gc` `gf` (bottom-pane tabs, paired with the `]`/`[` cycling).

**Mouse.** Same action path as keys. A click on a file row selects it and a double-click stages or unstages it (a deliberate deviation from "double-click opens": the diff already follows the selection). The diff's title row gets `stage`/`unstage` and `discard` buttons next to the hunk buttons; the list titles get `stage all`/`unstage all`; the `SignOff`, `No-Verify` and `Amend` checkboxes toggle and `Commit` commits; a drag over diff rows or file rows starts a Visual range; a click on the tab's dirty marker opens the view; a click on the diff box focuses it (on screens of 80 columns or more; narrow screens keep `Enter`/`l`). The discard itself is reached from the palette, not a list button.

### 3. Branches, tags, merge, cherry-pick, reset and the reflog (M3)

Planned keys, written here and not yet in the keymap table: each implementing issue adds its table rows with its bindings (`KeymapDocumentationTests` fails on a documented binding that is not registered). `<space>t` is the tools prefix (`td` `tm` `te`), so these use other prefixes: `<space>b` branches, `<space>g` operations on the selected commit, `<space>T` tags, `<space>w` worktrees, `<space>S` stashes. Every action is also a palette entry and has a mouse path (a button in the sidebar section header, or a click on the row and then the same key).

**Common rules.**
- **Target.** The graph acts on the selected commit, the sidebar on the selected row. The status bar names the target while a prompt is open.
- **Prompts.** A name is typed in a one-line prompt in the status bar (Insert mode: `Enter` runs, `Esc` cancels), prefilled where a default exists. A choice of a branch or ref uses the fuzzy picker of `<space>f`.
- **Confirm.** Every destructive step (delete, force, hard reset, drop, remove, discard) opens the Confirm dialog of the Modes section. It names the exact target, says what is lost and shows the undo hint (reflog hash, `git stash store <hash>`, the branch's old tip); `y` runs it, `n` or `Esc` drops it.
- **Refusals** run no git and say why in the status bar: a branch checked out in another worktree (the message points to `<space>ws`), a dirty tree where git would refuse, a detached HEAD where a branch is needed.
- **No editor, no prompts.** Every call that could open an editor passes `--no-edit` or `-c core.editor=true`; stdin is closed, so a hook that prompts fails with git's output shown in an overlay like a failed commit. Messages are edited in the commit box or with `<space>te`.
- **Result.** Success posts a status message with the new state; a failure shows git's message. An operation that stops on conflicts is not a failure: it becomes an in-progress operation (workflow 4). All lists refresh from the tab's status afterwards and the cursor follows the new HEAD or the created ref.

**Branches** (`<space>b` + key; implemented in #35 and #36):
1. `bc` create: prompt for the name, start point the selected commit (graph) or branch (sidebar); the new branch is checked out. `bC` creates without checking out. A name in use is refused before git runs; an invalid name is refused by `git check-ref-format` and shown as an error.
2. `bs` switch (checkout): a type-to-filter prompt (`Up`/`Down`/`Tab` choose, `Enter` runs) listing local branches, remote branches that have no local twin (a tracking branch is created) and, from a graph commit, a detached checkout there. In the sidebar `Enter` still only selects, so `bs` is the explicit checkout. The current branch and one held by another worktree are refused (the message names the worktree path and points to `<space>ws`). A dirty tree that conflicts with the target fails with git's message; nothing is stashed behind the user's back.
3. `br` rename the selected branch (prompt prefilled with its name). `bd` delete: Confirm naming the branch and its tip with the undo command; when the branch has commits that neither HEAD nor its upstream reach, a second, explicit Confirm says its commits become unreachable and prints `git branch <name> <hash>`. The current branch and one held by another worktree are refused. From the graph, `br` and `bd` act on the one local branch at the selected commit (several: pick one in the sidebar).
4. `bu` set the upstream of the selected local branch (type-to-filter picker of remote branches, nothing is fetched), `bU` unset it (the status line prints the `git branch --set-upstream-to=` command that restores it). Ahead/behind (`↑n↓m`) already shows in the sidebar and the tab bar; it is not new work.
5. Mouse: the Branches section header carries chips `[new]` `[switch]` `[rename]` `[delete]` `[new-only]` (`bc` `bs` `br` `bd` `bC`) at its right end, as many as fit after the title (the last go first). They act on the selected row like the keys, so click the branch row, then the chip; `[upstream]`/`[unset-up]` (`bu`/`bU`) come last, after `[revert]`, so a narrow sidebar drops them first and the right-click menu is the path. The same header-chip mechanism (`SidebarHeaderButtons`) serves the other sections. After create, switch, rename or delete the graph cursor moves to the created or checked-out branch's commit (after a delete, to the commit of the current branch).
6. No single-letter sidebar shortcuts (`d`, `r`): they are vim operator and replace keys (vim-clash rule), so the sidebar uses the same `<space>b` chords on the selected row. Every action is in the command palette.

**Integrating** (`<space>g` + key, on the selected commit or branch; the target is the current branch):
1. `gm` merge the selection into the current branch. Fast-forward when possible; `gM` forces a merge commit (`--no-ff`), `gs` squashes (`--squash`, leaves the changes staged for the commit box; no operation is in progress afterwards). The message is git's default (`--no-edit`). It runs at once, no Confirm (nothing is lost); the status line names the old tip and the undo (`git reset --hard <old>`, also `ORIG_HEAD`). A stop on conflicts becomes the in-progress merge (banner, `<space>gc`, `<space>ga`); a conflicting squash leaves the files marked `!` but no operation and says to resolve and commit. A branch row merges that branch, a tag row or a graph commit that commit. Refused during any in-progress operation and on a detached or unborn HEAD.
2. `gr` rebase the current branch onto the selection (implemented in #39; non-interactive with `-c core.editor=true`, interactive is #399). The Confirm names both sides and the undo (`git reset --hard <old tip>`, also `ORIG_HEAD` and the reflog) and warns when the branch has an upstream (history rewrite, force push needed; the text is stronger when every commit is already on it). Refused with no git run when an operation is in progress ("finish or abort the <operation> first"), HEAD is detached or unborn, the target is the current commit, or tracked files have uncommitted changes (untracked files are fine). A rebase that stops on conflicts is the in-progress rebase of workflow 4 (banner, `<space>gc`, `<space>ga`, `<space>gk`); the status line warns and names the keys. When the commits being replayed (the target does not have them) contain merge commits, the Confirm says so ("N merge commits in that range will be flattened"): a plain rebase drops the merge and replays its side's commits as one line; keeping the merges (`--rebase-merges`) is interactive rebase, #399. The count is one `git rev-list --merges` call, so the Confirm opens when it answers. Mouse: the right-click menu of a graph or sidebar row (Mouse section) and the `[rebase]` chip on the Branches and Tags headers (on the selected row; the chips come after the section actions and `[rebase]`, `[reset]`, `[pick]`, `[revert]` drop from the end, so a narrow sidebar drops them and the right-click menu is the path). While the Confirm is being prepared a second `<space>gr` says "Already asking", and the Confirm is dropped with a warning if an overlay was opened or HEAD moved meanwhile.
3. `gp` cherry-pick the selection onto the current branch (implemented in #40): the commit under the graph cursor, the commits of a Visual range in the graph (applied oldest first, whatever lanes they sit on) or the tip of the selected sidebar branch or tag. `gv` reverts it the same way (`--no-edit`); a range is reverted newest first, git's own order. Both run at once, no Confirm (nothing is lost); the status line names the old tip and the undo (`git reset --hard <old>`, also in the reflog). A commit whose changes HEAD already has is dropped (`--empty=drop`), and when that is every commit the status says "Nothing to cherry-pick". A stop on conflicts becomes the in-progress cherry-pick or revert (banner, `<space>gc` continue, `<space>ga` abort, `<space>gk` skip the commit). A detached HEAD is allowed (git allows it); refused during any in-progress operation and on an unborn HEAD. The Visual range ends when the action starts. Mouse: the `[pick]` and `[revert]` chips on the Branches and Tags headers (the selected row), and the row menus (right-click on a graph or sidebar row); a Visual range is picked with `<space>gp`/`<space>gv`.
4. A merge commit among the commits opens a one-key status bar prompt, once for the whole range: `1`, `2` pick the mainline parent (parent hashes and subjects shown), `Esc` cancels; a small overlay lists the same parents as rows, a click on a row answers like its digit and a click outside cancels. The answer is passed as `-m` to every commit of the call. Git accepts `-m 1` on a plain commit but fails the whole call on `-m 2`, so with a plain commit in the range only `1` is accepted and the `[2]` row of the overlay is dimmed and marked unavailable. The overlay is placed again when the terminal is resized.
5. A dirty tree is refused for rebase; for the others git decides and its message is shown.

**Reset** (`<space>gx`, not a single letter, so it cannot clash with vim motions or `g` chords): the current branch moves to the selected commit. A small dialog offers `s` soft (keeps index and files), `m` mixed (keeps files, clears the index), `h` hard. Soft runs at once and shows the old tip in the status ("Reset to abc1234, was def5678"). Mixed runs at once when the index equals HEAD; otherwise it opens a Confirm saying the staged state (including partially staged hunks) is dropped and cannot be recovered from the reflog. Hard opens a Confirm that lists the tracked files and the count of changes that will be lost (untracked files stay unless the target commit tracks a file at the same path, and it says so) and the old tip for the undo (the reflog restores commits, never uncommitted changes, and the Confirm says so). Refused during an in-progress operation and on a detached or unborn HEAD. The status after a reset names the old tip and the undo (`reset [--hard] <old hash>`, also `ORIG_HEAD` and the reflog). Implemented in #392 (`ResetActions.Begin(tab, commit)` is the entry the reflog view calls). The reflog view offers the same key on an entry. Mouse: the `[reset]` chip on the Branches and Tags headers, and the graph row menu.

**Tags** (`<space>T` + key; implemented in #397): `Tc` create at the selected commit, or the commit of the selected sidebar row: a name prompt, then a message prompt (empty makes a lightweight tag, text an annotated one, so no editor is needed; the call also passes `-c core.editor=true`). A name in use is refused before git runs, an invalid one by `git check-ref-format`. `Td` delete the local tag: Confirm with the tag's target hash and the undo command (`git tag <name> <hash>`; for an annotated tag the hash is the tag object, so the message survives); a copy on a remote is not touched. `Tp` push the selected tag to a remote: a type-to-filter remote prompt, then one ref (`git push --no-progress <remote> refs/tags/<name>`), no progress, no prompts, git's first error line shown; the push of workflow 5 generalises it. `TD` deletes the tag on a remote (`git push <remote> --delete refs/tags/<name>`): remote prompt, then a Confirm naming the remote and the tag and saying the local tag stays; no undo besides re-pushing the tag. From the graph the tag actions act on the one tag at the selected commit (several: pick one in the sidebar). Mouse (#434): the Tags header carries `[tag]` `[delete]` `[push]` `[remote-del]`, and the Branches and Tags headers `[merge]` `[merge-commit]` `[squash]` (`gm` `gM` `gs`), as many as fit; they act on the selected sidebar row, so click the row, then the chip. `[merge-commit]` and `[squash]` (and `[remote-del]`) only show in a wide sidebar; the right-click popup of a sidebar or graph row (#428) lists all tag and merge actions at any width. No sidebar `d` shortcut: it is a vim operator (vim-clash rule), so the sidebar uses the same `<space>T` chords on the selected row.

**Reflog** (`<space>l`, graph and sidebar; implemented in #398): a modal list of the reflog of `HEAD`, or, from a local branch row under the sidebar cursor, of that branch (`refs/heads/<name>`). Newest first (the last 1000 entries): short hash, `HEAD@{n}`, action and message, relative time. `j`/`k`/`gg`/`G`/`Ctrl-d`/`Ctrl-u` move; `Enter` selects the entry's commit in the graph and keeps the list open (a commit outside the loaded graph says so in the status bar); `<space>gx` resets the current branch to the entry (the reset rules above, via `ResetActions.Begin`); `<space>bc` opens the new-branch prompt at the entry; `<space>bs` checks the entry out with a detached HEAD (git refuses a conflicting dirty tree, nothing is stashed). The last three close the list first, because their prompt and Confirm live in the status bar and the confirm overlay. `Esc` or `h` returns. The list is read from git on open and on `<space>r`. `<space>l` inside the list opens the ref picker (implemented in #424): `HEAD` and the local branches, type to filter, `Tab`/`Down`/`Up` move, `Enter` opens that reflog, `Esc` returns to the list. `/` searches the entries (hash, `HEAD@{n}`, action and message, case-insensitive): the box and the match count (`/fix_  2/5 matches`) are in the list's footer, `Enter` keeps the search and leaves the box, `n`/`N` step through the matches (counts work, wraps), `Esc` in the box cancels and `Esc` in the list clears the search before it closes the list. Mouse: below the entries four action rows (`Enter` select, `<space>gx` reset, `<space>bc` branch, `<space>bs` checkout) run on the highlighted entry when clicked, so a click on an entry then a click on an action replaces the keys; in the picker a click on a row opens that reflog. The graph `<space>l` still opens HEAD's reflog directly; the picker is one key away.

### 4. In-progress operations: banner, continue, abort, skip (M3; banner, continue, abort and skip done, the blocking of item 5 comes with each blocked feature)

One flow serves merge, rebase, cherry-pick and revert, and the interactive rebase of M4 (#399) reuses it. `RepositoryOperationDetector` names the operation from the git directory.

1. **Banner.** While an operation is in progress the tab shows a banner on the frame's bottom edge (the line under the panes, in the status bar's colours): `MERGING feature/x  3 conflicts  <space>gc continue  <space>ga abort` (a rebase adds `step 2/5` and `<space>gk skip`; a cherry-pick or revert of a range shows how many commits are left, `3 commits left`). The title names the merged branch (read from git's default merge message, so a custom message gives just `MERGING`), the rebased branch, or the short hash of the picked or reverted commit. Each key group is also a clickable chip (mouse views `BannerButton.*`). `<space>gc`, `<space>ga` and `<space>gk` work in the graph, the sidebar and the working-tree view (where conflicts are resolved). On a terminal under 10 rows the banner is dropped and an empty status message shows the same text. It is read from the repository's state files (`MERGE_MSG`, `rebase-merge/`, `sequencer/`), so it survives a restart of the app, and the tab bar shows a `!` marker.
2. **Conflicts.** The working-tree view lists conflicted files with `!`. `<space>a` marks one resolved (workflow 2, item 4); `<space>tm` opens the configured mergetool. Resolving inside Gitunia (a conflict editor) stays post-MVP.
3. **Continue** (`<space>gc`, or the banner chip): refused while conflicted files remain ("2 files still conflicted"); otherwise runs `<op> --continue` with no editor (`-c core.editor=true`, so git's prepared message is used). A rebase may stop again at the next commit: the status line warns and the banner updates. A continue that fails without a new conflict (a dirty tree, a hook) shows git's reason and leaves the operation as it was.
4. **Abort** (`<space>ga`): Confirm that says the work of the operation is undone and resolutions are lost, with the old tip. **Skip** (`<space>gk`; rebase, cherry-pick and revert only): Confirm naming the commit that is dropped.
5. **Other actions are blocked** (each blocked feature adds its own refusal: #38 merge (done), #39 rebase, #40 cherry-pick and revert (done), #392 reset, #419 branch switch (done)). While an operation is in progress branch switch, merge, rebase, reset and new cherry-picks are refused with "finish or abort the <operation> first". Staging, committing (as the continue step) and viewing keep working.
6. **Completion.** A continue that finishes the operation posts "Merged feature/x into main (abc1234)" or the equivalent and removes the banner.

### 5. Fetch, pull and push (M4, keys planned here)

`<space>F` fetch the current branch's remote with `--prune` (`<space>F` again from the picker it opens chooses all remotes), `<space>L` pull of the current branch (fast-forward only by default; a diverged branch says so instead of creating a merge commit), `<space>P` push of the current branch. Chips in the tab bar and the sidebar header carry the same three actions. Each runs in the background with a progress line in the status bar (git's `--progress` output), `Esc` cancels while it runs (a Confirm is not needed; the status bar says "cancelled"), and the tab stays usable. `<space>x` stays the status message dismiss. `<space>Pd` deletes a remote branch (`push --delete`, remote picker then branch picker, Confirm naming both; the undo hint is the branch's old tip). A push to a branch without an upstream prompts for the remote (default `origin`) and sets the upstream. A force push is only ever `--force-with-lease`, behind a Confirm that names the remote branch it will overwrite. The capitals `F` `L` `P` `S` `T` are not "all" variants of the lowercase keys; they are chosen because the lowercase leader keys are taken, and the palette shows the full sequences. When the graph or the sidebar has no suitable row selected for a stash or tag key, a picker opens. Credential prompts and the generalised tag push are handled by #44.

### 6. Worktrees (M3)

`<space>w` + key. The sidebar's Worktrees section shows each worktree with branch, dirty counts, ahead/behind and `locked`/`prunable` flags, so dirty state across all worktrees is visible at a glance without switching. The counts read `S2 M3 U1` (staged, changed incl. untracked, conflicted; a zero is left out and a clean worktree shows none) and `↑n↓m` is the worktree branch against its upstream, both from `git status` run in that worktree. The tab's own worktree follows its live status; the others are read when the tab or its worktree list changes and then every 30 s, so a change made elsewhere shows up within that interval.
1. `wa` add: prompt for a branch name, then for the path (default a sibling directory named after the branch). An existing branch is checked out (one held by another worktree is refused); a new name (also one that exists only as a remote branch: no tracking branch is made) creates the branch at the selected sidebar row's or graph commit, else HEAD. The tab stays where it is.
2. `ws` switch (on a worktree row): the tab's active worktree changes in place and keeps its view state (selected commit, scroll, pane layout, filters, sidebar); HEAD, status and graph are read again for the new worktree, and the tab bar title and the sidebar `*` follow. `<space>wt` opens it as its own tab instead (or focuses the tab that already shows it). Refused without running git: no worktree row selected, the bare entry, a prunable one (its directory is gone), the worktree the tab is on, a worktree another tab shows (the message points to `wt`), and while an operation of the tab is still running. Switching never changes the process working directory.
3. `wd` remove: Confirm naming the path; a dirty worktree needs a second Confirm that lists what is lost and says there is no undo (`--force`); a locked one is refused until unlocked; the main worktree and the one the tab is on are refused.
4. `wl` lock (prompt for an optional reason), `wu` unlock, `wp` prune: lists the prunable entries in a Confirm and runs `git worktree prune` on `y`.
5. Mouse: the Worktrees section header carries chips `[add]` `[remove]` `[lock]` `[unlock]` `[prune]` (`wa` `wd` `wl` `wu` `wp`), as many as fit after the title. They act on the selected row, so click the worktree row, then the chip. `ws`/`wt` have no chip (palette).

### 7. Stashes (M3)

Keys are `<space>S` chords, not bare sidebar keys (`a` `p` `d` are vim keys, see the key rule). `Ss` `Su` work from every view; `Sa` `Sp` `Sd` act on the stash row under the sidebar cursor and say "Select a stash in the sidebar first" elsewhere (no picker: a destructive key never guesses a target).
1. `Ss` push the tracked changes, `Su` push them plus the untracked files (ignored ones never): a status-bar prompt for an optional message (`Enter` runs, `Esc` cancels). "Nothing to stash" is a warning and points at `Su`.
2. `Sa` apply, `Sp` pop: no Confirm (nothing is lost; git refuses when local changes would be overwritten and the status bar shows why). A conflict made by the call keeps the stash, starts no operation, marks the files `!` and says so in the status bar; if files were already conflicted (a stopped merge) git refuses and its message is shown. A pop that succeeds drops the entry and the message carries the restore command.
3. `Sd` drop: Confirm with the stash name and subject; the hint is `git stash store -m "<subject>" <hash>`, and the same command with the full hash is in the status message after the drop. The target travels as its commit hash and is looked up again right before the call, so a stack that changed meanwhile does not redirect it ("no longer in the stash list", nothing happens). The restore command is written for a POSIX shell.
4. `Enter` on a stash row shows its diff in the detail pane (a stash is not a graph node).
5. Mouse: the Stashes section header carries chips `[push]` `[apply]` `[pop]` `[drop]` `[push-all]` (`Ss` `Sa` `Sp` `Sd` `Su`), as many as fit after the title. `apply`, `pop` and `drop` act on the selected row, so click the stash row, then the chip; `drop` keeps its Confirm.

## Look and colours

Colours are decided in one place: `PaletteTable` (`Gitunia.Tui.Views`) holds the colours of a scheme and `Palette` says which role uses which colour, so a theme later swaps the table and nothing else. There is no config file yet.

**Frame.** Every pane is a box and neighbouring boxes share their edges, so the whole screen is one frame split into panes (`FrameGrid`, drawn the same way by the UI and by `--print-frame`):

```
 1 gitunia                                                   <- tab row
╭─ gitunia ────────────┬─ History ──────────────────────────────╮
│  ▾ Branches 2        │ ○─╮  (main) Merge feature x    Ada ... │
│     feature/x        │ ●  │  Fix parser                       │
├─ Details ─ Changes ─ Files ─ 64d72f2 ─ 3 files ┬─ Diff ... ──┤
│ commit  64d72f2...                             │ @@ -1 +1 @@  │
╰────────────────────────────────────────────────┴─────────────╯
 NORMAL  1/4  j/k gg G Ctrl-d Ctrl-u ? : q                          <- status bar
```

- Rounded outer corners and plain T and cross junctions; the ASCII tier uses `+`, `-` and `|`. Where two boxes meet there is one line, not two.
- Titles sit in the top edge of each box as chips after one stretch of line: the sidebar shows the repository name, the graph `History`, the bottom pane its tabs, the commit and the file count, the diff its title (the file under the cursor and its position, no commit hash: the detail title already shows it) and buttons. The title row of the bottom pane is the line between the graph and the bottom panes (so it costs no extra row).
- From 100 columns of screen width the History list and the bottom detail and diff panes are padded by two columns (`PanePadding.SpaciousColumns`), so text and rows clear the frame lines; the sidebar and screens under 100 columns keep one. Text is padded by one column inside each box otherwise (rows keep their selection tint over the padding); the first and last column and the top and bottom edge row belong to the frame, so the frame costs two columns and one row more than the old divider layout (the old header row is gone).
- Focus: the focused pane's whole box is drawn in the accent colour (bold), every other line in the quiet border colour, and its title chip is the filled accent chip (the other titles are dim chips). Focus never relies on colour alone: the chips differ in text style, and with `NO_COLOR` the focused box is bold.
- Layout numbers: the sidebar (clamped 20-36 columns, shown from 60 columns) and the detail/diff split are widths including the padding; drag and the `Ctrl-w` size keys move the shared line.

**Selection and focus.** The selected row is a background tint, never inverse video, so the row keeps its own colours (branch, hash, diff colours stay readable). The focused pane's selection uses the full tint (blue), a pane without focus keeps a quieter grey tint, so the cursor stays findable. This holds for the graph, sidebar, detail, diff, picker rows, overlay rows and the Visual range. Focus is also shown by the frame (above): the focused pane's box is accent, its title is a filled accent chip and the other titles are dim chips. The active tab, the status mode (`NORMAL`, `INSERT`...) and an overlay's title bar are the same accent chip; the tab row and the status bar sit on a slightly lighter bar. An overlay's frame is drawn in the accent colour.

**Roles to colours** (accent blue, green, red, yellow, cyan, purple, muted grey):

| Role | Colour |
|---|---|
| Section titles, buttons, diff file header, the focused pane's frame | accent, bold |
| Graph lanes | a cycle of 12 colours (6 in 16-colour mode) |
| Graph commit marks | the lane's colour |
| Local branch / current branch (HEAD) / remote branch / tag | green / cyan / purple / yellow, bold (remote branches are not red: red means an error or a deletion; blue on a light terminal in the 16-colour scheme, where yellow and purple are taken) |
| Hash, date, line numbers, hints, counts, punctuation | muted |
| Added line or file / removed / modified | green / red / yellow; hunk header cyan |
| Errors | red, bold (in the status bar on its own bar) |
| Search match | dark text on yellow (kept on a selected row) |
| Overlay key column | yellow, bold |

**Colour depth.** The scheme follows the terminal. 24-bit (`COLORTERM=truecolor`, also used for 256-colour terminals because Terminal.Gui has no 256-colour output of its own): the Catppuccin palette (Mocha on a dark terminal, Latte on a light one), checked by a test for a contrast ratio of at least 4.5 on a typical dark and light terminal background (3.0 for dividers), also on selected rows. 16-colour (`TERM` without `256color`, or a Terminal.Gui driver that cannot do 24-bit; the driver is then forced to 16 colours): only the 16 named colours, so the terminal's own theme decides the exact look; the focused selection is bright black (bold), the unfocused one underlined. `NO_COLOR` or `TERM=dumb`: text styles only (reverse video for the selection, underline when unfocused, bold underline for search matches, underline for the current branch). Light backgrounds are recognised from `COLORFGBG` (background 7 or 15) and get darker accents and light tints; a terminal that does not set it is assumed dark. A terminal whose `TERM` has no `256color` and that sets no `COLORTERM` (for example over ssh without `COLORTERM` forwarded) gets the 16-colour scheme; set `COLORTERM=truecolor` to force the 24-bit one.

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
| Global | `Alt-1`..`Alt-9` | Jump to tab N (terminal multiplexers may take `Alt-N` for themselves; `3gt`, typing the number and then `gt`, does the same and never conflicts; configurable keys arrive with) |
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
| Global | `<space>s` | Open the working-tree view of the tab, or leave it (workflow 2) |
| Global | `<space>Ss` | Stash the tracked changes (prompts for a message; `Enter` runs, `Esc` cancels) |
| Global | `<space>Su` | Stash the tracked and the untracked changes (prompts for a message) |
| Global | `<space>gc` | Continue the merge, rebase, cherry-pick or revert in progress (refused while conflicted files remain; workflow 4) |
| Global | `<space>ga` | Abort the operation in progress (Confirm; the work of the operation is undone) |
| Global | `<space>gk` | Skip the commit a rebase, cherry-pick or revert stopped on (Confirm; not for a merge) |
| Global | `Esc` | Cancel pending chord, clear search highlight, leave Insert mode; in the graph also dismiss the status message |
| Lists | `Ctrl-d` `Ctrl-u` | Half page down / up (mouse wheel scrolls) |
| Lists | `Down` `Up` `PageDown` `PageUp` | Move / page (same as `j` `k` `Ctrl-d` `Ctrl-u`) |
| Lists | `v` | Start / end range selection (Visual mode: motions move the far end, `Esc` / `q` / `v` end it; graph, detail, diff and the working-tree lists) |
| Lists (Visual) | `y` | Copy the selected range (OSC 52) and end it when the copy worked (a refused or failed copy keeps the range): graph one hash per line, detail the rows' text, diff the rows with their `+`/`-`/space prefix and no gutter (a wrapped row once; notes skipped), a working-tree file list one path per line; text too large for OSC 52 (about 75 KB) is refused with a warning |
| Graph | `<space>h` | Jump to HEAD |
| Graph | `<space>f` | Jump to ref or hash (fuzzy) |
| Graph | `<space>j` | Quick jump: type text, labels appear on the commits in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Graph | `Enter` | Focus commit details |
| Graph | `y` | Copy commit hash |
| Graph | `<space>r` | Refresh repository |
| Graph | `<space>td` | Open the selected commit (against its first parent) in the configured difftool |
| Graph | `<space>tm` | Resolve conflicts in the configured mergetool |
| Graph | `<space>te` | Commit the staged changes with `$EDITOR` (git's own editor; the working-tree view's `<space>te` does the same) |
| Graph | `<space>bc` | Create a branch at the selected commit and check it out (status bar prompt) |
| Graph | `<space>bC` | Create a branch at the selected commit without checking it out |
| Graph | `<space>bs` | Switch to a branch (type-to-filter prompt; a remote branch gets a tracking branch; the selected commit can be checked out detached) |
| Graph | `<space>br` | Rename the local branch at the selected commit (prefilled prompt) |
| Graph | `<space>bd` | Delete the local branch at the selected commit after a Confirm (a second one when it is unmerged) |
| Graph | `<space>bu` | Set the upstream of the local branch at the selected commit (type-to-filter picker of remote branches) |
| Graph | `<space>bU` | Unset the upstream of the local branch at the selected commit |
| Graph | `<space>Tc` | Create a tag at the selected commit (status bar prompts: name, then an optional message; empty is lightweight) |
| Graph | `<space>Td` | Delete the local tag at the selected commit after a Confirm |
| Graph | `<space>Tp` | Push the tag at the selected commit to a remote (type-to-filter picker; one ref, no progress) |
| Graph | `<space>TD` | Delete the tag at the selected commit on a remote (picker, then a Confirm) |
| Graph | `<space>gm` `<space>gM` `<space>gs` | Merge the selected commit into the current branch: fast-forward when possible / always a merge commit (`--no-ff`) / squash (changes staged, no commit; workflow 3) |
| Graph | `<space>gp` `<space>gv` | Cherry-pick / revert the selected commit or the Visual range onto the current branch (also in Visual mode; a merge commit asks for the mainline; workflow 3) |
| Graph | `<space>gx` | Reset the current branch to the selected commit: `s`/`m`/`h` picks soft, mixed or hard; Confirm where work is lost |
| Graph | `<space>gr` | Rebase the current branch onto the selected commit (Confirm; non-interactive; refused with an operation in progress, tracked changes or a detached HEAD) |
| Graph | `<space>l` | Open the reflog of HEAD (a list of its entries; see the Reflog flow) |
| Graph | `]` `[` | Next / previous tab of the bottom detail pane (Details, Changes, Files; wraps) |
| Graph | `gd` `gc` `gf` | Show the Details / Changes / Files tab of the bottom detail pane |
| Detail | `Enter` `l` | Go to the diff of the selected file (Changes tab): focus it; on narrow screens open it on demand |
| Detail | `y` | Copy the path of the selected file |
| Detail | `<space>j` | Quick jump: type text, labels appear on the rows in view that contain it (file paths on Changes), press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Detail | `<space>r` | Retry loading the commit details after a failure |
| Detail | `]` `[` | Next / previous tab of the detail pane (Details, Changes, Files; wraps) |
| Detail | `gd` `gc` `gf` | Show the Details / Changes / Files tab |
| Detail | `h` / `Esc` | Back to graph |
| WorkingUnstaged | `<space>j` | Quick jump: type text, labels appear on the files in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingUnstaged | `<space>r` | Refresh the status |
| WorkingUnstaged | `y` | Copy the path of the file under the cursor |
| WorkingUnstaged | `<space>a` | Stage the file under the cursor (the cursor keeps its index; a conflicted file asks to mark it resolved) |
| WorkingUnstaged | `<space>A` | Stage all files except conflicted ones |
| WorkingUnstaged | `<space>d` | Discard the changes of the file under the cursor after a Confirm; an untracked file is deleted (no undo) |
| WorkingUnstaged (Visual) | `<space>a` | Stage the files of the range (the range ends; conflicted files are skipped) |
| WorkingUnstaged (Visual) | `<space>d` | Discard the files of the range after one Confirm that counts them and names the untracked ones, which are deleted (the range ends when confirmed; no undo) |
| WorkingUnstaged | `h` / `Esc` | Leave the working-tree view (back to the history) |
| WorkingUnstaged | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingUnstaged | `<space>te` | Commit with `$EDITOR` (git's own editor; the TUI suspends) |
| WorkingUnstaged | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingUnstaged | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingStaged | `<space>j` | Quick jump: type text, labels appear on the files in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingStaged | `<space>r` | Refresh the status |
| WorkingStaged | `y` | Copy the path of the file under the cursor |
| WorkingStaged | `<space>u` | Unstage the file under the cursor (a rename unstages both paths) |
| WorkingStaged | `<space>U` | Unstage all files |
| WorkingStaged (Visual) | `<space>u` | Unstage the files of the range (the range ends; conflicted files are skipped) |
| WorkingStaged | `h` / `Esc` | Leave the working-tree view (back to the history) |
| WorkingStaged | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingStaged | `<space>te` | Commit with `$EDITOR` (git's own editor; the TUI suspends) |
| WorkingStaged | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingStaged | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingMessage | `i` / `Enter` | Edit the message (Insert mode) |
| WorkingMessage | `<space>c` | Commit the staged changes with the message and options |
| WorkingMessage | `<space>te` | Commit with `$EDITOR` (the draft is kept: git uses its own template) |
| WorkingMessage | `<space>r` | Refresh the status |
| WorkingMessage | `h` / `Esc` | Back to the file lists |
| WorkingMessage | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingMessage | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingMessage (Insert) | `Enter` | Start a new line (the other editing keys are in workflow 2 and are not rebindable rows) |
| WorkingMessage (Insert) | `Alt-Enter` `Ctrl-g` | Commit; `Ctrl-g` is the fallback for terminals that swallow `Alt-Enter` |
| WorkingMessage (Insert) | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend |
| WorkingUnstaged | `Enter` `l` | Go to the diff of the file under the cursor (on a screen under 80 columns the diff replaces the list until `h` / `Esc`) |
| WorkingStaged | `Enter` `l` | Go to the diff of the file under the cursor (on a screen under 80 columns the diff replaces the list until `h` / `Esc`) |
| WorkingUnstagedDiff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingUnstagedDiff | `<space>r` | Refresh the status (the diff reloads with it) |
| WorkingUnstagedDiff | `]c` `[c` | Next / previous change group |
| WorkingUnstagedDiff | `]f` `[f` | Next / previous file (the diff holds the one file under the list cursor, so these only report the end) |
| WorkingUnstagedDiff | `Esc` / `h` | Back to the file list (closes a search first) |
| WorkingUnstagedDiff | `/` `n` `N` | Search in diff, next, previous |
| WorkingUnstagedDiff | `<space>vw` | Toggle word wrap |
| WorkingUnstagedDiff | `y` | Copy the cursor line (with its `+`, `-` or space prefix) |
| WorkingUnstagedDiff | `Y` | Copy the displayed rows of the file (not a pasteable patch) |
| WorkingUnstagedDiff | `+` `-` | Show more / fewer unchanged lines around the changes (title `[ctx N]`) |
| WorkingUnstagedDiff | `<space>vf` | Toggle showing the whole file instead of hunks (title `[file]`) |
| WorkingUnstagedDiff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| WorkingUnstagedDiff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs 80 text columns) |
| WorkingUnstagedDiff | `<space>vl` | Load anyway: read the file without the row and per-file limits |
| WorkingUnstagedDiff | `<space>a` | Stage the change group under the cursor (a file without hunks goes whole) |
| WorkingUnstagedDiff (Visual) | `<space>a` | Stage the selected added and removed lines only (the range ends) |
| WorkingUnstagedDiff | `<space>d` | Discard the change group under the cursor after a Confirm (a file without hunks is discarded whole; no undo) |
| WorkingUnstagedDiff (Visual) | `<space>d` | Discard the selected added and removed lines only after a Confirm (the range ends when confirmed) |
| WorkingUnstagedDiff | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingUnstagedDiff | `<space>te` | Commit with `$EDITOR` (the editor of the repository configuration; the TUI suspends) |
| WorkingUnstagedDiff | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingUnstagedDiff | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingStagedDiff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingStagedDiff | `<space>r` | Refresh the status (the diff reloads with it) |
| WorkingStagedDiff | `]c` `[c` | Next / previous change group |
| WorkingStagedDiff | `]f` `[f` | Next / previous file (the diff holds the one file under the list cursor, so these only report the end) |
| WorkingStagedDiff | `Esc` / `h` | Back to the file list (closes a search first) |
| WorkingStagedDiff | `/` `n` `N` | Search in diff, next, previous |
| WorkingStagedDiff | `<space>vw` | Toggle word wrap |
| WorkingStagedDiff | `y` | Copy the cursor line (with its `+`, `-` or space prefix) |
| WorkingStagedDiff | `Y` | Copy the displayed rows of the file (not a pasteable patch) |
| WorkingStagedDiff | `+` `-` | Show more / fewer unchanged lines around the changes (title `[ctx N]`) |
| WorkingStagedDiff | `<space>vf` | Toggle showing the whole file instead of hunks (title `[file]`) |
| WorkingStagedDiff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| WorkingStagedDiff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs 80 text columns) |
| WorkingStagedDiff | `<space>vl` | Load anyway: read the file without the row and per-file limits |
| WorkingStagedDiff | `<space>u` | Unstage the change group under the cursor (a file without hunks goes whole) |
| WorkingStagedDiff (Visual) | `<space>u` | Unstage the selected added and removed lines only (the range ends) |
| WorkingStagedDiff | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingStagedDiff | `<space>te` | Commit with `$EDITOR` (the editor of the repository configuration; the TUI suspends) |
| WorkingStagedDiff | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingStagedDiff | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| Sidebar | `l` | Expand section; on a leaf, focus graph |
| Sidebar | `Enter` | Expand section; on a ref, select it in graph (focus stays) |
| Sidebar | `h` | Collapse section / parent |
| Sidebar | `/` | Filter entries |
| Sidebar | `<space>bc` | Create a branch starting at the selected branch and check it out (status bar prompt) |
| Sidebar | `<space>bC` | Create a branch starting at the selected branch without checking it out |
| Sidebar | `<space>bs` | Switch to a branch (type-to-filter prompt; a remote branch gets a tracking branch) |
| Sidebar | `<space>br` | Rename the selected local branch (prefilled prompt) |
| Sidebar | `<space>bd` | Delete the selected local branch after a Confirm (a second one when it is unmerged) |
| Sidebar | `<space>bu` | Set the upstream of the selected local branch (type-to-filter picker of remote branches) |
| Sidebar | `<space>bU` | Unset the upstream of the selected local branch |
| Sidebar | `<space>Tc` | Create a tag at the selected row's commit (status bar prompts: name, then an optional message) |
| Sidebar | `<space>Td` | Delete the selected local tag after a Confirm |
| Sidebar | `<space>Tp` | Push the selected tag to a remote (type-to-filter picker; one ref, no progress) |
| Sidebar | `<space>TD` | Delete the selected tag on a remote (picker, then a Confirm) |
| Sidebar | `<space>gm` `<space>gM` `<space>gs` | Merge the selected branch or tag into the current branch (as in the graph) |
| Sidebar | `<space>gp` `<space>gv` | Cherry-pick / revert the tip of the selected branch or tag onto the current branch |
| Sidebar | `<space>gx` | Reset the current branch to the selected branch or tag (`s`/`m`/`h`; Confirm where work is lost) |
| Sidebar | `<space>gr` | Rebase the current branch onto the selected branch or tag (Confirm; same refusals as in the graph) |
| Sidebar | `<space>l` | Open the reflog of the local branch under the cursor (HEAD's on any other row) |
| Sidebar | `y` | Copy the branch, tag or stash name, or the worktree path (OSC 52) |
| Sidebar | `<space>Sa` | Apply the stash under the cursor and keep it |
| Sidebar | `<space>Sp` | Pop the stash under the cursor (apply, then drop it) |
| Sidebar | `<space>Sd` | Drop the stash under the cursor after a Confirm that shows the restore command |
| Sidebar | `<space>wa` | Add a worktree: prompt for a branch (existing, or a new name started at the selected commit), then the path |
| Sidebar | `<space>wd` | Remove the selected worktree (Confirm; a dirty one needs a second Confirm) |
| Sidebar | `<space>wl` `<space>wu` | Lock (optional reason) / unlock the selected worktree |
| Sidebar | `<space>wp` | Prune worktrees whose directory is gone (Confirm lists them) |
| Sidebar | `<space>ws` | Switch the tab to the selected worktree, keeping its view state |
| Sidebar | `<space>wt` | Open the selected worktree in its own tab (focuses the tab that already shows it) |
| Diff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Diff | `]c` `[c` | Next / previous hunk |
| Diff | `]f` `[f` | Next / previous file |
| Diff | `Esc` / `h` | Back to the file list (closes an on-demand diff) |
| Diff | `/` `n` `N` | Search in diff, next, previous |
| Diff | `<space>vw` | Toggle word wrap (off by default; the cursor moves by screen line while on) |
| Diff | `y` | Copy the cursor line (with its `+`, `-` or space prefix) |
| Diff | `Y` | Copy the displayed rows of the file under the cursor (not a pasteable patch) |
| Diff | `+` `-` | Show more / fewer unchanged lines around the changes (3 at a time, default 3; the title shows `[ctx N]`) |
| Diff | `<space>vf` | Toggle showing whole files instead of hunks (title `[file]`) |
| Diff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| Diff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs a pane of 80 text columns) |
| Diff | `<space>vl` | Load anyway: reload the commit without the row and per-file limits |
| Reflog | `Enter` | Select the entry's commit in the graph (the list stays open) |
| Reflog | `h` `Esc` | Close the list |
| Reflog | `<space>r` | Read the reflog again |
| Reflog | `<space>l` | Open the ref picker: the reflog of HEAD or of any local branch |
| Reflog | `/` `n` `N` | Search the entries, next, previous |
| Reflog | `<space>gx` | Reset the current branch to the entry (`s`/`m`/`h`; Confirm where work is lost) |
| Reflog | `<space>bc` | Create a branch at the entry and check it out (status bar prompt) |
| Reflog | `<space>bs` | Check out the entry with a detached HEAD |
| Picker | `Up` `Down` | Move highlight |
| Picker | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases; terminals report `Ctrl-j` distinct from `Enter`) |
| Picker | `Enter` `Ctrl-l` | Open repository (new tab or focus existing); `Ctrl-l` is the home-row alias of `Enter`, `Ctrl-w l` is a chord and unaffected |
| Picker | `Tab` `Right` | Accept the suggestion: with a typed name, fill in the highlighted repository's path (then its subfolders are listed); in a path, complete the directory name (to the common prefix of the candidates, or to the highlighted one). Matching ignores case on every platform, the inserted text keeps the real case of the directory |
| Picker | `Alt-p` | Pin the entry to the top of the list / unpin it |
| Picker | `Alt-d` | Remove from recents |
| Palette | `Up` `Down` | Move highlight (typing filters; `Enter` runs the action, `Esc` closes) |
| Palette | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases, like the picker) |
| Palette | `Ctrl-l` | Run the highlighted action (alias of `Enter`, like the picker); `Ctrl-h` is left unbound because some terminals send it for `Backspace` |

Mouse paths are listed in the next section.

## Mouse

Mouse input is optional and goes through the same action path as keys: the mouse map (`DefaultMouseMap`) turns a gesture on a view into the action id a key would produce, and the same handler runs it. A test parses the table below and checks it against the defaults, so add a row in the same change that adds a mouse path. Views inherit like in the keymap: `Lists` covers Graph, Detail, Sidebar, Diff and the two working-tree lists. `CommandsButton`, `NewTabButton`, `Tabs`, `Picker`, the banner chips (`BannerButton.*`), the overlays (`Help`, `Messages`, `Palette`, and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `DetailTab.*`, `PickerButton.*`, `ListButton.*`, `SidebarButton.*`, one view per button or label) are views of their own.

| View | Gesture | Action | Effect |
|---|---|---|---|
| Lists | Wheel up | `list.up` | Moves the selection up 3 rows (like `3k`) in the pane under the pointer, which gets focus |
| Lists | Wheel down | `list.down` | Moves the selection down 3 rows (like `3j`) |
| Lists | Drag start | `visual.toggle` | Pressing a row and moving the pointer starts Visual mode (range selection, like `v`) at the pressed row; graph, detail, diff, the working-tree diff and the two working-tree file lists (to stage the files or lines, press `<space>a` / `<space>u`, or run the action from the palette, `:`) |
| Lists | Drag up | `list.up` | While dragging, the cursor follows the pointer: moves up by as many rows as the pointer is above it |
| Lists | Drag down | `list.down` | The same downwards |
| Graph | Double-click | `graph.open-details` | Same as `Enter`: focus the commit details |
| Detail | Double-click | `detail.open-diff` | Same as `Enter`: go to the file diff |
| Sidebar | Click | `sidebar.activate` | Same as `Enter`: expand or collapse a section, select the ref's commit in the graph |
| Tabs | Click | `tab.next` | Switch to the clicked tab (same as `3gt` with the tab's number); closes an open picker |
| TabDirtyMarker | Click | `working-tree.open` | The `*` of a dirty tab chip: switch to that tab and open its working-tree view (a click elsewhere on the chip only switches); no effect where the marker is cut off |
| NewTabButton | Click | `picker.open` | The ` + ` button right after the last tab: open the repository picker for a new tab, same as `Ctrl-t` |
| CommandsButton | Click | `palette.open` | The ` : commands ` button at the right end of the tab row (rows of 44 columns or more; there is no header bar): open the command palette, the menu of the actions of the focused pane and the global ones, each with its keys |
| BannerButton.Continue | Click | `operation.continue` | The `continue` chip of the in-progress operation banner: same as `<space>gc` |
| BannerButton.Abort | Click | `operation.abort` | The `abort` chip of the banner: same as `<space>ga` (asks first) |
| BannerButton.Skip | Click | `operation.skip` | The `skip` chip of the banner (not shown for a merge): same as `<space>gk` (asks first) |
| SidebarButton.BranchNew | Click | `branch.create` | Chip `[new]` in the Branches section header, same as `<space>bc` (acts on the selected row) |
| SidebarButton.BranchNewOnly | Click | `branch.create-only` | Chip `[new-only]`, same as `<space>bC` |
| SidebarButton.BranchSwitch | Click | `branch.switch` | Chip `[switch]`, same as `<space>bs` |
| SidebarButton.BranchRename | Click | `branch.rename` | Chip `[rename]`, same as `<space>br` |
| SidebarButton.BranchDelete | Click | `branch.delete` | Chip `[delete]`, same as `<space>bd`; opens the Confirm first |
| SidebarButton.Rebase | Click | `rebase.onto` | Chip `[rebase]` in the Branches and Tags section headers (the chips fit in the order rebase, reset, pick, revert), same as `<space>gr` on the selected row |
| SidebarButton.Reset | Click | `reset.branch` | Chip `[reset]` in the same headers, same as `<space>gx` |
| SidebarButton.CherryPick | Click | `pick.cherry-pick` | Chip `[pick]` in the Branches and Tags headers, same as `<space>gp` on the selected row |
| SidebarButton.Revert | Click | `pick.revert` | Chip `[revert]` in the same headers, same as `<space>gv` |
| SidebarButton.BranchSetUpstream | Click | `branch.set-upstream` | Chip `[upstream]` in the Branches header (second-to-last; dropped after `[unset-up]`), same as `<space>bu` |
| SidebarButton.BranchUnsetUpstream | Click | `branch.unset-upstream` | Chip `[unset-up]`, last chip of the Branches header, same as `<space>bU` |
| Graph | Right-click | `palette.open-for-row` | Selects the row under the pointer and opens the command popup (the palette) narrowed to the actions on a commit: branch, tag (create, delete, push, remote delete), merge, rebase, reset, cherry-pick, revert, copy hash. Typing filters further, `Esc` closes |
| SidebarButton.WorktreeAdd | Click | `worktree.add` | Chip `[add]` in the Worktrees section header, same as `<space>wa` |
| SidebarButton.WorktreeRemove | Click | `worktree.remove` | Chip `[remove]`, same as `<space>wd`; opens the Confirm first |
| SidebarButton.WorktreeLock | Click | `worktree.lock` | Chip `[lock]`, same as `<space>wl` |
| SidebarButton.WorktreeUnlock | Click | `worktree.unlock` | Chip `[unlock]`, same as `<space>wu` |
| SidebarButton.WorktreePrune | Click | `worktree.prune` | Chip `[prune]`, same as `<space>wp`; the Confirm lists the entries |
| SidebarButton.TagCreate | Click | `tag.create` | Chip `[tag]` in the Tags section header, same as `<space>Tc` (acts on the selected row) |
| SidebarButton.TagDelete | Click | `tag.delete` | Chip `[delete]`, same as `<space>Td`; opens the Confirm first |
| SidebarButton.TagPush | Click | `tag.push` | Chip `[push]`, same as `<space>Tp` |
| SidebarButton.TagDeleteRemote | Click | `tag.delete-remote` | Chip `[remote-del]`, same as `<space>TD`; opens the Confirm first |
| SidebarButton.Merge | Click | `merge.branch` | Chip `[merge]` in the Branches and Tags headers, same as `<space>gm` |
| SidebarButton.MergeNoFastForward | Click | `merge.branch-no-ff` | Chip `[merge-commit]`, same as `<space>gM` |
| SidebarButton.MergeSquash | Click | `merge.branch-squash` | Chip `[squash]`, same as `<space>gs` |
| Sidebar | Right-click | `palette.open-for-row` | Selects the row under the pointer and opens the command popup narrowed to the actions on a branch or tag (switch, create, rename, delete, set and unset upstream, tag actions, merge, rebase, reset, cherry-pick, revert); a click below the last row does nothing |
| SidebarButton.StashPush | Click | `stash.push` | Chip `[push]` in the Stashes section header, same as `<space>Ss` |
| SidebarButton.StashApply | Click | `stash.apply` | Chip `[apply]`, same as `<space>Sa` (acts on the selected row) |
| SidebarButton.StashPop | Click | `stash.pop` | Chip `[pop]`, same as `<space>Sp` |
| SidebarButton.StashDrop | Click | `stash.drop` | Chip `[drop]`, same as `<space>Sd`; opens the Confirm first |
| SidebarButton.StashPushUntracked | Click | `stash.push-untracked` | Chip `[push-all]`, same as `<space>Su` (tracked plus untracked) |
| Backdrop | Click | `cancel` | While the help, palette or status history is open, a click on a pane or the tab row outside it closes it (like `Esc`); the double-click that follows is ignored |
| Palette | Click | `command.execute` | Run the clicked entry (it is highlighted first) |
| Palette | Wheel up | `palette.previous` | Move the highlight up one row |
| Palette | Wheel down | `palette.next` | Move the highlight down one row |
| ConfirmButton.Yes | Click | `confirm.yes` | The `y` row of a confirm dialog, same as `y` |
| ConfirmButton.No | Click | `confirm.no` | The `n` row of a confirm dialog, same as `n` / `Esc` |
| Help | Wheel up | `list.up` | Scroll the help up 3 lines (wherever the pointer is while the overlay is open) |
| Help | Wheel down | `list.down` | Scroll the help down 3 lines |
| Messages | Wheel up | `list.up` | Scroll the status history up 3 lines |
| Messages | Wheel down | `list.down` | Scroll the status history down 3 lines |
| Reflog | Wheel up | `list.up` | Scroll the reflog list up 3 entries |
| Reflog | Wheel down | `list.down` | Scroll the reflog list down 3 entries |
| ReflogButton.Select | Click | `reflog.select-commit` | Action row `Enter` under the reflog list: select the highlighted entry's commit in the graph (the list stays open); a click on an entry itself only highlights it |
| ReflogButton.Reset | Click | `reset.branch` | Row `<space>gx`: reset the current branch to the highlighted entry (closes the list first) |
| ReflogButton.Branch | Click | `branch.create` | Row `<space>bc`: new-branch prompt at the highlighted entry |
| ReflogButton.Checkout | Click | `branch.switch` | Row `<space>bs`: check the entry out with a detached HEAD |
| ReflogPicker | Click | `input.accept` | A row of the reflog ref picker: highlights it and opens that ref's reflog, like `Enter` |
| MainlineParent | Click | `pick.mainline` | A parent row of the mainline overlay of a cherry-pick or revert of a merge commit: answers like the parent's digit key (the row number is the click target; a click outside cancels) |
| DiffButton.PreviousHunk | Click | `diff.previous-hunk` | Button `‹ hunk`, same as `[c` |
| DiffButton.NextHunk | Click | `diff.next-hunk` | Button `hunk ›`, same as `]c` |
| DiffButton.Wrap | Click | `diff.toggle-wrap` | Button `wrap`, same as `<space>vw` |
| DiffButton.Zoom | Click | `pane.zoom` | Button `zoom`, same as `<space>z` |
| DiffButton.MoreContext | Click | `diff.more-context` | Button `ctx+`, same as `+` |
| DiffButton.LessContext | Click | `diff.less-context` | Button `ctx-`, same as `-` |
| DiffButton.WholeFile | Click | `diff.toggle-whole-file` | Button `file`, same as `<space>vf` |
| DiffButton.IgnoreWhitespace | Click | `diff.toggle-ignore-whitespace` | Button `-w`, same as `<space>vi` |
| DiffButton.SideBySide | Click | `diff.toggle-side-by-side` | Button `split`, same as `<space>vs` |
| DiffButton.Stage | Click | `working-tree.stage` | Working-tree diff, Unstaged side: button `stage`, same as `<space>a` (the change group under the cursor, or the Visual range); also a chip at the right end of the group's first line (same view, same action) |
| DiffButton.Unstage | Click | `working-tree.unstage` | Working-tree diff, Staged side: button `unstage`, same as `<space>u`; also a chip on the group's first line |
| DiffButton.Discard | Click | `working-tree.discard` | Working-tree diff, Unstaged side: button `discard`, same as `<space>d`; opens the Confirm dialog, nothing is discarded before `y`; also a chip on the group's first line |
| DetailTab.Details | Click | `detail.tab-details` | The `Details` label in the detail pane's title row, same as `gd` (also focuses the pane) |
| DetailTab.Changes | Click | `detail.tab-changes` | The `Changes` label, same as `gc` |
| DetailTab.Files | Click | `detail.tab-files` | The `Files` label, same as `gf` |
| WorkingUnstaged | Double-click | `working-tree.stage` | Stage the file (same as `<space>a`) |
| WorkingStaged | Double-click | `working-tree.unstage` | Unstage the file (same as `<space>u`) |
| ListButton.StageAll | Click | `working-tree.stage-all` | Button `stage all` at the right end of the Unstaged list's title row, same as `<space>A` (also focuses the list) |
| ListButton.UnstageAll | Click | `working-tree.unstage-all` | Button `unstage all` in the Staged list's title row, same as `<space>U` |
| WorkingMessage | Click | `working-tree.edit-message` | Focus the message box and start writing (Insert mode), same as `i`; the cursor goes where the pointer is (the position is not part of the action id) |
| MessageButton.SignOff | Click | `working-tree.toggle-sign-off` | The `[ ] SignOff` checkbox of the message box, same as `Alt-s` |
| MessageButton.NoVerify | Click | `working-tree.toggle-no-verify` | The `[ ] No-Verify` checkbox, same as `Alt-n` |
| MessageButton.Amend | Click | `working-tree.toggle-amend` | The `[ ] Amend` checkbox, same as `Alt-a` |
| MessageButton.Commit | Click | `working-tree.commit` | The `Commit` button, same as `Alt-Enter` |
| WorkingUnstagedDiff | Double-click | `working-tree.stage` | Stage the change group under the pointer (same as `<space>a`) |
| WorkingStagedDiff | Double-click | `working-tree.unstage` | Unstage the change group under the pointer (same as `<space>u`) |
| Picker | Double-click | `picker.accept` | Open the repository of the row |
| Picker | Wheel up | `picker.previous` | Move the highlight up one row |
| Picker | Wheel down | `picker.next` | Move the highlight down one row |
| PickerButton.Pin | Click | `picker.pin` | Button `[pin]` / `[unpin]` on the highlighted row, same as `Alt-p` |
| PickerButton.Remove | Click | `picker.remove-recent` | Button `[remove]` on a highlighted recent row, same as `Alt-d` |

Pointer-position behaviour that has no action of its own: a click on a pane focuses it, a click on a list row selects it (graph, detail, sidebar, diff; the details pane follows the graph selection as with keys), a click on a picker row highlights it. A click on empty space does nothing. The wheel moves the selection, not just the view (the keyboard has no pure scroll either), so the details pane follows it as it does for `j`/`k`. A mouse action does not touch a pending count or chord typed before it. While the help, palette or status history is open, panes and the picker ignore every mouse gesture (no focus change, no selection), except that the wheel scrolls the help or history (or moves the palette highlight) when the pointer is over a pane or the overlay, and a click on a pane or the tab row closes it, like `Esc`; the which-key popup does not block the mouse.

Drag to select: press a row of the graph, detail, diff or file-list pane and move the pointer; the pane takes focus, the pressed row becomes the anchor, Visual mode starts (status bar `VISUAL`) and the cursor follows the pointer, also above or below the pane while the button is held: while the pointer rests outside the pane the cursor keeps moving every 80 ms, 1 row just outside, 1 more per 2 rows of distance, at most 6, until the pointer is back inside, the list ends or the button is released. The range is drawn like the selected row. The release leaves the range active; a click on a row, `Esc`, `q`, `v` or moving focus to another pane or tab ends it. `y` copies the range (the drag plus `y`, or `visual.copy` from the palette) and ends it; staging acts on it too (lines in the working-tree diff, files in its lists). The sidebar and the message box have no ranges (a drag there only moves the selection); a drag also does not start a range while a prompt (search, filter, ref jump) or the picker is open.

Drag to resize: the line between the sidebar and the graph, the line between the detail and diff panes, and the title rows of the detail and diff panes (the line between the graph and the bottom panes) follow the pointer while the button is held. Sizes are clamped to what fits (at least 12 columns for the sidebar and detail, 20 for the graph and diff, 3 rows for the bottom panes, 4 for the graph), belong to the tab and are saved with the session; `Ctrl-w =` (or `:`, then "Reset the pane sizes") returns to automatic, and `Ctrl-w >` `Ctrl-w <lt>` `Ctrl-w +` `Ctrl-w -` are the keyboard path. A zoomed pane has no dividers to drag.

Clickable titles and buttons: a click on the title row of the detail or diff pane focuses that pane. The detail title row starts with the three tab labels (`Details`, `Changes`, `Files`), drawn as buttons: the selected one is the accent chip (quieter when the pane has no focus), the others look exactly like the inactive repository tabs of the tab row (a chip on a subtle tinted background; underlined where the terminal tier has no background colours, `[Name]` in the ASCII tier from 29 columns, one padding column per side). The selected tab of an unfocused pane keeps the chip but in bold text (bold and underlined in the colourless tiers) so it still differs, one column of line between them; below 29 columns the padding around the labels goes. The content of the detail pane sits on a panel background, the bar colour (24-bit schemes only; the 16-colour and `NO_COLOR` tiers rely on the frame line and the buttons), so the pane reads as a separate area. A click on a label selects the tab. The diff title row ends in buttons (see the table), chips like the inactive repository tabs with one column of line between them and `<`/`>` arrows and brackets in the ASCII tier, when the pane is at least 30 columns wider than the buttons need; narrower panes drop buttons in this order, last to go first: `zoom`, `wrap`, the two hunk buttons, `split`, `-w`, `file`, `ctx+`, `ctx-`, the keys stay available. The working-tree diff has the same title row and buttons, with the staging buttons of its side first: `stage` and `discard` on the Unstaged side (`stage` is the last to be dropped on a narrow row, `discard` sits after the hunk buttons in the drop order), `unstage` on the Staged side; they act on the change group under the cursor or the Visual range like `<space>a`, `<space>u` and `<space>d`, and `discard` opens the Confirm dialog first. The change group under the cursor also carries the same chips on its first line, at the right edge (`stage` and `discard` on the Unstaged side, `unstage` on the Staged side; the history diff and a Visual range have none): a click or double-click on one runs the title button's action, so `discard` still opens the Confirm first. A row that would keep fewer than 30 columns of diff text drops `discard`, then `stage`/`unstage`; the keys and the title buttons stay. `<space>z` and the `zoom` button give the diff the whole view (lists and message box hidden) until focus leaves the diff (`h`, `Esc`) or the `zoom` button / `<space>z` is used again. In the working-tree view a click on the diff box focuses it (the mouse path from a list to its diff on screens of 80 columns or more; narrow screens keep `Enter` / `l`). The highlighted picker row shows `[pin]`/`[unpin]` and, for a recent repository, `[remove]` at its right edge (rows of 60 columns or more inside the frame, so screens of 62 columns or more). Clicking the commands button at the right end of the tab row opens the command palette, which lists the actions of the focused pane and the global ones, so every action without a button (for example `<space>vl` load anyway, the search keys, `<space>r` refresh) can be run with the mouse; typing a search or a filter text needs the keyboard, as nothing else can enter text.

Terminal.Gui turns on mouse reporting (SGR, mode 1006) at startup and turns it off on exit; there is no switch to disable it yet. Verified by writing SGR mouse sequences into the pane of a tmux 3.7c session running the Linux build: clicks, double-clicks, the wheel, press-drag-release (reported as a press, position reports with the button held, a release and then a click at the release position, which the drag logic swallows), divider and title drags, header, palette and button clicks all arrive; a double-click is delivered as a click followed by a double-click event (so a double-click on a sidebar section toggles it once), and a sideways wheel (Ctrl or Shift plus wheel) is ignored (unit-tested only). Not yet verified with a physical mouse, over ssh, or in kitty, Windows Terminal and macOS terminals. Terminals or ssh paths that drop mouse sequences leave the keyboard path unaffected.

Design history (why things are the way they are, measurements, rejected options): `docs/history/ux.md`. Not needed for routine work.
