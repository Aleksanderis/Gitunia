# UX: keymap

Part of the UX spec; start at [ux.md](ux.md). Temporary home until the generated reference of #857.

## Keymap

The single source of truth for bindings.
A redesign of the layout is proposed in "Keymap rework" below (#833); this table stays the truth until it is implemented (#840).
Add a row in the same change that adds a binding; no conflicts within a mode/view.
The defaults are data: `src/Gitunia.Core/Keymap/keymap.default.json` (one row per action, view and mode set: `action`, `view`, `modes`, `keys` with the main key first; a superset of the config `keys` entry, which has one `mode` and one `key`); change a default there, not in code. A test parses this table (columns `Mode/View | Key | Action`; keys in backticks, `Picker` rows are Insert mode, `Palette` rows Command mode, the rest Normal) and checks every key is a registered default in `DefaultKeymap` with no conflicts.
Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `<` is a literal `<` (config files also accept `<lt>`), `:name` is an ex command and not a key.
Modes: rows apply to Normal, except those whose view says `(Visual)` (Visual mode only); Visual repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action.

| Mode/View | Key | Action |
|---|---|---|
| Global | `?` | Show keys for current view |
| Global | `:` / `Ctrl-p` | Command palette |
| Global | `q` | Quit (closes an overlay or ends a Visual range first) |
| Global | `Ctrl-c` | Force quit from any mode or modal: cancels running git work and leaves at once (a failed session save still warns once, like `q`); matched before the keymap, so it cannot be remapped away |
| Global | `Tab` / `Shift-Tab` | Next / previous pane |
| Lists | `j` `k` / `gg` `G` | Move / top / bottom |
| Lists | `/` `n` `N` | Search, next, previous (graph search prefixes: `author:`, `hash:`, `body:`) |
| Global | `gt` / `gT` | Next / previous tab (counts: `3gt` goes to tab 3) |
| Global | `Alt-1`..`Alt-9` | Jump to tab N (terminal multiplexers may take `Alt-N` for themselves; `3gt`, typing the number and then `gt`, does the same and never conflicts; remap in the config file) |
| Global | `Ctrl-t` / `<space>o` | Open repository picker in a new tab |
| Global | `Ctrl-w c` / `:tabclose` | Close tab |
| Global | `<space>p` | Pin the repository to the top of the picker / unpin it |
| Global | `Ctrl-w r` | Rename the tab (prompt popup, `Enter` saves, an empty name restores the repository name) |
| Global | `Ctrl-w h` `Ctrl-w j` `Ctrl-w k` `Ctrl-w l` | Focus pane left / down / up / right |
| Global | `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l` `Ctrl-Backspace` | Same as `Ctrl-w h/j/k/l` without the prefix, Normal mode only (Insert and Command keep Backspace/Enter; picker and palette keep their own `Ctrl-j/k/l`). `Ctrl-j` arrives as `Ctrl+Enter`, distinct from `Enter`; `Ctrl-h` is `Backspace` (0x08) in many terminals, so `Ctrl-Backspace` (focus left) is bound too and plain `Backspace` stays unbound in Normal mode. Where a terminal or multiplexer swallows them, `Tab` and `Ctrl-w h/j/k/l` still work; remap in the config file |
| Global | `Ctrl-w w` | Next pane |
| Global | `Ctrl-^` / `<space><Tab>` | Previously active tab (second binding for terminals that drop `Ctrl-^`) |
| Global | `<space>z` | Zoom focused pane (Changes and diff together) |
| Global | `Ctrl-w >` `Ctrl-w <` | Focused pane wider / narrower: moves the divider beside it by 2 columns (a count multiplies; dragging a divider with the mouse does the same) |
| Global | `Ctrl-w +` `Ctrl-w -` | Focused pane taller / shorter: moves the edge between the graph and the bottom panes by 1 row |
| Global | `Ctrl-w =` | Reset the pane sizes to automatic |
| ContextRepeat | `+` | Repeat mode after a context step (see Modes): 3 more unchanged lines around each change; any other key ends the mode |
| ContextRepeat | `-` | Repeat mode: fewer context lines |
| ContextRepeat | `Esc` | Leave the repeat mode, nothing else happens |
| ResizeRepeat | `>` | Repeat mode after a resize step: focused pane wider |
| ResizeRepeat | `<` | Repeat mode: focused pane narrower |
| ResizeRepeat | `+` | Repeat mode: focused pane taller |
| ResizeRepeat | `-` | Repeat mode: focused pane shorter |
| ResizeRepeat | `=` | Repeat mode: reset the pane sizes (the mode stays) |
| ResizeRepeat | `Esc` | Leave the repeat mode, nothing else happens |
| Global | `<space>x` | Dismiss the status message |
| Global | `<space>m` | Show the status message history (overlay; `j`/`k` scroll, `Esc` closes) |
| Global | `<space>,` | Open the settings screen (overlay over the tab; `Esc` closes; see "Settings screen" in [ux-settings.md](ux-settings.md)) |
| Global | `<space>s` | Open the working-tree view of the tab, or leave it (workflow 2) |
| Global | `<space>F` | Fetch the remote of the current branch with `--prune`, in the background with a progress line (workflow 5) |
| Global | `<space>L` | Pull the current branch, fast-forward only (a diverged branch is refused, nothing merged) |
| Global | `<space>P` | Push the current branch the way git is configured; never forces, but a non-fast-forward rejection offers the force push Confirm; a branch without an upstream asks for the remote and sets the upstream |
| Global | `<space>Rf` | Force push the current branch with `--force-with-lease` (Confirm names the remote branch it overwrites) |
| Global | `<space>RD` | Delete a branch on a remote (remote picker, branch picker, Confirm; straight to the Confirm on a remote branch row or a graph commit with one remote branch) |
| Global | `<space>C` | Cancel the running fetch, pull or push of the tab (works from every pane; `Esc` does it only in the graph) |
| Global | `<space>Ra` | Add a remote: name prompt, then URL prompt; nothing is fetched |
| Global | `<space>Rd` | Remove a remote (picker, or the selected remote branch's remote) after a Confirm naming the tracking branches that disappear |
| Global | `<space>Rn` | Rename a remote (picker, then a name prompt); git moves its tracking branches and upstream settings |
| Global | `<space>Ru` | Change the URL of a remote (picker, then a URL prompt filled with the current one) |
| Global | `<space>Ss` | Stash the tracked changes (prompts for a message; `Enter` runs, `Esc` cancels) |
| Global | `<space>Su` | Stash the tracked and the untracked changes (prompts for a message) |
| Global | `<space>gc` | Continue the merge, rebase, cherry-pick or revert in progress (refused while conflicted files remain; workflow 4) |
| Global | `<space>ga` | Abort the operation in progress (Confirm; the work of the operation is undone) |
| Global | `<space>gk` | Skip the commit a rebase, cherry-pick or revert stopped on (Confirm; not for a merge) |
| Global | `<space>to` | Open the conflict editor from any pane while a file is conflicted (a merge, rebase, cherry-pick or revert stopped): on the conflicted file under the Unstaged cursor, else on the first conflicted file; says so in the status bar when no file is conflicted (#742) |
| Global | `Esc` | Cancel pending chord, clear search highlight, leave Insert mode; in the graph also cancel a running fetch, pull or push, else dismiss the status message |
| Lists | `Ctrl-d` `Ctrl-u` | Half page down / up (mouse wheel scrolls) |
| Lists | `Down` `Up` `PageDown` `PageUp` | Move / page (same as `j` `k` `Ctrl-d` `Ctrl-u`) |
| Lists | `v` | Start / end range selection (Visual mode: motions move the far end, `Esc` / `q` / `v` end it; graph, detail, diff and the working-tree lists) |
| Lists (Visual) | `y` | Copy the selected range (OSC 52) and end it when the copy worked (a refused or failed copy keeps the range): graph one hash per line, detail the rows' text, diff the rows as plain text (no `+`/`-`/space marker or gutter; a wrapped row once; notes skipped), a working-tree file list one path per line; text too large for OSC 52 (about 75 KB) is refused with a warning |
| Graph | `<space>h` | Jump to HEAD |
| Graph | `<space>f` | Jump to ref or hash (fuzzy) |
| Graph | `<space>j` | Quick jump: type text, labels appear on the commits in view whose subject or ref names (branches, tags) contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Graph | `Enter` | Focus commit details |
| Graph | `y` | Copy commit hash |
| Graph | `<space>=` | Mark the selected commit; while marked, the commit under the cursor is compared with it (`git diff older newer`) in the Changes tab and the diff pane. On another commit move the mark there; on the marked commit clear it (`Esc` clears it too; "Comparing two commits", #535, #636) |
| Graph | `<space>r` | Refresh repository |
| Graph | `<space>td` | Open the selected commit (against its first parent) in the configured difftool |
| Global | `<space>tm` | Resolve conflicts in the configured mergetool |
| Graph | `<space>te` | Commit the staged changes with `$EDITOR` (git's own editor; the working-tree view's `<space>te` does the same) |
| Graph | `<space>bc` | Create a branch at the selected commit and check it out (prompt popup) |
| Graph | `<space>bC` | Create a branch at the selected commit without checking it out |
| Graph | `<space>bs` | Check out the commit's branch (one label: at once; several: a picker of those plus detached; none: the prompt of all branches and a detached checkout) |
| Graph | `<space>br` | Rename the local branch at the selected commit (prefilled prompt) |
| Graph | `<space>bd` | Delete the local branch at the selected commit after a Confirm (a second one when it is unmerged); a commit with only remote branches deletes the remote one instead, after a Confirm |
| Graph | `<space>bu` | Set the upstream of the local branch at the selected commit (type-to-filter picker of remote branches) |
| Graph | `<space>bf` | Fast-forward the local branch at the selected commit to its upstream without checking it out (#572) |
| Graph | `<space>bU` | Unset the upstream of the local branch at the selected commit |
| Graph | `<space>Tc` | Create a tag at the selected commit (prompt popups: name, then an optional message; empty is lightweight) |
| Graph | `<space>Td` | Delete the local tag at the selected commit after a Confirm |
| Graph | `<space>Tp` | Push the tag at the selected commit to a remote (type-to-filter picker; one ref, no progress) |
| Graph | `<space>TD` | Delete the tag at the selected commit on a remote (picker, then a Confirm) |
| Graph | `<space>gm` `<space>gM` `<space>gs` | Merge the selected commit into the current branch: fast-forward when possible / always a merge commit (`--no-ff`) / squash (changes staged, no commit; workflow 3) |
| Graph | `<space>gp` `<space>gv` | Cherry-pick / revert the selected commit or the Visual range onto the current branch (also in Visual mode; a merge commit asks for the mainline; workflow 3) |
| Graph | `<space>gx` | Reset the current branch to the selected commit: `s`/`m`/`h` picks soft, mixed or hard; Confirm where work is lost |
| Graph | `<space>gr` | Rebase the current branch onto the selected commit (Confirm; non-interactive; refused with an operation in progress, tracked changes or a detached HEAD) |
| Graph | `<space>gi` | Interactive rebase onto the selected commit: read the commits the target lacks and open the todo editor (same refusals as `<space>gr`; merge commits in the range are kept with `--rebase-merges`, #461; workflow 3) |
| Graph | `<space>gI` | Interactive rebase of the whole history, root commit included (`--root`; no selection needed; same refusals as `<space>gi`; workflow 3, #561) |
| Graph | `<space>gf` `<space>gw` `<space>gd` | Quick rebase of the selected commit (workflow 3): fix it up into the commit before it / reword its subject / drop it; each asks a Confirm that names the undo (#459) |
| Graph | `<space>l` | Open the reflog of HEAD (a list of its entries; see the Reflog flow) |
| Graph | `<space>.` | Open the actions menu of the row under the cursor (same menu as a double-click on the row) |
| Graph | `]` `[` | Next / previous tab of the bottom detail pane (Details, Changes, Files; wraps) |
| Graph | `gd` `gc` `gf` | Show the Details / Changes / Files tab of the bottom detail pane |
| Detail | `Enter` | Go to the diff of the selected file, or of the folder under the cursor in the tree style (Changes tab): focus it; on narrow screens open it on demand. Files tab: open or close the directory under the cursor; on a file, move focus into the content pane beside the tree (on narrow screens: open it) |
| Detail | `y` | Copy the path of the selected file (Files tab: of the row) |
| Detail | `gx` | Details tab: open the issue link on the message line under the cursor (`2gx` the second); falls back to copying the address |
| Detail | `/` | Filter the file tree by path (Files tab; Insert mode: `Enter` keeps the filter, `Esc` clears it) |
| Detail | `<space>j` | Quick jump: type text, labels appear on the rows in view that contain it (file paths on Changes), press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Detail | `<space>r` | Retry loading the commit details after a failure |
| Detail | `]` `[` | Next / previous tab of the detail pane (Details, Changes, Files; wraps) |
| Detail | `gd` `gc` `gf` | Show the Details / Changes / Files tab |
| DetailChanges | `za` | Open or close the folder under the cursor (tree style; on a file nothing happens). `h` does the same for an open folder, else moves to the parent folder |
| DetailChanges | `<space>vn` | Cycle the file path style: name first with the directory dimmed, full path, tree with folder rows (the `view ▾` dropdown picks one directly) |
| DetailChanges | `<space>vv` | Open the `view ▾` dropdown (title row menu: path style name first, full path, tree) |
| DetailChanges | `<space>v1` `<space>v2` `<space>v3` | Set the file path style directly: name first, full path, tree (the `view ▾` dropdown lists them) |
| DetailFiles | `<space>vv` | Open the `view ▾` dropdown (title row menu: filter the tree by path, as `/`) |
| DetailChanges | `J` `K` `D` `U` | Scroll the diff beside the file list by a line (a count multiplies) / half a page, without moving focus into it (the diff cursor moves) |
| DetailFiles | `J` `K` `D` `U` | Scroll the file content beside the tree by a line (a count multiplies) / half a page, without moving focus into it |
| DetailChanges | `<space>vp` | Show a merge commit against its next parent (Changes tab and diff; wraps to the first; per selected commit) |
| Detail | `h` | Files tab: close the open directory, else move to the parent row; Changes tab, tree style: the same for folders; nothing at the top level (never changes the focus) |
| Detail | `l` | Files tab and Changes tab, tree style: open a closed directory or folder; nothing on a file (never changes the focus) |
| Detail | `Esc` | Back to graph (Files tab: first clears an active filter) |
| WorkingUnstaged | `<space>j` | Quick jump: type text, labels appear on the files in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingUnstaged | `J` `K` `D` `U` | Scroll the diff beside the list by a line / half a page without moving focus into it |
| WorkingUnstaged | `<space>vn` | Cycle the file path style of both lists: name first with the directory dimmed, full path, tree with folder rows (per tab, not saved; the `view ▾` dropdown picks one directly) |
| WorkingUnstaged | `<space>vv` | Open the `view ▾` dropdown (title row menu: path style name first, full path, tree) |
| WorkingUnstaged | `<space>v1` `<space>v2` `<space>v3` | Set the file path style directly: name first, full path, tree (the `view ▾` dropdown lists them) |
| WorkingUnstaged | `za` | Open or close the folder under the cursor (tree style) |
| WorkingUnstaged | `<space>r` | Refresh the status |
| WorkingUnstaged | `y` | Copy the path of the file or folder under the cursor |
| WorkingUnstaged | `<space>a` | Stage the file under the cursor, or every file under the folder there (the cursor keeps its index; a conflicted file asks to mark it resolved) |
| WorkingUnstaged | `<space>A` | Stage all files except conflicted ones |
| WorkingUnstaged | `<space>d` | Discard the changes of the file under the cursor, or of every file under the folder there, after a Confirm that names it (the folder dialog counts the files and names the untracked ones); an untracked file is deleted (no undo) |
| WorkingUnstaged (Visual) | `<space>a` | Stage the files of the range (a folder row stands for all the files under it; the range ends; conflicted files are skipped) |
| WorkingUnstaged (Visual) | `<space>d` | Discard the files of the range after one Confirm that counts them and names the untracked ones, which are deleted (the range ends when confirmed; no undo) |
| WorkingUnstaged | `Esc` | Leave the working-tree view (back to the history) |
| WorkingUnstaged | `h` | Tree style: close the open folder under the cursor, else move to the parent folder; nothing at the top level and in the flat styles (`Esc` leaves the view) |
| WorkingUnstaged | `l` | Tree style: open the closed folder under the cursor; nothing otherwise (never changes the focus) |
| WorkingUnstaged | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingUnstaged | `<space>te` | Commit with `$EDITOR` (git's own editor; the TUI suspends) |
| WorkingUnstaged | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingUnstaged | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingStaged | `<space>j` | Quick jump: type text, labels appear on the files in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingStaged | `J` `K` `D` `U` | Scroll the diff beside the list by a line / half a page without moving focus into it |
| WorkingStaged | `<space>vn` | Cycle the file path style of both lists: name first with the directory dimmed, full path, tree with folder rows (per tab, not saved; the `view ▾` dropdown picks one directly) |
| WorkingStaged | `<space>vv` | Open the `view ▾` dropdown (title row menu: path style name first, full path, tree) |
| WorkingStaged | `<space>v1` `<space>v2` `<space>v3` | Set the file path style directly: name first, full path, tree (the `view ▾` dropdown lists them) |
| WorkingStaged | `za` | Open or close the folder under the cursor (tree style) |
| WorkingStaged | `<space>r` | Refresh the status |
| WorkingStaged | `y` | Copy the path of the file or folder under the cursor |
| WorkingStaged | `<space>u` | Unstage the file under the cursor, or every file under the folder there (a rename unstages both paths) |
| WorkingStaged | `<space>U` | Unstage all files |
| WorkingStaged (Visual) | `<space>u` | Unstage the files of the range (a folder row stands for all the files under it; the range ends; conflicted files are skipped) |
| WorkingStaged | `Esc` | Leave the working-tree view (back to the history) |
| WorkingStaged | `h` | Tree style: close the open folder under the cursor, else move to the parent folder; nothing at the top level and in the flat styles (`Esc` leaves the view) |
| WorkingStaged | `l` | Tree style: open the closed folder under the cursor; nothing otherwise (never changes the focus) |
| WorkingStaged | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingStaged | `<space>te` | Commit with `$EDITOR` (git's own editor; the TUI suspends) |
| WorkingStaged | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingStaged | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingMessage | `i` / `Enter` | Edit the message (Insert mode) |
| WorkingMessage | `<space>c` | Commit the staged changes with the message and options |
| WorkingMessage | `<space>te` | Commit with `$EDITOR` (the draft is kept: git uses its own template) |
| WorkingMessage | `<space>r` | Refresh the status |
| WorkingMessage | `Esc` | Back to the file lists |
| WorkingMessage | `h` | Message box: Cursor left (Normal mode, stops at the line start) |
| WorkingMessage | `j` | Message box: Cursor down a line |
| WorkingMessage | `k` | Message box: Cursor up a line |
| WorkingMessage | `l` | Message box: Cursor right (stops at the line end) |
| WorkingMessage | `w` | Message box: Next word |
| WorkingMessage | `b` | Message box: Previous word |
| WorkingMessage | `0` | Message box: Line start |
| WorkingMessage | `$` | Message box: Line end |
| WorkingMessage | `a` | Message box: Insert after the cursor (Insert mode) |
| WorkingMessage | `A` | Message box: Insert at the line end (Insert mode) |
| WorkingMessage | `I` | Message box: Insert at the line start (Insert mode) |
| WorkingMessage | `o` | Message box: Open a line below and insert (Insert mode) |
| WorkingMessage | `x` | Message box: Delete the character under the cursor |
| WorkingMessage | `dd` | Message box: Delete the line |
| WorkingMessage | `u` | Message box: Undo the last change |
| WorkingMessage | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingMessage | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| WorkingMessage (Insert) | `Enter` | Start a new line (the other editing keys are in workflow 2 and are not rebindable rows) |
| WorkingMessage (Insert) | `Ctrl-g` | Commit |
| WorkingMessage (Insert) | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend |
| WorkingUnstaged | `Enter` | Go to the diff of the file or folder under the cursor (on a conflicted `!` file: open the conflict editor; on a screen under 80 columns the diff replaces the list until `Esc`) |
| WorkingStaged | `Enter` | Go to the diff of the file or folder under the cursor (on a screen under 80 columns the diff replaces the list until `Esc`) |
| WorkingUnstagedDiff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingUnstagedDiff | `<space>r` | Refresh the status (the diff reloads with it) |
| WorkingUnstagedDiff | `]c` `[c` | Next / previous change group |
| WorkingUnstagedDiff | `]f` `[f` | Next / previous file (the pane shows that one file; the file list cursor follows; stops at the first / last file; in the folder view of the tree style the diff cursor steps between the folder's files and the list stays) |
| WorkingUnstagedDiff | `Esc` | Back to the file list (closes a search first) |
| WorkingUnstagedDiff | `/` `n` `N` | Search in diff, next, previous |
| WorkingUnstagedDiff | `<space>vw` | Toggle word wrap |
| WorkingUnstagedDiff | `<space>vv` | Open the `view ▾` dropdown (title row menu of the view options and zoom) |
| WorkingUnstagedDiff | `y` | Copy the cursor line as plain text (no `+`, `-` or space marker) |
| WorkingUnstagedDiff | `Y` | Copy the displayed rows of the file as plain text, without markers (not a pasteable patch) |
| WorkingUnstagedDiff | `+` `-` | Show more / fewer unchanged lines around the changes (title `[ctx N]`) |
| WorkingUnstagedDiff | `<space>vf` | Toggle showing the whole file instead of hunks (title `[file]`) |
| WorkingUnstagedDiff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| WorkingUnstagedDiff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs 80 text columns) |
| WorkingUnstagedDiff | `<space>vc` | Toggle showing whitespace: tabs, spaces and carriage returns as marks (title `[ws]`) |
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
| WorkingStagedDiff | `]f` `[f` | Next / previous file (the pane shows that one file; the file list cursor follows; stops at the first / last file) |
| WorkingStagedDiff | `Esc` | Back to the file list (closes a search first) |
| WorkingStagedDiff | `/` `n` `N` | Search in diff, next, previous |
| WorkingStagedDiff | `<space>vw` | Toggle word wrap |
| WorkingStagedDiff | `<space>vv` | Open the `view ▾` dropdown (title row menu of the view options and zoom) |
| WorkingStagedDiff | `y` | Copy the cursor line as plain text (no `+`, `-` or space marker) |
| WorkingStagedDiff | `Y` | Copy the displayed rows of the file as plain text, without markers (not a pasteable patch) |
| WorkingStagedDiff | `+` `-` | Show more / fewer unchanged lines around the changes (title `[ctx N]`) |
| WorkingStagedDiff | `<space>vf` | Toggle showing the whole file instead of hunks (title `[file]`) |
| WorkingStagedDiff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| WorkingStagedDiff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs 80 text columns) |
| WorkingStagedDiff | `<space>vc` | Toggle showing whitespace: tabs, spaces and carriage returns as marks (title `[ws]`) |
| WorkingStagedDiff | `<space>vl` | Load anyway: read the file without the row and per-file limits |
| WorkingStagedDiff | `<space>u` | Unstage the change group under the cursor (a file without hunks goes whole) |
| WorkingStagedDiff (Visual) | `<space>u` | Unstage the selected added and removed lines only (the range ends) |
| WorkingStagedDiff | `<space>c` | Write the commit message (focus the message box, Insert mode) |
| WorkingStagedDiff | `<space>te` | Commit with `$EDITOR` (the editor of the repository configuration; the TUI suspends) |
| WorkingStagedDiff | `Alt-s` `Alt-n` `Alt-a` | Toggle the commit options SignOff, No-Verify, Amend (also in Insert mode) |
| WorkingStagedDiff | `<space>1` `<space>2` `<space>3` `<space>4` | Jump to the Unstaged list / Staged list / diff of the file under the cursor / message box (Normal mode) |
| Sidebar | `l` | Expand section or folder; on a leaf or a stash nothing (never changes the focus; `Enter` selects, `Ctrl-l` / `Ctrl-j` move on) |
| Sidebar | `Enter` | Expand or collapse a folder, expand a section; on a ref, select it in graph (focus stays); on a stash, show its files and diff in the detail pane (focus stays) |
| Sidebar | `h` | Collapse section or folder / move to parent |
| Sidebar | `/` | Filter entries |
| Sidebar | `<space>bc` | Create a branch starting at the selected branch and check it out (prompt popup) |
| Sidebar | `<space>bC` | Create a branch starting at the selected branch without checking it out |
| Sidebar | `<space>bs` | Check out the branch row at once (a remote branch gets a tracking branch); other rows open the type-to-filter prompt |
| Sidebar | `<space>br` | Rename the selected local branch (prefilled prompt) |
| Sidebar | `<space>bd` | Delete the selected local branch after a Confirm (a second one when it is unmerged); on a remote branch row, delete it on its remote after a Confirm |
| Sidebar | `<space>bu` | Set the upstream of the selected local branch (type-to-filter picker of remote branches) |
| Sidebar | `<space>bf` | Fast-forward the selected local branch to its upstream without checking it out (#572) |
| Sidebar | `<space>bU` | Unset the upstream of the selected local branch |
| Sidebar | `<space>Tc` | Create a tag at the selected row's commit (prompt popups: name, then an optional message) |
| Sidebar | `<space>Td` | Delete the selected local tag after a Confirm |
| Sidebar | `<space>Tp` | Push the selected tag to a remote (type-to-filter picker; one ref, no progress) |
| Sidebar | `<space>TD` | Delete the selected tag on a remote (picker, then a Confirm) |
| Sidebar | `<space>gm` `<space>gM` `<space>gs` | Merge the selected branch or tag into the current branch (as in the graph) |
| Sidebar | `<space>gp` `<space>gv` | Cherry-pick / revert the tip of the selected branch or tag onto the current branch |
| Sidebar | `<space>gx` | Reset the current branch to the selected branch or tag (`s`/`m`/`h`; Confirm where work is lost) |
| Sidebar | `<space>gr` | Rebase the current branch onto the selected branch or tag (Confirm; same refusals as in the graph) |
| Sidebar | `<space>gi` | Interactive rebase onto the selected branch or tag (as in the graph) |
| Sidebar | `<space>gI` | Interactive rebase of the whole history (as in the graph) |
| Sidebar | `<space>l` | Open the reflog of the local branch under the cursor (HEAD's on any other row) |
| Sidebar | `<space>.` | Open the actions menu of the row under the cursor (same menu as a click on the `...` marker) |
| Sidebar | `y` | Copy the branch, tag or stash name, or the worktree path (OSC 52) |
| Sidebar | `<space>Sa` | Apply the stash under the cursor and keep it |
| Sidebar | `<space>Sp` | Pop the stash under the cursor (apply, then drop it) |
| Sidebar | `<space>Sd` | Drop the stash under the cursor after a Confirm that shows the restore command |
| Sidebar | `<space>wa` | Add a worktree: prompt for a branch (existing, or a new name started at the selected commit), then the path |
| Sidebar | `<space>wd` | Remove the selected worktree (Confirm; a dirty one needs a second Confirm) |
| Sidebar | `<space>wl` `<space>wu` | Lock (optional reason) / unlock the selected worktree |
| Sidebar | `<space>wp` | Prune worktrees whose directory is gone (Confirm lists them) |
| Sidebar | `<space>ws` | Switch the tab to the selected worktree, keeping its view state |
| Sidebar | `<space>wt` | Open the selected worktree, or the one of the selected branch (added first if it has none), in its own tab (focuses the tab that already shows it) |
| Diff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| Diff | `]c` `[c` | Next / previous hunk |
| Diff | `]f` `[f` | Next / previous file (the pane shows that one file; the file list cursor follows; stops at the first / last file) |
| Diff | `Esc` | Back to the file list (closes an on-demand diff) |
| Diff | `/` `n` `N` | Search in diff, next, previous |
| Diff | `<space>vw` | Toggle word wrap (off by default; the cursor moves by screen line while on) |
| Diff | `<space>vv` | Open the `view ▾` dropdown (title row menu of the view options and zoom; not in a file's content, whose only option is wrap) |
| Diff | `y` | Copy the cursor line as plain text (no `+`, `-` or space marker) |
| Diff | `Y` | Copy the displayed rows of the file under the cursor as plain text, without markers (not a pasteable patch) |
| Diff | `+` `-` | Show more / fewer unchanged lines around the changes (3 at a time, default 3; the title shows `[ctx N]`) |
| Diff | `<space>vf` | Toggle showing whole files instead of hunks (title `[file]`) |
| Diff | `<space>vi` | Toggle ignoring whitespace-only changes (title `[-w]`) |
| Diff | `<space>vs` | Toggle the side-by-side view (title `[split]`; needs a pane of 80 text columns) |
| Diff | `<space>vc` | Toggle showing whitespace: tabs, spaces and carriage returns as marks (title `[ws]`) |
| Diff | `<space>vl` | Load anyway: reload the commit without the row and per-file limits |
| Diff | `<space>vp` | Show a merge commit against its next parent (same as in the detail pane; the file list and the diff follow) |
| Reflog | `Enter` | Select the entry's commit in the graph (the list stays open) |
| Reflog | `h` `Esc` | Close the list |
| Reflog | `<space>r` | Read the reflog again |
| Reflog | `<space>l` | Open the ref picker: the reflog of HEAD or of any local branch |
| Reflog | `/` `n` `N` | Search the entries, next, previous |
| Reflog | `<space>gx` | Reset the current branch to the entry (`s`/`m`/`h`; Confirm where work is lost) |
| Reflog | `<space>bc` | Create a branch at the entry and check it out (prompt popup) |
| Reflog | `<space>bs` | Check out the entry with a detached HEAD |
| RebaseEditor | `p` | Pick: keep the highlighted commit as it is |
| RebaseEditor | `r` | Reword: open the message editor for the highlighted commit (its full message; Insert mode) |
| RebaseEditor | `s` | Squash: meld the commit into the one above and keep both messages |
| RebaseEditor | `f` | Fixup: meld the commit into the one above and keep only that one's message |
| RebaseEditor | `d` | Drop: remove the commit |
| RebaseEditor | `K` `Alt-k` | Move the commit up (replayed earlier; a count moves it that many rows) |
| RebaseEditor | `J` `Alt-j` | Move the commit down (replayed later) |
| RebaseEditor | `Enter` | Start the rebase: a Confirm names what is kept and dropped and the undo |
| RebaseEditor | `h` `Esc` | Close the editor; an edited todo asks for a second `Esc` first |
| ConflictEditor | `n` `]c` | Next conflict (wraps; from the cursor row down) |
| ConflictEditor | `N` `[c` | Previous conflict (wraps) |
| ConflictEditor | `u` | Undo the latest pick, untake or typing session (the cursor goes to its conflict) |
| ConflictEditor | `Ctrl-r` | Redo the undone change |
| ConflictEditor | `<space>u` | Untake, the inverse of `<space>a`: the current conflict is unresolved again and its markers come back (one step of undo); with a Visual selection only the selected lines leave the result |
| ConflictEditor | `zh` `zl` | Scroll the text of the base, ours and theirs panes sideways together, 8 columns per press (a count multiplies it); vim's `zh`/`zl` meaning. Normal and Visual mode |
| ConflictEditor | `R` | Re-create the conflict of the file (`git checkout --merge`) after a Confirm |
| ConflictEditor | `Esc` | Close the editor; changes ask for a second `Esc` first (a Visual selection ends first; in Insert mode it stops typing and keeps the text) |
| ConflictEditor | `Enter` | Save the result to the file (a Confirm first when conflicts are unresolved; then the "mark resolved" Confirm when none is) |
| ConflictSide | `<space>a` | Take this pane's version (base, ours or theirs) for the current conflict |
| ConflictSide (Visual) | `<space>a` | Take only the selected lines of this pane for the current conflict (the selection ends) |
| ConflictSide | `o` | Take our side for the current conflict |
| ConflictSide | `t` | Take their side for the current conflict |
| ConflictSide | `b` | Take the common ancestor for the current conflict |
| ConflictSide | `B` | Take both sides for the current conflict, ours first |
| ConflictSide | `O` | Take our side for every conflict still unresolved (settled ones stay; one step of undo) |
| ConflictSide | `T` | Take their side for every conflict still unresolved |
| ConflictSide | `e` | Edit in the result: move into the result pane at the first line of the current conflict and start typing (Insert mode; `Esc` or `Ctrl-g` keeps the text) |
| ConflictSide | `h` | Close the editor, as `Esc` does |
| ConflictResult | `i` | Start typing at the cursor (Insert mode) |
| ConflictResult | `h` `l` | Cursor left / right in the line |
| ConflictResult | `w` `b` | Next / previous word |
| ConflictResult | `0` `$` | Line start / end |
| ConflictResult | `a` `A` `I` | Insert after the cursor / at the line end / at the line start |
| ConflictResult | `o` | Open a line below and insert |
| ConflictResult | `x` | Delete the character under the cursor |
| ConflictResult | `dd` | Delete the line |
| ConflictResult (Insert) | `Enter` | New line in the result |
| ConflictResult (Insert) | `Ctrl-g` | Stop typing and keep the text (one step of undo; `Esc` does the same) |
| RebaseEditor (Insert) | `Enter` | New line in the reword's message |
| RebaseEditor (Insert) | `Ctrl-g` | Keep the message (the commit becomes a reword); `Esc` cancels the edit |
| Settings | `Enter` `l` | Enter the category, edit the setting under the cursor (toggle flips, choice opens its options, number and path list start typing) or keep the option under the cursor; with a filter active, jump to the setting |
| Settings | `h` `Esc` | Undo the innermost thing (the removal question, typing, an open choice, the filter), else go back to the categories, else close |
| Settings | `x` | Flip a toggle and save |
| Settings | `/` | Filter the settings across all categories by name and help text, and the key lines by title, keys and action id (Insert mode; `Enter` keeps the filter, `Esc` clears it) |
| Settings | `a` | Add a folder to the path list under the cursor (prompt; `~` is expanded; must be an existing folder) |
| Settings | `d` | Remove the folder under the cursor after a question naming it (`y` removes, `n` or `Esc` keeps; the folder itself is not touched) |
| Settings | `J` `K` | Move the folder under the cursor down / up in its list |
| Settings | `+` `-` | Raise / lower a number setting by its step |
| Picker | `Up` `Down` | Move highlight |
| Picker | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases; terminals report `Ctrl-j` distinct from `Enter`) |
| Picker | `Enter` `Ctrl-l` | Open repository (new tab or focus existing); `Ctrl-l` is the home-row alias of `Enter`, `Ctrl-w l` is a chord and unaffected |
| Picker | `Tab` `Right` | Accept the suggestion: with a typed name, fill in the highlighted repository's path (then its subfolders are listed); in a path, complete the directory name (to the common prefix of the candidates, or to the highlighted one). Matching ignores case on every platform, the inserted text keeps the real case of the directory |
| Picker | `Alt-p` | Pin the entry to the top of the list / unpin it |
| Picker | `Alt-d` | Remove from recents |
| Picker | `Alt-c` | Clone a repository: prompt for the URL or path, then the target folder; opens it in a new tab (modal: `Enter` asks, `Esc` closes the prompt) |
| Picker | `Alt-i` | Initialise a directory (`git init`, created when missing) and open it in a new tab |
| Palette | `Up` `Down` | Move highlight (typing filters; `Enter` runs the action, `Esc` closes) |
| Palette | `Ctrl-j` `Ctrl-k` | Move highlight down / up (aliases, like the picker) |
| Palette | `Ctrl-l` | Run the highlighted action (alias of `Enter`, like the picker); `Ctrl-h` is left unbound because some terminals send it for `Backspace` |
| ContextMenu | `j` `k` `Down` `Up` `Ctrl-j` `Ctrl-k` | Move the highlight down / up (`Esc` closes; typed text does nothing, there is no filter) |
| ContextMenu | `Enter` `Ctrl-l` | Run the highlighted action |
| Help | `Tab` `l` / `Shift-Tab` `h` | Next / previous help tab (wraps around; scroll starts again at the top) |

**Actions menu** (`<space>.` in the graph or sidebar, a double-click on a graph row, or a click on the `...` marker at the right edge of the selected sidebar row; #471, #479, #482).
A small popup that hangs under the row (above it near the bottom of the screen), titled `Actions`, one line per action of that row with its key at the right edge, so the menu teaches the keyboard path.
The menu is as wide as its longest title plus key needs, up to 96 columns, so no title is cut at 120 columns or more (a test renders every row menu at 100, 120 and 160; titles in `ActionCatalog` stay short, details belong in the Confirm; #573).
It is not the command palette (`:`): no text field, no filter.
The command palette is up to 100 columns wide so its titles are not cut either.
The same holds for the other popups (#597): Help and which-key take up to 96 columns and settings up to 100; Confirm, the prompt popup and the mainline picker fit their longest line, from 64, 64 and 72 columns up to 96, so a short dialog stays compact (the layout at 80 columns changes too, wherever the content needs more than the old limit).
`j`/`k`/`Down`/`Up` move, `Enter` or a click runs the highlighted action, `Esc` or a click outside closes it; the action runs after the menu closes, exactly as its key would, so its prompt popup or Confirm opens as usual.
A resize closes it.
There is no right-, middle- or Ctrl-click binding (they feel like a desktop app and multiplexers may swallow them, #479): the mouse path is a double-click on a graph row (the first click selects it) or a plain left click on the `...` marker that the selected sidebar row shows in its last three columns (not shown in panes narrower than 20 columns; a first click on another row only selects it, as before).
Graph rows have no marker and use their full width (#482).
Actions without a key in the pane are not listed.
The list depends on the row:

- Graph commit: new branch (`bc`, `bC`), switch, tag create, delete, push, remote delete, merge, merge-commit, squash, rebase, interactive rebase, reset, cherry-pick, revert, fixup, reword, drop (quick rebase, #459), copy hash, mark or compare a commit (`<space>=`, #535), open details (`Enter`; the mouse route to the details pane; on screens under 14 rows the pane is hidden, so the row is left out and `Enter` shows a notice, #486, #490).
- Sidebar local branch: switch, new branch, new-only, rename, delete, set and unset upstream, merge (three kinds), rebase, interactive rebase, reset, cherry-pick, revert. Remote branch: the same without rename, delete and upstream.
- Sidebar tag: tag create, delete, push, remote delete, new branch, new-only, merge (three kinds), rebase, interactive rebase, reset, cherry-pick, revert.
- Sidebar worktree: switch, open, add, remove, lock, unlock, prune.
- Sidebar stash: apply, pop, drop, push, push-all.
- Section headers: Branches new, new-only; Tags create; Worktrees add, prune; Stashes push, push-all. Remotes fetch, fetch all, pull, push, force push, delete remote branch, add, remove, rename, set URL.

The menu is the mouse path of every sidebar action; the header chips it replaced are gone (#469).

**Title dropdown (#645, `view ▾`).** The reusable title-row dropdown is the actions menu popup (above) opened under a title button: titled `View`, one line per option with its key at the right edge (`+` / `-` context, `<space>vf` whole file, `<space>vi` ignore space, `<space>vs` split, `<space>vw` wrap, `<space>vc` whitespace marks, then `<space>z` zoom), `j`/`k`/`Down`/`Up` move, `Enter` or a click runs the option after the menu closes, `Esc` or a click outside closes it, a resize closes it.
It hangs under the button (above the row near the bottom of the screen), is as wide as its longest line needs, and when the screen is too small for it the status bar says so and the keys still work.
The history diff, the Changes tab diff and both working-tree diffs carry it; `<space>vv` opens it from the keyboard.
A file's content has no dropdown (its only option, wrap, stays a button).
The Changes tab, both working-tree lists and the Files tab use the same component (#646): titled `View`, hanging under a `view ▾` chip at the right end of their title row, opened by `<space>vv` or a click.
Changes and the working-tree lists offer the path style (`name first`, `full path`, `tree`, set directly; `<space>vn` still cycles), the Files tab the path filter (its only display option; the same as `/`).
A tab without options (Details) has no chip and `<space>vv` is not bound there.

**Grouped actions (#483).** The palette and the actions menu list actions under non-selectable subheaders (`Branch`, `Merge and rebase`, `Tag`, `Worktree`, `Stash`, `Working tree`, `Copy`, `Diff`, `Details`, `External tools`, `Navigate`, `View`, `Tabs`, `General`).
The group of an action comes from `ActionCatalog.GroupOf` (metadata, not a per-list hand-written list; a test fails when a bound action has none) and the which-key popup names its chord groups from the same `ActionGroups`, so there is one vocabulary.
`j`/`k`, the wheel and clicks only ever land on actions: a click on a subheader or a gap does nothing.
A blank gap line precedes every subheader except the first (#489), like the help overlay's sections, so it is visible in every colour tier and the ASCII set; on a short screen the gaps are dropped before any row (the menu when gaps plus rows exceed its height, the scrolling palette when it has fewer than 8 list rows).
The palette keeps the groups in the fixed order above, the focused view's own actions first inside a group; a filter keeps matching actions under their groups, the group with the best match first, and groups without a match disappear.
The row menu uses subheaders only when it has more than 5 actions and at least two groups (the graph row menu; the worktree and stash menus are short or single-group and stay flat).
Subheaders use the section-title style and are plain text, so they work in the 16-colour, `NO_COLOR` and ASCII tiers.
The help overlay uses the same groups as subheaders inside its tabs (#652); the which-key popup lists chord continuations and gets none.

Mouse paths are listed in the next section.

## Keymap rework (design #833; proposal, built by #839 and #840)

Status: **proposal for the owner to edit in this PR.** After #840 lands the tables below are folded into "Keymap" and this section is cut down to the rules and the level list, so the Old column does not go stale. The table in "Keymap" above stays the truth until #840 changes `DefaultKeymap`; nothing here is implemented, and the keymap test does not read this section.
Source: the owner's feedback on #833 (too many keys, `<space>b` / `<space>r` confusing, tabs on `Ctrl-w` but repositories on `<space>o`, rare commands mixed with everyday ones).
Counts, read from `DefaultKeymap` and `ActionIds` at this commit: 245 action ids (the issue said 244), 240 bound, 5 without a key (`help.tab-select` is mouse-only, `pick.mainline` modal, `remote.fetch-all`, `settings.open-theme` and `working-tree.open` palette-only), plus the nine `tab.go-to.N` ids (`Alt-1`..`Alt-9`, Everyday, unchanged). The design adds one action, `view.cycle-command-level`, and a key for `detail.retry`. 50 actions change their main key (the rows of the tables below with a key in "New"); the rest keep theirs.

### Layout rules

1. **One noun, one prefix.** Every `<space>` group is one noun and a noun has exactly one group: `b` branch, `t` tag, `w` worktree, `k` stash, `r` remote, `o` repositories and tabs, `g` operation in progress, `e` external tools, `v` view options.
2. **No lowercase/uppercase pair with unrelated meanings.** Gone: `s`/`S` (working tree / stash), `t`/`T` (tools / tags), `r`/`R` (refresh / remotes), `l`/`L` (reflog / pull), `p`/`P` (pin / push), `f`/`F` (jump to ref / fetch), `c`/`C` (commit / cancel), and `b`/`B`, `r`/`R` in the conflict editor. Kept, because the two keys are one action in two strengths or its direct opposite: `bc`/`bC` (create and check out / create only), `bu`/`bU` (set / unset upstream), `bm`/`bM`, `bd`/`bD` (local / remote branch), `rp`/`rP` (push / force push), `td`/`tD`, `a`/`A`, `u`/`U` (stage and unstage, one file / all), `n`/`N`, `o`/`O`, `t`/`T`, `y`/`Y`, `j`/`J`, `k`/`K`.
3. **All branch work under `<space>b`.** Create, switch, rename, delete (local `bd`, remote `bD`), upstream, fast-forward, merge (three kinds), rebase (the sub-group `<space>br`, which also holds fixup, reword and drop), reset, cherry-pick and revert. Tags and worktrees are nouns of their own (rule 1) and keep their own groups, listed next to `b` in the which-key popup.
4. **Tabs and repositories together under `<space>o`** (`oo` open, `or` refresh, `oc` close tab, `on` rename tab, `op` pin, `ol` last tab). `Ctrl-t`, `Ctrl-w c`, `Ctrl-w r`, `Ctrl-^`, `<space><Tab>`, `gt`, `gT` and `Alt-1`..`Alt-9` stay (aliases, or the tab-switching keys).
5. **Aliases on one row.** Help, palette and the tables below show one row per action: main key first, then the other keys (`Tab  also Ctrl-w w`). The main key is the first one in the "New" column. Built by #838.
6. **Vim-clash rule unchanged** (#335): Gitunia actions are `<space>` chords or `Ctrl-`/`Alt-` keys. The bare letters of the modal screens (rebase todo, conflict editor, settings) stay: they are keys of a modal list.

### The leader map

| Prefix | Noun | Keys (after `<space>`) |
|---|---|---|
| `b` | Branch | `bc` create and check out, `bC` create only, `bs` switch, `bn` rename, `bd` delete, `bD` delete on a remote, `bu` / `bU` set / unset upstream, `bf` fast-forward, `bm` merge, `bM` merge with a commit, `bq` squash-merge, `bp` cherry-pick, `bv` revert, `bx` reset |
| `br` | Rebase (inside branch) | `brr` onto, `bri` interactive, `brI` interactive from the root, `brf` fixup, `brw` reword, `brd` drop |
| `t` | Tag | `tc` create, `td` delete, `tp` push, `tD` delete on a remote |
| `w` | Worktree | unchanged: `wa` add, `ws` switch, `wt` open in a tab, `wd` remove, `wl` lock, `wu` unlock, `wp` prune |
| `k` | Stash | `ks` push, `ku` push with untracked, `ka` apply, `kp` pop, `kd` drop |
| `r` | Remote | `rf` fetch, `rl` pull, `rp` push, `rP` force push, `rc` cancel, `ra` add, `rd` remove, `rn` rename, `ru` set URL (`rF` reserved for fetch all) |
| `o` | Repositories and tabs | `oo` open a repository (new tab), `or` refresh, `oc` close tab, `on` rename tab, `op` pin, `ol` last tab |
| `g` | Operation in progress | `gc` continue, `ga` abort, `gs` skip, `gr` resolve (open the conflict editor) |
| `e` | External tools | `ed` difftool, `em` mergetool, `ec` commit in `$EDITOR` |
| `v` | View options | unchanged (`vv` menu, `vw`, `vf`, `vi`, `vs`, `vc`, `vl`, `vp`, `vn`, `v1`..`v3`), plus `vb` base in the conflict editor |
| leaves | | `s` working tree, `c` write the message or commit (working tree), `a` `A` `d` `u` `U` stage, discard, unstage (working tree), `1`..`4` working-tree panes, `h` jump to HEAD, `f` jump to a ref, `j` quick jump, `=` compare, `l` reflog, `z` zoom, `x` dismiss, `m` messages, `,` settings, `.` actions menu, `*` toggle advanced commands |

No leaf shares its letter with a group. Letters left free for later: `i` `n` `p` `q` `y`. The stash letter is `k` because `s` stays the working tree (the most used leader key) and `S` would be an unrelated pair; the owner may prefer another free letter.
The which-key popup names the groups after the nouns (`+more (branch)`, `+more (rebase)`, `+more (tag)`, ...); `ActionGroups` stays the vocabulary of the palette, the row menu and the help subheaders.

Old chords that now mean something else (muscle-memory hazards; every destructive one still asks first): `<space>td` (difftool, now delete tag), `<space>gs` (squash-merge, now skip), `<space>gr` (rebase, now resolve), `<space>br` (rename, now the rebase group), `<space>P` (push; push is now `rp`, and `rP` is force push: one Shift slip, but it is a lease push that asks first), `<space>r` and `<space>o` (leaves, now groups that wait for a second key). Chords that are simply gone: `<space>gm` `gM` `gp` `gv` `gx` `gi` `gI` `gf` `gw` `gd` `gk`, `<space>tm`, `<space>te`, `<space>to` (see #742 below), `<space>p`, `<space>F` `L` `C`, `<space>R...`, `<space>S...`, `<space>T...`. In the conflict editor `B` (base) becomes `<space>vb` and `R` (re-create) `Ctrl-x`. `detail.retry` moves from `<space>r` to `<space>od` because `<space>r` is now a group and a key cannot be both a complete binding and the start of a longer chord.

### Decisions for the owner

- **Tags and worktrees stay their own groups** (`t`, `w`), and fetch, pull, push stay under `r`, although the issue's rule "all branch work under `<space>b`" listed `T` and `w` among today's places of branch work. Rule 1 (one noun, one prefix) and the length of everyday chords (`<space>bwa` for adding a worktree) decided it; only deleting a branch on a remote moved into `b` (`bD`). To follow the rule literally, move them to `<space>bt` and `<space>bw` and say so in the PR.
- **Stash is `k`**, and the working tree keeps `s` (see the leader map).
- **Levels** (the Level column) and the judgement notes in it.
- **`<space>*`** as the toggle key.

### Conflicts with other issues

- **#834 (h/l stay in the pane, `Ctrl-h/j/k/l` between panes).** Merged; the rework keeps it. `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l` become the main keys of `pane.focus-*` in the tables (the old table listed `Ctrl-w h/j/k/l` first), `Ctrl-w h/j/k/l/w` are aliases on the same row, `Ctrl-Backspace` stays an alias of `Ctrl-h`. Bare `h`/`l` keep their in-pane meaning (fold, expand, the message box cursor); in the modal screens that replace the panes (help, settings, reflog, rebase todo, conflict editor) `h` stays "back" and `l` "next tab" / "activate", as #834 decided. No new binding uses bare `h`/`l`, and `<space>h` / `<space>l` stay leader leaves (HEAD, reflog), so the key sets do not clash. The picker, palette and row menu keep their own `Ctrl-j` / `Ctrl-k` / `Ctrl-l` (their view wins over `Global`). The help tabs of #838 switch with `h` / `l` and `Tab` / `Shift-Tab` as now.
- **#742 (conflict editor entry).** Its open PR #835 binds `<space>to`, which collides with rule 1 (`t` is the tag group). The rework moves `conflict.open` to `<space>gr` ("resolve", next to continue, abort and skip, where a user in a conflict already looks) and keeps `Enter`, `l` and the double-click on the conflicted row. #835 can merge as it is; #840 re-keys the action and sweeps the hint text and tests that name `<space>to` (the hints come from the keymap). #840 waits for #742 for that reason.
- **Settings.** The Key reference stays read-only (owner decision on #833); the new keys appear there from the keymap.
- **Existing `keys` entries keep working.** An entry is `{view, key, action}` and binds a key to an action id, so ids are the contract and none changes (the one new id is `view.cycle-command-level`). A user entry that binds an action to a key of the user's choice is untouched. Two edge cases, both reported by the existing config check, never fatal: an entry that binds a key to a prefix the new defaults now use (a user leaf `<space>r` against the group `<space>r...`) is refused with the message naming both keys; an entry `none` on an old default key that no longer exists reports "nothing to unbind". #840 lists both in the user guide (#842).

### Fast toggle for the command level

`<space>*` (action `view.cycle-command-level`, Global, Normal mode). `*` reads as "show all", needs no letter from the noun groups, and the leader keeps it clear of vim's bare `*` (word search, which Gitunia does not use). Alternatives considered: `<space>A` (taken, stage all), `<space>M` (unrelated pair with `m`), `<space>?` (confusable with the help key).
It cycles Everyday, Advanced, Advanced+ and back to Everyday. It changes the same setting as the Settings row (Settings, Keys; the row shows the key), writes it to `config.json` like any setting, and the status bar confirms (`Command level: Advanced`). It is also in the palette as "Cycle the command level" (Everyday).

### Levels (Everyday, Advanced and Advanced+)

- **Three levels, each includes the ones below.** Everyday shows only Everyday actions; Advanced adds the Advanced ones; Advanced+ shows everything (rare or risky commands such as upstream set/unset, interactive rebase from the root, remote URL, worktree lock/unlock, stash with untracked, reflog). One setting, `Command level`, holds the choice.

- **Hidden, never disabled.** An Advanced or Advanced+ action keeps its key, its palette id and its command (typing its exact id in `:` still runs it); it is only left out of the places that teach keys: the help tabs, the which-key popup, the palette list, the row menu and the status bar hints. A user `keys` entry for a hidden action keeps working.
- **Messages keep their keys.** A message or confirm text that names a key (`<space>vl` load all, the reflog hint after a reset, the retry key) always names it, whatever the level; only the lists that teach keys (help, which-key, palette, row menu, status hints) filter. So an undo hint never points at nothing.
- **Default Everyday.** `Command level` defaults to Everyday (advanced commands off); Settings (Keys) and `<space>*` change it. The owner sets Advanced in their own config.
- **Where the level lives.** One level per action in `ActionCatalog` (metadata like the group; a test fails when an action has none), so every surface reads the same list. A which-key group whose actions are all above the current level is hidden with them (`<space>e` and `<space>br` disappear at Everyday; `<space>t` stays visible because it holds two Everyday actions).
- **Modal screens.** The keys of a screen follow the level of the way into it: the rebase todo keys are Advanced because rebasing is; the conflict editor keys are Everyday except base, re-create, redo, the diff jumps and the side scroll. While a screen is open its footer and help show all its keys, whatever the level, since a screen hiding its own keys is unusable.
- **How the proposal was made.** It starts from the issue (Everyday: move and search, details and diff, tabs and opening repositories, stage/unstage/discard, commit/amend, branch create/switch/delete, merge, fetch/pull/push, stash push/pop, worktree add/switch, copy; Advanced: reflog, reset, interactive and quick rebase, cherry-pick/revert, upstream, fast-forward, remote management, tag push and remote delete, force push, difftool/mergetool/`$EDITOR` commit, squash and no-ff merge, extra view options, status history) and fills the rest with one test: would someone who just wants to commit and push see this daily? Judgement calls have a note. `conflict.open` is plain Everyday (the `g` group is shown even with no operation running; the row only acts in a conflict). Counts of the first proposal: 165 Everyday, 81 Advanced (246 ids with the new action; the nine `tab.go-to.N` are Everyday and not counted).
- **The Level column is the approved list** (owner-edited, #847): 188 Everyday, 47 Advanced, 9 Advanced+ rows; #839 implements it as it stands.

### Old key and new key, level per action

Grouped as the palette groups them (`ActionCatalog.GroupOf`). "Old" lists every default key of the action over all views, "New" the main key then its aliases, `same` when the main key does not change. A key written `<space>bc` is the leader followed by `b`, `c`.
**Branch**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `branch.create` | Create a branch and check it out | `<space>bc` | same | Everyday |  |
| `branch.create-only` | Create a branch without checking out | `<space>bC` | same | Advanced |  |
| `branch.delete` | Delete the branch (asks first) | `<space>bd` | same | Everyday |  |
| `branch.fast-forward` | Fast-forward the branch to its upstream (no checkout) | `<space>bf` | same | Advanced |  |
| `branch.rename` | Rename the branch | `<space>br` | `<space>bn` | Everyday | not on the issue's Everyday list |
| `branch.set-upstream` | Set the branch upstream | `<space>bu` | same | Advanced+ |  |
| `branch.switch` | Switch to a branch (check out) | `<space>bs` | same | Everyday |  |
| `branch.unset-upstream` | Unset the branch upstream | `<space>bU` | same | Advanced+ |  |

**Merge and rebase**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `conflict-editor.all-ours` | Conflict editor: take our side for every unresolved hunk of the file | `O` | same | Everyday |  |
| `conflict-editor.all-theirs` | Conflict editor: take their side for every unresolved hunk of the file | `T` | same | Everyday |  |
| `conflict-editor.base` | Conflict editor: show or hide the base below the sides (narrow screens) | `B` | `<space>vb` | Advanced |  |
| `conflict-editor.both` | Conflict editor: take both sides for the hunk, ours first | `b` | same | Everyday |  |
| `conflict-editor.edit` | Conflict editor: edit the result of the hunk as text | `e` | same | Everyday |  |
| `conflict-editor.edit-done` | Conflict editor: keep the edited text | `Ctrl-g` | same | Everyday |  |
| `conflict-editor.edit-newline` | Conflict editor: insert a new line in the edited text | `Enter` | same | Everyday |  |
| `conflict-editor.next` | Conflict editor: go to the next conflict hunk | `n` | same | Everyday |  |
| `conflict-editor.next-diff` | Conflict editor: scroll the sides to the next line where they differ | `]c` | same | Advanced |  |
| `conflict-editor.ours` | Conflict editor: take our side for the hunk | `o` | same | Everyday |  |
| `conflict-editor.previous` | Conflict editor: go to the previous conflict hunk | `N` | same | Everyday |  |
| `conflict-editor.previous-diff` | Conflict editor: scroll the sides to the previous line where they differ | `[c` | same | Advanced |  |
| `conflict-editor.recreate` | Conflict editor: re-create the conflict of the file (asks first) | `R` | `Ctrl-x` | Advanced |  |
| `conflict-editor.redo` | Conflict editor: redo the undone pick or edit | `Ctrl-r` | same | Advanced |  |
| `conflict-editor.reset` | Conflict editor: make the hunk unresolved again (it keeps its conflict markers) | `r` | same | Everyday |  |
| `conflict-editor.save` | Conflict editor: save the file (asks first) | `Enter` | same | Everyday |  |
| `conflict-editor.scroll-down` | Conflict editor: scroll our side, their side and the base down together | `Ctrl-d` | same | Advanced |  |
| `conflict-editor.scroll-left` | Conflict editor: scroll the base, ours and theirs panes sideways to the left | `zh` | same | Advanced |  |
| `conflict-editor.scroll-right` | Conflict editor: scroll the base, ours and theirs panes sideways to the right | `zl` | same | Advanced |  |
| `conflict-editor.scroll-up` | Conflict editor: scroll our side, their side and the base up together | `Ctrl-u` | same | Advanced |  |
| `conflict-editor.theirs` | Conflict editor: take their side for the hunk | `t` | same | Everyday |  |
| `conflict-editor.undo` | Conflict editor: undo the last pick or edit | `u` | same | Everyday |  |
| `merge.branch` | Merge the selection (fast-forward if possible) | `<space>gm` | `<space>bm` | Everyday |  |
| `merge.branch-no-ff` | Merge the selection with a merge commit | `<space>gM` | `<space>bM` | Advanced |  |
| `merge.branch-squash` | Squash-merge the selection (stages changes) | `<space>gs` | `<space>bq` | Advanced |  |
| `operation.abort` | Abort the merge, rebase, cherry-pick or revert in progress (asks first) | `<space>ga` | same | Everyday |  |
| `operation.continue` | Continue the merge, rebase, cherry-pick or revert in progress | `<space>gc` | same | Everyday |  |
| `operation.skip` | Skip the commit a rebase, cherry-pick or revert stopped on (asks first) | `<space>gk` | `<space>gs` | Everyday |  |
| `pick.cherry-pick` | Cherry-pick the selection onto the current branch | `<space>gp` | `<space>bp` | Everyday |  |
| `pick.mainline` | Choose the mainline parent in the open cherry-pick or revert prompt | none | same | Advanced |  |
| `pick.revert` | Revert the selection with new commits | `<space>gv` | `<space>bv` | Advanced |  |
| `rebase-editor.drop` | Rebase todo: remove the commit | `d` | same | Advanced |  |
| `rebase-editor.fixup` | Rebase todo: meld the commit into the one above and keep that one's message | `f` | same | Advanced |  |
| `rebase-editor.message-newline` | Rebase todo: new line in the message | `Enter` | same | Advanced |  |
| `rebase-editor.message-save` | Rebase todo: keep the edited message | `Ctrl-g` | same | Advanced |  |
| `rebase-editor.move-down` | Rebase todo: replay the commit later (move it down) | `J` `Alt-j` | same | Advanced |  |
| `rebase-editor.move-up` | Rebase todo: replay the commit earlier (move it up) | `K` `Alt-k` | same | Advanced |  |
| `rebase-editor.pick` | Rebase todo: keep the commit as it is | `p` | same | Advanced |  |
| `rebase-editor.reword` | Rebase todo: keep the commit with a new message (opens the message editor) | `r` | same | Advanced |  |
| `rebase-editor.squash` | Rebase todo: meld the commit into the one above and keep both messages | `s` | same | Advanced |  |
| `rebase-editor.start` | Rebase todo: start the rebase (asks first) | `Enter` | same | Advanced |  |
| `rebase.drop` | Drop the commit from the branch (asks first) | `<space>gd` | `<space>brd` | Advanced |  |
| `rebase.fixup` | Fix up the commit into the one before it (asks first) | `<space>gf` | `<space>brf` | Advanced |  |
| `rebase.interactive` | Interactive rebase onto the selection (edit the todo) | `<space>gi` | `<space>bri` | Advanced |  |
| `rebase.interactive-root` | Interactive rebase from the root commit (selection ignored) | `<space>gI` | `<space>brI` | Advanced+ |  |
| `rebase.onto` | Rebase the current branch onto the selection | `<space>gr` | `<space>brr` | Everyday | plain rebase is common; the issue list names only interactive and quick rebase as advanced |
| `rebase.reword` | Reword the commit subject (asks first) | `<space>gw` | `<space>brw` | Advanced |  |
| `reset.branch` | Reset the current branch to the selection | `<space>gx` | `<space>bx` | Everyday |  |

**Tag**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `tag.create` | Create a tag at the selected commit | `<space>Tc` | `<space>tc` | Everyday |  |
| `tag.delete` | Delete the tag (asks first) | `<space>Td` | `<space>td` | Everyday |  |
| `tag.delete-remote` | Delete the tag on a remote (asks first) | `<space>TD` | `<space>tD` | Everyday |  |
| `tag.push` | Push the tag to a remote | `<space>Tp` | `<space>tp` | Everyday |  |

**Remote**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `remote.add` | Add a remote (name, then URL) | `<space>Ra` | `<space>ra` | Advanced |  |
| `remote.cancel` | Cancel the running fetch, pull or push | `<space>C` | `<space>rc` | Everyday |  |
| `remote.delete-branch` | Delete a branch on a remote (asks first) | `<space>RD` | `<space>bD` | Everyday |  |
| `remote.fetch` | Fetch the remote of the current branch (prunes) | `<space>F` | `<space>rf` | Everyday |  |
| `remote.fetch-all` | Fetch all remotes (prunes) | none | none (palette) | Advanced | unbound today; `<space>rF` is reserved for it; bound in #840 only if the owner wants a key |
| `remote.pull` | Pull the current branch (fast-forward only) | `<space>L` | `<space>rl` | Everyday |  |
| `remote.push` | Push the current branch | `<space>P` | `<space>rp` | Everyday |  |
| `remote.push-force` | Force push the current branch with a lease (asks first) | `<space>Rf` | `<space>rP` | Everyday |  |
| `remote.remove` | Remove a remote (asks first) | `<space>Rd` | `<space>rd` | Advanced |  |
| `remote.rename` | Rename a remote | `<space>Rn` | `<space>rn` | Advanced |  |
| `remote.set-url` | Change the URL of a remote | `<space>Ru` | `<space>ru` | Advanced+ |  |

**Worktree**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `worktree.add` | Add a worktree (branch, then path) | `<space>wa` | same | Everyday |  |
| `worktree.lock` | Lock the selected worktree (optional reason) | `<space>wl` | same | Advanced+ |  |
| `worktree.open` | Open the selected worktree (or the branch's, made if needed) in its own tab | `<space>wt` | same | Everyday |  |
| `worktree.prune` | Prune worktrees whose directory is gone | `<space>wp` | same | Everyday |  |
| `worktree.remove` | Remove the selected worktree (asks first) | `<space>wd` | same | Everyday | cleanup of what Add created; consider Everyday |
| `worktree.switch` | Switch this tab to the selected worktree | `<space>ws` | same | Everyday |  |
| `worktree.unlock` | Unlock the selected worktree | `<space>wu` | same | Advanced+ |  |

**Stash**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `stash.apply` | Apply the selected stash and keep it | `<space>Sa` | `<space>ka` | Everyday |  |
| `stash.drop` | Drop the selected stash (asks first) | `<space>Sd` | `<space>kd` | Everyday | consider Everyday (cleanup after Apply) |
| `stash.pop` | Apply the selected stash and drop it | `<space>Sp` | `<space>kp` | Everyday |  |
| `stash.push` | Stash the tracked changes (asks for a message) | `<space>Ss` | `<space>ks` | Everyday |  |
| `stash.push-untracked` | Stash the tracked and untracked changes (asks for a message) | `<space>Su` | `<space>ku` | Advanced+ |  |

**Working tree**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `working-tree.collapse` | Close the folder, else go to the parent folder | `h` | same | Everyday |  |
| `working-tree.commit` | Commit the staged changes | `<space>c` `Ctrl-g` | same | Everyday |  |
| `working-tree.discard` | Discard the changes of the file or folder, the hunk or the selected lines (asks first) | `<space>d` | same | Everyday |  |
| `working-tree.edit-message` | Edit the commit message | `i` `Enter` | same | Everyday |  |
| `working-tree.expand` | Open the folder under the cursor | `l` | same | Everyday |  |
| `working-tree.focus-diff` | Focus the diff | `<space>3` | same | Advanced |  |
| `working-tree.focus-message` | Focus the commit message | `<space>4` | same | Advanced |  |
| `working-tree.focus-staged` | Focus the Staged list | `<space>2` | same | Advanced |  |
| `working-tree.focus-unstaged` | Focus the Unstaged list | `<space>1` | same | Advanced | direct jumps; Tab and Ctrl-h/j/k/l cover the same |
| `working-tree.message-append` | Message box: insert after the cursor | `a` | same | Everyday |  |
| `working-tree.message-append-at-end` | Message box: insert at the line end | `A` | same | Everyday |  |
| `working-tree.message-delete-char` | Message box: delete the character under the cursor | `x` | same | Everyday |  |
| `working-tree.message-delete-line` | Message box: delete the line | `dd` | same | Everyday |  |
| `working-tree.message-down` | Message box: cursor down | `j` | same | Everyday |  |
| `working-tree.message-insert-at-start` | Message box: insert at the line start | `I` | same | Everyday |  |
| `working-tree.message-left` | Message box: cursor left | `h` | same | Everyday |  |
| `working-tree.message-line-end` | Message box: line end | `$` | same | Everyday |  |
| `working-tree.message-line-start` | Message box: line start | `0` | same | Everyday |  |
| `working-tree.message-new-line` | Start a new line in the commit message | `Enter` | same | Everyday |  |
| `working-tree.message-open-line` | Message box: open a line below and insert | `o` | same | Everyday |  |
| `working-tree.message-right` | Message box: cursor right | `l` | same | Everyday |  |
| `working-tree.message-undo` | Message box: undo | `u` | same | Everyday |  |
| `working-tree.message-up` | Message box: cursor up | `k` | same | Everyday |  |
| `working-tree.message-word-next` | Message box: next word | `w` | same | Everyday |  |
| `working-tree.message-word-previous` | Message box: previous word | `b` | same | Everyday |  |
| `working-tree.open` | Open the working tree | none | same | Advanced |  |
| `working-tree.open-diff` | Go to the diff of the file or folder | `Enter` | same | Everyday |  |
| `working-tree.stage` | Stage the file or folder, the hunk or the selected lines | `<space>a` | same | Everyday |  |
| `working-tree.stage-all` | Stage all files | `<space>A` | same | Everyday |  |
| `working-tree.toggle` | Open or leave the working tree | `<space>s` | same | Everyday |  |
| `working-tree.toggle-amend` | Commit option: Amend (toggle) | `Alt-a` | same | Everyday |  |
| `working-tree.toggle-folder` | Open or close the folder under the cursor (tree style) | `za` | same | Everyday |  |
| `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) | `Alt-n` | same | Advanced |  |
| `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) | `Alt-s` | same | Advanced |  |
| `working-tree.unstage` | Unstage the file or folder, the hunk or the selected lines | `<space>u` | same | Everyday |  |
| `working-tree.unstage-all` | Unstage all files | `<space>U` | same | Everyday |  |
| `working-tree.write-message` | Write the commit message | `<space>c` | same | Everyday |  |

**Copy**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `detail.copy-path` | Copy the file path | `y` | same | Everyday |  |
| `diff.copy-file` | Copy the displayed rows of the current file | `Y` | same | Everyday |  |
| `diff.copy-line` | Copy the current diff line | `y` | same | Everyday |  |
| `graph.copy-hash` | Copy the commit hash | `y` | same | Everyday |  |
| `sidebar.copy` | Copy the branch, tag, stash name or worktree path | `y` | same | Everyday |  |
| `visual.copy` | Copy the selected range | `y` | same | Everyday |  |
| `working-tree.copy-path` | Copy the file or folder path | `y` | same | Everyday |  |

**Diff**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `diff.less-context` | Show fewer lines around the changes in the diff | `-` | same | Everyday |  |
| `diff.load-anyway` | Load the whole diff (lift the size limits) | `<space>vl` | same | Advanced |  |
| `diff.merge-parent` | Switch the merge commit's parent | `<space>vp` | same | Advanced |  |
| `diff.more-context` | Show more lines around the changes in the diff | `+` | same | Everyday |  |
| `diff.next-file` | Next file | `]f` | same | Everyday |  |
| `diff.next-hunk` | Next hunk | `]c` | same | Everyday |  |
| `diff.open-view-options` | Open the view options menu of the diff | `<space>vv` | same | Everyday |  |
| `diff.previous-file` | Previous file | `[f` | same | Everyday |  |
| `diff.previous-hunk` | Previous hunk | `[c` | same | Everyday |  |
| `diff.scroll-down` | Scroll the diff down without focusing it | `J` | same | Everyday |  |
| `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it | `D` | same | Everyday |  |
| `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it | `U` | same | Everyday |  |
| `diff.scroll-up` | Scroll the diff up without focusing it | `K` | same | Everyday |  |
| `diff.search-next` | Next diff search match | `n` | same | Everyday |  |
| `diff.search-open` | Search in the diff | `/` | same | Everyday |  |
| `diff.search-previous` | Previous diff search match | `N` | same | Everyday |  |
| `diff.toggle-ignore-whitespace` | Toggle ignoring whitespace changes in the diff | `<space>vi` | same | Everyday |  |
| `diff.toggle-side-by-side` | Toggle the side-by-side diff | `<space>vs` | same | Everyday |  |
| `diff.toggle-whitespace` | Toggle whitespace marks in the diff | `<space>vc` | same | Everyday |  |
| `diff.toggle-whole-file` | Toggle showing whole files in the diff | `<space>vf` | same | Everyday |  |
| `diff.toggle-wrap` | Toggle word wrap in the diff | `<space>vw` | same | Everyday |  |
| `files.open-view-options` | Open the view options menu of the file list | `<space>vv` | same | Everyday |  |
| `files.path-full-path` | Show file paths in full | `<space>v2` | same | Everyday |  |
| `files.path-name-first` | Show file paths name first | `<space>v1` | same | Everyday |  |
| `files.path-tree` | Show files as a tree | `<space>v3` | same | Everyday |  |
| `files.toggle-path-style` | Cycle the file paths: name first, full path, tree | `<space>vn` | same | Everyday |  |

**Details**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `detail.collapse` | Close the directory or folder under the cursor, else go to its parent | `h` | same | Everyday |  |
| `detail.expand` | Open the directory or folder under the cursor | `l` | same | Everyday |  |
| `detail.filter` | Filter the file tree by path (Files tab) | `/` | same | Everyday |  |
| `detail.next-tab` | Next bottom pane tab | `]` | same | Everyday |  |
| `detail.open-diff` | Go to the file diff | `Enter` | same | Everyday |  |
| `detail.open-link` | Open the issue link in the message line (a count picks the nth link) | `gx` | same | Everyday |  |
| `detail.previous-tab` | Previous bottom pane tab | `[` | same | Everyday |  |
| `detail.retry` | Retry loading the commit details | `<space>r` | `<space>od` | Advanced |  |
| `detail.tab-changes` | Show the Changes tab | `gc` | same | Everyday |  |
| `detail.tab-details` | Show the Details tab | `gd` | same | Everyday |  |
| `detail.tab-files` | Show the Files tab | `gf` | same | Everyday |  |
| `detail.toggle-folder` | Open or close the folder under the cursor (Changes tab, tree style) | `za` | same | Everyday |  |

**External tools**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `conflict.open` | Resolve the conflicted file under the cursor in the conflict editor | `<space>to` | `<space>gr` | Everyday | also `Enter` / `l` on the row |
| `tool.commit-editor` | Commit the staged changes in $EDITOR | `<space>te` | `<space>ec` | Advanced |  |
| `tool.difftool` | Open the commit in the difftool | `<space>td` | `<space>ed` | Advanced |  |
| `tool.mergetool` | Resolve conflicts in the mergetool | `<space>tm` | `<space>em` | Advanced |  |

**Navigate**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `graph.compare` | Mark or compare a commit | `<space>=` | same | Advanced | new in #535, not on the issue's lists |
| `graph.jump-to-head` | Jump to HEAD | `<space>h` | same | Everyday |  |
| `graph.jump-to-ref` | Jump to a ref or hash | `<space>f` | same | Everyday |  |
| `graph.open-details` | Focus the commit details | `Enter` | same | Everyday |  |
| `list.bottom` | Go to the bottom | `G` | same | Everyday |  |
| `list.down` | Move down | `j` `Down` | same | Everyday |  |
| `list.half-page-down` | Half page down | `Ctrl-d` `PageDown` | same | Everyday |  |
| `list.half-page-up` | Half page up | `Ctrl-u` `PageUp` | same | Everyday |  |
| `list.top` | Go to the top | `gg` | same | Everyday |  |
| `list.up` | Move up | `k` `Up` | same | Everyday |  |
| `navigate.back` | Go back | `h` `Esc` | same | Everyday |  |
| `reflog.open` | Open the reflog of HEAD (of the branch under the sidebar cursor) | `<space>l` | same | Advanced+ |  |
| `reflog.select-commit` | Select the reflog entry's commit in the graph | `Enter` | same | Advanced+ |  |
| `search.next` | Next search match | `n` | same | Everyday |  |
| `search.open` | Search | `/` | same | Everyday |  |
| `search.previous` | Previous search match | `N` | same | Everyday |  |
| `sidebar.activate` | Activate the sidebar entry | `Enter` | same | Everyday |  |
| `sidebar.collapse` | Collapse the sidebar entry | `h` | same | Everyday |  |
| `sidebar.expand` | Expand the sidebar entry | `l` | same | Everyday |  |
| `sidebar.filter` | Filter the sidebar | `/` | same | Everyday |  |
| `view.quick-jump-to-row` | Quick jump to a visible row | `<space>j` | same | Everyday |  |
| `visual.toggle` | Start or end range selection | `v` | same | Everyday |  |

**View**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `pane.focus-down` | Focus the pane below | `Ctrl-w j` `Ctrl-j` | `Ctrl-j` also `Ctrl-w j` | Everyday |  |
| `pane.focus-left` | Focus the pane to the left | `Ctrl-w h` `Ctrl-h` `Ctrl-Backspace` | `Ctrl-h` also `Ctrl-w h`, `Ctrl-Backspace` | Everyday |  |
| `pane.focus-right` | Focus the pane to the right | `Ctrl-w l` `Ctrl-l` | `Ctrl-l` also `Ctrl-w l` | Everyday |  |
| `pane.focus-up` | Focus the pane above | `Ctrl-w k` `Ctrl-k` | `Ctrl-k` also `Ctrl-w k` | Everyday |  |
| `pane.narrower` | Make the focused pane narrower | `Ctrl-w <` `<` | same | Everyday |  |
| `pane.next` | Next pane | `Tab` `Ctrl-w w` | `Tab` also `Ctrl-w w` | Everyday |  |
| `pane.previous` | Previous pane | `Shift-Tab` | same | Everyday |  |
| `pane.reset-sizes` | Reset the pane sizes | `Ctrl-w =` `=` | same | Everyday |  |
| `pane.shorter` | Make the focused pane shorter | `Ctrl-w -` `-` | same | Everyday |  |
| `pane.taller` | Make the focused pane taller | `Ctrl-w +` `+` | same | Everyday  |  |
| `pane.wider` | Make the focused pane wider | `Ctrl-w >` `>` | same | Everyday  |  |
| `pane.zoom` | Zoom the focused pane | `<space>z` | same | Everyday |  |

**Tabs**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `tab.close` | Close the tab | `Ctrl-w c` | `<space>oc` also `Ctrl-w c` | Everyday |  |
| `tab.last-active` | Previously active tab | `Ctrl-^` `<space><Tab>` | `<space>ol` also `Ctrl-^`, `<space><Tab>` | Everyday |  |
| `tab.next` | Next tab | `gt` | same | Everyday |  |
| `tab.previous` | Previous tab | `gT` | same | Everyday |  |
| `tab.rename` | Rename the tab | `Ctrl-w r` | `<space>on` also `Ctrl-w r` | Everyday |  |

**General**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `app.quit` | Quit | `q` `Ctrl-c` | same | Everyday |  |
| `cancel` | Cancel | `Esc` | same | Everyday |  |
| `command.execute` | Run the command | `Ctrl-l` `Enter` | same | Everyday |  |
| `confirm.no` | Answer no | `n` | same | Everyday |  |
| `confirm.yes` | Answer yes | `y` | same | Everyday |  |
| `help.show` | Show keys for this view | `?` | same | Everyday |  |
| `help.tab-next` | Next help tab | `Tab` `l` | same | Everyday |  |
| `help.tab-previous` | Previous help tab | `Shift-Tab` `h` | same | Everyday |  |
| `help.tab-select` | Show the clicked help tab | none | same | Everyday |  |
| `input.accept` | Accept the input | `Enter` | same | Everyday |  |
| `menu.open-for-row` | Open the actions menu of the selected row | `<space>.` | same | Everyday |  |
| `palette.next` | Next palette entry | `Down` `Ctrl-j` `j` | same | Everyday |  |
| `palette.open` | Open the command palette | `:` `Ctrl-p` | same | Everyday |  |
| `palette.previous` | Previous palette entry | `Up` `Ctrl-k` `k` | same | Everyday |  |
| `picker.accept` | Open the repository | `Enter` `Ctrl-l` | same | Everyday |  |
| `picker.clone` | Clone a repository into a new tab | `Alt-c` | same | Everyday |  |
| `picker.complete-path` | Accept the suggestion / complete the path | `Tab` `Right` | same | Everyday |  |
| `picker.init` | Initialise a directory as a repository | `Alt-i` | same | Everyday |  |
| `picker.next` | Next picker entry | `Down` `Ctrl-j` | same | Everyday |  |
| `picker.open` | Open the repository picker | `Ctrl-t` `<space>o` | `<space>oo` also `Ctrl-t` | Everyday |  |
| `picker.pin` | Pin to the top of the picker (toggle) | `Alt-p` | same | Everyday |  |
| `picker.previous` | Previous picker entry | `Up` `Ctrl-k` | same | Everyday |  |
| `picker.remove-recent` | Remove from recents | `Alt-d` | same | Everyday |  |
| `repeat.end` | Leave the repeat mode | `Esc` | same | Advanced |  |
| `repo.pin` | Pin to the top of the picker (toggle) | `<space>p` | `<space>op` | Everyday |  |
| `repo.refresh` | Refresh the repository | `<space>r` | `<space>or` | Everyday | the views refresh by themselves; consider Advanced |
| `settings.activate` | Settings: enter the column, edit the setting or keep the option | `Enter` `l` | same | Everyday |  |
| `settings.add` | Settings: add a folder to the list under the cursor | `a` | same | Everyday |  |
| `settings.decrease` | Settings: lower the number under the cursor | `-` | same | Everyday |  |
| `settings.filter` | Settings: filter the settings by name and help text | `/` | same | Everyday |  |
| `settings.increase` | Settings: raise the number under the cursor | `+` | same | Everyday |  |
| `settings.move-down` | Settings: move the folder down in its list | `J` | same | Everyday |  |
| `settings.move-up` | Settings: move the folder up in its list | `K` | same | Everyday |  |
| `settings.open` | Open the settings | `<space>,` | same | Everyday |  |
| `settings.open-theme` | Open the settings at the theme | none | same | Advanced |  |
| `settings.remove` | Settings: remove the folder under the cursor (asks first) | `d` | same | Everyday |  |
| `settings.toggle` | Settings: flip the toggle under the cursor | `x` | same | Everyday |  |
| `status.dismiss` | Dismiss the status message | `<space>x` | same | Everyday |  |
| `status.history` | Show the status message history | `<space>m` | same | Everyday |  |

**New action**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `view.cycle-command-level` | Cycle the command level | none | `<space>*` | Everyday | built by #839 |

The nine `tab.go-to.N` ids (`Alt-1`..`Alt-9`) are Everyday and unchanged.

