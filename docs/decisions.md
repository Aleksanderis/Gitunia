# Decisions

Short dated log of settled choices that cut across docs. Agents follow these without asking. Open questions live in `roadmap.md`.

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Git layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. The full text of long entries, implementation detail, measurements and limits live in `docs/history/` (moved there, same wording), the area docs (`architecture.md`, `git-layer.md`, `ux.md`, `testing.md`), or the issue or PR. When a decision changes, append a new entry that supersedes the old one.

## 2026-10-10

**Stack:** C# on .NET with Native AOT (Rust was the runner-up). Why: the owner is a .NET developer doing heavy agentic development, so the code must stay reviewable by them.

**Git layer:** Git access via the `git` CLI, not libgit2. Why: it matches the user's real config, hooks, credentials and new git features such as worktrees.

**Architecture:** Standalone multi-repo client (tabs, picker, session restore), not bound to the current directory. Why: retrofitting multi-repo later is expensive, so the Git API is repository-scoped from the start.

**Platform:** Cross-platform from day one (Linux first; macOS lowest priority and droppable). Platform-specific code only behind a platform abstraction.

**Stack:** Minimum git 2.43, `net10.0`, JSON config with source generation, MIT licence, binary name `gitunia`, config in a `gitunia` dir (location: see the 2026-10-16 Platform entry). Why: LTS on current distros and AOT-safe.

**Architecture:** `Gitunia.Platform` holds config/state dirs and console capability detection, so Core stays free of OS code. SDK pinned to 10.0.400 (`rollForward: latestFeature`), test projects use xUnit v3, underscores allowed in test names.

**UI:** External tools (`$EDITOR`, difftool, mergetool) get the terminal by suspending the TUI; clipboard via OSC 52; commit messages use a built-in Insert-mode editor or `$EDITOR` (2026-10-15).

**Process:** Other open-source Git clients are sources of ideas, never of copied code. Why: licence hygiene.

**Process:** Spikes live on unmerged `spike/` branches and close with findings; `needs-review` issues are merged by the owner while agents continue elsewhere.

**Process:** PRs are merged with a merge commit (never squash or rebase) and the branch is deleted afterwards. Why: the owner wants the full branch history visible in the graph.

