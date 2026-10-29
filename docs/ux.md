# UX

Spec for workflows and keys.
Keyboard and mouse are both fully supported: every action is reachable with either.
Bindings live in the Keymap and Mouse tables near the end; the sections before them give the behaviour the tables cannot.
How decisions came about and which issue built what: `docs/history/ux.md` and the issues.

## Principles

- **Modal and discoverable.** Normal mode navigates, actions are short mnemonic chords, `?` shows the keys of the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- **Vim-like motions everywhere:** `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work. Bindings are data (mode, view, key sequence, action id); remapping is done in the config file (see "Config file" below).
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

**Repeat mode** (#758) is a sticky sub-mode of Normal for step actions, so a value can be nudged several times without the popup or the prefix each time.

- **Which actions.** Only steppers: the context lines `+`/`-` in the diffs (history diff and both working-tree diffs; not the file content, which has no diff options) and the pane resize keys `Ctrl-w >` `Ctrl-w <` `Ctrl-w +` `Ctrl-w -` in the sidebar, graph, detail and history diff panes.
  Toggles (wrap, whole file, split, whitespace) are not repeatable and never enter the mode.
- **Entering.** No extra key: running a step by key, chip or the `view ▾` dropdown (which closes as usual) starts the mode on the focused pane after that first step.
  A resize that was refused (a pane that cannot be resized, a zoomed pane) does not start it; a context step that only hit its limit does, since more presses are the likely next thing.
- **While on.** The step keys work bare: in the context mode `+` `-`; in the resize mode `>` `<` (wider / narrower), `+` `-` (taller / shorter) and `=` (reset the sizes); they have the meaning of the mode, also where the pane binds the key otherwise (`+` in the diff).
  Each press runs the step again and keeps the mode.
  Any other key ends the mode and then acts as usual (no key is swallowed); only `Esc` ends it and does nothing else.
  The bare `>` and `<` exist only in this mode, so they are not in the vim-clash list.
- **Feedback.** The status bar shows one hint line instead of the pane's own (`+/- more/fewer context   Esc done`; resize: `>/< wider/narrower   +/- taller/shorter   = reset   Esc done`) and the title of the pane gets the marker `[repeat]` (in front of the name in the sidebar and graph titles, so a narrow box cuts the name first; right after `Diff` in the diff titles and after the tab labels in the detail pane, where the option markers such as `[ctx 6]` and the path are cut first); nothing else is tinted.
  The mode is not saved with the session.
- **Scope.** It belongs to one tab and one pane: switching tab or moving focus ends it, as does opening any overlay, prompt or search.
  Chip clicks and the dropdown keep working as before.
  There is no click target to end it: `Esc`, any other key or a click on another pane do.
  The state is a small pure value (`RepeatMode` in Core), the keys are the keymap views `ContextRepeat` and `ResizeRepeat` (Keymap table), so remapping and the key text follow the keymap.

**Paste** (#575).
A terminal paste works in every text input (prompt popups such as the clone URL, the picker and path filter, tree and search filters, the `:` palette, the message editors).
A bracketed paste (`ESC[200~ ... ESC[201~`) is replayed as the keys typing it would have sent, so each input uses its normal typing path; a terminal without bracketed paste sends plain keys, a line break as `Enter`, which cannot be told from a typed one (so there a pasted `url` plus newline accepts the prompt; known limit).
In a single-line input a run of line breaks becomes one space and breaks at either end are dropped (a pasted `url\n` never submits); the message editors keep line breaks as new lines; tabs become spaces and other control characters are dropped.
Outside Insert and Command mode a paste does nothing.

**Confirm mode** is used by every destructive action. A centred dialog holds a title, what will happen, what cannot be undone or how to undo it (in the warning colour) and the name of `y`.
- Only `y` runs the action, after the dialog has closed. `n` and `Esc` drop it. Every other key is swallowed: never `Enter`, and nothing behind the dialog moves.
- The two answers are rows of the dialog and clickable; a click anywhere else closes it like `n`.
- If the dialog does not fit the screen it is not shown and the action does not start (the status bar says so): an answer nobody can read is no confirmation. A dialog that stops fitting after a resize is closed unanswered, with a warning.
- The input mode before the dialog (Normal, or Visual with a range) is restored when it closes. What the action does is fixed when the dialog opens, not when `y` is pressed.

**Prompt popups** (#470) are used by every flow that asks for text or a choice: new, rename and switch branch, the branch to delete, set upstream, new tag and its message, the remote of a tag push or delete, the stash message, worktree branch, path and lock reason, the reset mode and the tab name.

- A centred box holds a title, what the answer is for, the text field (`> text_`; the end of a long text stays in view), the candidate list of a filter prompt and the answering keys: `Enter` accepts, `Esc` cancels, `Up`/`Down` select in a filter.
- **Filter pickers** (#766) are one component (`FilterPicker<T>` in Core, drawn by the prompt popup): the remote of a push, tag push or delete, remote management and remote-branch delete, the upstream of a branch and the branch checkout.
  The candidates are always on screen under the field, best match first, at most 8 rows (the list scrolls with the highlight), the highlighted row marked `> `, then the position `(2/5)`; an empty list says what is missing ("No matching remote").
  `Enter` runs the highlighted candidate without typing anything.
  The hint is the same line in every picker: `Type filters   Up/Down select   Enter <verb>   Esc cancel` (`Tab` also selects; letters always filter, so `j`/`k` are text here).
  A remote branch in the checkout list is written `origin/foo -> local foo`: it is checked out as a new local branch with the remote's name without the remote prefix, tracking it (#762).
- The keys are the same whether the flow was started by key, palette or mouse; a click outside the popup cancels like `Esc`. A resize places the popup again.
- One record (`PromptPopup` in Core) and one frame builder (`PromptPopupFrameBuilder`) draw them. A prompt handler exposes its popup as `IPromptHandler.Popup`, so a new prompt only describes its text.
- The status bar keeps transient messages and the find-as-you-type bars (search `/`, ref jump `<space>f`, quick jump `<space>j`, the diff, reflog and sidebar filters), which narrow a list in place and would hide what they filter.
- When the screen is too small for a popup, the prompt shows in the status bar and still answers to its keys. Confirm dialogs and the mainline choice are centred boxes as well.

## Focus model and key style

- **Panes:** sidebar (left), graph (centre), detail pane with Details, Changes and Files tabs (bottom), and the diff (it belongs to the Changes tab and sits beside its file list, and on the Files tab it shows the file content beside the tree; on screens too narrow for both it opens on demand and replaces the detail pane, see 1c).
- **Moving focus.** `Tab` cycles sidebar, graph, detail, diff (skipping closed panes); `Shift-Tab` reverses.
  `Ctrl-w` + `h` `j` `k` `l` (or `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l`, no prefix, Normal mode only) moves by direction (sidebar and graph on top, detail and diff below; `j` from the sidebar or graph goes to the detail pane, or the diff when the detail pane is hidden; `k` from a bottom pane goes to the graph); `Ctrl-w w` cycles; `Ctrl-w c` closes the tab.
  The focused pane has a highlighted border and its own status hints.
- **Keys.** Frequent actions are single keys per view (`Enter`, `y`, `/`). Global actions use the `<space>` leader (`<space>o` open repository, `<space>p` pin, `<space>z` zoom). `g`, `]`/`[` and `Ctrl-w` start chords; counts prefix motions (`5j`, `3gt`).
- **Chords.** A pending prefix shows in the status bar and, after 250 ms, in a which-key popup listing the continuations, plain actions first and `+more` groups after, with a horizontal rule between the two parts (#573;
  shown in every colour tier and the ASCII set, dropped when no row would be left for an action), each alphabetical by key label with lowercase before uppercase (#472);
  a row that only opens a longer chord reads `+more` plus what the group holds where it is a named leader group (`<space>b` `+more (branch operations)`, also merge/rebase/reset/pick, tag, worktree, stash operations, external tools, diff view options; #443).
  `Esc` cancels; there is no chord timeout.
- **Insert mode.** Printable keys are text; only `Esc`, `Enter` and `Ctrl-`/`Alt-` combinations act, so the leader and chords never fire while typing.
- **`Esc` precedence:** cancel a pending chord, then leave Insert mode, then clear a filter or search highlight, then go back one pane level (diff to detail to graph).
  In the graph, when nothing else claims it, `Esc` clears the comparison mark (#535), and when there is none it dismisses the status message.
- **`q` always quits,** from any pane, except that it first closes an open overlay or ends a Visual range. `h` is back in Detail and Diff and collapse in the Sidebar; `l` is forward (Detail to the diff, Sidebar expand; the diff has no horizontal scroll).
- **Notation:** keys are spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `gg`, `<space>o`), never `^d` or `c-d`, in the help overlay, the palette, the which-key popup and the status bar.
- **Help overlay (`?`):** the keys of the focused view, generated from the keymap table and the action titles, never hand-written, in two columns separated by a vertical line (`Ctrl-d │ Half page down`).
  It has one tab per source (#652): the view's own bindings, then the inherited `Lists` and `Global` ones, and `Markers` on the graph and the sidebar; an overlay with a single page (the status history, a command's output) shows no tab row.
  Inside a tab the rows sit under the `ActionCatalog.GroupOf` subheaders (`Branch`, `Merge and rebase`, `Navigate`, ...; the same vocabulary and order as the palette; an action without a group lands in `Other`, a test forbids it).
  The tab row is under the title: the active tab in brackets (`[Graph]  Lists  Global`), so it works in every colour tier and the ASCII set; when the labels do not fit the width it collapses to the active label with its position (`[Graph] 1/4`).
  `Tab`/`l` and `Shift-Tab`/`h` switch tabs (wrapping), a click on a label jumps to it; each tab scrolls from its top and the keys column has the same width on every tab.
  `j`/`k`/`Ctrl-d`/`gg` scroll it; `Esc`, `q` or `?` close it; every other action is ignored while it is open.
  The `Alt-1`..`Alt-9` entry also names the multiplexer-safe `3gt`.
  Digits are not tab keys: they stay counts.
- **Command palette (`:` or `Ctrl-p`):** typing filters every action fuzzily (title or action id), `Up`/`Down` (or `Ctrl-j`/`Ctrl-k`) move, `Enter` (or `Ctrl-l`) runs the highlighted action, `Esc` closes.
  Each row shows its bindings and actions are grouped under subheaders (see Actions menu), the focused view's own actions first inside a group.
  Modal-only actions (the Confirm answers, the picker's own keys, the palette's own keys, text-input accept and `Esc`) are not listed; actions bound ahead of their feature are listed greyed out.
- **Status bar hints** are key plus short description pairs (`j/k move  gg/G top/end`), keys in the accent colour.
  They start with `? help  : commands  q quit`, so a narrow terminal cuts the pane's own hints, never those.
  Every hint names the action of each of its keys, and a test checks the default keymap binds that key to that action in that pane.
  - **Few hints, view options grouped (#633).** The default line of a pane lists only what matters most there: move, the jumps of that pane, search, `h back`, `<space>j quick jump`.
    The history diff and both working-tree diffs fold their view options (wrap, context, whole file, ignore space, side by side, load all, merge parent in the history diff) and the Changes tab its two (path style, merge parent) into one group hint, placed right after `/ search` so it is the last of the pane's own hints a narrow line cuts, `<space>v view options`; pressing `<space>v` opens the which-key popup that lists them (see Chords), and the individual keys keep working and stay in help and the command palette.
    A group hint names no single action: its test checks the keymap binds something below the prefix.
    Dropped as noise: `n/N` after a search and `y copy line` in the working-tree diffs (the keys stay; the box and status bar already show the match count), `y copy path` and `<space>r refresh` in the working-tree lists (the lists refresh by themselves), `<space>r retry` in the detail pane (palette).
    The graph, sidebar, working-tree lists and message box were reviewed and kept: each shows only its own few actions.
    The file content view keeps `<space>vw wrap` since wrap is its only option.
- **Status messages.** Background work that fails (a tab's worktree, stash or status read, a failed session save, a reset session file, a failed graph load) reports to one channel; later features post to it the same way.
  - The newest message shows in the status bar between the position and the key hints until it expires, is dismissed with `<space>x` (or `Esc` in the graph when nothing else claims it), or is replaced by a newer one.
  - **Look per kind (#500).** Success or info notices use the good (green) colour, warnings the warning colour, errors the error colour, bold, on the bar's background.
    For the first 1.5 s a message is a filled chip (dark text on the kind's colour; reverse video without colour) and then settles to coloured text.
    Key notices and progress lines keep the plain bar style.
    Colours come from `PaletteTable` and the contrast test covers all variants.
  - **Expiry (#500).** Info disappears after 5 s, warnings after 15 s, errors after 30 s; the key hints take the room back.
    Expiry is pure state (`StatusLifetime` with the channel's injected clock); the UI schedules a redraw at each change.
    Expired messages stay in the `<space>m` history.
    Posting the same text again after it expired shows it again; the same text while it still shows is not posted again and does not renew the timer.
  - `<space>m` opens the history (last 50, newest first, long messages wrap) as an overlay that updates live.
  - Tab-scoped messages start with the repository name; a message equal to the one showing is not posted again. A key action's own one-off notice (`HEAD is not loaded yet`) takes the place of the message while it lasts.

## Workflows

### 0. Open a repository, switch tabs, reopen the session

1. **Start.** `gitunia` restores the last session (tabs, active tab, per-tab selection and focus); with no session it opens the picker. `gitunia <path>` opens that path as a tab (a repository already open just gets focused) and keeps the other restored tabs.
2. **Picker.** `Ctrl-t` or `<space>o` opens it (Insert mode, input focused). It lists open, pinned, recent and discovered repositories, fuzzy-filtered as you type, with a tag per row (`open`, `pinned`, `recent`, `found`).
   - `Up`/`Down` and `Ctrl-j`/`Ctrl-k` move; `Enter` or `Ctrl-l` opens in a new tab (or focuses the existing one); `Backspace` deletes a character; `Esc` cancels (with no tab open it quits, since there is nothing to go back to).
   - `Tab` or `Right` accepts the suggestion: the highlighted repository's path is filled in and its subfolders are listed; in a typed path it completes the folder name.
   - Typing and matching ignore case: every segment of a typed path is resolved against the real folder names (`~/projects/x` lists `~/Projects/x` on Linux) and the inserted text keeps the real case.
   - Discovered repositories come from a scan of the conventional source folders below the home directory (`Projects`, `src`, `code`, `dev`, `repos`, `git`, `work`, ... up to three levels deep, hidden and dependency folders skipped).
     The `scanRoots` config option replaces these folders (see "Config file").
   - A typed path is listed off the UI thread: the previous rows stay until the new listing arrives, `scanning...` appears after about 150 ms when there are none, a newer keystroke cancels the older listing, and a listing slower than 5 s ends with a "timed out" notice.
   - A click highlights a row, a double-click opens it.
   - **Clone and init** (#401).
     `Alt-c` (or the `Alt-c clone` chip at the right end of the hint line, shown from 94 columns) asks for a URL or a path in a prompt popup, then for the directory in a second popup (default: the folder typed in the picker, else the first scan root, plus the repository name; `Esc` in either popup closes only the popup).
     A local path is taken from the active repository or home, never from the process working directory.
     The target must be missing or empty (a file or a folder with content is refused by the clone with a status bar message, nothing is touched; if a stopped clone's folder cannot be removed the message names it).
     The clone runs off the UI thread, belongs to no tab, and shows `Cloning: Receiving objects 45%` in the status bar with the cancel chip; `Esc` (once no other holder of `Esc` claims it) or the chip stops it and git's half-made folder (and any parents it created) is removed.
     When it ends the repository opens in a new tab and the picker closes; on failure the status bar says why and the picker stays.
     A clone never prompts: a failure that needs credentials says to set up a credential helper or an ssh-agent key (prompting comes with #45).
     `Alt-i` (or the `Alt-i init` chip) asks for a directory (default: the typed path, else the active repository's folder or home), creates it when missing, runs `git init` (files already in it are kept, nothing is staged) and opens it; a folder that already is a repository root is just opened.
     One clone or init at a time.
     Both are unavailable while locating a missing repository.
3. **Typed paths.** Text starting with `/`, `~/`, `./`, `../` or a drive (`C:\` or `C:/`) is a path; anything else is a fuzzy query.
   For a path the list shows the matching directories (repositories marked), `Tab` completes to the common prefix and then to the highlighted directory, and `Enter` opens the resolved path.
   On Windows paths are shown native (`C:\Users\name\repos`, no `~` abbreviation, the hint names `C:\ .\ ..\`); typing `~` or `/` still works, and Tab after a bare `~` continues with `\`.
   Relative paths are taken from the active repository, or the home directory, never from the process working directory.
   A non-repository path shows an inline "not a git repository" message and the picker stays open.
4. **Pin and remove.** In the picker `Alt-p` (or the `Alt-p pin`/`Alt-p unpin` chip on the highlighted row) pins or unpins the entry; `Alt-d` removes it from recents (never deletes anything on disk). `<space>p` pins the repository of the active tab.
5. **Switch tabs** with `gt`/`gT` (or `3gt`), `Alt-1`..`Alt-9`, or `Ctrl-^` / `<space><Tab>` for the previously active tab. Mouse: click a tab; a click on a tab's `*` also opens its working-tree view. `Ctrl-w c` closes the tab; closing the last tab shows the picker.
6. **Tab bar.** One row on the bar background (the status bar's, so the bars bracket the screen), a margin, then every tab as a chip with two columns of padding per side, ` N name markers `, one blank column between tabs, and a ` + ` button after the last tab that opens the picker.
   - The active tab is the filled accent chip. The others are buttons: the same chip on a subtle tinted background so they read as clickable; underlined without background colours, bracketed (`[ 2 name ]`, same width) in ASCII. With `NO_COLOR` the active chip is reverse video.
   - The name is the repository name, or the name given with `Ctrl-w r` (a prompt popup that starts with the current name: `Enter` saves it with the tab in the session file, an empty name restores the repository name, `Esc` cancels).
     The branch is not part of the tab (the sidebar marks the current branch with `*`).
   - Markers: `!` an operation (merge, rebase, cherry-pick, revert) is in progress (workflow 4), `*` dirty working tree, `↑n` ahead, `↓n` behind (`+n`/`-n` without Unicode), an ellipsis while the tab loads.
     The active tab follows external git activity by polling (refs every 2 s, working tree every 5 s; `watchFiles` in the config turns on live file watching instead), background tabs update every 30 s and when shown; `<space>r` refreshes manually.
   - Remote chips (#456, #536): right of the tabs, left of ` : commands `, the fetch, pull and push chips show their key from the keymap before the label, in the status bar hint style (bold accent key, plain muted label; the chips lose their button look when keyed): `<space>F fetch  <space>L pull  <space>P push` (`[<space>F fetch]` in ASCII).
     Rows under about 127 columns drop the key text first (` fetch  pull  push `), rows under 100 columns drop the chips.
     While one runs a single `<space>C cancel` chip replaces them.
     The key text is generated from the keymap (a rebinding follows), never written by hand.
   - Two tabs with the same name show their parent directory. Names are cut at 40 characters. When the tabs do not fit, the row scrolls to keep the active one visible and `<` marks hidden tabs on the left.
7. **Missing repository.** A tab whose repository was moved or deleted stays in the bar as `! name (missing)`. `Enter` on it opens the picker (Locate repository, starting in the folder that held it) and the chosen repository replaces the tab; `Ctrl-w c` closes it.
8. **Unsent message.** Closing a tab that holds an unsent commit message (typed or edited, not committed) does not close it at once: the status bar names the tab, and a second `Ctrl-w c` right after (nothing else pressed in between) closes it and drops the message.
9. **Terminal key caveats** (Terminal.Gui 2.5.0 under tmux).
   `Ctrl-j` (0x0A) arrives as `Ctrl+Enter`, distinct from `Enter`, so the translator maps it back to `Ctrl-j` and it works as a picker alias for `Down` (a terminal that sends one byte for both would break the alias; `Down` still works).
   `Ctrl-^` (0x1E) arrives as `Ctrl+6` and is mapped back; `<space><Tab>` is the fallback for terminals that swallow it.
   Some terminals send `Backspace` as `Ctrl-h`, which the picker treats as `Backspace`.

### 1. Browse the graph, jump, search

1. **Open.** A repository tab opens with focus on the graph, the newest commit selected and HEAD marked. Commits stream in; the list is usable immediately and the status bar shows loading progress.
2. **Graph row.** A table: branch lanes, message (ref decorations then subject), author, date and time (`yyyy-MM-dd HH:mm`, the author's local time) and short hash (dimmed).
   - The message starts two columns after the last line of the row's own lanes, so with many branches open, rows that only use the first lanes keep their message close to the line. The message column is the flexible one and takes all width the other columns leave (no cap).
   - Fixed columns: author 16 (ellipsis when longer; 20 when 140 or more columns are left after the lanes), date 16 (10, date only, when the time does not fit), hash 7. The columns after the message sit one blank column apart; from 100 columns left after the lanes (for example 160x45) the gaps are three columns and the row keeps a two-column right margin (the message column gives up 7 columns for that, intended).
   - Narrow terminals drop columns instead of squeezing the message below 30 columns: hash first, then the time (the date stays), then the date, then author. They return when the terminal widens. `gitunia <repository> --print-frame WxH` shows any width.
   - Example at 120 columns (without the sidebar):
     ```
     ●  Fix parser crash on empty input                Ada Lovelace       2026-10-13 09:00   7a21f84
     ●  ⎇ main   Merge branch 'topic'              Alexander the G…   2026-10-12 23:30   ee3f455
     ```
3. **Ref labels.** The symbolic `origin/HEAD` is hidden.
   A local branch and its remote-tracking branch of the same name on the same commit are one label, `main ` (ASCII: `main (r)`).
   Every local branch gets a `⎇` before its name, checked out or not: it says the branch exists locally (#826); a remote branch without a local twin has none. The branch HEAD is on has a `◆` in front of it (`◆ ⎇ main`; ASCII: `HEAD -> main`, other local names stay unmarked, so ASCII shows "local" only by the missing `origin/`).
   A remote branch with no local branch of that name, or on another commit, keeps its own `origin/x` label.
   A local branch whose upstream is gone carries the same dimmed mark after its label (`topic ✗`, ASCII `topic (x)`; see the sidebar, #570).
4. **Lanes.** Branches are laid out by tip date (#757): the branch with the newest tip commit runs in the leftmost lane, the next newest to its right, and so on, so the graph stays as compact as possible; the checked-out branch gets no lane of its own (it keeps the `◆ ⎇` mark on its label).
   The log is read newest first and a lane is only ever assigned from the rows above it, so columns never move while scrolling or loading; the order holds among branches whose lanes are open at the same row; a lane freed by a branch that ended is reused from the left by the next tip.
   A detached HEAD is laid out the same way.
   **Own lane per branch (#759):** every branch keeps one lane, and one colour, from its tip down to the commit it forked from: where a branch forks off another, its lane runs straight down beside the other branch and joins it at the fork commit (`●─┴─┴─╯`), it does not bend in earlier.
   The leftmost lane, the one of the newest tip, goes on to the very bottom: where several lanes meet at a commit the leftmost keeps it and the others, `main` included, bend into it, so they look as if they went off from it.
   In the usual case main's tip is the newest, main is the leftmost lane and one straight line from the root up, with `release`, `develop` and `hotfix` on consecutive commits of its line as labels only (a name on a commit of another branch's line never moves the line).
   A merge opens a lane for the merged branch at the merge row and the branch keeps it down to its fork.
   When the log lists the tips in another order (a feature tip newer than main's, or every commit with one timestamp and git's own order) the same rule gives the same picture with another branch leftmost.
   With more lanes than fit (at most 40% of the pane), the lanes past the limit are drawn in the last column.
   **Colours (#761):** every branch gets one colour, a hash of its name, so the same name starts from the same colour in every repository and session; when another lane that is open at the same row, or another branch tip within the last 50 rows (#807), already has that colour it moves on to the next free one, so the colour of a branch can change when a colliding branch appears (never while scrolling or loading: within one graph a name keeps its colour); a local branch and its remote-tracking branch (`main`, `origin/main`) are one branch and share it, wherever their tips are.
   The lane line, the node and the branch labels of the commit use it; a label is a box in that colour (#760) with no parentheses around it (several labels on one commit are told apart by their boxes, a space between them): the local-branch glyph (`⎇ `; `◆ ⎇ ` on the checked-out branch, ASCII `HEAD -> `) and the remote glyph (cloud, ASCII ` (r)`) sit inside the box, a space between glyph and name (#804); a branch's label sits on its tip, in the colour of the branch's lane, which runs down to the fork commit where a curve in that colour joins the parent's lane, while the unlabelled commits of a run stay part of the plain line (tags are yellow everywhere); a lane that no branch name starts (a merged feature whose branch is gone) takes a colour derived from its first commit.
   Colours are the theme's `lanes` list (at most 12, 6 at 16-colour depth, one without colour; the layout uses only as many as the palette has, so open lanes differ while a colour is free: the lines are told apart by position alone and the labels are bold inverse video boxes, which on a focused selected row merge into the reversed row and are bold only).
   A commit with several branch names shows them all in the colour of its lane, which follows its checked-out branch, else the smallest local branch name, else the smallest remote one.
5. **Straight lines.** A commit with one child and one parent on its own lane is drawn as part of the line (`│`, ASCII `|`), not as a node.
   Node dots (`●`, merge `○`; ASCII `*`, `o`) appear only where a lane starts, ends, forks or merges, and on every commit that carries a ref (branch, tag, remote, HEAD), so labels never hang on a bare line.
6. **Move** with `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G`, counts and the mouse wheel.
   While the log is still streaming, `G` goes to the last loaded commit and stays there (it does not follow the stream); the status bar says `Still loading: at the last loaded commit` until the load finishes (a plain `G` only, not `5G`).
   A log longer than 100,000 commits loads its newest 100,000 first (status bar `12/100000+ of 1204331`); a plain `G` there goes to the last loaded commit and loads the rest, with the same hint (#515).
   A faint row `⋮ more commits below, G loads the rest` follows the last loaded commit while the log is cut and idle; the cursor can rest on it, it has no details and no commit (#555).
7. **Jump.** `<space>h` jumps to HEAD.
   `<space>f` opens a status bar prompt (Insert mode) that fuzzy-filters refs (branches, tags, remotes) and also accepts a hash prefix of at least 4 hex digits; the best candidate is shown, `Up`/`Down`/`Tab` cycle through the others, `Enter` selects that commit, `Esc` cancels.
8. **Details.** `Enter` on a commit moves focus to the detail pane; `Esc` or `h` returns.
   The pane follows the graph selection while the graph is focused.
   It sits below the graph (hidden on screens under 14 rows, where `Enter` only shows a notice) and has three tabs, shown as buttons on its title row:
   - **Details:** hash, author, committer (only when different), parents, refs and the full message.
   - **Issue links (#213).** The `[issuetracker "name"]` sections of the git config (repository, global or system, so what other clients read) and of a `.issuetracker` file in the repository (or worktree) root turn references in the message into links.
     The file is git-config syntax, read with `git config --file` (VS Code's proposal); a section of the same name in the file wins over the config. One `[issuetracker "name"]` section per tracker with `regex` (group 1 is the reference; without a group the whole match) and `url` (`$1` is the reference, percent-encoded where a URL needs it).
     Quote values that contain `#` or `;`, which start a comment in git config: `regex = "#(\\d+)"`.
     Only `http` and `https` addresses become links.
     Matches in the message show underlined in the accent colour; where two matches overlap the earlier one (then the earlier section) wins.
     `gx` (`Enter`, `l` or a double-click on such a line does the same) opens the first link on the line under the cursor in the default browser, `2gx` the second.
     Opening uses `xdg-open` (Linux), `open` (macOS) or the shell (Windows); when no opener starts, the address is copied through OSC 52 and the status line says so.
     A missing, empty or malformed file, or a bad regex, means no links and no error.
     The file is read when the tab loads and again on a manual refresh (`r`), so an edit shows after `r`; the browser is started in the background and the status line reports the outcome; no network access is involved.
   - **Changes:** the changed files with status `A` `M` `D` `T`, `R old -> new`, `C old -> new` (the similarity score is not shown); merges show changes against the first parent by default, and the heading names the parent (`(merge: against first parent <hash>)`).
     `<space>vp` (detail and diff panes) switches a merge commit to its next parent, wrapping from the last back to the first (`(merge: against parent 2 <hash>)`, status line `Switching to parent 2 of 2...`): the file list is read again against that parent (`diff-tree` with it) and the diff viewer shows the files of that list against it.
     The choice belongs to the selected commit and is not remembered: selecting another commit, or coming back to this one, starts at the first parent.
     Not a merge commit says so.
     Combined diffs (`--cc`) are not offered, and the difftool (`<space>td`) always uses the first parent (#95, #497).
   - **Files:** the file tree of the commit (`ls-tree -r -z --long`): directories first, then files, each ordinal by name; a directory whose only child is a directory folds into one row (`src/Gitunia.Core`).
     Every directory starts closed (the open ones are remembered per repository tab while the selection moves to other commits); a closed directory shows its file count, a file its size, and executables, symbolic links and submodules say so (`(executable, 1.2 KB)`, `(symlink, 12 B)`, `(submodule)`).
     `Enter`, `l` and a double-click open or close the directory under the cursor; `h` closes an open directory, else moves to the parent row, and at the top goes back to the graph.
     `/` filters by path (case-insensitive substring, typed in the status bar, Insert mode): the rows narrow as you type and every directory that holds a match is open (and cannot be closed) until the filter is cleared; `Enter` keeps the filter, `Esc` in the box clears it, and `Esc` in Normal mode clears an active filter before it goes back to the graph.
     The filter stays when the selection moves to another commit.
     The pane beside the tree shows the content of the file under the cursor at once and follows the cursor (see 1c, "File content"); `Enter`, `l` and a double-click on a file move focus into it.
     `y` copies the path of the row.
     The tree is listed off the UI thread when the tab is shown (not while another tab is selected), cancelled when the selection moves on, and the last four trees are kept; the title shows the number of files in the tree.
     Timing of large trees: `docs/architecture.md`.
   - A fresh tab opens on Changes.
     `]`/`[` switch to the next/previous tab (wrapping), `gd` `gc` `gf` jump to Details, Changes, Files from the graph or the detail pane; a click on a label does the same.
     The selected tab is part of the repository tab's view: kept while the graph selection moves, saved with the session, and each tab remembers its own cursor row for the selected commit.
   - **File paths (#540).** A file row of the Changes tab and of the working-tree Unstaged and Staged lists shows its path in one of three styles, a per-tab view option like `wrap` and `file` in the diff (not saved with the session, name first on a fresh tab):
     `name first` (default) draws the file name, then the directory dimmed (`parser.cs  src/Core/Git`),
     `full path` the path from the repository root as before (`src/Core/Git/parser.cs`),
     and `tree` (#548, and in the working-tree lists #617) folders as a tree like the Files tab: a folder row (`src/`) with its files indented below it, each with its change mark and its name only (a rename `R old.cs -> new.cs`, the old path in full when it sits in another folder);
     folders come before files.
     Folders open and close (#616): `za` or a click toggles the folder under the cursor (a double-click leaves the fold as it was: its first click toggles, the double-click undoes that and acts, #622), `h` closes an open folder, else moves the cursor to the parent folder and at the top level goes back to the graph (`Enter`/`l` keep going to the folder diff).
     A closed folder hides its rows and shows its file count (`src/  3 files`); its diff is still the whole folder.
     Closed folders are remembered per repository tab (by path, across commits and style changes, not saved with the session); every folder starts open.
     Switching from a flat style puts the cursor on the closed folder when its file is hidden.
     The cursor lands on the first file, as in the other styles.
     A folder row is selectable: the diff pane then shows all changes below that folder in one view (see the diff pane, 1), `Enter` on it focuses the diff and `y` copies the folder path.
     Commit details, comparisons and merge commits (against the parent chosen) all list this way.
     The working-tree Unstaged and Staged lists have the same tree (#617), see workflow 2, "Tree style in the lists".
     When the row is too narrow the directory is cut from the left (`…Core/Git`) and dropped when not even that fits, so the file name keeps its columns until nothing else is left; a status mark (`R`, a conflict text) keeps its place too.
     A rename shows its old name (or old path) dimmed after the directory when all of it fits, in the full style `old -> new`.
     `<space>vn` cycles name first, full path and tree for the tab; the `view ▾` dropdown (#646, `<space>vv`) picks one of them directly.
     The dropdown chip sits in the title row of the Changes tab (the detail pane's, right end, only while Changes or Files is selected) and of both working-tree lists (left of `stage all` / `unstage all`, which stay buttons: staging is an action, not a display option); the chip shows its key before the label when the row is wide enough and is left out when the title leaves no room.
     The status bar says which style is on.
     The quick jump labels what is drawn.
     Copy (`y`) still copies the whole path.
   - `j`/`k` scroll the rows of the tab.
     On Changes, focus lands on the first file row, that file's diff is already shown beside the list and `j`/`k` show the diff of each file as the cursor reaches it, no `Enter` (see 1c).
     `Enter` or `l` moves focus into the diff (on Details they only say to open Changes; on Files they focus the content beside the tree).
   - When the details fail to load the pane says so and `<space>r` in the detail pane (or the palette) retries. The title row shows the file count (`.. files` until the details have arrived). The rows of all tabs start at the same left edge; only the commit message is indented.
9. **Search.** `/` opens the search box (Insert mode).
   Plain text matches the commit subject, `author:` matches the author name, `hash:` the start of the hash and `body:` the whole commit message (subject and body).
   `body:` is answered by `log --grep` on demand, so bodies cost no memory and no graph load time; the status bar shows `searching messages` until the answer is in, and a failure shows `Message search failed: ...`.
   All are case-insensitive.
   Matches are highlighted while typing and the status bar shows the count (`/fix_  3/17`).
   `Enter` jumps to the first match at or after the selection and returns to Normal mode, `n`/`N` step through matches (counts work, the search wraps), `Esc` in the box cancels, `Esc` in Normal mode clears the highlight.
   In the box `Backspace`, `Ctrl-u` and `Ctrl-w` edit the text.
   The search scans the loaded commits in small slices between key presses and keeps scanning as the log streams in (on a log cut at the soft row limit, reaching the loaded end loads the rest, about 10 s and ~300 MB on a 1M-commit log, and the scan goes on); the status bar shows `searching N%` meanwhile.
10. **Copy and refresh.** `y` copies the selected commit hash (OSC 52); `<space>r` refreshes the repository now.
11. **Quick jump** (`<space>j`, flash/easymotion style).
    It opens a prompt in Insert mode (`jump _` in the status bar).
    Type text and every row in view that contains it (case-insensitive; in the graph the text of a row is its ref labels and its subject, so a branch or tag name jumps too) gets a one-letter label drawn over the character after the match, in a chip so it shows without colour.
    Pressing a label selects that row and closes the prompt; `Enter` selects the nearest match; `Esc` cancels.
    - Only the rows in view are looked at, so the cost does not depend on the log size.
      Labels go to the matches nearest the cursor first, home-row letters first.
      A letter that would continue a match in view is never a label, so typing more text and pressing a label never clash (any other key edits the text).
      With more matches than letters the farthest stay unlabelled: type more to narrow them.
    - A match whose label would fall past the right edge of its row takes no letter, so no label is ever hidden. A wide character under a label leaves a blank so the rest of the row does not shift.
    - It works in the graph, the diffs (history and working tree; it matches the row text, not line numbers or the `+`/`-` marker) and the Unstaged and Staged lists (file paths); the jump moves that list's cursor and the diff beside it follows.
      In the side-by-side diff both sides are labelled (a label sits within its half; an unchanged line is labelled on its left side).
      The detail pane has it too (rows in view, file paths on Changes); not yet the sidebar.
      The prompt closes when focus moves to another pane or tab.
      Mouse: a click selects any row, as always.

12. **Comparing two commits** (#535, #636). Mark one commit; from then on the commit under the cursor is compared with it, as `git diff <older> <newer>`. One mark is enough, there is no second key press.
    - **Keys.** `<space>=` in the graph marks the commit under the cursor: its row starts with `[mark]` (text, so it shows in every colour tier and the ASCII set).
      While a mark is set the status bar hints read `<space>= move mark  Esc end compare`.
      `<space>=` on a different commit moves the mark there (no compare toggle); on the marked commit it clears the mark.
      `Esc` in the graph clears the mark and the comparison (after the usual claims on `Esc`: a Visual range, a running fetch).
      The key is a `<space>` chord, free in the keymap table and not a bare vim key; a plain letter would clash with vim (vim-clash rule).
      A Visual range keeps its meaning (cherry-pick and revert of the range); the mark is separate and does not use it.
    - **Order.** The older commit is the one lower in the graph (the log is newest first), whichever of the two was marked first, so the diff always runs older to newer and reads as the change from one to the other.
      Two commits on different branches are compared the same way (a plain `git diff A B`, no merge base).
    - **What shows.** Moving the cursor to any other commit shows the comparison with the mark, without another key.
      The first comparison after marking selects the Changes tab (later moves keep the tab you chose).
      The Changes tab lists the files of `git diff <older> <newer>` (renames and copies detected as in the commit view), with the heading `N files changed (<older>..<newer>)`; the diff pane beside it shows the file under the cursor from the same command, so list and diff match what `git diff A B` prints.
      The Details tab names the comparison (`compare  abc1234..def5678`) in place of the parents and shows the commit under the cursor otherwise.
      With the cursor on the marked commit itself there is nothing to compare: its own changes show.
      Binary files and mode changes get the notes of the commit view.
      A merge commit as either end is compared as a tree, like any other (no parent choice; `<space>vp` does nothing then).
      Diff options (`+`/`-`, `<space>vf`, `<space>vi`, `<space>vs`, `<space>vl`) work as in the commit view.
      The comparison is not cached: it is read again on every request.
    - **Cost.** The comparison starts once the cursor's own commit details have loaded, which goes through the same debounce as the commit view: holding `j` or `k` starts no process per row (a commit whose details are cached waits for the same pause), and a load for a commit the cursor left is cancelled.
      Measured: `git diff --numstat -M` between two commits 300 apart (1359 files changed) takes 0.26 s, so no cap or "comparing" state is needed.
      Each resting position costs one `git diff <older> <newer>` (async, cancellable).
      While it runs the Changes tab shows the commit's own files, which the comparison then replaces.
    - **When it ends.** `Esc` or `<space>=` on the marked commit drops the mark and returns to the selected commit's own changes.
    - **State.** The mark is part of the tab's view state (`RepositoryViewState.CompareMark`), so every tab has its own, it survives switching tabs, and it is not saved with the session.
      A refresh keeps it and compares the (possibly moved) selection with it; when the marked commit is no longer in the graph nothing is compared, and `Esc` clears the mark or `<space>=` moves it to the cursor.
      The shown comparison lives in the details store of the tab and is gone with the selection.
    - **Mouse.** The actions menu of a graph row (`<space>.` or a double-click) lists "Mark or compare a commit", the same action.

### 1b. Sidebar navigation

1. `Ctrl-w h` (or `Tab` cycling) focuses the sidebar (sections: branches, remotes, tags, worktrees, stashes). `j`/`k` move, `l`/`Enter` expand a section or folder, `h` collapses it (or moves to the parent).
2. `Enter` on a branch, remote branch or tag selects its commit in the graph and keeps focus in the sidebar; `l` on a leaf moves focus to the graph.
   A stash is not a graph node, so `Enter` or `l` on it shows the stash's files and diff in the detail pane (workflow 7, item 4); `l` also moves focus to the detail pane.
3. `/` filters the entries by name (Insert mode: type, `Backspace` erases, `Enter` keeps the filter and returns to Normal mode, `Esc` drops it).
   The filter also matches the worktree shown beside a branch and unfolds every section with a match.
   In Normal mode `Esc` clears an active filter, and otherwise returns focus to the graph.
4. **Prefix folders (#643).** Local and remote branches are grouped by their `/`-separated prefix: `feature/ui/x` sits under `feature/` and `ui/` (nested to any depth), a branch without `/` stays at the top level, and a remote's branches fold under the remote's own folder first (`origin/`, then the prefix).
   A folder whose only content is one more folder folds into one row (`feature/ui/`, #647); its key is the deepest folder's path.
   A folder row shows `▸`/`▾` (ASCII `>`/`v`), its name with a trailing `/` and its entry count dimmed; the branch row below shows the short name, while checkout and every other branch action use the full name.
   `l`/`Enter` open a folder, `h` closes it (on a closed folder or a branch it moves to the enclosing folder or section); `Enter` and a click on a folder toggle it, and it has no actions menu.
   Folders are closed by default except the ones holding the current branch and a remote's own folder; what the user opened or closed is kept per tab (not saved with the session).
   The `/` filter matches the full branch name and shows only the matching branches with every folder above them open.
   The `*`, `wt:<directory>`, `↑n↓m` and `✗` markers stay on the branch row.
5. **Tight tree layout (#647).** Section headers start at column 0, their entries (branches, remote folders) sit one column in, and each further nesting level indents by one more column (the Files tab tree uses the same one-column indent and the same row renderer, #658, so its long names and folders are cut in the middle too and a closed folder keeps its file count), so a narrow sidebar gives its width to the names.
   A name that must be cut is cut in the middle (`worktree-agent-…5cab5`, `feature/6…-diff`) so its distinguishing tail stays; the right-hand markers keep their columns and the last one (`wt:<directory>`, a worktree's branch) is the first to give way, also cut in the middle.
   While the sidebar has focus the status bar shows the full name of the selected row after the position (`3/120 feature/ui/button`, the graph position then the name; cut in the middle to a third of the line at most so hints and messages stay; a worktree shows its path, a section or folder nothing), so a short or cut name is never ambiguous (#657).
6. A section header shows its entry count.
   Remotes and Tags are folded by default.
   A branch shows `wt:<directory>` for the worktree that has it checked out (`*` marks the current branch and the current worktree) and `↑n↓m` against its upstream.
   A local branch whose upstream was deleted on the remote (`%(upstream:track)` says `gone` after a fetch with `--prune`, which is what happens once a PR is merged and its branch removed) gets a dimmed `✗` after its name (ASCII `(x)`), so the branches a local prune could clean up stand out; on a narrow sidebar the name is never cut for the marker below 8 columns or half of the width left after the prefix.
   The help overlay of the graph and the sidebar ends with a `Markers` section that explains it (#570).
   The sidebar is hidden below 60 columns.

### 1c. Diff viewing

1. **Where it is.** The diff is part of the Changes tab, not a mode of its own.
   While Changes is selected, the diff pane is shown beside the file list.
   It shows one file at a time: the file under the file list's cursor, from the commit's diff against its first parent, or the parent a merge commit was switched to with `<space>vp` (or the comparison's two commits), so the diff always matches the file list.
   The pane never shows another file; the next one is chosen in the file list (#537).
   The one exception is the folder view (#548): in the tree style of the file list, a folder row shows the files below it, one after the other in the order of the commit diff, as a single document (title `folder, N files`, hunk counts for all of them).
   `]f` and `[f` then step from file to file inside it (and stop at the first and last), the options (`+`/`-`, `<space>vf`, ...) keep the folder, and moving the list's cursor to a file or another folder switches the pane.
   The whole commit diff is loaded once as before, so selecting a folder costs no extra read.
   - It is there as soon as the tab is, for the first file. Moving the file cursor with `j`/`k` (or a click on a row) shows that file's diff at once. Selecting another commit in the graph loads that commit's diff at its first file (a load in flight is cancelled when another commit is asked for).
   - `Enter` or `l` on the file list moves focus into the diff to scroll, search and copy; `h` or `Esc` comes back and the diff stays. On the Details tab the pane is not shown; on the Files tab it shows the content of the file under the tree's cursor (item 11).
   - The diff sits beside the compact file list (a third of the width, 24-60 columns) on screens of 72 columns or more, so both are visible together at 80 columns.
     Narrow or short screens (under 72 columns, or without the bottom strip) keep the on-demand behaviour: the diff opens with `Enter` or `l` on a file row, takes focus and replaces the detail pane (screens under 14 rows zoom it), and `Esc` or `h` closes it.
     An on-demand diff on a screen that is then widened stays open and also keeps showing a Details or Files tab beside it, following the graph selection.
   - `<space>z` zooms the focused graph, detail or diff pane to the full area between the tab row and the status bar and back (the sidebar does not zoom; moving focus to another pane ends the zoom).
     Where the Changes or Files tab sits beside the diff / file content (72 columns or more, bottom strip shown) the two zoom as one view, side by side, whichever has focus (#632): moving focus between them keeps the zoom, moving it to another pane ends it.
     The Details tab and narrow or short screens zoom the single pane.
     While zoomed, `Tab` only reaches panes that are shown.
     `Ctrl-w >` / `Ctrl-w <` in the paired zoom move the divider between the two panes (the same width as the normal split, remembered with it, #683); in a single-pane zoom (and the zoomed working-tree diff) no divider is shown, so every resize key leaves the sizes alone and the status bar says `Unzoom the pane to resize`; `Ctrl-w +` / `Ctrl-w -` do the same in the paired zoom, as the bottom strip is not shown; `Ctrl-w =` resets all sizes as usual.
2. **Navigate.** `j`/`k`, `Ctrl-d`/`Ctrl-u`, `gg`/`G` scroll (from the file list beside the diff, `J`/`K` scroll by a line and `D`/`U` by half a page without taking focus; `Ctrl-d`/`Ctrl-u` keep paging the list, #755); `]c`/`[c` jump to the next/previous hunk of the file (counts work; at the first or last one the pane stops and the status bar says `No next hunk` / `No previous hunk`, it never moves on to another file).
   `]f`/`[f` pick the next/previous file of the commit (counts work): the pane then shows that file only and the file list's cursor follows; at the first or last file nothing moves and the status bar says `No next file` / `No previous file`.
   The working-tree diff does the same within the list of the file it shows.
   The title shows the file under the cursor, `file n/m` among the commit's files and `hunk n/m` of that file.
3. **Search.** `/` opens a search box (Insert mode, case-insensitive substring over every row of the shown file including headers (other files of the commit are not searched), highlighted while typing); `Enter` jumps to the first match at or after the cursor, `n`/`N` step through matches and wrap, `Esc` in the box cancels.
4. **Buttons.** The right end of the title row has chips (`‹ file`, `file ›`, `‹ hunk`, `hunk ›` and `view ▾`, which is always the rightmost; a file's content has `wrap` and `zoom` instead); each is the mouse path of a key (Mouse table) and shows that key before its label (`[c ‹ hunk`, `<space>vv view ▾`) like the tab bar chips (#536).
   The key text comes from the keymap of the diff's view (the history diff's or the working-tree side's), so rebinding follows; a pane too narrow for the keys drops them first and shows the same buttons plain, then drops buttons as below.
   The file buttons are `[f` and `]f`; files can also be chosen in the file list.
   The working-tree diff also puts its staging chips on the first line of the current change group and they show their keys too (`<space>a stage  <space>d discard`; the keys go first when the row cannot keep 30 text columns with them, before any chip is dropped).
   Narrower panes drop buttons (see Mouse).
   Quick repeated clicks on a button each count (the toolkit reports the second and third click of a burst as double and triple click, which buttons treat as clicks).
   A drag selects a range of rows.
5. **Rows.** Hunks are told apart by a blank row between them and a separator row at the start of each: a short rule, the hunk header and a rule to the right edge (`── @@ -1,3 +1,3 @@ ─────`, plain dashes in the ASCII set), drawn in the quiet colour, in the unified and the side-by-side view alike (it spans both sides); a long header wraps with the rule closing the last line.
   Then `old new` line numbers, a `+`/`-`/space marker and the text.
   Text is cut with an ellipsis while word wrap is off (the default).
   `<space>vw` turns wrap on: a long row continues on the following screen lines under a blank gutter, `j`/`k` move by screen line, the title shows `[wrap]`, there is no horizontal scroll.
   Tabs expand to 4-column stops and wide characters are never split.
   `<space>vc` (or the `view ▾` dropdown; per tab, not saved; title `[ws]`) draws whitespace as quiet marks: a tab as `→` followed by spaces to the stop, each leading or inner space as `·`, each trailing space as the heavier `•`, a carriage return as `¶` (ASCII set: `>`, `.` for both kinds of space, `<`); these are the usual marks of editors, drawn in their own subtle colour, much dimmer than the muted text (about 2:1 to 3.5:1 against the background instead of 5:1 or more; #316, #736).
   A mark takes the column of what it replaces, so wrapping and cutting do not change, and search, quick jump and copy still see the real text (a search match is highlighted in the drawn text, so a space typed in the search does not light up the marks that stand for it).
   **Changed words** (#740, always on): inside a changed line the exact words that differ from the paired line are drawn on a tinted background (bold as well, underlined where there is no background colour: 16 colours and `NO_COLOR`), the rest of the line keeps the plain added or removed colour.
   A run of removed lines is paired line by line with the added lines right after it, the same pairing as the side-by-side view, in the unified and the side-by-side diff alike, and the working-tree diff; a line without a partner (a pure insertion or deletion, context, a "no newline" marker between the runs) is not marked.
   Lines are compared as words, runs of whitespace and single symbols (so indentation, a gained carriage return and a changed operator are marked); lines that share no word or symbol at all (rewritten, not edited) get no marks, the line colour already says it; a line over 2000 characters, a run of over 2000 changed lines and a changed part too big for a 200-by-200 word table are not compared (the part is marked whole), so a huge diff costs only its visible rows.
   Whitespace marks inside a changed word sit on the same tint.
   A search match and the selected row keep their own look over the tint.
   The conflict editor marks words the same way in every layout (#740, #764).
   Where the base is shown (three panes, or below the sides after `B`) and the hunk has one, the words of the ours (removed tint) and theirs (added tint) lines that differ from the base line of the same row are marked, and the base words either side changed are marked on the removed tint (the union of its differences against both); only on rows where ours and theirs differ.
   Without a shown base the ours and theirs lines are compared with each other.
   A side line without a base line in its row is not marked.
   Binary files, renames without edits, mode changes and "no newline" markers get a note row; parser warnings (such as invalid UTF-8 in the patch) show as a red `!` row above the first file.
6. **Options** (per tab, not saved with the session): `+`/`-` show 3 more/fewer unchanged lines around each change (title `[ctx N]`), `<space>vf` shows whole files (`[file]`), `<space>vi` ignores whitespace-only changes (`[-w]`).
   `+`/`-` start the repeat mode (see Modes), so more presses, also after choosing them in the `view ▾` dropdown, need no prefix; the title shows `[repeat]` until `Esc` or another key.
   Each reloads the commit with git's `-U<n>` / `-w` (the cache is dropped, the cursor returns to the same file).
   - In whole-file mode `]c`/`[c` still visit each group of changes, grouped as git's own hunks would be for the current context (changes closer than twice the context lines are one hunk), and the title's `hunk n/m` counts those groups.
     The grouping uses the context set with `+`/`-` (3 by default), not a `diff.context` from the user's config.
   - The options reset when the tab's repository is reloaded; a user's `diff.context` config applies until the first change. Files whose changes are whitespace-only stay in the file list but have no diff while `[-w]` is on.
7. **Side by side** (`<space>vs` or the `view ▾` dropdown; per tab, not saved; title `[split]`).
   The old side on the left with its line numbers, the new side on the right, a vertical rule between.
   Unchanged lines appear on both sides; a run of removed lines is paired line by line with the added lines after it (the shorter side stays blank); file and hunk headers and notes span both sides.
   - It needs a pane of 80 text columns; on a narrower pane the unified rows stay and the title says `[split 80+]` (zoom to get room).
   - The cursor is one line of the pair: the old side's row (the new side's for an added-only line). `y` copies that row, `Y` still copies the file's rows in diff order, and a Visual range copies from the first row of its first line to the last row of its last line, in diff order.
   - Wrap wraps each side inside its half and a line takes as many screen lines as its longer side. Search, `]c`, `]f` and the options work as in the unified view (a match on either side puts the cursor on its line).
8. **Back.** `Esc` or `h` first clears an active search, then returns focus to the file list (beside the file list the diff stays; an on-demand diff closes). If the tab is changed away from Changes while the diff has focus, focus returns to the detail pane.
9. **Limits.** A diff is capped at 400,000 rows and 100,000 lines per file (the last row, or the file's note row, says it was cut short).
   `<space>vl` loads the same commit again without the caps (the cursor stays on the same file; with nothing cut it only says so; binary files have no content to load and stay a note).
10. **Following the graph.** While the pane is shown, moving the graph (or a refresh that moves the selection) loads the newly selected commit once its details have arrived; the old diff stays until then.
    Beside the file list it shows the file under the file cursor (the first one for a new commit); an on-demand diff keeps showing the same file if the new commit touches it, else the first one.
    A resize that makes room for the diff loads it at once; one that takes the room away returns focus from the diff to the file list.
    The status bar shows the keys of the focused pane.
11. **File content** (Files tab, #511, #635).
    Like the diff of the Changes tab, the pane sits beside the tree from the start (screens of 72 columns or more with the bottom strip) and shows the blob of the selected commit read-only for the file under the tree's cursor, so no key is needed.
    With no file under the cursor (a folder row, a submodule, a tree still loading) it shows a placeholder (`Select a file to view its contents`).
    Moving the cursor (`j`/`k`, a click, jumps) changes the content; the read is off the UI thread, starts 40 ms after the cursor rests and is cancelled when it moves on, and the old content stays until the new one arrives.
    `J`/`K` scroll the content by a line and `D`/`U` by half a page from the tree (`Ctrl-d`/`Ctrl-u` page the tree).
    `Enter`, `l` or a double-click on a file moves focus into the pane; `Esc` or `h` returns to the tree and the content stays; `Tab`/`Ctrl-w` and the zoom work as for Changes.
    Where the diff is not shown beside the list (narrow or short screens) the content opens on demand with `Enter`, `l` or a double-click, takes focus and replaces the detail pane (zoomed under 14 rows), and `Esc` or `h` closes it.
    The title reads `File  <n> lines  <path>` (`File` alone on the placeholder); the rows are the lines, each with one line number column and no change marker (side by side does not apply).
    The 1 MiB blob limit is the only cap: a file of blank lines is about 1M rows and is shown in full (measured in `docs/performance.md`); a binary file shows a note.
    Scrolling, `/` search with `n`/`N`, quick jump, `<space>vw` wrap and the mouse work as in a diff; `y` copies the line, `Y` the whole file as plain text; the status bar hints and the title buttons name no hunk, file or diff option (only `zoom` and `wrap` remain as buttons).
    In the pane `]f`/`[f` move the tree's cursor to the next / previous file row (folders and submodules are skipped; `No next file` / `No previous file` at the ends; on demand they say the content has no other files to jump to), `]c`/`[c` say the content has no changes, and `+`/`-`/`<space>vf`/`<space>vi` say the content has no diff options.
    Neither `]f` nor `]c` is bound in the tree itself.
   - The blob is read with `cat-file blob` off the UI thread (title shows `Loading...` meanwhile).
     Over 1 MiB (judged from the size in the tree listing, so nothing is read) the pane says `File too large to show (<size>, the limit is 1.0 MB)`; a file with a NUL byte in its first 8000 bytes says `Binary file (<size>): its content is not shown`; an empty file says `Empty file`; a submodule has no content (status notice).
     CRLF line ends are shown without the CR; invalid UTF-8 shows as replacement characters.
     A symbolic link shows the target path it stores.
     Filters and `.gitattributes` text conversions are not applied: it is the stored blob.
   - The content belongs to the commit and the Files tab: selecting another commit in the graph shows the placeholder until that commit's tree has loaded, and switching to another tab leaves it (the pane follows the selection again where it is shown beside the file list).

### 2. Working-tree view: stage, commit, amend, discard

The working tree is a separate view of the tab, not a graph pane or pseudo-row.
It has its own views for the keymap (`WorkingUnstaged`, `WorkingStaged`, `WorkingUnstagedDiff`, `WorkingStagedDiff`, `WorkingMessage`); they inherit `Lists` and `Global`, and the diffs the `Diff` navigation rows.
Everything specified here is built.

**Layout.** Left column: the `Unstaged` list on top (conflicted files first in their own group, then modified, deleted, renamed and untracked files, each with its status letter and path) and the `Staged` list below it, each titled with its count.
Right: the diff of the file under the cursor and, below it, the message box with an option row `[ ] SignOff  [ ] No-Verify  [ ] Amend` and a `Commit` button (it shows its key, `Ctrl-g`, and drops it on a narrow row).
The sidebar is hidden (the view gets the whole width); the tab bar stays.
- Status letters: `M` `A` `D` `R` `C` `T`, `?` untracked, `!` conflicted (with the kind of conflict). A rename or copy shows as `old -> new` in Staged, an edit of the renamed file under its new path in Unstaged. A path that is not valid UTF-8 shows its stray bytes as replacement characters.
- Ignored files are in no list. List order follows git's, except that conflicted files come first.
- Terminals under 80 columns show one pane at a time (the focused list shows, `Tab` switches; the focused diff replaces the list, as in 1c).
  Under 7 rows of content the two lists do not stack and the focused one takes the left column.
  Under 12 rows the message box shows only while it has focus, in place of the diff area.
- Lists and diff follow the pane, selection, zoom and diff option rules of workflows 1 and 1c.

**Open and leave.** `<space>s` toggles the view for the active tab; so do a click on the tab's dirty marker and the palette entry `Open or leave the working tree`. The history status bar hints `<space>s working tree` right after `j/k move` in the graph, sidebar and detail panes.
- The view is per tab: a tab keeps its view mode, cursors, draft message and option checkboxes while another tab or the history is shown. The view mode is not saved with the session (a restart opens on the history); the draft is.
- Leave with `h`/`Esc` from a list (in the tree style `h` first closes a folder or climbs to its parent, see "Tree style in the lists"; back to the history with the same graph selection) or `<space>s` again.
  `Esc` in the diff and the message box steps back to the lists first (the 1c rule).
  `q` quits from every pane.
- Opening focuses the first non-empty list (Unstaged, else Staged). A clean tree shows "Nothing to commit, working tree clean"; the message box stays usable for Amend.

**Focus.** `Tab`/`Shift-Tab` cycle Unstaged, Staged, Diff, Message.
`Ctrl-w h/j/k/l` move by direction (lists left, diff top right, message below it); `Ctrl-w j`/`k` move between the stacked lists, `Ctrl-w l` from either goes to the diff.
`l`/`Enter` on a file or folder moves into its diff, `h`/`Esc` back.
`y` copies the file's or folder's path.
`<space>1`..`<space>4` jump straight to Unstaged, Staged, Diff (of the file under the cursor) and Message from any pane, including the message box in Normal mode; not in Insert mode, where space is text (`Esc` first).

**Tree style in the lists (#617).** `<space>vn` cycles name first, full path and tree for both lists, as on the Changes tab (same rows: folders before files, each level in ordinal order, a folder with its files indented below it; an untracked directory stays one file row, `dir/`).
Every row is a file or a folder, and everything that acts on rows goes through the row-to-file map of the state (`WorkingRows`), never through the row number as a file number.
- **Cursor.** `j`/`k`, `gg`/`G`, ranges and clicks move over folder and file rows; `za` or a click opens and closes the folder under the cursor (a double-click leaves the fold as it was and stages or unstages the folder, #622; each list keeps its own closed folders per tab, not saved with the session; a refresh keeps them); `h` closes an open folder, else moves to the parent folder, else leaves the view.
  A closed folder shows its file count (`src/  3 files`).
  The cursor stays on the same file or folder when the style or a fold changes (on the folder that hides it, or, going to a flat style, on the first file under a folder); after a stage or unstage it stays at the same row, as in the flat styles.
- **Folder diff.** A folder row selects the combined diff of every file under it, hidden ones in a closed folder included (the working-tree diff for Unstaged, the index diff for Staged, files in list order; the same row and per-file limits, and at most 200 files, one git call each: a longer folder ends with "Showing the first 200 of N files; M more files left out (<space>vl loads all of them)", #625).
  The title reads `Diff  unstaged  change n/m  src/`.
  Hunks and lines cannot be staged or discarded from it (the status bar says to pick a file); `Enter`/`l` focus it, `y` copies the folder path, `]f`/`[f` from it go to the next / previous file row.
- **Stage and unstage.** `<space>a` / `<space>u` (and a double-click) on a folder row act on every file under it in one call to the index (conflicted files skipped and counted; status "Staged src/ (3 files)"); no confirm, as for files.
  In a Visual range a folder row stands for all the files under it, each file counted once, also when its own row is in the range.
- **Discard.** `<space>d` on a folder row opens one Confirm, "Discard changes to N files in src/?", that counts every file under the folder (hidden ones too), names the untracked ones (deleted from disk) and says it cannot be undone, like a range of files; `n`/`Esc` do nothing.
  Conflicted files, submodules and unreadable names are left out and counted.
  Same for a range with folder rows.

**Side-specific keys.** Each list and each diff side is its own view for the keymap, so help and hints show only what applies: `<space>a`, `<space>A`, `<space>d` exist only on the Unstaged side, `<space>u`, `<space>U` only on the Staged side; the other key there is unbound, not a no-op.

**The diff.** The right box shows the diff of the file under the cursor of the focused list, read off the UI thread with the history diff's limits: the working-tree diff for Unstaged, the index diff for Staged (both paths of a rename), an untracked file as additions, a binary file as a note, and a conflicted file as the combined diff (one file; each line added, removed or context as in the result, with the result's line numbers; the per-parent columns are not shown).
It is the same widget as the history diff (hunk jumps, wrap, context, whole file, ignore whitespace, split, search, `y`/`Y`, load anyway).
The title reads `Diff  unstaged|staged  change n/m  <path>`.
Here a unit is a change group, not a hunk of the patch: every run of consecutive added/removed lines (separated by unchanged lines) is its own unit, even when the patch merged several into one hunk (changes less than twice the context apart).
The group under the cursor (the cursor on unchanged lines belongs to the group above) is highlighted: a bar in the left margin of each of its rows (`│`, `|` in the ASCII set) and, in the 24-bit schemes, a quiet tint behind them (`GroupBackground`, below the selection tint); the 16-colour and `NO_COLOR` tiers have the bar alone.
The mark is off while a Visual range is open, since the range is then what the keys act on.
The staging buttons in the title row, and the same chips on the group's first line, act on this group.
`]c`/`[c`, the title count and the stage, unstage and discard keys all use these groups, always (the history diff keeps the patch's own hunks: only the working tree stages, decided in #386).
- It follows the cursor and reloads after every status read, also when the file's status letters did not change (a second edit), keeping the cursor row. A path that is not valid UTF-8 shows its diff too, with replacement characters.
- Zoom (`<space>z` or the `zoom` entry of the `view ▾` dropdown) gives the diff the whole view (lists and message box hidden) until focus leaves the diff or zoom is used again.

**Stage and unstage.** All run off the UI thread, one operation per tab at a time (a key pressed meanwhile says "busy").
When one is done the tab re-reads its status and the cursor lands on the same index, so the keys repeat.
Failures show git's message in the status bar.
Paths are always passed literally.
1. **Files.** `<space>a` in Unstaged stages the file under the cursor (a deletion or rename as git reports it); `<space>u` in Staged unstages it (a rename unstages both paths).
   `<space>A` stages all (conflicted files skipped, the count reported), `<space>U` unstages all.
   Neither needs a confirm, since nothing is lost.
   A double-click on a row does the same as the key.
2. **Ranges of files.** `v` or a mouse drag in a list starts a Visual range (rows drawn stronger than the selected row); the motions move its far end, `<space>a`/`<space>u` act on every file of the range in one git call (conflicted files skipped and counted, as in stage all), and `y` copies the paths one per line.
   The range ends when the operation starts, or on a click, `Esc`, `q`, `v`, a change of focus, or a refresh that changes the list (the status bar then says "Selection ended: the rows changed").
   The title rows have `stage all` / `unstage all` buttons at the right end, showing their key (`<space>A stage all`, `<space>U unstage all`) when the row is wide enough, label-only when narrower and left out when there is no room; the `stage all` key goes before the `path` chip does (#578).
3. **Hunks and lines.** In the diff of an Unstaged file `<space>a` stages the change group under the cursor (the cursor on unchanged lines belongs to the group above); in the diff of a Staged file `<space>u` unstages it.
   The patch of a group is built like a Visual range patch, so the other groups of the same hunk stay as they were.
   `v` (or a drag) starts a Visual range in the diff and `<space>a`/`<space>u` then act on the added and removed lines inside it only (context, header and note rows are ignored; a range with no change is refused).
   The range survives a refresh that brings the same diff rows (a file watcher or fetch reload with no change, #806); if the rows really changed it ends, as it does on a wrap, side-by-side or width change, and the status bar says "Selection ended: the rows changed". The cursor stays where it was while the range survives.
   A double-click on a diff row acts on its change group.
   - The patch is never taken from the rows on screen: the file's diff is read again with git's default context (so the context, whole-file and whitespace options do not matter), the picked lines are found in it by line number and text, and the resulting patch is applied to the index.
     Git refuses a patch that does not apply and leaves the index alone.
     If a picked line is no longer in the file the operation stops with "changed since the diff was read" and the diff reloads.
   - Lines of an untracked text file are staged like any other lines (the file is recorded as intent-to-add first; the rest stays an unstaged addition). A mode change is never part of a partial patch.
   - Files that cannot be split (binary, staged-added, deleted, renamed without edits, untracked binary or symlink, a name that is not valid UTF-8) go whole when the key acts on the change group, and the status bar says so.
     Lines picked in a Visual range are never widened to the whole file: the status bar warns "cannot be split into lines" and nothing changes.
     A binary file has no hunks, so the key stages it whole.
   - The keys are refused while the diff is loading, for a conflicted file, and while the diff is cut short (`<space>vl` loads all of it first).
     Afterwards the status is read again and the cursor goes to the change group now at the same position (the next group when it was staged), or to the next file when the file is done.
     A renamed file with edits can be split: the rename header travels with the patch.
4. **Conflicted files** show `!`.
   `<space>a` (or a double-click) on one opens a Confirm, "Mark X as resolved?", saying that conflict markers still in the file are not removed or checked and that `git checkout --merge <file>` re-creates the conflict (overwriting the file's current content).
   `y` runs `git add` on it (status: "Marked X resolved").
   Stage all and a Visual range skip conflicted files and count them; a range of only conflicted files is refused.
   The banner with continue, abort and skip and the mergetool key are built (workflow 4); `Enter` (or `<space>to`) on a conflicted file opens the built-in conflict editor (workflow 4, item 2a); its mouse path is the `resolve` chip in the Unstaged title row, shown while the cursor is on a conflicted file (a double-click keeps asking "Mark X as resolved?").
5. Every action is also a palette entry. The lists refresh from the tab's status after each operation and by polling; `<space>r` forces a refresh.

**Discard.** Nothing discarded can be undone: git keeps no copy, so there is no reflog hint and no dangling stash; the Confirm dialog is the safety.
`<space>d` on a file in Unstaged, or on a change group or Visual lines in the Unstaged diff, opens a Confirm that names what is lost; `y` runs it.
There is no key for discarding staged changes (unstage first) and no "discard all".
- **Dialog texts:** "Discard all unstaged changes to X?" (plus "The deleted file is restored from the index." for a file deleted on disk and "Its staged changes are kept." when the file also has staged changes), "Discard this change (N lines) of X?", "Discard the N selected lines of X?", and for an untracked file "Delete the untracked file X?" (an untracked directory: "...and everything in it").
- **What happens:** a tracked file is put back as the index has it (staged changes stay).
  An untracked file is deleted from disk, but never a tracked or an ignored path.
  Change groups and lines are cut from the file's unstaged diff, read again with git's default context (the same patches staging uses), and applied backwards to the working tree only; the index is never touched.
  Git refuses a patch that does not apply and changes nothing; a line no longer in the file stops with "changed since the diff was read".
- **Whole file behind a hunk key:** a file with no hunks to pick from (binary, deleted on disk, untracked) is discarded whole when the key acts on the hunk, and the dialog says so.
  A file that shows hunks but cannot be cut (text that is not valid UTF-8, a renamed file with a quoted path) is refused with "cannot be split into hunks or lines ... nothing was discarded; discard the whole file from the list".
  Lines picked in a Visual range are never widened to the whole file.
- **Ranges of files:** one Confirm, "Discard changes to N files?", names the untracked files, which are deleted (the first five, then a count), and says it cannot be undone.
  The range ends when `y` is answered; `n`/`Esc` keep it.
  Conflicted files, submodules and untracked names that are not valid UTF-8 are left out and counted in the dialog.
  Files go in batches (one restore for the tracked files, then one clean for the untracked, in chunks of 64 paths); if one fails the status bar says so and that the tracked files may already be restored.
- **Refused before the dialog:** a conflicted file, a submodule, and an untracked name that is not valid UTF-8 on an OS without a byte-exact delete (Windows, macOS; Linux deletes it by its exact bytes behind the same dialog).
- Same busy rule as staging. The discard buttons and mouse path are in the Mouse section.

**Message and commit.**
1. **Writing.** `<space>c` (from a list or the diff) focuses the message box and enters Insert mode.
   The built-in editor: the first line is the subject, a blank line then the body; the title counts the subject width as `12/72` and the part past column 72 is an error-coloured hint, never enforced.
   `Esc` leaves Insert mode but stays in the box (#748): the box has its own Normal mode, the status bar says NORMAL and the cursor stays visible.
   Normal-mode keys, a vim subset (counts are ignored here):
   `h` `j` `k` `l` move the cursor inside the text (`h`/`l` stop at the line ends, `j`/`k` keep the display column), `w`/`b` go to the next or previous word, `0`/`$` to the line start or end;
   `i` and `Enter` insert at the cursor, `a` after it, `I` at the line start, `A` at the line end, `o` opens a line below;
   `x` deletes the character under the cursor, `dd` the line, `u` undoes the last change (an Insert session counts as one change; the history is kept while the tab lives, not in the session file).
   Leaving the box (Normal mode; in Insert mode these are editing keys) is `Ctrl-h`/`Ctrl-j`/`Ctrl-k`/`Ctrl-l`, `Ctrl-w` plus a direction, `Tab`, `<space>1`..`<space>3`, or `Esc` in Normal mode (back to the lists); bare `h`/`j`/`k`/`l` never leave it.
   The status bar names the pane-move keys on every pane (`Ctrl-h/j/k/l pane`; second in the message box, last elsewhere so a narrow bar cuts it first).
   A click in the box places the cursor at the pointer before it starts Insert mode.
   - Editor keys (Insert mode;
     not rebindable rows in the Keymap table): typed text and pastes insert at the cursor (line endings become new lines);
     `Enter` is a new line;
     `Backspace`/`Delete` join lines at their ends;
     `Left`/`Right` wrap across lines;
     `Up`/`Down` keep the display column (a wide character takes two, a shorter line clamps the cursor and the wanted column returns on the next longer line);
     `Tab` inserts spaces up to the next multiple of four (no tab character;
     `Shift-Tab` does nothing);
     `Home`/`End` and `Ctrl-a`/`Ctrl-e` go to the line edges;
     `Ctrl-u` deletes to line start, `Ctrl-k` to line end, `Ctrl-w` the previous word.
     The cursor never splits a wide character, emoji sequence or combining mark.
   - Lines are not wrapped on screen: the cursor's line scrolls sideways, and the box scrolls vertically with the cursor line.
2. **Draft.** The message, SignOff and No-Verify (not Amend) are kept per tab until a commit succeeds, and saved in the session file (debounced after each edit and on exit) so they survive restarts.
   Quitting never loses a message: `q` quits at once; only when the final save fails while a message is unsent does the first `q` warn and the next one leave.
   Locating a missing tab moves its draft to the replacing tab (when that tab holds a message of its own, both stay and a notice says so).
   Closing a tab with an unsent message needs a second `Ctrl-w c` (workflow 0).
3. **Options.** `Alt-s` (SignOff, `-s`), `Alt-n` (No-Verify, `--no-verify`) and `Alt-a` (Amend) toggle from anywhere in the view including Insert mode, or by clicking the checkboxes. All start off; hooks run unless No-Verify is on.
4. **Commit.** `Ctrl-g` in Insert mode, `<space>c` in the box's Normal mode, or the `Commit` button.
   There is no `Alt-Enter` (Windows Terminal turns it into fullscreen); the other options fall back to the checkboxes and the palette.
   (Verified in tmux on Linux with Terminal.Gui 2.5.0: `Alt-s`/`-n`/`-a` arrive as Alt plus the letter.
   Windows and macOS terminals are not verified.)
   - The message goes to `git commit -F -` on stdin, so comment lines (`#`) are kept, unlike a commit through `$EDITOR`.
     Hooks, signing and the author come from the user's git config.
     The commit runs off the UI thread with a 10 minute timeout so hooks can run; the box is read-only meanwhile, and `Esc` cancels it (git is killed, the draft stays; the notice says to check the history, because git may have finished first).
   - `<space>te` (also in the lists and the diff) commits through `$EDITOR` instead: the TUI suspends and git's own `commit` runs on the staged index. The draft is kept; git uses its own template.
   - **Nothing staged.** With nothing staged but unstaged or untracked changes present (Amend off, no merge, cherry-pick or revert in progress), the commit action opens a Confirm, `Nothing is staged. Stage all changes and commit?`.
     `y` stages everything as `<space>A` does (untracked included, conflicted skipped), waits for the status re-read and commits; `n`/`Esc` leaves the draft and the index untouched.
     The message and options are fixed when `y` is pressed.
5. **Refusals,** without running git, with the reason in the status bar and focus kept in the box: an empty message, and nothing staged with Amend off and nothing to stage ("Nothing staged").
   An amend with nothing staged is allowed and rewrites only the message.
   While a merge, cherry-pick or revert is in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`) the nothing-staged check is skipped, because finishing such an operation with an empty tree is valid; the operations themselves are built (workflows 3 and 4).
   Amend is refused when there is no commit yet and during a merge.
6. **Amend.** Toggling it on fills the box with HEAD's message when the draft is empty (the draft is kept aside and restored when Amend is toggled off), shows `Amending <short hash> <subject>` in the box title and runs `git commit --amend`; staged changes are added to the amended commit.
   If HEAD is already on the upstream branch, the status bar warns that a force push will be needed.
   Undo path: the status message after the commit names the old hash ("Amended to abc1234; previous commit 9f8e7d6, `git reset --soft 9f8e7d6` restores it") and the reflog keeps it reachable.
7. **Finishing a merge.** The box starts with git's `MERGE_MSG` (comment lines dropped); untouched, it does not count as an unsent message.
8. **After a commit.** The draft and Amend are cleared, SignOff and No-Verify keep their state, the lists refresh, the status bar says `Committed abc1234` and focus returns to the lists. The history view picks up the commit through the normal refresh.
9. **Failure.** A failing hook (non-zero exit), a prompt, or a signing program that needs a passphrase fails the commit, keeps the draft and the staged state, and shows git's and the hooks' output (stderr, then stdout such as `nothing to commit`) in a scrollable overlay (title `Commit failed`, `Esc` closes it; the status bar carries the first line).
   Under the output a hint says that no keyboard input reaches git and its hooks (stdin is closed) and that `<space>te` runs `git commit` in the real terminal where it can prompt.
   Files a hook rewrites show as new unstaged changes after the refresh.
   A commit that fails on a tab that is not active leaves the status bar notice and keeps its output; the overlay opens once that tab is shown.

**Key labels and ligatures (#753).** Ligature fonts draw `<space>=` as `>=`.
Key labels in help, the palette, context menus, which-key and the status bar therefore get a thin space (U+2009, one cell wide) between a `>` and a following `=`, `-` or `>`; the config file and the Settings key reference (whose text is also filtered and copied into config snippets) keep the plain text.
Fonts that still join other pairs need ligatures turned off in the terminal font.

**Key rule.** Standard vim/neovim keys keep their vim meaning (`j` `k` `h` `l` `gg` `G` `/` `n` `N` `v` `y` `i` `Ctrl-d` `Ctrl-u` ...).
A Gitunia-specific action never takes a bare vim command (`s` substitute, `u` undo, `c` change, `a` append, `x` delete, `r` replace, `D` `M` `E` motions and so on) but a leader chord (`<space>` then a key) or a `Ctrl-`/`Alt-` combination.
Check this before adding a binding.
In an Insert-mode picker or prompt printable keys are text (they filter or are typed), so pickers move with `Up`/`Down`/`Tab` only, the same in all of them (the delete-branch list has no field and also takes `j`/`k`/`h`/`l`).
- **Leader keys in use:** `<space>o` `p` `x` `m` `s` `z` `<Tab>` (global); `<space>a` `A` `u` `U` `d` `c` `r` (working tree and refresh; `<space>c` is a single key in each of the lists, the diff and the box, each its own view); `<space>t` + `d` `m` `e` `o` (external tools; `o` opens the conflict editor); `<space>,` (settings, #523); `<space>h` (jump to HEAD) and `<space>f` (find a ref) in the graph; `<space>v` + `w` `i` `f` `s` `l` (diff view options) and `p` (merge parent); `<space>j` (quick jump); `<space>1`..`<space>4` (working-tree pane jumps).
- **`Alt-`:** `Alt-p` and `Alt-d` are Picker-only, `Alt-1`..`Alt-9` jump tabs, `Alt-s` `Alt-n` `Alt-a` act only in the working-tree view.
- **Documented exceptions:** a few bare keys keep a non-vim meaning on purpose, because the panes are read-only or the key is navigation: `q` (quit; vim records a macro), `h` `l` `]` `[` (navigation), `?` (help; vim searches backwards, same as `less` and lazygit), `Y` (copy the file's rows; close to vim's yank line), `+` `-` (diff context), `J` `K` `D` `U` in the Changes, Files and working-tree lists (scroll the diff or content beside them by a line or half a page, #755) and `gd` `gc` `gf` (bottom-pane tabs, paired with the `]`/`[` cycling).
  The conflict editor (#689) binds bare `n` `N` `o` `t` `b` `u` for the same reason: it is a modal like the rebase todo editor and shadows the search keys there.
  The settings screen (#523) is a modal list, not a buffer, so its view keys `x` `a` `d` `J` `K` `+` `-` are bare on purpose (owner to confirm, asked in the PR of #523).

**Mouse.** Same action path as keys.
A click on a file row selects it and a double-click stages or unstages it (a deliberate deviation from "double-click opens": the diff already follows the selection).
The diff's title row gets `stage`/`unstage` and `discard` buttons next to the hunk buttons; the list titles get `stage all`/`unstage all`; the `SignOff`, `No-Verify` and `Amend` checkboxes toggle and `Commit` commits; a drag over diff rows or file rows starts a Visual range; a click on the tab's dirty marker opens the view; a click on the diff box focuses it (on screens of 80 columns or more; narrow screens use the `diff ›` chip of the list's title row, #376).
The discard itself is reached from the palette, not a list button.

### 3. Branches, tags, merge, cherry-pick, reset and the reflog (M3)

Planned keys, written here and not yet in the keymap table: each implementing issue adds its table rows with its bindings (`KeymapDocumentationTests` fails on a documented binding that is not registered).
`<space>t` is the tools prefix (`td` `tm` `te`), so these use other prefixes: `<space>b` branches, `<space>g` operations on the selected commit, `<space>T` tags, `<space>w` worktrees, `<space>S` stashes.
Every action is also a palette entry and has a mouse path (a button in the sidebar section header, or a click on the row and then the same key).

**Common rules.**
- **Target.** The graph acts on the selected commit, the sidebar on the selected row. The status bar names the target while a prompt is open.
- **Prompts.** A name is typed in a prompt popup (see Prompt popups in the Modes section; Insert mode: `Enter` runs, `Esc` cancels), prefilled where a default exists. A choice of a branch or ref uses the fuzzy picker of `<space>f`.
- **Confirm.** Every destructive step (delete, force, hard reset, drop, remove, discard) opens the Confirm dialog of the Modes section.
  It names the exact target, says what is lost and shows the undo hint (reflog hash, `git stash store <hash>`, the branch's old tip); `y` runs it, `n` or `Esc` drops it.
- **Refusals** run no git and say why in the status bar: a branch checked out in another worktree, a dirty tree where git would refuse, a detached HEAD where a branch is needed.
- **No editor, no prompts.** Every call that could open an editor passes `--no-edit` or `-c core.editor=true`; stdin is closed, so a hook that prompts fails with git's output shown in an overlay like a failed commit. Messages are edited in the commit box or with `<space>te`.
- **Result.** Success posts a status message with the new state; a failure shows git's message.
  An operation that stops on conflicts is not a failure: it becomes an in-progress operation (workflow 4).
  All lists refresh from the tab's status afterwards and the cursor follows the new HEAD or the created ref.

**Branches** (`<space>b` + key; implemented in #35 and #36):
1. `bc` create: prompt for the name, start point the selected commit (graph) or branch (sidebar); the new branch is checked out. `bC` creates without checking out. A name in use is refused before git runs; an invalid name is refused by `git check-ref-format` and shown as an error.
2. `bs` switch (checkout), context based (#571): the branch under the cursor is checked out at once, no prompt.
   A sidebar local branch row checks that branch out; a remote branch row creates the tracking branch and checks it out (a remote branch that has a local twin checks the twin out).
   In the graph, a commit with exactly one branch label (a local branch, or a remote branch without a local twin) checks that branch out; with several labels a filter picker (candidates listed, `Up`/`Down`/`Tab` choose, `Enter` runs the highlighted one, remote branches shown as `origin/foo -> local foo`) lists only those plus a detached checkout of the commit; with none, or on any other row (tag, stash), the prompt lists all local branches, remote branches that have no local twin and, from a graph commit, a detached checkout there.
   In the sidebar `Enter` still only selects.
   The current branch and one held by another worktree are refused with no prompt (the message names the worktree path).
   A dirty tree that conflicts with the target fails with git's message; nothing is stashed behind the user's back.
3. `br` rename the selected branch (prompt prefilled with its name).
   `bd` delete (on a folder row or a graph commit with several local branches a list opens first: `j`/`k`/`h`/`l` or the arrows move, `Enter` picks, `Esc` cancels; #727): Confirm naming the branch and its tip with the undo command; when the branch has commits that neither HEAD nor its upstream reach, a second, explicit Confirm says its commits become unreachable and prints `git branch <name> <hash>`.
   The current branch and one held by another worktree are refused.
   On a section header or a Remotes or Tags folder row they refuse with "Select a local branch first" and never act on the graph branch (#729); create from them still starts at the graph commit.
   From the graph, `br` and `bd` act on the one local branch at the selected commit (several: `br` asks to pick one in the sidebar, `bd` opens the list).
4. `bu` set the upstream of the selected local branch (type-to-filter picker of remote branches, nothing is fetched), `bU` unset it (the status line prints the `git branch --set-upstream-to=` command that restores it).
   Ahead/behind (`↑n↓m`) already shows in the sidebar and the tab bar; it is not new work.
   `bf` (#572) fast-forwards the selected local branch (sidebar row, or the local branch at the graph commit) to its upstream as last fetched, without checking it out and without a Confirm (nothing is lost): fetch first with `<space>F`.
   Only a real fast-forward runs; the status line names the old and new tip and the undo (`git update-ref refs/heads/<b> <old>`).
   Refused with nothing changed: up to date, diverged (it has commits the upstream lacks), no upstream or a gone one, the branch checked out here (points to `<space>L` pull) or in another worktree.
   Fast-forwarding to the commit under the graph cursor instead is not part of this.
5. Mouse: the actions menu of a branch row (`<space>.`, or the `...` marker of the selected sidebar row) lists create, switch, rename, delete, fast-forward and the upstream actions, and the Branches header menu offers create and create-only; there are no header chips (dropped in #469).
   After create, switch, rename or delete the graph cursor moves to the created or checked-out branch's commit (after a delete, to the commit of the current branch).
6. No single-letter sidebar shortcuts (`d`, `r`): they are vim operator and replace keys (vim-clash rule), so the sidebar uses the same `<space>b` chords on the selected row. Every action is in the command palette.

**Integrating** (`<space>g` + key, on the selected commit or branch; the target is the current branch):

The which-key popup of `<space>g` lists the everyday keys only: `m` `M` `p` `r` `i` `x` `v`.
`gs` (squash), `gf` `gw` `gd` (quick rebase) and `gI` (rebase from the root) stay bound and are in the actions menu, the palette and `?` help, but are left out of the popup.
`ga` `gc` `gk` are listed only while an operation is in progress (the banner shows them too).

1. `gm` merge the selection into the current branch.
   Fast-forward when possible; `gM` forces a merge commit (`--no-ff`), `gs` squashes (`--squash`, leaves the changes staged for the commit box; no operation is in progress afterwards).
   The message is git's default (`--no-edit`).
   It runs at once, no Confirm (nothing is lost); the status line names the old tip and the undo (`git reset --hard <old>`, also `ORIG_HEAD`).
   A stop on conflicts becomes the in-progress merge (banner, `<space>gc`, `<space>ga`); a conflicting squash leaves the files marked `!` but no operation and says to resolve and commit.
   A branch row merges that branch, a tag row or a graph commit that commit.
   Refused during any in-progress operation and on a detached or unborn HEAD.
2. `gr` rebase the current branch onto the selection (implemented in #39; non-interactive with `-c core.editor=true`; the interactive variant is `gi` below).
   The Confirm names both sides and the undo (`git reset --keep <old tip>`, also `ORIG_HEAD` and the reflog) and warns when the branch has an upstream (history rewrite, force push needed; the text is stronger when every commit is already on it).
   Refused with no git run when an operation is in progress ("finish or abort the <operation> first"), HEAD is detached or unborn, the target is the current commit, or tracked files have uncommitted changes (untracked files are fine; not when the user's `rebase.autoStash` is on: then the Confirm adds a line saying git stashes and restores them, and the undo is `git reset --keep`, which never drops local changes).
   A rebase that stops on conflicts is the in-progress rebase of workflow 4 (banner, `<space>gc`, `<space>ga`, `<space>gk`); the status line warns and names the keys.
   When the commits being replayed (the target does not have them) contain merge commits, the Confirm says so ("N merge commits in that range will be flattened"): a plain rebase drops the merge and replays its side's commits as one line; the Confirm adds that `<space>gi` keeps the merges (interactive rebase with `--rebase-merges`, #461).
   The count is one `git rev-list --merges` call, so the Confirm opens when it answers.
   Mouse: the actions menu of a graph row (`<space>.` or a double-click) or sidebar row (`<space>.` or the `...` marker).
   While the Confirm is being prepared a second `<space>gr` says "Already asking", and the Confirm is dropped with a warning if an overlay was opened or HEAD moved meanwhile.
3. `gp` cherry-pick the selection onto the current branch (implemented in #40): the commit under the graph cursor, the commits of a Visual range in the graph (applied oldest first, whatever lanes they sit on) or the tip of the selected sidebar branch or tag.
   `gv` reverts it the same way (`--no-edit`); a range is reverted newest first, git's own order.
   Both run at once, no Confirm (nothing is lost); the status line names the old tip and the undo (`git reset --hard <old>`, also in the reflog).
   A commit whose changes HEAD already has is dropped (`--empty=drop`), and when that is every commit the status says "Nothing to cherry-pick".
   A stop on conflicts becomes the in-progress cherry-pick or revert (banner, `<space>gc` continue, `<space>ga` abort, `<space>gk` skip the commit).
   A detached HEAD is allowed (git allows it); refused during any in-progress operation and on an unborn HEAD.
   The Visual range ends when the action starts.
   Mouse: the actions menu of a row; a Visual range is picked with `<space>gp`/`<space>gv`.
3a.
**Quick rebase** (#459; `gf` fixup, `gw` reword, `gd` drop, graph only, on the commit under the cursor; the interactive engine of #399 with a todo built by the app, no editor).
`gf` melds the commit into the one before it (that commit keeps its message and the fixed-up commit disappears), `gw` opens a prompt popup prefilled with the subject (the body of the message is kept; an empty or unchanged subject stays in the prompt with a reason), `gd` removes the commit.
A Confirm follows each: it names the commit (short hash and subject), how many later commits are replayed and get new hashes, the force-push warning for a branch with an upstream (as `gr`) and the undo (`git reset --keep <old tip>`, also `ORIG_HEAD` and the reflog).
Same refusals as `gr` (operation in progress, detached or unborn HEAD, tracked changes) plus: the commit is a merge commit, it is not in the history of the current branch, it is the only commit of the branch (drop), or (fixup) it is the first commit or the one before it is a merge.
These show as the status error with git's or the engine's reason.
A stop on conflicts is the in-progress rebase of workflow 4. The Confirm is dropped, with a warning, when HEAD moved, an overlay opened or the tab changed while the plan was read.
Mouse: the actions menu of a graph row lists the three.
The todo-list editor (#458) is the full interactive route.
4. A merge commit among the commits opens a one-key prompt, once for the whole range (the overlay below; the status bar carries it only when the screen is too small for the overlay): `1`, `2` pick the mainline parent (parent hashes and subjects shown), `Esc` cancels; a small overlay lists the same parents as rows, a click on a row answers like its digit and a click outside cancels.
   The answer is passed as `-m` to every commit of the call.
   Git accepts `-m 1` on a plain commit but fails the whole call on `-m 2`, so with a plain commit in the range only `1` is accepted and the `[2]` row of the overlay is dimmed and marked unavailable.
   The overlay is placed again when the terminal is resized.
5. A dirty tree is refused for rebase; for the others git decides and its message is shown.

**Interactive rebase** (`<space>gi`, graph and sidebar; engine #399, editor #458): rebases the current branch onto the selection with a todo edited in a modal list, no external editor.
The refusals are those of `gr` (operation in progress, detached or unborn HEAD, target is the current commit, tracked changes), no commits to rebase says so.
A range with merge commits is rebased with `--rebase-merges` (#461): the list then also shows git's `label`, `reset` and `merge` lines (dim, in git's order), which keep the merges and the branches they join.
They cannot be changed: `p`, `s`, `f`, `d` and `r` ignore them, but they move like any row (`K`/`J`), so the commits of a merged branch can be reordered or reworded and the merge is recreated on top.
The footer refuses (and `Enter` is refused) an order git cannot replay: a merge or reset before the label it names, or a squash or fixup right after a reset.
Quick rebase (`gf` `gw` `gd`) of a commit below or inside a merged branch keeps the merges the same way; a conflict stops as in workflow 4 and `<space>gc` continues, `<space>ga` aborts to the original merged history.
Cousin branches (not based on the target) keep their original base, as git's default does.
The commits are read off the UI thread; the editor opens when they arrive.
`<space>gI` (#561) opens the same editor for the whole history (`--root`): the list starts at the root commit, the selection is ignored, the title and the Confirm say "onto the root", and the refusals, Start, Confirm and undo are the same.
The graph actions menu of the root commit (no parents) lists it as "Interactive rebase of the whole history" after "Interactive rebase"; other rows do not.
Any parentless commit shows it (orphan branches, shallow boundaries), but the action always rewrites the current branch from its own root and ignores the selection.
- **The list** shows the commits in replay order, the first row replayed first (git's own todo order, the reverse of the graph), each as `action hash subject`; dropped rows are dim.
  Every row starts as `pick`.
  `j`/`k`/`gg`/`G`/`Ctrl-d`/`Ctrl-u` move; `p` pick, `s` squash, `f` fixup, `d` drop set the action of the highlighted commit; `K`/`J` (or `Alt-k`/`Alt-j`, with a count) move it up or down.
  Squash and fixup meld into the row above, so the first kept commit cannot be one: the footer says so and `Enter` is refused until it is fixed.
  The single letters are git's todo words and keys of this modal list, like the settings screen's, not buffer commands (vim-clash rule).
- **Reword** (`r`) opens the message editor in the same box with the commit's full message (read from git, not only the subject), the multi-line editor of the commit box (workflow 2): `Enter` new line, `Ctrl-g` keeps the message and makes the commit a `reword` (the row then shows the new subject), `Esc` cancels the edit.
  A blank message is refused.
  Choosing `p`, `s`, `f` or `d` afterwards keeps the text, so `r` brings it back.
- **Start** (`Enter`): a Confirm names the branch, the target, how many commits are kept, reworded, squashed, fixed up and dropped, lists the dropped commits (hash and subject, five at most), warns about a branch with an upstream (history rewrite, force push needed) and shows the undo (`git reset --keep <old tip>`, also `ORIG_HEAD` and the reflog).
  The Confirm opens over the editor, so `n` keeps the todo.
  On `y` the editor closes and the rebase runs in the background under the tab's operation gate; the state is checked again first (tab, HEAD and refusals), because it can change while the todo is open.
  A stop on conflicts is the in-progress rebase of workflow 4 (banner, `<space>gc`, `<space>ga`, `<space>gk`).
  If git refuses before anything ran (the range changed, `GIT_SEQUENCE_EDITOR` is set), the status says why and the todo comes back.
- **Closing.** `Esc`, `h` or `q` closes; with an edited todo the first press says "Esc again discards the changes to the todo" and the next closes. Nothing was changed in the repository until `y`.
- **Mouse.** The wheel scrolls, a click highlights a commit, and chips under the list (`p pick  r reword  s squash  f fixup  d drop  K move up  J move down  Enter start  Esc cancel`, drawn as key hints like the conflict editor's, flowing onto more rows on a narrow terminal so none is cut off) run the same actions.
  The actions menu of a graph or sidebar row lists "Interactive rebase" next to "Rebase".
  When the box does not fit (under about 9 rows, fewer with a wide terminal) it is not drawn and the keys still answer, blind.

**Reset** (`<space>gx`, not a single letter, so it cannot clash with vim motions or `g` chords): the current branch moves to the selected commit.
A small dialog offers `s` soft (keeps index and files), `m` mixed (keeps files, clears the index), `h` hard.
Soft runs at once and shows the old tip in the status ("Reset to abc1234, was def5678").
Mixed runs at once when the index equals HEAD; otherwise it opens a Confirm saying the staged state (including partially staged hunks) is dropped and cannot be recovered from the reflog.
Hard opens a Confirm that lists the tracked files and the count of changes that will be lost (untracked files stay unless the target commit tracks a file at the same path, and it says so) and the old tip for the undo (the reflog restores commits, never uncommitted changes, and the Confirm says so).
Refused during an in-progress operation and on a detached or unborn HEAD.
The status after a reset names the old tip and the undo (`reset [--hard] <old hash>`, also `ORIG_HEAD` and the reflog).
Implemented in #392 (`ResetActions.Begin(tab, commit)` is the entry the reflog view calls).
The reflog view offers the same key on an entry.
Mouse: the actions menu of a graph or sidebar row.

**Tags** (`<space>T` + key; implemented in #397): `Tc` create at the selected commit, or the commit of the selected sidebar row: a name prompt, then a message prompt (empty makes a lightweight tag, text an annotated one, so no editor is needed; the call also passes `-c core.editor=true`).
A name in use is refused before git runs, an invalid one by `git check-ref-format`.
`Td` delete the local tag: Confirm with the tag's target hash and the undo command (`git tag <name> <hash>`; for an annotated tag the hash is the tag object, so the message survives); a copy on a remote is not touched.
`Tp` push the selected tag to a remote: a type-to-filter remote prompt, then one ref (`git push --no-progress <remote> refs/tags/<name>`), no progress, no prompts, git's first error line shown; the push of workflow 5 generalises it.
`TD` deletes the tag on a remote (`git push <remote> --delete refs/tags/<name>`): remote prompt, then a Confirm naming the remote and the tag and saying the local tag stays; no undo besides re-pushing the tag.
From the graph the tag actions act on the one tag at the selected commit (several: pick one in the sidebar).
Mouse: the actions menu of a sidebar or graph row (#428, #471) lists all tag and merge actions; they act on the selected row.
No sidebar `d` shortcut: it is a vim operator (vim-clash rule), so the sidebar uses the same `<space>T` chords on the selected row.

**Reflog** (`<space>l`, graph and sidebar; implemented in #398): a modal list of the reflog of `HEAD`, or, from a local branch row under the sidebar cursor, of that branch (`refs/heads/<name>`).
Newest first (the last 1000 entries): short hash, `HEAD@{n}`, action and message, relative time.
`j`/`k`/`gg`/`G`/`Ctrl-d`/`Ctrl-u` move; `Enter` selects the entry's commit in the graph and keeps the list open (a commit outside the loaded graph says so in the status bar); `<space>gx` resets the current branch to the entry (the reset rules above, via `ResetActions.Begin`); `<space>bc` opens the new-branch prompt at the entry; `<space>bs` checks the entry out with a detached HEAD (git refuses a conflicting dirty tree, nothing is stashed).
The last three close the list first, because their prompt popup and Confirm are overlays of their own.
`Esc` or `h` returns.
The list is read from git on open and on `<space>r`.
`<space>l` inside the list opens the ref picker (implemented in #424): `HEAD` and the local branches, type to filter, `Tab`/`Down`/`Up` move, `Enter` opens that reflog, `Esc` returns to the list.
`/` searches the entries (hash, `HEAD@{n}`, action and message, case-insensitive): the box and the match count (`/fix_  2/5 matches`) are in the list's footer, `Enter` keeps the search and leaves the box, `n`/`N` step through the matches (counts work, wraps), `Esc` in the box cancels and `Esc` in the list clears the search before it closes the list.
Mouse: below the entries four action rows (`Enter` select, `<space>gx` reset, `<space>bc` branch, `<space>bs` checkout) run on the highlighted entry when clicked, so a click on an entry then a click on an action replaces the keys; in the picker a click on a row opens that reflog.
The graph `<space>l` still opens HEAD's reflog directly; the picker is one key away.

### 4. In-progress operations: banner, continue, abort, skip (M3; banner, continue, abort and skip done, the blocking of item 5 comes with each blocked feature)

One flow serves merge, rebase, cherry-pick and revert, and the interactive rebase (#399, editor #458) reuses it. `RepositoryOperationDetector` names the operation from the git directory.

1. **Banner.** While an operation is in progress the tab shows a banner on the frame's bottom edge (the line under the panes, in the status bar's colours;
   the title is a warning-coloured chip starting at column 0 like the `NORMAL` badge): `MERGING feature/x  3 conflicts  <space>gc continue  <space>ga abort  Unstaged: Enter, <space>to open conflict editor` (while conflicts remain the line ends with the keys that open the conflict editor, said to work in the Unstaged list (item 2a, #742), and left out when the chips would not leave room for it (an 80-column terminal);
   a rebase adds `step 2/5` and `<space>gk skip`;
   a cherry-pick or revert of a range shows how many commits are left, `3 commits left`).
   The title names the merged branch (read from git's default merge message, so a custom message gives just `MERGING`), the rebased branch, or the short hash of the picked or reverted commit.
   Each key group is also a clickable chip (mouse views `BannerButton.*`).
   `<space>gc`, `<space>ga` and `<space>gk` work in the graph, the sidebar and the working-tree view (where conflicts are resolved).
   On a terminal under 10 rows the banner is dropped and an empty status message shows the same text.
   It is read from the repository's state files (`MERGE_MSG`, `rebase-merge/`, `sequencer/`), so it survives a restart of the app, and the tab bar shows a `!` marker.
2. **Conflicts.** The working-tree view lists conflicted files with `!`. `<space>a` marks one resolved (workflow 2, item 4); `<space>tm` (graph) opens the configured mergetool; `Enter` on a conflicted file opens the conflict editor (next item).
2a.
**Conflict editor** (#689; `Enter`, `l` or `<space>to` on a `!` row of the Unstaged list).
A modal view of one conflict hunk at a time in one continuous box-drawing frame (the title set into the top border;
rules join the side borders, with T-junctions where the divider between the two sides meets them;
the key hints and the footer sit in a bottom section above the closing border; #733): the base (common ancestor), ours and theirs as three panes side by side on top, in that order (as in KDiff3, #739;
a hunk without a base says how to get one;
the labels git wrote after the markers, `HEAD` and the branch), the result below it (a rule separates the panes from the result) with up to two unconflicted lines around the hunk, line numbers in a gutter on the left of every pane (the lines of the result file, counted from the hunk;
ours and theirs are numbered from where the hunk starts, the base pane (on the left) has a blank gutter;
a line that differs from the other side in the same row is marked `-` (ours) or `+` (theirs) after its number and coloured like a removed or added diff line), a strip of every hunk with its pick (`1 ours  [2 -]  3 both`, the cursor in brackets, unresolved ones in the error colour), clickable key hints for every key (drawn like the status bar hints, the key in bold and then what it does) and a footer (`2 resolved  1 unresolved`).
On a screen under 114 columns (a frame under 108) the old two-pane layout is used: ours and theirs side by side, the base below them only after `B`; under 96 columns the two sides are stacked above the result; under about 17 rows or 22 columns nothing is drawn and the keys still work.
Keys: `n`/`N` (or `j`/`k`, the wheel) next and previous conflict (wraps), `]c`/`[c` next and previous difference between ours and theirs inside the hunk (lines compared row by row, so a line inserted on one side makes the rows after it differ) and `Ctrl-d`/`Ctrl-u` scroll (a hunk longer than the panes: ours, theirs and the base always scroll together and the headers say `lines 3-8 of 40`;
moving to another conflict scrolls back to the top), `o` take ours, `t` take theirs, `b` take both (ours first), `O`/`T` take ours or theirs for every hunk still unresolved (picks already made stay;
one undo step), `r` reset the hunk to unresolved, `e` edit the result as text, `u` undo and `Ctrl-r` redo (every pick, reset and edit of the session is one step), `B` show or hide the base below the sides (only in the two-pane and stacked layouts;
with three panes the base is always shown, `B` does nothing and its chip is not drawn), `R` re-create the conflict, `Enter` save, `Esc`/`h` close (a second `Esc` when picks would be lost).
A hunk without a pick keeps its conflict markers in the result, so a partial save leaves the file conflicted exactly where nothing was decided.
   - **Free editing (#690).** `e` opens the multi-line text editor of the commit message (same keys; `Tab` inserts a tab character) on the hunk's result in Insert mode: the pick's lines, or both sides for an unresolved hunk.
     `Esc` or `Ctrl-g` keeps the text (the strip then shows `edited`); text left unchanged changes nothing.
     The result gets a final line break when it lacks one, and CRLF line breaks in a CRLF file.
     A hunk emptied of all text resolves it to nothing.
     Picking a side later replaces the typed text, and `u` brings it back.
   - **Undo and redo.** One stack per editor session (the latest 200 changes) covers picks, resets and edits; `u` restores the state before the change and moves the cursor to its hunk, `Ctrl-r` redoes; a new change clears redo.
     Moving between hunks and the base toggle are not steps.
     Closing the editor drops the stack.
   - **Base.** With three panes the base of the hunk under the cursor is always the left pane.
     In the narrower layouts `B` adds a `base  common ancestor` block between the sides and the result (dropped when the screen leaves no room for it).
     The base comes from the file's `|||||||` section (`merge.conflictStyle` `diff3` or `zdiff3`); for the default `merge` style it is found by merging stages 1 to 3 in memory and matching each hunk by its two sides (the file on disk is not touched).
     A hunk edited by hand, or one that merge does not reproduce, shows "no base for this hunk" and names the setting; `R` then gives the base once `diff3` is on.
   - **Re-create.** `R` asks "Re-create the conflict of X?" (the file goes back to its conflict markers, also after a save or when it was marked resolved; what the editor holds is discarded; no undo for the file's current content) and on `y` runs `git checkout --merge -- <file>`, then reads the file again into a fresh editor (the base toggle is kept).
     Git's own refusal (no recorded conflict) is shown in the status bar and the editor stays as it was.
   - **Save.** With every hunk picked, `Enter` writes the file to the working tree and then asks the usual "Mark X as resolved?" Confirm (item `<space>a` above, workflow 2, item 4).
     With hunks left it asks first ("Save X with 2 unresolved hunks?"; the unresolved ones keep their markers, the undo is `git checkout --merge -- <file>`), writes, and asks nothing about resolving.
     Nothing picked: refused.
     The file is read again right before the write; if it changed on disk since the editor opened, nothing is written and the picks stay.
     The git index is only touched by the Confirm's `git add`.
   - **What opens.** Text files that both sides changed (`UU`), including a merge, rebase, cherry-pick and revert stop, with `merge`, `diff3` and `zdiff3` markers (CRLF kept).
     Add/add, delete/modify, binary, non-UTF-8 and over-1-MiB files, a symlink or submodule, a file whose markers are unbalanced or gone, and a name that is not valid UTF-8 are refused with the reason and a pointer to the mergetool (`<space>tm`).
     `Enter` and `l` on a `!` row both go to the editor (or to that refusal); the file's combined diff is still shown beside the list while the row is selected, and the pane keys move focus into it.
3. **Continue** (`<space>gc`, or the banner chip): refused while conflicted files remain ("2 files still conflicted"); otherwise runs `<op> --continue` with no editor (`-c core.editor=true`, so git's prepared message is used).
   A rebase may stop again at the next commit: the status line warns and the banner updates.
   A continue that fails without a new conflict (a dirty tree, a hook) shows git's reason and leaves the operation as it was.
4. **Abort** (`<space>ga`): Confirm that says the work of the operation is undone and resolutions are lost, with the old tip. **Skip** (`<space>gk`; rebase, cherry-pick and revert only): Confirm naming the commit that is dropped.
5. **Other actions are blocked** (each blocked feature adds its own refusal: #38 merge (done), #39 rebase, #40 cherry-pick and revert (done), #392 reset, #419 branch switch (done)).
   While an operation is in progress branch switch, merge, rebase (interactive too), reset and new cherry-picks are refused with "finish or abort the <operation> first".
   Staging, committing (as the continue step) and viewing keep working.
6. **Completion.** A continue that finishes the operation posts "Merged feature/x into main (abc1234)" or the equivalent and removes the banner.

### 5. Fetch, pull and push (M4, keys planned here)

`<space>F` fetch the current branch's remote with `--prune` (all remotes: `remote.fetch-all` from the palette or the Remotes header menu; no second key, #456), `<space>L` pull of the current branch (fast-forward only by default; a diverged branch says so instead of creating a merge commit), `<space>P` push of the current branch.
Chips in the tab bar (`<space>F fetch`, `<space>L pull`, `<space>P push`, rows of 100 columns or more, the key text from about 127; a cancel chip replaces them while one runs) and the actions menu of the sidebar's Remotes header (the sidebar has no header chips since #469) carry the same actions; the chips are hidden while the repository picker is open (#744).
A pull also takes the tab's operation gate (it moves the working tree); only one remote operation runs per tab, other tabs are unaffected, and the tab is read again afterwards.
Each runs in the background with a progress line in the status bar (git's `--progress` output), `Esc` in the graph (or the cancel chip) cancels while it runs, only when nothing else claims it first (a Confirm is not needed; the status bar says "Fetch cancelled"), and the tab stays usable.
`<space>x` stays the status message dismiss.
`<space>RD` deletes a remote branch (`push --delete`, remote picker (skipped when only one remote has branches) then branch picker of the cached remote-tracking branches, Confirm naming both; the undo hint is `git push <remote> <old tip>:refs/heads/<branch>`; the local branch stays; with the cursor on a remote branch row, or on a graph commit that has exactly one remote branch, `<space>RD` skips the pickers and goes straight to the Confirm; `<space>bd` does the same when the graph commit has no local branch).
`<space>P` on a branch without an upstream prompts for the remote (type-to-filter, `origin` first) and runs `push --set-upstream <remote> <branch>`.
`<space>Rf` force pushes the current branch, only ever as `--force-with-lease=<branch>:<our remote-tracking tip>`, behind a Confirm that names the remote branch it overwrites and its current tip; if the remote moved since the last fetch git rejects it ("stale info") and nothing is overwritten.
A branch without an upstream cannot be force pushed (push it first).
A plain `<space>P` that git rejects as a non-fast-forward (a rewritten or diverged branch, #746) opens the same Confirm (which also says that commits only on the remote branch are gone afterwards) by itself when the branch is ahead of its upstream, right after the failure shows in the status bar (not for a "fetch first" rejection, where the lease would be stale too; not while another overlay is open).
The chord is not `<space>Pd` because the key engine always waits for a longer chord (no timeout), so `<space>P` and `<space>Pd` cannot both exist; the new chords sit in the `<space>R` remote group, and all three actions are also in the Remotes header menu.
The capitals `F` `L` `P` `S` `T` are not "all" variants of the lowercase keys; they are chosen because the lowercase leader keys are taken, and the palette shows the full sequences.
When the graph or the sidebar has no suitable row selected for a stash or tag key, a picker opens.
The runner and progress are #44, the keys, chips and mouse #456 (the first push, force push and remote branch delete are #457; the tag push already uses the runner; background calls never prompt, ssh runs in batch mode); the real-terminal credential retry is #45.

**Credentials and ssh prompts** (#45, #512).
The background run never prompts (`GIT_TERMINAL_PROMPT=0`, ssh in `BatchMode`).
The remote branch delete (`<space>RD`), the tag push and the remote tag delete offer the same Confirm on a credentials failure (`Runs: git push --delete --force-with-lease=refs/heads/topic:<tip> -- origin refs/heads/topic`, `git push -- origin refs/tags/v1`); there the terminal run reports `remote branch delete finished` or `tag push finished`.
When a fetch, pull or push fails because it needs a passphrase, a username and password, or a host key confirmation, the error stays in the status bar and a Confirm "Credentials needed" opens (only on the active tab and when no other overlay is open, so it never replaces what you type;
otherwise the status names the command to run by hand): it names the repository and the command (`Runs: git fetch --prune`), `y` ("Retry in terminal") suspends the TUI exactly like `<space>te` and runs the same command with the terminal handed to git (ssh, askpass or the credential helper ask as they do in a shell), then the TUI returns, the status bar reports `fetch finished` or the exit code (with a pause for Enter on failure so git's message stays readable) and the tab is read again;
`n` or `Esc` leaves it at the failure.
A pull retries as `git pull --no-rebase --ff-only`.
A failure that is not about credentials (missing repository, rejected push, network) and a cancel offer nothing.
If the Confirm cannot show (tiny terminal) the status names the command to run by hand.
Not available on an operating system without external-tool support (the status says so).

**Managing remotes** (`<space>R` + key; implemented in #400, config only, nothing is fetched): `Ra` add (name prompt, then URL prompt; a name in use is refused before git runs; the status line suggests a fetch).
`Rd` remove: a type-to-filter remote picker, or no picker when a remote branch row is selected in the sidebar (its remote is used), then a Confirm naming the remote, its URL and the remote-tracking branches we know of that disappear (the first five, then a count), saying that branches tracking it lose their upstream and the remote itself is untouched; the undo line is `git remote add <name> <url>` and a fetch.
`Rn` rename: picker, then a name prompt; git renames the remote-tracking branches and upstream settings, so no Confirm.
`Ru` set URL: picker, then a URL prompt filled with the current URL.
Refused while a fetch, pull or push runs on the tab.
The keys work from every pane; the Remotes header menu lists the four actions; the sidebar and graph labels follow after the refresh that every success triggers.

### 6. Worktrees (M3)

`<space>w` + key.
The sidebar's Worktrees section shows each worktree with branch, dirty counts, ahead/behind and `locked`/`prunable` flags, so dirty state across all worktrees is visible at a glance without switching.
The counts read `S2 M3 U1` (staged, changed incl. untracked, conflicted; a zero is left out and a clean worktree shows none) and `↑n↓m` is the worktree branch against its upstream, both from `git status` run in that worktree.
The tab's own worktree follows its live status; the others are read when the tab or its worktree list changes and then every 30 s, so a change made elsewhere shows up within that interval.
1. `wa` add: prompt for a branch name, then for the path (default a sibling directory named after the branch).
   An existing branch is checked out (one held by another worktree is refused); a new name (also one that exists only as a remote branch: no tracking branch is made) creates the branch at the selected sidebar row's or graph commit, else HEAD.
   The tab stays where it is.
2. `ws` switch (on a worktree row): the tab's active worktree changes in place and keeps its view state (selected commit, scroll, pane layout, filters, sidebar); HEAD, status and graph are read again for the new worktree, and the tab bar title and the sidebar `*` follow.
   `<space>wt` opens it as its own tab instead (or focuses the tab that already shows it).
   On a local branch row `<space>wt` does the same for the worktree that has the branch checked out; a branch without one asks for the path only (the default sibling directory), adds the worktree and opens it in its own tab.
   Refused without running git: no worktree row selected, the bare entry, a prunable one (its directory is gone), the worktree the tab is on, a worktree another tab shows (the message points to `wt`), and while an operation of the tab is still running.
   Switching never changes the process working directory.
3. `wd` remove: Confirm naming the path; a dirty worktree needs a second Confirm that lists what is lost and says there is no undo (`--force`); a locked one is refused until unlocked; the main worktree and the one the tab is on are refused.
4. `wl` lock (prompt for an optional reason), `wu` unlock, `wp` prune: lists the prunable entries in a Confirm and runs `git worktree prune` on `y`.
5. Mouse: the actions menu of a worktree row lists switch, open, add, remove, lock, unlock and prune; the Worktrees header menu offers add and prune.

### 7. Stashes (M3)

Keys are `<space>S` chords, not bare sidebar keys (`a` `p` `d` are vim keys, see the key rule). `Ss` `Su` work from every view; `Sa` `Sp` `Sd` act on the stash row under the sidebar cursor and say "Select a stash in the sidebar first" elsewhere (no picker: a destructive key never guesses a target).
1. `Ss` push the tracked changes, `Su` push them plus the untracked files (ignored ones never): a prompt popup for an optional message (`Enter` runs, `Esc` cancels). "Nothing to stash" is a warning and points at `Su`.
2. `Sa` apply, `Sp` pop: no Confirm (nothing is lost; git refuses when local changes would be overwritten and the status bar shows why).
   A conflict made by the call keeps the stash, starts no operation, marks the files `!` and says so in the status bar; if files were already conflicted (a stopped merge) git refuses and its message is shown.
   A pop that succeeds drops the entry and the message carries the restore command.
3. `Sd` drop: Confirm with the stash name and subject; the hint is `git stash store -m "<subject>" <hash>`, and the same command with the full hash is in the status message after the drop.
   The target travels as its commit hash and is looked up again right before the call, so a stack that changed meanwhile does not redirect it ("no longer in the stash list", nothing happens).
   The restore command is written for a POSIX shell.
4. `Enter` (or `l`, which also moves focus to the detail pane; a click on the row) on a stash row shows what is inside it in the detail pane, read-only (#699).
   The pane is the commit pane: the Changes tab lists the stash's files (tracked changes with their status letters, then the untracked files saved by `Su` marked `?`) under a heading that names the stash (`Stash stash@{0}: 2 files changed, 1 untracked`), the title shows its name instead of a hash, the Details tab shows the stash commit's message and people with a `stash` row, and the Files tab says a stash has no tree.
   The file under the cursor opens in the diff pane exactly as for a commit (tracked changes as `git stash show -p`, untracked files as additions; context, whole-file, whitespace and split options apply).
   There is no stage or unstage inside a stash (git has none).
   The stash is known by its commit hash: while it is shown, moving the sidebar cursor to another stash row shows that one;
   selecting a commit in the graph (the user's own move;
   a graph reload, restore or tab switch that moves the selection keeps the view), or `Enter` on a branch or tag row, ends the view;
   while a stash is shown the commit actions that read the graph selection (`<space>bc` and the other branch, merge, rebase, reset, pick, compare, tag and external-tool actions) refuse with a notice instead of acting on the graph row (the worktree add starts at HEAD), so select a commit first;
   the graph row menu still opens (#716);
   a stash that leaves the stack (dropped, popped, in another client) ends the view with a warning in the status bar instead of showing another stash, and a stash that only moves down the stack keeps showing under its new name.
5. Mouse: the actions menu of a stash row lists apply, pop, drop, push and push-all (`drop` keeps its Confirm); the Stashes header menu offers push and push-all.

## Look and colours

Colours are decided in one place: `PaletteTable` (`Gitunia.Tui.Views`) holds the colours of a scheme and `Palette` says which role uses which colour, so a theme swaps the table and nothing else.
The config file's `theme` setting picks the scheme: `auto` (Catppuccin Mocha or Latte by the terminal's background) or the name of a theme file (#61, "Theme files" below).
`dark` and `light` are still accepted in the file as the built-in dark and light tables (#747), which are close to but not identical with the `catppuccin-mocha` and `catppuccin-latte` theme files (see "Theme files").
The settings screen no longer offers them and shows them as those two themes; keeping that option rewrites the value to the theme file name.

**Frame.** Every pane is a box and neighbouring boxes share their edges, so the whole screen is one frame split into panes (`FrameGrid`, drawn the same way by the UI and by `--print-frame`):

```
 1 gitunia                                                   <- tab row
╭─ gitunia ────────────┬─ History ──────────────────────────────╮
│  ▾ Branches 2        │ ○─╮  main Merge feature x      Ada ... │
│     feature/x        │ ●  │  Fix parser                       │
├─ Details ─ Changes ─ Files ─ 64d72f2 ─ 3 files ┬─ Diff ... ──┤
│ commit  64d72f2...                             │ @@ -1 +1 @@  │
╰────────────────────────────────────────────────┴─────────────╯
 NORMAL  1/4  j/k gg G Ctrl-d Ctrl-u ? : q                          <- status bar
```

- Rounded outer corners and plain T and cross junctions; the ASCII tier uses `+`, `-` and `|`. Where two boxes meet there is one line, not two.
- Titles sit in the top edge of each box as chips after one stretch of line: the sidebar shows the repository name, the graph `History`, the bottom pane its tabs, the commit and the file count, the diff its title (the file shown and the cursor's hunk position, no commit hash: the detail title already shows it) and buttons.
  The title row of the bottom pane is the line between the graph and the bottom panes (so it costs no extra row).
- From 100 columns of screen width the History list and the bottom detail and diff panes are padded by two columns (`PanePadding.SpaciousColumns`; the graph on its right side only), so text and rows clear the frame lines; the sidebar and screens under 100 columns keep one.
  The graph rows have no left padding (#752): the first lane starts in the column next to the frame, and only the right side is padded.
  Text is padded by one column inside each box otherwise (rows keep their selection tint over the padding); the first and last column and the top and bottom edge row belong to the frame, so the frame costs two columns and one row more than the old divider layout (the old header row is gone).
- Focus: the focused pane's whole box is drawn in the accent colour (bold), every other line in the quiet border colour, and its title chip is the filled accent chip (the other titles are dim chips).
  Focus never relies on colour alone: the chips differ in text style, and with `NO_COLOR` the focused box is bold.
- Layout numbers: the sidebar (clamped 20-36 columns, shown from 60 columns) and the detail/diff split are widths including the padding; drag and the `Ctrl-w` size keys move the shared line.

**Selection and focus.** The selected row is a background tint, never inverse video, so the row keeps its own colours (branch, hash, diff colours stay readable).
The focused pane's selection uses the full tint (blue), a pane without focus keeps a quieter grey tint, so the cursor stays findable.
This holds for the graph, sidebar, detail, diff, picker rows and overlay rows.
The rows of a Visual range are drawn stronger than the selected row (#814): a background one step further towards the accent at 24-bit colour (text that would lose its 4.5 contrast on it is lightened or darkened), and in every tier bold text, plus reverse video at 16 colours and `NO_COLOR` (where no tint can tell the range from the cursor row), so a range never relies on colour alone. A range looks the same whether the pane has focus or not (Visual mode ends when the focus changes), and its far end is drawn like the other rows.
Focus is also shown by the frame (above): the focused pane's box is accent, its title is a filled accent chip and the other titles are dim chips.
The active tab, the status mode (`NORMAL`, `INSERT`...) and an overlay's title bar are the same accent chip; the tab row and the status bar sit on a slightly lighter bar.
An overlay's frame is drawn in the accent colour.

**Roles to colours** (accent blue, green, red, yellow, cyan, purple, muted grey):

| Role | Colour |
|---|---|
| Section titles, buttons, diff file header, the focused pane's frame | accent, bold |
| Graph lanes | one of 12 colours (6 in 16-colour mode, none with `NO_COLOR`) per branch, see Lanes in the graph (#761) |
| Graph commit marks | the lane's colour |
| Graph branch labels (#760) | a chip (box) filled with the lane's colour, bold, text in the theme's chip colour when it reads on it (4.5) else black or white; inverse video without colours; at 16 colours the text is the scheme's chip colour on the named lane colour, whose contrast the terminal's own palette decides; the current branch keeps its `◆` mark |
| Local branch / current branch (HEAD) / remote branch outside the graph (sidebar, details); tag everywhere | green / cyan / purple / yellow, bold (remote branches are not red: red means an error or a deletion; blue on a light terminal in the 16-colour scheme, where yellow and purple are taken) |
| Hash, date, line numbers, hints, counts, punctuation | muted |
| Diff whitespace marks (`·` `→` `•` `¶`) | subtle: muted blended 55% into the background (70% into the selection tint on a selected row); at 16 colours muted plus the faint attribute; at `NO_COLOR` faint |
| Changed words inside an added / removed line | the line's colour on a tint of it (`addedWord` / `removedWord`: good / bad blended 15% into the base, less where the text would fall under 4.8:1), bold; at 16 colours and `NO_COLOR` bold and underlined, no background |
| Added line or file / removed / modified | green / red / yellow; hunk header cyan |
| Errors | red, bold (in the status bar on its own bar) |
| Search match | dark text on yellow (kept on a selected row) |
| Overlay key column | yellow, bold |

**Colour depth.** The scheme follows the terminal. 24-bit (`COLORTERM=truecolor`, also used for 256-colour terminals because Terminal.Gui has no 256-colour output of its own): the Catppuccin palette (Mocha on a dark terminal, Latte on a light one), checked by a test for a contrast ratio of at least 4.5 on a typical dark and light terminal background (3.0 for dividers), also on selected rows. 16-colour (`TERM` without `256color`, or a Terminal.Gui driver that cannot do 24-bit;
the driver is then forced to 16 colours): only the 16 named colours, so the terminal's own theme decides the exact look;
the focused selection is bright black (bold), the unfocused one underlined.
`NO_COLOR` or `TERM=dumb`: text styles only (reverse video for the selection, underline when unfocused, bold underline for search matches, underline for the current branch).
Light backgrounds get darker accents and light tints.
With `theme = auto` the terminal is asked for its background (OSC 11, before the UI starts, 50 ms timeout, skipped when not interactive, at `NO_COLOR`, for `TERM` unset, `dumb` or `linux`, and on Windows, where the answer is not reliable); an unanswered or unreadable query falls back to `COLORFGBG` (background 7 or 15), and a terminal that offers neither is assumed dark.
A terminal that answers costs a few ms of startup, a silent one the full timeout.
The `theme` setting overrides that detection (`light` or `dark`, the legacy aliases; a theme file's `variant` does the same; `auto`, the default, keeps it), at every colour depth except `NO_COLOR`, which has no colours to switch.
`theme` tells Gitunia what the terminal looks like: `light` assumes a light terminal background, so at 16 colours (where the terminal's own background is kept) it is only readable on a light terminal, while at 24 bits the scheme paints its own backgrounds and works on either.
A terminal whose `TERM` has no `256color` and that sets no `COLORTERM` (for example over ssh without `COLORTERM` forwarded) gets the 16-colour scheme; set `COLORTERM=truecolor` to force the 24-bit one.

**Theme files (#61).** A theme is a JSON file (comments and trailing commas allowed), so a user adds one without a release.
Built-in themes are the same files, embedded in the binary: `catppuccin-mocha`, `catppuccin-macchiato`, `catppuccin-frappe`, `catppuccin-latte`, `tokyo-night`, `tokyo-night-storm`, `tokyo-night-moon`, `tokyo-night-day`, `gruvbox-dark-hard`, `gruvbox-dark-medium`, `gruvbox-dark-soft`, `gruvbox-light-hard`, `gruvbox-light-medium`, `gruvbox-light-soft`, `dracula`, `nord`, `solarized-dark`, `solarized-light`, `rose-pine`, `rose-pine-moon`, `rose-pine-dawn` (#610).
A user file is `themes/<name>.json` in the config directory; its lower-case file name is the value of `theme`, and it wins over a built-in theme of the same name.
`auto`, `dark` and `light` are reserved.
A file that is unreadable or invalid is not offered, and each reason is reported as a config problem at startup (a `theme` naming it falls back to `auto`).
The files are read and parsed once, at startup; edits show after a restart.

```jsonc
{
  "version": 1,                 // optional; the only format there is, so a later change cannot break old files
  "name": "My Theme",           // optional, defaults to the file name
  "variant": "dark",            // required: dark or light, the side of the terminal the theme is made for
  "colors": {                   // "#rrggbb" only
    // required
    "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7", "warn": "#f9e2af", "info": "#89dceb",
    "muted": "#9399b2", "border": "#6c7086", "barBackground": "#181825",
    "selectionFocused": "#313244", "selectionDim": "#262637",
    "chipForeground": "#11111b", "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e",
    "lanes": ["#89b4fa", "#a6e3a1", "#f9e2af"],            // the branch colours of the graph, by slot; at least one
    // optional (the default is in parentheses)
    "mutedOnSelection": "#a6adc8",    // (muted)
    "subtle": "#5e6277",              // (muted blended 55% into baseBackground: diff whitespace marks)
    "subtleOnSelection": "#8388a0",   // (mutedOnSelection blended 70% into selectionFocused)
    "groupBackground": "#222232",     // (none: the cursor group is marked by its bar only)
    "tabButtonBackground": "#313244", // (selectionFocused)
    "chipBackground": "#89b4fa",      // (accent)
    "matchForeground": "#11111b",     // (chipForeground)
    "matchBackground": "#f9e2af",     // (warn)
    "addedWord": "#323c3f",           // (good blended into baseBackground, as strongly as the text stays readable)
    "removedWord": "#3e2e40"          // (bad blended into baseBackground likewise)
  },
  // Optional 16-colour mapping, see below.
  "ansi": { "accent": "brightBlue" }
}
```

The built-in values are the published palettes, except that roles which must meet the contrast tests are moved the least that does it (lighter on dark themes, darker on light ones: `muted`, `border`, some accents, the base text of Tokyo Night Day; Dracula red and Nord red move furthest, because the published red fails on the selection tint).
The Gruvbox, Dracula, Nord, Solarized and Rosé Pine files use the published hex values (all MIT-licensed palettes) with the same rule, and the default 16-colour mapping) and the quieter tints (`selectionDim`, `groupBackground`) are mixes of the base and the selection colour; so `catppuccin-mocha` differs slightly from the `dark` table.
Roles are the `PaletteTable` colours (the table above says what each paints).
Unknown keys are ignored; a missing required role, a malformed colour, an unknown `variant` or `version`, or a bad `ansi` entry refuses the whole file, one problem per reason.
The colours are used at 24-bit and 256-colour output, and the base text and background are always painted, so the theme looks the same on any terminal background.

**The 16-colour mapping rule.** At 16 colours the terminal's own palette decides what the named colours look like, so a theme cannot carry its hex values there.
The rule: the `variant` picks the built-in 16-colour table (dark: bright accents on the terminal's background; light: normal-intensity accents), and the optional `ansi` object overrides single roles (`accent`, `good`, `bad`, `remote`, `warn`, `info`, `muted`, `mutedOnSelection`, `border`, `chipBackground`) with one of `black red green yellow blue magenta cyan gray darkGray brightRed brightGreen brightYellow brightBlue brightMagenta brightCyan white` (case-insensitive).
Selection and lanes keep the variant's choice.
A theme whose `variant` differs from the detected terminal background paints black or white as base, like a forced `dark` or `light`.
The built-in themes use the default mapping.
Contrast tests cover every built-in theme at 24-bit (4.5, dividers 3.0, cursor group and lanes included), after rounding to the xterm 256-colour palette (3.0), and at 16 colours (refs stay apart, no text in its own background colour); a user theme is not checked.

**Looking at it without the UI.** `--print-frame` reads `config.json` like the app (`frameStyle`).
`gitunia <repository> --print-frame WxH [--diff] --colors truecolor|16|none [--light]` writes the frame with ANSI colours (the graph is focused, the sidebar cursor shows the dim tint).
The overlays and the picker are not part of the printed frame; open them with `?`, `:` and `Ctrl-t`.

## Config file

`config.json` in the config directory (`$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, see `architecture.md`).
JSON with comments and trailing commas, read with source-generated System.Text.Json.
Unknown properties are ignored, so later sections fit beside `keys`.
The file is read once at startup and never modified by the loader; the settings screen (below) is the only writer.

```jsonc
{
  // auto (default: follow the terminal's background) or a theme file name such as "catppuccin-mocha" or "tokyo-night".
  // The older values dark and light still work (Catppuccin Mocha and Latte).
  "theme": "auto",
  // Tab chips show the checked-out branch after the name (default false: it makes tabs long).
  "tabs": { "showBranch": false },
  // Folders the repository picker scans for repositories (default: Projects, src, code, dev, ... below the home folder).
  "scanRoots": ["~/Projects", "/srv/git"],
  // Live file watching instead of polling (default false: each watcher takes a scarce inotify instance; read at startup).
  "watchFiles": false,
  "keys": [
    // Bind a free key (mode defaults to Normal, view to Global).
    { "view": "Graph", "key": "J", "action": "list.down" },
    // Replace what a key does.
    { "key": "q", "action": "palette.open" },
    // Unbind: the action "none".
    { "view": "Lists", "key": "gg", "action": "none" }
  ]
}
```

- Entry fields: `mode` (`Normal`, `Visual`, `Insert`, `Command`, `Confirm`), `view` (`Global`, `Lists`, `Graph`, ... as in the keymap table; a view's keys also apply to the views that inherit from it), `key` (notation of the keymap table), `action` (id from the palette/help, or `none`).
  One entry binds one mode and view; bind a key in Normal and Visual with two entries.
- An entry on a key that already has a binding in that mode and view replaces it; `none` removes it (and uncovers a binding of the parent view, if any). Two entries for the same mode, view and key: the first applies, the second is reported.
- Each entry is checked alone.
  A bad one (unknown mode, view or action, key that does not parse, nothing to unbind, a field of the wrong type such as `"key": 5`, an entry that is not an object) is skipped and reported; the others apply.
  A file that is not valid JSON, is unreadable or is over 1 MB is ignored as a whole.
  The app always starts: the defaults fill in whatever could not be used.
- Conflicts: the engine always waits for a longer chord (no timeout), so a key cannot be both complete and the start of a longer chord (`g` next to `gg`, `gt`).
  An entry that would create that, in its view or any view that inherits from it, is refused with a message naming both keys.
  Unbind the other key first (earlier entry) if you want the shorter one.
- Config can rebind guard rails too (a key for `confirm.yes` in Confirm mode, `Esc`, the quit key); it is the user's file and an entry is accepted as long as no conflict arises.
- Options (#453), read by `OptionsConfigurator` (Core, pure; the folder check is injected).
  `tabs.showBranch`: a boolean; a `tabs` that is not an object or a non-boolean value is reported and false applies.
  `scanRoots`: a list of absolute paths, a leading `~` stands for the home folder; it replaces the conventional folders, an empty list turns discovery off, and the picker scans these folders only (depth and count limits unchanged).
  An entry that is not a string, is relative (never resolved against the process working directory) or is not an existing folder is reported by index (`scanRoots[2]`) and skipped; if no entry of a non-empty list is usable, or the value is not a list, the conventional folders apply.
  `watchFiles` (#543): a boolean, false by default (tabs poll, see `git-layer.md`); another value is reported and false applies; it has no row in the settings screen and takes effect after a restart.
  Options are read at startup; the settings screen applies a change at once (and reads the file again when it opens and when the editor row returns).
- Problems appear as warnings at startup (the first in the status bar with a count of the others; `<space>m` keeps the first five).
  The text span of any value is found by `ConfigKeyLocator.Locate` (found, absent, empty, invalid with line, parent not an object; the last duplicate wins), which `ConfigTextEditor` and `ConfigWriter` (#522) use to change one key and keep comments and formatting.

## Settings screen (design #465; the screen is implemented in #523, the theme in #524, the key reference in #525)

Implemented by #522 (config writer: `ConfigWriter`, `ConfigTextEditor`), #523 (the screen: overlay, categories, toggle, choice, number and path-list controls, filter, mouse, the `$EDITOR` row; `SettingsRegistry`, `SettingsState`, `SettingsReducer`, `SettingsController`), #524 (the Theme setting: live preview, `settings.open-theme`) and #525 (key reference).
A category without a setting is hidden, so the Git category does not appear yet.
The registry is the single list the screen, the writer and the table below agree on (a test compares the table with it).

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

**Open choices for the owner** (the default is built unless the owner says otherwise):

1. Entry key: `<space>,` (default) or a different leader key; `:settings` always exists.
2. Overlay (default) or a full-screen tab.
3. Edit key bindings in the screen (default: no, the `keys` list stays a hand-edited file; the key reference shows what is bound).
4. Save on commit of each control (default) or an explicit save key with a dirty marker.
5. Theme `auto` default (follows the terminal).
6. Whether the screen offers "Open config.json in `$EDITOR`" as a last row (default: yes, it suspends the TUI like `<space>te`).

## Keymap

The single source of truth for bindings.
Add a row in the same change that adds a binding; no conflicts within a mode/view.
A test parses this table (columns `Mode/View | Key | Action`; keys in backticks, `Picker` rows are Insert mode, `Palette` rows Command mode, the rest Normal) and checks every key is a registered default in `DefaultKeymap` with no conflicts.
Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `<` is a literal `<` (config files also accept `<lt>`), `:name` is an ex command and not a key.
Modes: rows apply to Normal, except those whose view says `(Visual)` (Visual mode only); Visual repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action.

| Mode/View | Key | Action |
|---|---|---|
| Global | `?` | Show keys for current view |
| Global | `:` / `Ctrl-p` | Command palette |
| Global | `q` | Quit (closes an overlay or ends a Visual range first) |
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
| Global | `<space>,` | Open the settings screen (overlay over the tab; `Esc` closes; see "Settings screen") |
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
| Graph | `<space>tm` | Resolve conflicts in the configured mergetool |
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
| Detail | `Enter` `l` | Go to the diff of the selected file, or of the folder under the cursor in the tree style (Changes tab): focus it; on narrow screens open it on demand. Files tab: open or close the directory under the cursor; on a file, move focus into the content pane beside the tree (on narrow screens: open it) |
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
| Detail | `h` | Back to graph (Files tab: close the open directory, else move to the parent row; back to graph at the top. Changes tab, tree style: the same for folders) |
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
| WorkingUnstaged | `<space>to` | Open the conflict editor on the conflicted file under the cursor (`Enter` and `l` do the same on a `!` row) |
| WorkingUnstaged | `<space>d` | Discard the changes of the file under the cursor, or of every file under the folder there, after a Confirm that names it (the folder dialog counts the files and names the untracked ones); an untracked file is deleted (no undo) |
| WorkingUnstaged (Visual) | `<space>a` | Stage the files of the range (a folder row stands for all the files under it; the range ends; conflicted files are skipped) |
| WorkingUnstaged (Visual) | `<space>d` | Discard the files of the range after one Confirm that counts them and names the untracked ones, which are deleted (the range ends when confirmed; no undo) |
| WorkingUnstaged | `Esc` | Leave the working-tree view (back to the history) |
| WorkingUnstaged | `h` | Tree style: close the open folder under the cursor, else move to the parent folder; on a file at the top level, and in the flat styles, leave the working-tree view (back to the history) |
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
| WorkingStaged | `h` | Tree style: close the open folder under the cursor, else move to the parent folder; on a file at the top level, and in the flat styles, leave the working-tree view (back to the history) |
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
| WorkingUnstaged | `Enter` `l` | Go to the diff of the file or folder under the cursor (on a conflicted `!` file: open the conflict editor; on a screen under 80 columns the diff replaces the list until `h` / `Esc`) |
| WorkingStaged | `Enter` `l` | Go to the diff of the file or folder under the cursor (on a screen under 80 columns the diff replaces the list until `h` / `Esc`) |
| WorkingUnstagedDiff | `<space>j` | Quick jump: type text, labels appear on the diff rows in view that contain it, press a label to move the cursor there (`Enter` nearest, `Esc` cancels) |
| WorkingUnstagedDiff | `<space>r` | Refresh the status (the diff reloads with it) |
| WorkingUnstagedDiff | `]c` `[c` | Next / previous change group |
| WorkingUnstagedDiff | `]f` `[f` | Next / previous file (the pane shows that one file; the file list cursor follows; stops at the first / last file; in the folder view of the tree style the diff cursor steps between the folder's files and the list stays) |
| WorkingUnstagedDiff | `Esc` / `h` | Back to the file list (closes a search first) |
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
| WorkingStagedDiff | `Esc` / `h` | Back to the file list (closes a search first) |
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
| Sidebar | `l` | Expand section or folder; on a leaf, focus graph; on a stash, show it in the detail pane and focus that |
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
| Diff | `Esc` / `h` | Back to the file list (closes an on-demand diff) |
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
| ConflictEditor | `n` | Next conflict (hunk of the file) (wraps; `j` and the wheel do the same) |
| ConflictEditor | `N` | Previous conflict (wraps; `k` does the same) |
| ConflictEditor | `o` | Take our side for the hunk |
| ConflictEditor | `t` | Take their side for the hunk |
| ConflictEditor | `b` | Take both sides for the hunk, ours first |
| ConflictEditor | `O` `T` | Take our (`O`) or their (`T`) side for every hunk still unresolved; earlier picks stay; one step of undo |
| ConflictEditor | `]c` | Scroll the sides to the next line where ours and theirs differ (a run of differing lines counts once) |
| ConflictEditor | `[c` | Scroll the sides to the previous line where they differ |
| ConflictEditor | `Ctrl-d` | Scroll ours, theirs and the base down together by half the side panes |
| ConflictEditor | `Ctrl-u` | Scroll them up together |
| ConflictEditor | `u` | Undo the latest pick or edit of the session (the cursor goes to its hunk) |
| ConflictEditor | `Ctrl-r` | Redo the undone pick or edit |
| ConflictEditor | `r` | Reset the hunk: unresolved again, it keeps its conflict markers (one step of undo) |
| ConflictEditor | `e` | Edit the hunk's result as text (Insert mode; starts from the pick, or from both sides for an unresolved hunk) |
| ConflictEditor | `B` | Show or hide the base (common ancestor) below the sides; narrow layouts only, a no-op with three panes |
| ConflictEditor | `R` | Re-create the conflict of the file (`git checkout --merge`) after a Confirm |
| ConflictEditor | `Enter` | Save the result to the file (a Confirm first when hunks are unresolved; then the "mark resolved" Confirm when none is) |
| ConflictEditor | `h` `Esc` | Close the editor; picks made ask for a second `Esc` first |
| ConflictEditor (Insert) | `Enter` | New line in the hunk's text |
| ConflictEditor (Insert) | `Ctrl-g` | Keep the text as the hunk's result (one step of undo; text left unchanged changes nothing); `Esc` keeps it too |
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

## Mouse

Mouse input is optional and goes through the same action path as keys: the mouse map (`DefaultMouseMap`) turns a gesture on a view into the action id a key would produce, and the same handler runs it.
A test parses the table below and checks it against the defaults, so add a row in the same change that adds a mouse path.
Views inherit like in the keymap: `Lists` covers Graph, Detail, Sidebar, Diff and the two working-tree lists.
`CommandsButton`, `NewTabButton`, `Tabs`, `Picker`, the banner chips (`BannerButton.*`), the overlays (`Help`, `Messages`, `Palette`, `Reflog`, `Settings` and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `DetailTab.*`, `PickerButton.*`, `ListButton.*`, one view per button or label) are views of their own.

| View | Gesture | Action | Effect |
|---|---|---|---|
| Lists | Wheel up | `list.up` | Moves the selection up 3 rows (like `3k`) in the pane under the pointer, which gets focus |
| Lists | Wheel down | `list.down` | Moves the selection down 3 rows (like `3j`) |
| Lists | Drag start | `visual.toggle` | Pressing a row and moving the pointer starts Visual mode (range selection, like `v`) at the pressed row; graph, detail, diff, the working-tree diff and the two working-tree file lists (to stage the files or lines, press `<space>a` / `<space>u`, or run the action from the palette, `:`) |
| Lists | Drag up | `list.up` | While dragging, the cursor follows the pointer: moves up by as many rows as the pointer is above it |
| Lists | Drag down | `list.down` | The same downwards |
| Graph | Double-click | `menu.open-for-row` | Same as `<space>.`: open the actions menu of the row (the first click selected it); a double-click on blank space below the last row does nothing; the details pane is reached by a click on it, `Enter`, `l` or the menu's Open details row |
| Detail | Click | `detail.toggle-folder` | Open or close the folder row under the pointer (Changes tab, tree style; nothing on other tabs and styles; the row is selected as usual, so the diff follows; a click on a file only selects it, and a double-click undoes the toggle of its first click, then opens the diff, #622) |
| Detail | Double-click | `detail.open-diff` | Same as `Enter`: go to the file diff (Files tab: open or close the directory, or focus the file's content; Details tab: open the issue link on the line, if any) |
| Sidebar | Click | `sidebar.activate` | Same as `Enter`: expand or collapse a section, select the ref's commit in the graph, show a stash in the detail pane (then a click on its file rows opens their diff) |
| Tabs | Click | `tab.next` | Switch to the clicked tab (same as `3gt` with the tab's number); closes an open picker |
| TabDirtyMarker | Click | `working-tree.open` | The `*` of a dirty tab chip: switch to that tab and open its working-tree view (a click elsewhere on the chip only switches); no effect where the marker is cut off |
| NewTabButton | Click | `picker.open` | The ` + ` button right after the last tab: open the repository picker for a new tab, same as `Ctrl-t` |
| CommandsButton | Click | `palette.open` | The ` : commands ` button at the right end of the tab row (rows of 44 columns or more; there is no header bar): open the command palette, the menu of the actions of the focused pane and the global ones, each with its keys |
| RemoteButton.Fetch | Click | `remote.fetch` | The `<space>F fetch` chip of the tab row (rows of 100 columns or more, the key text from 127), left of the commands button: same as `<space>F` |
| RemoteButton.Pull | Click | `remote.pull` | The ` pull ` chip: same as `<space>L` |
| RemoteButton.Push | Click | `remote.push` | The ` push ` chip: same as `<space>P` |
| RemoteButton.Cancel | Click | `remote.cancel` | The `<space>C cancel` chip that replaces the three chips while one of them runs on the active tab: same as `<space>C` |
| BannerButton.Continue | Click | `operation.continue` | The `continue` chip of the in-progress operation banner: same as `<space>gc` |
| BannerButton.Abort | Click | `operation.abort` | The `abort` chip of the banner: same as `<space>ga` (asks first) |
| BannerButton.Skip | Click | `operation.skip` | The `skip` chip of the banner (not shown for a merge): same as `<space>gk` (asks first) |
| RowMenuButton | Click | `menu.open-for-row` | The `...` marker in the last three columns of the selected sidebar row: opens the actions menu of that row without moving the cursor (see Actions menu); other rows only select on a click |
| Backdrop | Click | `cancel` | While the help, palette or status history is open, a click on a pane or the tab row outside it closes it (like `Esc`); the double-click that follows is ignored |
| Palette | Click | `command.execute` | Run the clicked entry (it is highlighted first) |
| Palette | Wheel up | `palette.previous` | Move the highlight up one row |
| Palette | Wheel down | `palette.next` | Move the highlight down one row |
| ContextMenu | Click | `command.execute` | Run the clicked entry of the actions menu (it is highlighted first); a click outside closes the menu |
| ContextMenu | Wheel up | `palette.previous` | Move the highlight up one row |
| ContextMenu | Wheel down | `palette.next` | Move the highlight down one row |
| ConfirmButton.Yes | Click | `confirm.yes` | The `y` row of a confirm dialog, same as `y` |
| ConfirmButton.No | Click | `confirm.no` | The `n` row of a confirm dialog, same as `n` / `Esc` |
| HelpTab | Click | `help.tab-select` | A tab label in the help overlay's tab row: shows that tab (the row number is the click target; when the labels do not fit, the one compact label `[Graph] 1/4` moves to the next tab) |
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
| RebaseEditor | Wheel up | `list.up` | Scroll the todo up 3 commits |
| RebaseEditor | Wheel down | `list.down` | Scroll the todo down 3 commits |
| RebaseButton.Pick | Click | `rebase-editor.pick` | Chip `p pick` under the todo: sets the highlighted commit; a click on a commit itself only highlights it |
| RebaseButton.Reword | Click | `rebase-editor.reword` | Chip `r reword`: opens the message editor |
| RebaseButton.Squash | Click | `rebase-editor.squash` | Chip `s squash` |
| RebaseButton.Fixup | Click | `rebase-editor.fixup` | Chip `f fixup` |
| RebaseButton.Drop | Click | `rebase-editor.drop` | Chip `d drop` |
| RebaseButton.Up | Click | `rebase-editor.move-up` | Chip `K move up` |
| RebaseButton.Down | Click | `rebase-editor.move-down` | Chip `J move down` |
| RebaseButton.Start | Click | `rebase-editor.start` | Chip `Enter start`: asks the Confirm (its `y` and `n` rows are the `ConfirmButton.Yes` and `ConfirmButton.No` views) |
| RebaseButton.Cancel | Click | `navigate.back` | Chip `Esc cancel`: closes the editor like `Esc` |
| ConflictEditor | Wheel up | `conflict-editor.previous` | Previous conflict hunk |
| ConflictEditor | Wheel down | `conflict-editor.next` | Next conflict hunk |
| ConflictButton.Ours | Click | `conflict-editor.ours` | Chip `o ours` under the result: takes our side for the hunk under the cursor |
| ConflictButton.Theirs | Click | `conflict-editor.theirs` | Chip `t theirs` |
| ConflictButton.Both | Click | `conflict-editor.both` | Chip `b both` |
| ConflictButton.AllOurs | Click | `conflict-editor.all-ours` | Chip `O all ours` |
| ConflictButton.AllTheirs | Click | `conflict-editor.all-theirs` | Chip `T all theirs` |
| ConflictButton.Undo | Click | `conflict-editor.undo` | Chip `u undo` |
| ConflictButton.Redo | Click | `conflict-editor.redo` | Chip `Ctrl-r redo` |
| ConflictButton.Reset | Click | `conflict-editor.reset` | Chip `r reset` |
| ConflictButton.Edit | Click | `conflict-editor.edit` | Chip `e edit` |
| ConflictButton.Base | Click | `conflict-editor.base` | Chip `B base` (narrow layouts; not drawn with three panes) |
| ConflictButton.Recreate | Click | `conflict-editor.recreate` | Chip `R re-create`: asks first |
| ConflictButton.EditDone | Click | `conflict-editor.edit-done` | Chip `Ctrl-g keep`, the only chip while a hunk's text is edited |
| ConflictButton.Next | Click | `conflict-editor.next` | Chip `n next conflict` |
| ConflictButton.Previous | Click | `conflict-editor.previous` | Chip `N previous conflict` |
| ConflictButton.NextDiff | Click | `conflict-editor.next-diff` | Chip `]c next diff` |
| ConflictButton.PreviousDiff | Click | `conflict-editor.previous-diff` | Chip `[c previous diff` |
| ConflictButton.ScrollDown | Click | `conflict-editor.scroll-down` | Chip `Ctrl-d scroll down` |
| ConflictButton.ScrollUp | Click | `conflict-editor.scroll-up` | Chip `Ctrl-u scroll up` |
| ConflictButton.Save | Click | `conflict-editor.save` | Chip `Enter save`: saves (asks first when hunks are unresolved) |
| ConflictButton.Cancel | Click | `navigate.back` | Chip `Esc close`: closes the editor like `Esc` |
| Settings | Wheel up | `list.up` | Scroll the column under the pointer up 3 rows (the categories or the settings) |
| Settings | Wheel down | `list.down` | Scroll the column under the pointer down 3 rows |
| SettingsButton.Edit | Click | `settings.activate` | A click on a setting's control, on an option of an open choice, or a second click on the selected row: edits it like `Enter` (toggle flips, choice opens, option is kept). A first click on a row, and a click on a category, only select |
| SettingsButton.Add | Click | `settings.add` | The `[+]` marker of a path list: add a folder, like `a` |
| SettingsButton.Remove | Click | `settings.remove` | The `[x]` marker of a folder: ask to remove it, like `d`; the question's `y` and `n` rows are the `ConfirmButton.Yes` and `ConfirmButton.No` views |
| MainlineParent | Click | `pick.mainline` | A parent row of the mainline overlay of a cherry-pick or revert of a merge commit: answers like the parent's digit key (the row number is the click target; a click outside cancels) |
| DiffButton.PreviousFile | Click | `diff.previous-file` | Button `‹ file`, same as `[f` |
| DiffButton.NextFile | Click | `diff.next-file` | Button `file ›`, same as `]f` |
| DiffButton.PreviousHunk | Click | `diff.previous-hunk` | Button `‹ hunk`, same as `[c` |
| DiffButton.NextHunk | Click | `diff.next-hunk` | Button `hunk ›`, same as `]c` |
| DiffButton.Wrap | Click | `diff.toggle-wrap` | Button `wrap` (a file's content only; the diff has it in the `view ▾` dropdown), same as `<space>vw` |
| DiffButton.Zoom | Click | `pane.zoom` | Button `zoom` of a file's content (the diff has the entry in the `view ▾` dropdown instead, #758), same as `<space>z` |
| DiffButton.View | Click | `diff.open-view-options` | Button `view ▾`, same as `<space>vv`: opens the dropdown of context, whole file, ignore space, split, wrap, whitespace marks and zoom |
| DiffButton.Stage | Click | `working-tree.stage` | Working-tree diff, Unstaged side: button `stage`, same as `<space>a` (the change group under the cursor, or the Visual range); also a chip at the right end of the group's first line (same view, same action) |
| DiffButton.Unstage | Click | `working-tree.unstage` | Working-tree diff, Staged side: button `unstage`, same as `<space>u`; also a chip on the group's first line |
| DiffButton.Discard | Click | `working-tree.discard` | Working-tree diff, Unstaged side: button `discard`, same as `<space>d`; opens the Confirm dialog, nothing is discarded before `y`; also a chip on the group's first line |
| DetailTab.Details | Click | `detail.tab-details` | The `Details` label in the detail pane's title row, same as `gd` (also focuses the pane) |
| DetailTab.Changes | Click | `detail.tab-changes` | The `Changes` label, same as `gc` |
| DetailTab.Files | Click | `detail.tab-files` | The `Files` label, same as `gf` |
| DetailTab.View | Click | `files.open-view-options` | The `view ▾` chip at the right end of the title row while the Changes or Files tab is selected (shown when the row has room), same as `<space>vv`: opens the dropdown of the path style (Changes) or the filter (Files) |
| WorkingUnstaged | Click | `working-tree.toggle-folder` | A folder row of the tree style opens or closes (same as `za`); on a file row it only moves the cursor |
| WorkingStaged | Click | `working-tree.toggle-folder` | A folder row of the tree style opens or closes (same as `za`); on a file row it only moves the cursor |
| WorkingUnstaged | Double-click | `working-tree.stage` | Stage the file, or every file under the folder (same as `<space>a`) |
| WorkingStaged | Double-click | `working-tree.unstage` | Unstage the file, or every file under the folder (same as `<space>u`) |
| ListButton.StageAll | Click | `working-tree.stage-all` | Button `stage all` at the right end of the Unstaged list's title row, same as `<space>A` (also focuses the list) |
| ListButton.UnstageAll | Click | `working-tree.unstage-all` | Button `unstage all` in the Staged list's title row, same as `<space>U` |
| ListButton.Resolve | Click | `conflict.open` | The `resolve` chip in the Unstaged list's title row, left of the `view ▾` chip; shown only while the cursor is on a conflicted file, same as `Enter` / `<space>to`: opens the conflict editor (also focuses the list; #701) |
| ListButton.Diff | Click | `working-tree.open-diff` | The `diff ›` chip in the title row of either file list, shown only on screens under 80 columns where the diff is not beside the list; left of the stage all / unstage all button and ahead of the `view ▾` and `resolve` chips (they give way to it), same as `Enter` / `l`: the diff of the file or folder under the cursor takes focus and replaces the list (also focuses the list; #376) |
| ListButton.View | Click | `files.open-view-options` | The `view ▾` chip in the title row of either file list, left of the stage all / unstage all button, same as `<space>vv`: opens the dropdown of the path style |
| WorkingMessage | Click | `working-tree.edit-message` | Focus the message box and start writing (Insert mode), same as `i`; the cursor goes where the pointer is (the position is not part of the action id) |
| MessageButton.SignOff | Click | `working-tree.toggle-sign-off` | The `[ ] SignOff` checkbox of the message box, same as `Alt-s` |
| MessageButton.NoVerify | Click | `working-tree.toggle-no-verify` | The `[ ] No-Verify` checkbox, same as `Alt-n` |
| MessageButton.Amend | Click | `working-tree.toggle-amend` | The `[ ] Amend` checkbox, same as `Alt-a` |
| MessageButton.Commit | Click | `working-tree.commit` | The `Commit` button, same as `Ctrl-g` |
| WorkingUnstagedDiff | Double-click | `working-tree.stage` | Stage the change group under the pointer (same as `<space>a`) |
| WorkingStagedDiff | Double-click | `working-tree.unstage` | Unstage the change group under the pointer (same as `<space>u`) |
| Picker | Double-click | `picker.accept` | Open the repository of the row |
| Picker | Wheel up | `picker.previous` | Move the highlight up one row |
| Picker | Wheel down | `picker.next` | Move the highlight down one row |
| PickerButton.Pin | Click | `picker.pin` | Chip `Alt-p pin` / `Alt-p unpin` on the highlighted row, same as `Alt-p` |
| PickerButton.Remove | Click | `picker.remove-recent` | Chip `Alt-d remove` on a highlighted recent row, same as `Alt-d` |
| PickerButton.Clone | Click | `picker.clone` | Chip `Alt-c clone` at the right end of the picker's hint line (94 columns or more, not while locating), same as `Alt-c` |
| PickerButton.Init | Click | `picker.init` | Chip `Alt-i init` next to it, same as `Alt-i` |

Pointer-position behaviour that has no action of its own: a click on a pane focuses it, a click on a list row selects it (graph, detail, sidebar, diff; the details pane follows the graph selection as with keys), a click on a picker row highlights it.
A click on empty space does nothing.
The wheel moves the selection, not just the view (the keyboard has no pure scroll either), so the details pane follows it as it does for `j`/`k`.
A mouse action does not touch a pending count or chord typed before it.
While the help, palette or status history is open, panes and the picker ignore every mouse gesture (no focus change, no selection), except that the wheel scrolls the help or history (or moves the palette highlight) when the pointer is over a pane or the overlay, and a click on a pane or the tab row closes it, like `Esc`; the which-key popup does not block the mouse.

Drag to select: press a row of the graph, detail, diff or file-list pane and move the pointer; the pane takes focus, the pressed row becomes the anchor, Visual mode starts (status bar `VISUAL`) and the cursor follows the pointer, also above or below the pane while the button is held: while the pointer rests outside the pane the cursor keeps moving every 80 ms, 1 row just outside, 1 more per 2 rows of distance, at most 6, until the pointer is back inside, the list ends or the button is released.
The range is drawn stronger than the selected row (bolder and a brighter tint, reverse video where there are no colours).
The release leaves the range active; a click on a row, `Esc`, `q`, `v` or moving focus to another pane or tab ends it.
`y` copies the range (the drag plus `y`, or `visual.copy` from the palette) and ends it; staging acts on it too (lines in the working-tree diff, files in its lists).
The sidebar and the message box have no ranges (a drag there only moves the selection); a drag also does not start a range while a prompt (search, filter, ref jump) or the picker is open.

Drag to resize: the line between the sidebar and the graph, the line between the detail and diff panes, and the title rows of the detail and diff panes (the line between the graph and the bottom panes) follow the pointer while the button is held.
Sizes are clamped to what fits (at least 12 columns for the sidebar and detail, 20 for the graph and diff, 3 rows for the bottom panes, 4 for the graph), belong to the tab and are saved with the session; `Ctrl-w =` (or `:`, then "Reset the pane sizes") returns to automatic, and `Ctrl-w >` `Ctrl-w <` `Ctrl-w +` `Ctrl-w -` are the keyboard path; after a step the repeat mode (see Modes) takes bare `>` `<` `+` `-` `=` until `Esc` or another key.
A single zoomed pane has no dividers to drag (the divider between the paired Changes and diff panes does not drag while zoomed, but `Ctrl-w >` `Ctrl-w <` move it, #683).

Clickable titles and buttons: a click on the title row of the detail or diff pane focuses that pane.
The detail title row starts with the three tab labels (`Details`, `Changes`, `Files`), drawn as buttons: the selected one is the accent chip (quieter when the pane has no focus), the others look exactly like the inactive repository tabs of the tab row (a chip on a subtle tinted background; underlined where the terminal tier has no background colours, `[Name]` in the ASCII tier from 29 columns, one padding column per side).
The selected tab of an unfocused pane keeps the chip but in bold accent-coloured text (bold and underlined in the colourless tiers) so it still differs from the other tabs; the title of a pane with a single view (graph, sidebar, diff, working-tree lists) is drawn the same way when the pane has no focus, one column of line between them; below 29 columns the padding around the labels goes.
The content of the detail pane has the same background as the other panes; the frame line and the buttons separate it.
A click on a label selects the tab.
The diff title row ends in buttons (see the table), chips like the inactive repository tabs (with the key in bold before the label when the pane is wide enough; the chips on a change group's first line stay label-only) with one column of line between them and `<`/`>` arrows and brackets in the ASCII tier, when the pane is at least 30 columns wider than the buttons need; narrower panes drop buttons in this order, last to go first: `view ▾`, the two hunk buttons, the two file buttons, the keys stay available.
The path gets the room first: the diff options that used to be five chips (`ctx-`, `ctx+`, `file`, `-w`, `split`) and `wrap` sit behind the one `view ▾` dropdown (#645).
The working-tree diff has the same title row and buttons, with the staging buttons of its side first: `stage` and `discard` on the Unstaged side (`stage` is the last to be dropped on a narrow row, `discard` sits after the hunk buttons in the drop order), `unstage` on the Staged side; they act on the change group under the cursor or the Visual range like `<space>a`, `<space>u` and `<space>d`, and `discard` opens the Confirm dialog first.
The change group under the cursor also carries the same chips on its first line, at the right edge (`stage` and `discard` on the Unstaged side, `unstage` on the Staged side; the history diff and a Visual range have none; a line whose text reaches the chips gets none, the title row still has them): a click or double-click on one runs the title button's action, so `discard` still opens the Confirm first.
A row that would keep fewer than 30 columns of diff text drops `discard`, then `stage`/`unstage`; the keys and the title buttons stay.
`<space>z` and the `zoom` entry of the `view ▾` dropdown give the diff the whole view (lists and message box hidden) until focus leaves the diff (`h`, `Esc`) or zoom is used again; in the history view with the Changes tab beside the diff they zoom the file list and the diff together and `h`/`Esc` only return focus to the list (#632).
In the working-tree view a click on the diff box focuses it (the mouse path from a list to its diff on screens of 80 columns or more; narrow screens use the `diff ›` chip in the title row of the list).
The highlighted picker row shows the chips `Alt-p pin`/`Alt-p unpin` and, for a recent repository, `Alt-d remove` at its right edge, the same key-first chips as the diff title (#744; rows of 72 columns or more).
The repository picker (the opener) hides the fetch, pull and push chips of the tab row, since they act on the active tab's repository; `: commands` stays.
Clicking the commands button at the right end of the tab row opens the command palette, which lists the actions of the focused pane and the global ones, so every action without a button (for example `<space>vl` load anyway, the search keys, `<space>r` refresh) can be run with the mouse; typing a search or a filter text needs the keyboard, as nothing else can enter text.

Terminal.Gui turns on mouse reporting (SGR, mode 1006) at startup and turns it off on exit; there is no switch to disable it yet.
Verified by writing SGR mouse sequences into the pane of a tmux 3.7c session running the Linux build: clicks, double-clicks, the wheel, press-drag-release (reported as a press, position reports with the button held, a release and then a click at the release position, which the drag logic swallows), divider and title drags, header, palette and button clicks all arrive; a double-click is delivered as a click followed by a double-click event (so a double-click on a sidebar section toggles it once), and a sideways wheel (Ctrl or Shift plus wheel) is ignored (unit-tested only).
Not yet verified with a physical mouse, over ssh, or in kitty, Windows Terminal and macOS terminals.
Terminals or ssh paths that drop mouse sequences leave the keyboard path unaffected.

Design history (why things are the way they are, measurements, rejected options): `docs/history/ux.md`. Not needed for routine work.