**UI:** Renderer is Terminal.Gui v2 (2.5.0), not a custom renderer (M0 decision #7, owner chose after the spikes). Why: toolkit widgets are far easier to read and maintain, and it is fast enough; accepted costs are startup near the 100 ms budget, a larger binary and a native library. Numbers: `docs/history/architecture.md`.

**UI:** Terminal.Gui needs `Application.MaximumIterationsPerSecond` ~100 (default 25 gave ~34 ms key latency), an append-only list source for streamed commits, and key handling at the application level. Why and further pitfalls: `docs/history/architecture.md`.

**Git layer:** Standalone launch resolves paths with `git -C <path> rev-parse` and dedupes tabs by the resolved toplevel (spike #3). Detail: `docs/history/git-layer.md`.

## 2026-10-11

**Code structure:** Conventions from the PR #60 review live in `.claude/rules/csharp-style.md`: no truncated names, one type per file, scripts in `scripts/`, tmux-based interactive testing is Linux/macOS only.

**UI:** The owner confirmed Terminal.Gui as the renderer, so M1 UI work may start. Key style for M1 (#8): agents draft it from vim, yazi and lazygit patterns, the owner adjusts after use, so the keymap stays data-driven.

**UI:** M1 key design (#8): `Tab` and `Ctrl-w` + direction move pane focus, single keys per view with a `<space>` leader for global actions, chords show which-key after 500 ms and never time out, Insert mode swallows printable keys. Detail: `docs/history/ux.md`.

**Git layer:** Commit ids are a fixed 40-byte `ObjectId` struct (#13) instead of interned strings. Why: no allocation per parent link or comparison; the default log excludes `refs/stash`.

**Git layer:** The diff parser (#15) is incremental, keeps every header and hunk line verbatim so patches reproduce git's text, and partial staging builds patches from hunk and line selections. Limits (combined diffs, invalid UTF-8, binary files): `docs/history/git-layer.md`.

**Architecture:** Graph lane layout (#16) is incremental with stable columns: a lane waits for exactly one commit id, lanes are not shifted left. Why: columns must not jump while scrolling or loading, and cost is O(open lanes) per commit.

**Architecture:** The keymap engine (#19) is pure: bindings are rows with a view hierarchy, and a binding that is a strict prefix of a longer one is a conflict (no chord timeout). Key notation is the `ux.md` one.

**Architecture:** Workspace state (#17) is immutable records with a pure reducer; tab ids are never reused, late results for closed tabs are dropped, tabs dedupe by root path.

**Architecture:** The graph view (#22) keeps Terminal.Gui code in `Views` and `Input`; `Rendering` has only text frames (`--print-frame WxH` prints one for snapshot tests and CI smoke). Graph rows keep a compact entry, not the full `CommitInfo`. Detail: `docs/history/architecture.md`.

**UI:** An Esc followed by a key within a few milliseconds arrives as `Alt-<key>` (terminal limitation); scripts must send them separately.

**Git layer:** File watching (#27) uses one `RepositoryWatcher` per tab and a shared watcher budget, with polling as the fallback where watching fails. Why: `FileSystemWatcher` is cross-platform, so no Platform involvement.

**Architecture:** Session persistence (#18) is a versioned `session.json` written atomically; a corrupt file is renamed to `.corrupt` and never blocks startup.

**Architecture:** Graph search (#26) scans on the UI thread in 5000-row slices (100k rows ~70 ms) so no other thread touches the graph; plain text matches the subject only.

**UI:** Clipboard (#52) uses OSC 52 with env-based detection (injected lookup, tmux and screen passthrough). Why: no platform clipboard APIs; success is a best guess because OSC 52 has no acknowledgement.

## 2026-10-12

**UI:** The repository picker (#21, #81) is pure state with an injected file-system context, discovers repositories under conventional folders (depth 3, rescanned at most every 30 s) and never resolves paths against the process working directory.

**Architecture:** Background tabs load lazily (#91, #111): only the active tab loads, idle preloading was rejected (first visit ~37 ms, ~0.5-1 MB per loaded tab). Background markers come from a status poller (#129).

**UI:** `Ctrl-j`, `Ctrl-^` and `Ctrl-h` arrive as other keys in Terminal.Gui 2.5.0 under tmux; `KeyTranslator` maps `Ctrl-j` and `Ctrl-^` back so the documented bindings work; `Ctrl-h` arrives as `Ctrl-Backspace` and is not mapped.

**Architecture:** Session wiring (#81): restore at startup, `gitunia <path>` opens or focuses a tab, no tabs opens the picker, save is debounced 500 ms and flushed on exit; tab markers stay current through `TabRefresher` (#90).

**UI:** Sidebar (#23) state (cursor, folding, filter) lives per tab in the pure state layer; it is a quarter of the screen (20-36 columns) and hidden under 60 columns.

**UI:** Help, palette and which-key (#20) are pure overlay state fed by an action catalog and key formatter, so nothing is hand-listed. The palette lists every registered action; unimplemented ones say so in the status bar.

**Architecture:** Session save failures post one status error per failing streak (#93); replacing a missing tab opens the new repository first so a failed open never loses the entry.

**Git layer:** Commit details (#24) read the full message with `git log -1` and files with `diff-tree` against the explicit first parent. Why: `diff-tree` shows merges against every parent and ignores `--first-parent`. Loading was later changed in #215.

**UI:** Mouse is optional and the keyboard path stays complete; if Terminal.Gui's mouse is limited over tmux or ssh, document the limits.

**UI:** The diff viewer (#25) always loads the whole commit's diff (against its first parent), not one file, because file jumps and rename detection need all files; layout is pure data (`PaneLayout`). Detail: `docs/history/ux.md`.

**UI:** Status messages (#93) go through one global `StatusChannel` with a log of the last 50; key-action notices stay transient. Dismiss `<space>x`, history `<space>m`.

**Git layer:** Graph reload after a refresh is incremental (#126). Why: a full re-read of 100k commits took ~840 ms (git itself ~780 ms), the incremental one ~105 ms; only a `git replace` ref that keeps the commit count goes unnoticed until a manual refresh (`r`).

**UI:** Diff extras (#106): word wrap is real wrapping, "Load anyway" lifts the row caps, and the diff follows the graph selection. Dropped: progressive display while streaming and binary content.

**UI:** The mouse follows the key path (#64): gestures map to action ids through a mouse map and run the same handlers as keys; the toolkit's own wheel scrolling is replaced. Detail: `docs/history/ux.md`.

**UI:** Drag, resize, buttons and overlays (#100) follow the same rule; drag-select is the existing Visual mode, and pane resizing has keys (`Ctrl-w > < + - =`) because nothing may be mouse-only.

**Git layer:** Background status polling (#135) uses `--untracked-files=normal`. Why: the tab bar only needs to know the tree is dirty, and it is far cheaper on trees with many untracked files.

## 2026-10-13

**UI:** The bottom pane has three tabs (#169: Details, Changes, Files), a fresh tab opens on Changes, and the Files tab is a placeholder until its own issue.

**UI:** Graph rows are a table (#168): lanes, message, author, date, short hash; narrowing the terminal drops hash, then date, then author (#318: the date column shows `yyyy-MM-dd HH:mm`; the time goes before the date). Why: fixed widths keep columns still while commits stream in.

**UI:** Look and feel (#171): background tint for the selected row, GitHub-like palette in 24-bit colour with a 16-colour tier and `NO_COLOR` fallback, dark unless `COLORFGBG` says light. Contrast is a test (4.5, dividers 3.0).

**Process:** CI skips draft PRs, `review/**` bases and docs-only PRs (#211). Why: the free plan allows 2,000 Actions minutes a month and M1 burned 690 in days; agents open drafts and mark them ready after review.

**UI:** The default palette (#216) is Catppuccin Mocha (dark) and Latte (light), colour values from https://catppuccin.com/palette (MIT, values only); Latte accents are darkened until they pass the contrast test.

**UI:** Graph ref labels (#214) fold a remote-tracking branch into the local branch of the same name on the same commit and hide `*/HEAD`; merging is by name, not by configured upstream.

**UI:** Every pane is a titled box with shared edges and one column of padding (#197), rounded outer corners, focus shown by the accent box and filled title chip. Why: separate boxes cost two rows and columns each. Research (ideas only): lazygit, btop, zellij; sources in `docs/history/ux.md`.

**UI:** Owner review of PR #198 (#217-#223): the header row is removed (its command-palette click moved to a ` : commands ` button), remote branches are purple, tabs show `N name markers`, `Ctrl-w r` renames a tab, and a pin keeps a repository at the top of the picker.

**Architecture:** Commit details load faster (#215): a selection after a pause reads at once, the next 3 and previous 1 commits are prefetched into a bounded LRU cache. Why: stepping with `j` took ~118 ms and now takes ~20 ms.

**UI:** Ref label glyphs (owner review of #198): `◆ ` marks the current branch and ` ↔` a branch that also stands for its remote; the bottom pane's tabs are buttons on a panel background. Why: emoji looked odd; both glyphs are one cell wide in common fonts.

**UI:** Diffs are part of the Changes tab (owner decision): at 72 columns or more the diff sits beside the file list and `j`/`k` in the list show that file's diff at once; narrower screens keep the on-demand open. Rules for `h`/`l`: `docs/history/ux.md`.

**Architecture:** Diff prefetch (#225) reads the neighbours of the shown commit into a bounded cache (`diff_step_ms` ~37 to ~20 ms); bigger commits are not prefetched, a partial-diff cache was tried and dropped (#235).

**Process:** Screenshots of the real UI (#227) are made from a tmux capture of the live binary with `tools/AnsiToSvg`, local only, never in CI. Why: no extra dependency and it shows real key-driven states.

**Process:** Screenshots are hosted in the separate private repository `Aleksanderis/Gitunia-screenshots` (#229, owner decision); agents may push there with plain pushes. Why: the images' history can be reset without touching the code repository.

**UI:** Picker improvements (#232): a ` + ` button after the last tab, `Tab` or `Right` accepts the highlighted suggestion, folder-name matching is case-insensitive. Known limit: the directory part of a typed path is resolved by the file system, so case matters on Linux.

## 2026-10-14

**UI:** M1 follow-ups from the owner: same-branch commits drawn as a line (#238), `Alt-1`..`Alt-9` keep jumping tabs with `3gt` as the multiplexer-safe alternative (#242), more space between graph columns (#244).

**UI:** `G` stops at the last loaded commit and shows a hint while loading (#194); the notice for `Enter` on Details and Files stays (#190). The owner's M1 review (#200) comes first.

**Process:** Decision entries are short: 1-3 lines (decision and why), a bold topic tag, a blank line between entries; detail lives in the area doc or the issue. Why: the log grew to ~62 KB and was hard to read. Done in #253.

**Code structure:** Folders group by domain first, technical kind second (`Models/` inside a domain); test folders mirror the code. Why: one flat folder with 72 files hides what belongs together. Done in #246.

**Code structure:** Pure parsers and formatters are static; classes with I/O, a clock or a cache are instances with constructor injection, wired by hand, no DI container (AOT). Why: static leaves no seam for fakes (#252).

**Code structure:** Git readers are instances holding a `GitRunner` and taking a `RepositoryLocation` per call; `GitServices.Create` is the hand-wired composition root and the Tui sees reader interfaces (#252).

**UI:** Key notation is the spelled-out `Ctrl-d` / `Alt-1` form everywhere, and keys and descriptions sit in separate columns (help) or as key plus description pairs (status hints) (#242). Why: `^d` and bare key lists were unreadable without knowing the keys.

**UI:** Inactive repository tabs are buttons: a chip on its own tint (`PaletteTable.TabButtonBackground`, visibly apart from the bar in both schemes), underlined without colours, bracketed in ASCII. Why: plain muted text did not read as clickable (#240).

**UI:** Graph (#238): unlabelled single-parent, single-child commits are drawn as part of the line, node dots only where a lane starts, ends, forks or merges. Partly superseded by #280.

**UI:** The detail list view paints cells its rows leave blank in the panel colour, also with the cursor on the last row (#243); a frame test cannot see this, so it is checked in a real terminal and screenshots.

**UI:** `Enter` on the Details or Files tab keeps showing a notice instead of switching to Changes (#190, owner decision). Why: the tab switch is explicit (`1`-`3`, click), a key that jumps tabs would surprise.

## 2026-10-15

**UI:** User-visible text spells out "Repository", never "Repo" (sidebar title fallback, picker tag, docs; #262, owner decision). Stable ids such as `repo.pin` keep the short form.

**UI:** Graph (#280, owner): a commit that carries any ref (branch, tag, remote, HEAD) keeps the node dot; only unlabelled commits become the line. Why: the line glyph under a label read as missing.

**UI:** Palette (#96, owner did not answer, recommended default taken): modal-only actions (`confirm.*`, `picker.*` except open, palette/input accept, `Esc`) are hidden; actions bound before their feature are listed greyed out. Keeps the palette to what a user can run.

**Code structure:** `RepositoryOperationDetector` and `FramePrinter` are instances, not static, so they can be faked (#269).

**Code structure:** Unused usings (IDE0005) fail the build; Roslyn needs `GenerateDocumentationFile` for it, so doc warnings CS1573/1591 are silenced (CS1574/1734 were re-enabled in #295). Small `Models/` folders (Refs, Watching, Execution) stay for consistency (#258).

**UI:** Status bar hints (#287, owner): wording stays short and is not derived from `ActionCatalog` titles; each hint names an `ActionId` per key and a test checks the default keymap binds the key to it.

**UI:** Graph lane 0 (#301, #307, #308, owner): with a branch checked out, lane 0 is the first-parent chain of its tip and ahead branches continue it upwards, so the current branch is a straight shaft on the left. A detached or unborn HEAD keeps the leftmost-free-lane layout.

**UI:** Bottom pane tabs (#300): inactive tabs reuse the repository tab chip role (`TabButton` removed); the selected tab of an unfocused pane has its own role (`SelectedTab`: bold on the chip, bold underline without colours), so it stays distinct in every tier.

**UI:** Diff title buttons (#302, owner): chips like the tabs with words (`‹ file`, `hunk ›`, `wrap`, `zoom`) instead of key-like brackets; the `[x]` close button is gone (the diff is part of the Changes tab).

**UI:** Pane padding (#244, owner): two blank columns inside History and the bottom panes from 100 columns of width, one below; the sidebar keeps one.

**UI:** The diff title row has no `‹ file` / `file ›` buttons (#310): files are chosen in the file list, `[f`/`]f` stay. Buttons count every click of a quick burst (the toolkit reports the second and third as double and triple click); a triple click elsewhere stays ignored.

**UI:** Commit messages (#32, owner): a built-in Insert-mode editor and the `$EDITOR` hand-off (#51) both exist. The working tree is its own view with a message box (#28).

**UI:** Diff options (#312, owner): context lines, whole files and ignore-whitespace can be changed from the diff pane, per tab and not saved (keys in `docs/ux.md`). Side-by-side (README says "later") and visible whitespace are split off as follow-ups, not part of this change.

**UI:** Diff options follow-up (#312, #315): every option has a title button; whole-file mode keeps per-group hunk navigation; side-by-side is in, unified stays when the pane is under 80 columns.

**Git layer:** Non-UTF-8 paths (#94) are decoded surrogate-escaped and reach git on stdin, never as arguments; line patches of such files are refused and the files staged whole.

**UI:** Working-tree view keys (#28) are designed in workflow 2 of `docs/ux.md` and stay out of the Keymap table until implemented, because the keymap test requires every row there to be bound.

**UI:** The working-tree view mode (#29) is tab state and is not saved with the session; a restart opens the history.

**UI:** External tools (#51): `git commit`, `git difftool` and `git mergetool` run with the real terminal by suspending the TUI (`docs/architecture.md`, "External tools").

**UI:** Commit message editor (#314): an own `MessageEditor` in Core drawn by our renderer, not Terminal.Gui `TextView` (obsolete, its replacement is a separate package to vet for AOT); testable without a terminal.

**UI:** `q` always quits from any pane (#333); `h` and `Esc` step back. Supersedes "q goes back one level".

**UI:** Vim-safe keys (#335, owner): standard vim keys keep their vim meaning; Gitunia actions use `<space>` chords or `Ctrl-`/`Alt-` keys. Mapping in `docs/ux.md` ("Key rule").

**UI:** Stage and unstage (#30): stage all skips conflicted files and counts them; one index operation per tab at a time; paths are always literal.

**UI:** Commit (#32): comment lines of a typed message are kept; a prompting hook or signing program fails with a hint to use `<space>te`. The quit guard is superseded by #347 and #369.

**UI:** `q` quits from the working-tree view too (#333 rule applied to #29/#30, which left `q` unbound there): `h`/`Esc` leave it.

**UI:** Working-tree diff (#330): `q` quits, `h`/`Esc` go back; a conflicted file shows git's combined diff folded into one old/new pair.

**Git layer:** Hunk and line staging (#31): the file's diff is re-read with default context and the picked lines applied with `git apply --cached` (`-R` to unstage), never built from the rows on screen.

**UI:** A double-click on a row of the working-tree diff stages or unstages its hunk (#31); lines are picked with `v` or a drag and staged with `<space>a` / `<space>u` or the palette.

**UI:** Discard (#33) is always behind a Confirm dialog that states what is lost; no dangling stash; one file at a time.

**UI:** Quick jump (#334): `<space>j` labels only rows in view; a letter that would continue a match is never a label.

## 2026-10-16

**UI:** vim-safe keys (#338, owner): diff display toggles move to the `<space>v` family (`w` `i` `f` `s` `l`), `@` to `<space>h`, `gr` to `<space>f`; `q` `h` `l` `]` `[` `?` `Y` `+` `-` `gd` `gc` `gf` stay as documented exceptions (read-only panes or navigation, muscle memory; list in `docs/ux.md`).

**Process:** CI cost (free plan): CI runs once per ready PR, weekly on main and by dispatch; AOT publish only on PRs touching project files; agents verify locally and push once. Supersedes the draft-PR flow. Revisit at v1.0.

**Platform:** Untracked names that are not valid UTF-8 are deleted on Linux only, by raw bytes through libc `unlink`/`rmdir` (P/Invoke) after `git ls-files -o -z` lists them; Windows and macOS keep the refusal (#358). Why: process arguments cannot carry such bytes and .NET paths are strings.

**UI:** Commit drafts (#347) are saved in the session file (message, SignOff, No-Verify, not Amend) and restored per tab, so the quit guard is gone (partly restored by #369, see below); closing a tab with an unsent message needs a second `Ctrl-w c` instead.

**Git layer:** The working-tree diff of a non-UTF-8 name (#344) streams the unfiltered diff and picks the file by its lossy path; untracked ones are read through a small `open(2)` P/Invoke (Linux/macOS).

**UI:** Working-tree diff chrome (#343): zoom ends when focus leaves the diff; the title row has `stage`/`discard` (Unstaged) or `unstage` (Staged) buttons. Click-through on narrow screens is #376.

**Git layer:** Lines of an untracked text file (#351) are staged via `git add -N` first, undone when nothing is staged; untracked symlinks go whole (#379). Supersedes "new files go whole" of #31.

**UI:** Quick jump (#353) is one action on `<space>j` in the graph, diffs and working-tree lists; the side-by-side diff (#381) and the detail pane (#382) have it too.

**UI:** Drafts and quit (#369): a missing tab's draft moves to the replacing tab; `q` quits at once unless the final session save fails with an unsent message. Supersedes the quit-guard removal of #347.

**Platform:** Config and state dirs are the same on every OS: `$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, `$XDG_STATE_HOME/gitunia` or `~/.local/state/gitunia` (supersedes the per-OS dirs, e.g. `%APPDATA%`). Why: Gitunia is a terminal app, config is hand-edited and kept in dotfiles repositories, and config stays apart from machine-local state.

**UX:** working-tree diff units (#386, owner): every group of consecutive added/removed lines is its own unit for `]c`/`[c`, the `change n/m` title and stage/unstage/discard, even inside one hunk. The history diff keeps its hunks (only the working tree stages).

**UI:** Commands popup (#385): `Ctrl-j`/`Ctrl-k` move, `Ctrl-l` runs (like the picker), `Ctrl-h` stays unbound (some terminals send it for Backspace). The footer reuses the status bar hint rendering.

**UI:** Working-tree pane jumps (#361): `<space>1` Unstaged, `<space>2` Staged, `<space>3` Diff, `<space>4` Message, bound in every working-tree pane in Normal mode. Why: leader chords keep vim keys intact, `Alt-1`..`Alt-9` already jump tabs, and the digits match the pane order on screen. Insert mode has no jump (space is text).

**UI:** Current change group of the working-tree diff (#393): a bar in the left margin plus a quiet tint (`GroupBackground`); the 16-colour and NO_COLOR tiers get the bar alone. Why: selection must never rely on colour. Stage/discard buttons stay in the title row; buttons beside the group are #405.

**UI:** Group buttons (#405): the change group under the cursor carries the title row's `stage`/`discard`/`unstage` chips on its first line, same views and actions (discard still confirms); dropped below 30 columns of text. Why: the action sits where the eye is.

## 2026-10-17

**UI:** M3 key flows (ux.md workflows 3-7): prefixes `<space>b` branches, `<space>g` commit operations and continue/abort/skip, `<space>T` tags, `<space>w` worktrees, `<space>S` stashes; reset is `<space>gx` (no single letter, no vim clash); `<space>t` stays the tools prefix. Planned keys live in prose; table rows arrive with each implementing issue.

**Git layer:** M3 calls that could open an editor pass `--no-edit` or `-c core.editor=true`. Pull is fast-forward only, fetch prunes, force push is `--force-with-lease` only. Cancel of fetch/pull/push is `Esc`, never `<space>x`.

**UI:** Stash keys are `<space>S` chords (`Ss` `Su` push without/with untracked; sidebar `Sa` `Sp` `Sd`), not the bare `a` `p` `d` of the first draft, which clash with vim. Apply, pop and drop refuse outside a stash row instead of opening a picker; only drop asks.

**UI:** Branch operations (#35): `<space>bc` `bC` `bs` `br` `bd` in the graph and the sidebar; no sidebar `d`/`r` shortcuts (vim-clash rule, supersedes workflow 3 item 5). Deleting an unmerged branch (not on HEAD or its upstream) asks a second Confirm. Why: chords keep vim keys intact and the unmerged case is the one that loses commits.

**UI:** Worktree add (#41) prompts for the branch first, then the path (default: sibling directory named after the branch), instead of path first plus a picker. Why: the default path needs the branch. Details in ux.md workflow 6.

**UI:** Upstream (#36): `<space>bu` picks a remote branch (type-to-filter) and `<space>bU` unsets, no Confirm; the status line prints the restoring `--set-upstream-to` command. Why: both are config-only and reversible.

**UI:** Worktree switch (#42) re-points the tab at the other worktree in place: same tab id and view state, new location, session and watcher (a new load). `wt` opens a tab instead. The sidebar overview runs `git status` in each other worktree (on change and every 30 s) because their changes raise no event for this tab. Details in ux.md workflow 6.

**UI:** Tags (#397): `<space>Tc` `Td` `Tp` `TD` in the graph and the sidebar, no sidebar `d` (vim-clash rule, supersedes workflow 3 "Tags"). A tag push is a plain one-ref push with no progress and no prompts; #44 generalizes it. Why: tags are usable before the full push flow.
**UI:** Reset (#392) is a one-key prompt (`s`/`m`/`h`) after `<space>gx`; soft and a mixed reset with nothing staged run at once, the rest opens a Confirm with the files lost and the old hash. Why: ux.md reset flow; the undo for commits is the printed old hash (ORIG_HEAD, reflog).

**UI:** The in-progress operation banner (#37) is drawn on the frame's bottom edge, not as a row under the tab bar. Why: a new row shifts every pane coordinate (views, mouse, layouts) for a rare state, and the edge line carries no information. Under 10 rows the status bar carries its text. Supersedes the "row under the tab bar" wording of workflow 4.

**Git layer:** A call that runs or advances a merge, rebase, pick or revert returns `OperationResult` (`Completed` or `Conflicted`), decided in one place (`OperationRunner`); editor-capable calls pass `-c core.editor=true`. Why: #38-#40 and the interactive rebase reuse it instead of each guessing "did it stop?".

**UI:** The reflog (#398) is a modal list overlay with its own keymap view, not a tab view: reset, branch and detached checkout close it first and reuse the status bar prompts and Confirm. Why: keeps tab state unchanged and reuses #392 and #35; the ref picker, `/` search and entry mouse actions are #424.

**UI:** Merge (#38): `<space>gm` (ff when possible), `<space>gM` (`--no-ff`), `<space>gs` (squash) act on the selected commit or sidebar ref and run at once, no Confirm. Why: a merge loses nothing and the status line prints the `reset --hard <old>` undo; a squash that conflicts leaves unmerged files but no operation, so the message says to resolve and commit.

**UI:** Rebase onto (#39) is `<space>gr` with a Confirm naming the old tip for the undo; a dirty tracked tree, a detached HEAD or a running operation is refused with no git call. Why: git would refuse or leave a half-state; a stop on conflicts reuses the #37 banner and continue/abort/skip.

**UI:** Cherry-pick and revert (#40): `<space>gp`/`<space>gv` act on the cursor commit, a Visual range or a sidebar ref tip and run at once, no Confirm (the status prints the `reset --hard <old>` undo). A pick applies oldest first, a revert newest first; a detached HEAD is allowed. Detail: ux.md workflow 3.

**UI:** Checkout (`<space>bs`, `<space>bc`, reflog detached checkout) is refused while an operation is in progress, no git call (#419). Create-only (`<space>bC`) stays allowed. Why: git would switch under a half-done merge or rebase.

**UI:** Mouse follow-ups of M3 (owner accepted the defaults, 2026-10-17): sidebar actions (branches, worktrees, stashes, tags) get buttons in the section header; graph commit operations (merge, rebase, reset, cherry-pick, revert) open from a right-click that selects the row and shows the command popup filtered to that row's actions. A plain rebase warns in its Confirm that merge commits are flattened (`--rebase-merges` stays with #399). A branch merge passes the full ref and fixes the message when a tag has the same name (#425).

**UI:** Tag and merge mouse paths (#434): chips in the Tags header (`[tag]` `[delete]` `[push]` `[remote-del]`) and merge chips in the Branches and Tags headers; graph-commit merge rides on the right-click popup of #428. Header chips superseded by #469.

**UI:** Sidebar header buttons (#414) are chips drawn on the section header row and hit-tested by the sidebar list (`SidebarHeaderButtons`); they act on the selected row, no row is chosen by the click. After a branch operation the graph cursor follows by saving the target commit as the tab's selection before the refresh. Header chips superseded by #469.

**Git:** Merge (#425): the UI passes a branch as its full ref; the writer uses the short name unless a tag of the same name would win, then the full ref with `-m` (git would write the full ref into the message).

**UI:** Rebase and reset by mouse (#428): header chips plus a right-click menu on graph and sidebar rows. A plain rebase's Confirm warns that merge commits are flattened (`--rebase-merges` stays with #399); details in ux.md workflow 3. Header chips superseded by #469.

**UI:** Reflog follow-ups (#424): the ref picker opens with `<space>l` inside the list (the graph key keeps opening HEAD in one step), `/` search lives in the list footer, and the mouse paths of an entry are action rows under the list (no right-click or header button: the overlay is modal and has no pane header).

**UI:** Cherry-pick and revert by mouse (#432): `[pick]` and `[revert]` header chips after `[rebase]`/`[reset]`, no Confirm (same as the keys). The mainline prompt of a merge commit stays key-only (#449). Header chips superseded by #469.

**UI:** Upstream by mouse (#436): `[upstream]` and `[unset-up]` chips close the Branches header, after `[revert]`, so narrow sidebars drop them first (right-click menu is the path); not on Tags. Header chips superseded by #469.

**Git:** Pick/revert cancelled or timed out midway (#439) is aborted like any other failure, so no half-applied sequence stays; the old tip is in the reflog. Why: the user cancelled the whole action, and a banner for a state they never chose is worse.

**UI:** Reflog mouse paths (#441): the four action rows and a ref picker row are mouse views in `DefaultMouseMap` (overlay views), so ux.md lists them; an entry click only highlights (a position, no action). Why: one table for every gesture.

**UI:** Mainline prompt by mouse (#449): a small overlay beside the status-line keys; a click on a parent row answers, any other click cancels. Supersedes the key-only note of #432.
## 2026-10-18

**Process:** M4 plan (owner): themes in M4 are light, dark and auto only, named themes are M5 (#61); the Windows CI leg (#155) is M4 and blocks release packaging (#48), the macOS leg and anything costly in CI minutes is M5 and runs on release tags only; interactive rebase refuses ranges with merge commits (`--rebase-merges` post-MVP) and supports `--root`; background remote calls never prompt, a credential failure offers a retry on the real terminal (#45); #399, #45 and #48 are `needs-review`.

**UI:** Which-key group rows (#443) read `+more (branch operations)` etc. for the named `<space>` leader groups; other prefixes stay plain `+more`. The table is hand-written (the keymap has no group metadata); a test checks every prefix exists. Why: the owner wants to see what a group holds before opening it.

**Process:** CI is manual-dispatch only (supersedes the 2026-10-16 CI cost entry): no PR, push or scheduled runs. Why: the free-plan minutes still ran out; local `dotnet build`/`dotnet test` are the gate. Dispatch CI (`gh workflow run ci.yml`) before closing a milestone or cutting a release. Revisit at v1.0.

**UI:** Which-key rows (#472) list plain actions first, then `+more` groups, each alphabetical by key label (lowercase before uppercase), instead of keymap declaration order. Why: stable, scannable rows.

**UI:** Prompts that ask for text or a choice are centred popups (#470): one `PromptPopup` record and frame builder, exposed by prompt handlers; the status bar keeps transient messages and find-as-you-type bars (search, jump, filters). Why: a status-bar input is not evident, especially for mouse-started actions. Supersedes the status bar prompts of the reflog entry.

**UI:** Pane focus by `Ctrl-h/j/k/l` (#473, owner): Alt clashes with multiplexers; same actions as `Ctrl-w h/j/k/l`, Normal mode only so Backspace/Enter in text entry are never stolen; `Ctrl-Backspace` is the Ctrl-h fallback.

**UI:** Row actions are a real popup menu, not the palette (#471, owner): `<space>.` or a click on the `...` marker of the selected row (#479) opens a menu under the row listing that row's actions with their keys; replaces the narrowed palette of #428. It covers every action of the sidebar header chips, which #469 drops. Details in ux.md, "Actions menu".

**UI:** Sidebar header chips removed (#469, owner): the actions menu (#471) is the mouse path of every sidebar action, so the section headers are plain again. Why: chips were dropped at narrow widths and duplicated the menu. Supersedes the chip entries of #414, #428, #432, #434, #436.

**UI:** No right-, middle- or Ctrl-click bindings (#479, owner): they feel like a desktop app and multiplexers may swallow them. The mouse path of the row actions menu is a `...` marker at the right edge of the selected graph or sidebar row (not click-on-selected-row: it clashes with sidebar activate and graph double-click). Supersedes the right-click wording of #428, #434, #436 and #471; #477 is moot.

**UI:** Graph row menu opens on double-click, the graph `...` marker is gone (#482, owner): double-click on a graph row runs `menu.open-for-row`; the details pane is reached by clicking it, `Enter` or `l`. The sidebar keeps its marker. Supersedes the graph part of the #479 entry. An "Open details" menu row was considered and not added (keeps the menu to keyed actions; #483 reshapes it). Why: the marker cost columns on every graph row.

**UI:** Graph double-click on blank space below the last row is ignored (#484): it would open the menu of the still-selected row. The "Open details" route for a hidden details pane (under 14 rows) is registered as #486.

**UI:** Palette and row menu list actions under subheaders (#483, owner), groups from `ActionCatalog.GroupOf` (shared with which-key, superseding the hand-written table of #443); the menu groups only above 5 actions and 2+ groups, help and which-key stay flat. The palette orders by group first, superseding view-first.

**UI:** The graph row menu ends with "Open details" (`Enter`, #486): the mouse route to the details pane, which is hidden under 14 rows and no longer reached by a double-click. Always listed, not only when hidden, so the menu does not change shape with the screen size. Supersedes the "Open details" sentence of the #482 entry.

**UI:** Settings (owner, 2026-10-18): one general settings screen (#465), no separate theme picker (#464 folded in); it writes the config file by a minimal text edit of the changed key (comments kept) and refuses to save a file that does not parse. **Stack:** config format stays JSON with comments and trailing commas (source-generated, AOT-safe); TOML and YAML rejected for now.

**UI:** Subheaders in the row menu and palette get a blank line before each but the first (#489, owner), like the help overlay sections. On a short screen the gaps go before any row: the menu drops them when gaps plus rows exceed its height, the scrolling palette when it has fewer than 8 list rows. Refines #483.

**UI:** Below 14 rows (no details pane) the graph row menu omits "Open details" and `Enter` shows a notice instead of moving focus (#490). Zooming was rejected: the pane is hidden by design and the graph is the useful view there. Supersedes the "always listed" sentence of the #486 entry.

**UI:** Merge commits (#95) keep showing the diff and file list against the first parent (what the merge brought into the branch); the Changes heading names that parent. Combined diffs and a per-parent switch were not added: a clean merge has an empty `--cc` diff, and a switch needs a new key (follow-up: #497).

**Git:** Remote calls (#44) set `GIT_SSH_COMMAND` with `-o BatchMode=yes` (the one env var beyond `LC_ALL` and `GIT_TERMINAL_PROMPT`): ssh prompts on the tty, not stdin. Pull is always `--no-rebase --ff-only`. Detail in `git-layer.md`.

**Git layer:** Working-tree watching (#83) is ignore-aware: one non-recursive watcher per directory that `ls-files --cached --others --exclude-standard` lists, none for ignored trees; a tab over 2048 directories polls. Supersedes the recursive working-tree watcher of the #27 entry. Detail in `git-layer.md`.

**UI:** Fetch, pull and push (#456): `<space>F`/`L`/`P` global, tab-row chips (100+ columns) and the Remotes header menu; progress is one live status-bar line, not history messages; `Esc` in the graph or `<space>C` (any pane, also the cancel chip) cancels. All-remotes fetch is the palette action `remote.fetch-all`, not a second key press.

**Process:** CI also runs on `v*` tags and weekly (Monday), besides dispatch (owner, 2026-10-18; supersedes the manual-dispatch-only entry above). The Windows leg (#155) is a second job in `ci.yml` on the same triggers, never per PR. Why: minutes are scarce but a Windows break must surface before release.

**UI:** `DisplayWidth` treats East Asian Width W/F (Unicode 16.0) as two columns, from a constant sorted range table with binary search (#99). Regional indicators and text-presentation symbols stay one column; sequences (flags, ZWJ) are not joined.

**Process:** The weekly CI schedule is dropped; CI runs on `v*` tags and manual dispatch only (owner, 2026-10-18; supersedes the weekly part of the entry above). The Windows test step is disabled (`if: false`) until #494 fixes its 43 failures; build, AOT publish and smoke still run. Why: a Windows run takes 10-11 min (about 20 billed) against 3 on Linux and minutes are scarce.

**UI:** Remote management (#400): `<space>Ra` `Rd` `Rn` `Ru` (global) add, remove, rename and re-point a remote; only remove asks (Confirm naming the tracking branches that vanish), rename and set URL are reversible config edits. Why: `<space>R` was free, and `git remote` keeps tracking branches consistent itself.

**UI:** Status messages (#500) are coloured by kind (success/info green, warning, error), drawn as a filled chip for 1.5 s, and expire: info 5 s, warning 15 s, error 30 s, by an injected clock in the state layer. Then the hints return; `<space>m` history keeps them. Why: a stale result must not look current.

**UI:** Files tab (#173): the whole tree is read at once, not per directory (timings in `architecture.md`). `h` in Detail is now `detail.collapse` (closes a directory, climbs to the parent, else back), `/` is `detail.filter`. `Enter` on a file does nothing: a content view is outside the MVP (asked in #511).

**UI:** Force push is `<space>Rf`, remote branch delete `<space>RD` (#457), not `<space>Pd`: the key engine always waits for a longer chord, so a binding that is complete and a prefix never fires. Force is only a lease behind a Confirm.


**UI:** Graph search `body:` (#97) asks `git log --grep` on demand and filters rows by the returned hit set. Why: bodies stay out of graph rows, so load time and memory are unchanged.

**UI:** Key config (#46): `config.json` has a `keys` list of entries (mode, view, key, action; `none` unbinds) applied over the defaults; each entry is validated alone and bad ones are skipped with a startup warning, so config never blocks startup. An entry that would make a key both complete and a chord prefix is refused (the engine always waits for a longer chord, #457). Detail in `ux.md`.

**Performance:** Cold-tab eviction (#199): only the 5 most recently used tabs keep their graph; older idle tabs reload on activation (view state stays in the workspace, search and diff position do not); a tab with a running operation is never evicted. Why 5: recent tabs stay instant, RSS stays bounded. Soft row limit deferred to #515 with numbers.

**UI:** Settings screen (#465): one overlay opened by `<space>,` or `:settings` edits `config.json` by a minimal text edit of the changed key (comments kept, unparsable file never written, atomic rename); the theme picker is an entry of it, not a second UI. Key bindings stay hand-edited. Design in `ux.md`.
