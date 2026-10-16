# Decisions

Short dated log of settled choices that cut across docs. Agents follow these without asking. Open questions live in `roadmap.md`.

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Git layer**, **UI**, **Testing**, **Process**, **Code structure**), 1-3 lines (decision and why), a blank line between entries. Implementation detail, measurements and limits go in the area doc (`architecture.md`, `git-layer.md`, `ux.md`, `testing.md`, or `docs/history/` for the why and the measurements) or stay in the issue or PR. When a decision changes, append a new entry that supersedes the old one.

## 2026-10-10

**Stack:** C# on .NET with Native AOT (Rust was the runner-up). Why: the owner is a .NET developer doing heavy agentic development, so the code must stay reviewable by them.

**Git layer:** Git access via the `git` CLI, not libgit2. Why: it matches the user's real config, hooks, credentials and new git features such as worktrees.

**Architecture:** Standalone multi-repo client (tabs, picker, session restore), not bound to the current directory. Why: retrofitting multi-repo later is expensive, so the Git API is repository-scoped from the start.

**Platform:** Cross-platform from day one (Linux first; macOS lowest priority and droppable). Platform-specific code only behind a platform abstraction.

**Stack:** Minimum git 2.43, `net10.0`, JSON config with source generation, MIT licence, binary name `gitunia`, config under the platform config dir `gitunia`. Why: LTS on current distros and AOT-safe.

**Architecture:** `Gitunia.Platform` holds config/state dirs and console capability detection, so Core stays free of OS code. SDK pinned to 10.0.400 (`rollForward: latestFeature`), test projects use xUnit v3, underscores allowed in test names.

**UI:** External tools (`$EDITOR`, difftool, mergetool) get the terminal by suspending the TUI; clipboard via OSC 52; commit messages use a built-in Insert-mode editor or `$EDITOR` (2026-10-15).

**Process:** Other open-source Git clients are sources of ideas, never of copied code. Why: licence hygiene.

**Process:** Spikes live on unmerged `spike/` branches and close with findings; `destructive` and `needs-review` issues are merged by the owner while agents continue elsewhere.

**Process:** PRs are merged with a merge commit (never squash or rebase) and the branch is deleted afterwards. Why: the owner wants the full branch history visible in the graph.

**UI:** Renderer is Terminal.Gui v2 (2.5.0), not a custom renderer (M0 decision #7, owner chose after the spikes). Why: toolkit widgets are far easier to read and maintain, and it is fast enough; accepted costs are startup near the 100 ms budget, a larger binary and a native library. Numbers: `docs/history/architecture.md`.

**UI:** Terminal.Gui needs `Application.MaximumIterationsPerSecond` ~100 (default 25 gave ~34 ms key latency), an append-only list source for streamed commits, and key handling at the application level. Why and further pitfalls: `docs/history/architecture.md`.

**Git layer:** Standalone launch resolves paths with `git -C <path> rev-parse` and dedupes tabs by the resolved toplevel (spike #3). Detail: `docs/history/git-layer.md`.

**Architecture:** The renderer stays swappable but only Terminal.Gui is maintained; `src/Gitunia.CustomRenderTui` is a parked future option, not referenced by the app and only kept building in CI. Terminal.Gui types stay in the rendering project and no renderer interface is added until a second renderer is revived.

## 2026-10-11

**UI:** The Terminal.Gui spike (#4) is kept as a reference sample in `samples/Gitunia.TerminalGuiSpike` (tag `archive/spike-4-terminal-gui-heavy`), unmaintained; the custom demo's look is achievable in Terminal.Gui.

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

**UI:** Diff extras (#106): word wrap is real wrapping, "Load anyway" (`L`) lifts the row caps, and the diff follows the graph selection. Dropped: progressive display while streaming and binary content.

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

**Code structure:** Pure parsers and formatters stay static; classes with I/O, a clock or a cache are instances with constructor injection, wired by hand, with interfaces only where another layer needs a fake and no DI container. Why: static leaves no seam for effect tests; a reflection-based container conflicts with AOT and startup. Tracked in #252.

**Code structure:** The git readers are instance classes that hold a `GitRunner` and take a `RepositoryLocation` per call; the `Repository` handle is gone. `GitServices.Create` is the hand-wired composition root, the Tui sees reader interfaces; the details store, diff store and status poller tests use fakes, the rest follow in #269. Startup is unchanged. Done in #252.

**UI:** Key notation is the spelled-out `Ctrl-d` / `Alt-1` form everywhere, and keys and descriptions sit in separate columns (help) or as key plus description pairs (status hints) (#242). Why: `^d` and bare key lists were unreadable without knowing the keys.

**UI:** Inactive repository tabs are buttons: a chip on its own tint (`PaletteTable.TabButtonBackground`, visibly apart from the bar in both schemes), underlined without colours, bracketed in ASCII. Why: plain muted text did not read as clickable (#240).

**UI:** Graph (#238): a commit with one child and one parent on its own lane is drawn as part of the line (`GraphGlyphSet.LineNode`, `│` / `|`), node dots only where a lane starts, ends, forks or merges. Why: runs of commits on one branch read as a line, dots mark the branch structure. Terminal-drawn glyph tiers stay in #154. Superseded in part by #280 below.

**UI:** The detail list view paints cells its rows leave blank in the panel colour, also with the cursor on the last row (#243); a frame test cannot see this, so it is checked in a real terminal and screenshots.

**UI:** `Enter` on the Details or Files tab keeps showing a notice instead of switching to Changes (#190, owner decision). Why: the tab switch is explicit (`1`-`3`, click), a key that jumps tabs would surprise.

## 2026-10-15

**UI:** User-visible text spells out "Repository", never "Repo" (sidebar title fallback, picker tag, docs; #262, owner decision). Stable ids such as `repo.pin` keep the short form.

**UI:** Graph (#280, owner): a commit that carries any ref (branch, tag, remote, HEAD) keeps the node dot; only unlabelled commits become the line. Why: the line glyph under a label read as missing.

**UI:** Palette (#96, owner did not answer, recommended default taken): modal-only actions (`confirm.*`, `picker.*` except open, palette/input accept, `Esc`) are hidden; actions bound before their feature are listed greyed out. Keeps the palette to what a user can run.

**Code structure:** `RepositoryOperationDetector` (behind `IRepositoryOperationDetector`, injected into `RepositoryRefresher`) and `FramePrinter` (takes `GitServices`) are instances, not static (#269). Why: classes with I/O are instances so the operation section can be faked; static was only there to satisfy CA1822. Every reader interface has a fake (the detector is faked inline in the refresher test).

**Code structure:** Unused usings (IDE0005) fail the build; Roslyn needs `GenerateDocumentationFile` for it, so doc warnings CS1573/1591 are silenced (CS1574/1734 were re-enabled in #295). Small `Models/` folders (Refs, Watching, Execution) stay for consistency (#258).

**UI:** Status bar hints (#287, owner): wording stays short and is not derived from `ActionCatalog` titles; each hint names an `ActionId` per key and a test checks the default keymap binds the key to it.

**UI:** Graph lane 0 (#301, owner): when a branch is checked out, lane 0 is reserved for the first-parent chain of its tip, so the current branch is a straight shaft on the left and other branches join it from the right. A detached or unborn HEAD keeps the leftmost-free-lane layout. Why: other tips listed first (`origin/main`, merged features) pushed the current branch off the left edge and bent it. Amended (#307, owner): a tip directly above the chain (first parent is HEAD, e.g. `origin/main` one commit ahead) also takes lane 0, so the shaft is not bent at the top; deeper ahead branches still bend in (the streamed layout cannot look ahead).

**UI:** Bottom pane tabs (#300): inactive tabs reuse the repository tab chip role (`TabButton` removed); the selected tab of an unfocused pane has its own role (`SelectedTab`: bold on the chip, bold underline without colours), so it stays distinct in every tier.

**UI:** Diff title buttons (#302, owner): chips like the tabs with words (`‹ file`, `hunk ›`, `wrap`, `zoom`) instead of key-like brackets; the `[x]` close button is gone (the diff is part of the Changes tab).

**UI:** Pane padding (#244, owner): two blank columns inside the frame for the History list and the bottom detail and diff panes from 100 columns of screen width, one below (the sidebar keeps one). The graph's own two-column right margin stays, so History has four blank columns at its right end on wide screens (kept on purpose, #301 owns the lane layout). The diff title has no hash (the detail title shows it).

**UI:** Graph lane 0 and ahead branches (#308, owner follow-up to #307): commits above HEAD whose first-parent chain reaches it continue the lane 0 shaft upwards, so a branch ahead by any number of commits (for example `origin/main`) is straight too; this supersedes "deeper ahead branches still bend in". Why: the topmost branch should run straight and leftmost; the layout gets the set from one extra git run (cost in `docs/performance.md`).

**UI:** The diff title row has no `‹ file` / `file ›` buttons (#310): files are chosen in the file list, `[f`/`]f` stay. Buttons count every click of a quick burst (the toolkit reports the second and third as double and triple click); a triple click elsewhere stays ignored.

**UI:** Commit messages (#32, owner): a built-in Insert-mode editor and `$EDITOR` hand-off (#51) both exist, as in most git clients. Supersedes "no built-in text editor". Working tree is its own view: unstaged and staged lists left, diff right, message box below with SignOff, No-Verify and Amend options (#28).

**UI:** Diff options (#312, owner): context lines, whole files and ignore-whitespace can be changed from the diff pane, per tab and not saved (keys in `docs/ux.md`). Side-by-side (README says "later") and visible whitespace are split off as follow-ups, not part of this change.

**UI:** Diff options follow-up (#312, #315): every option has a title button; whole-file mode keeps per-group hunk navigation; side-by-side (`S`) is in, unified stays when the pane is under 80 columns.

**Git layer:** Non-UTF-8 paths (#94): status output is decoded surrogate-escaped (`GitPathText`) and paths reach git on stdin (`--pathspec-from-file=-`), never as arguments. Line and hunk patches of files with invalid UTF-8 are refused (`InexactPatchException`); such files are staged whole. Affects #30, #31, #33.

**UI:** Working-tree view keys (#28) are designed in workflow 2 of `docs/ux.md` and stay out of the Keymap table until implemented, because the keymap test requires every row there to be bound.

**UI:** Working-tree view (#29): the view mode is tab state (`RepositoryViewState.Mode`) and is not saved with the session, so a restart opens the history; Unstaged shows conflicted files first, applied in `WorkingTreeState` so selection indexes stay row indexes. Diff and message box are placeholder boxes until the working-tree diff and #314 exist.

**UI:** External tools (#51): `git commit` (git picks the editor), `git difftool` and `git mergetool` run with the real terminal; the TUI suspends by disposing the Terminal.Gui application and creating a fresh one afterwards (see `docs/architecture.md`, "External tools"). Interim keys in the graph: `D` difftool of the commit, `M` mergetool, `E` commit with the editor; the working-tree view (#28) adds `ce` on the same hand-off.

**UI:** Commit message editor (#314): an own `MessageEditor` in Core drawn by our renderer, not Terminal.Gui `TextView` (obsolete, its replacement is a separate package to vet for AOT); testable without a terminal.

**UI:** `q` always quits from any pane (it first closes an open overlay or ends a Visual range); `h` and `Esc` step back a pane (#333). Supersedes the "q goes back one level" rule: a key that sometimes only moves focus reads as a bug. Working-tree view keys in workflow 2 must not bind `q` to leaving.

**UI:** Vim-safe keys (#335, owner): standard vim/neovim keys keep their vim meaning; Gitunia-specific actions use `<space>` leader chords or `Ctrl-`/`Alt-` keys, never a bare vim command. Supersedes the bare keys of the earlier working-tree and external-tool entries; mapping in `docs/ux.md` ("Key rule"); `<space>j` is reserved for the quick jump (#334).

**UI:** Stage and unstage (#30): stage all takes everything in Unstaged except conflicted files (skipped and counted), untracked files included; one index operation per tab, and the next action waits for the status re-read so the cursor keeps its index. Paths are always literal (--literal-pathspecs). Open: Visual ranges #340, resolve a conflicted file #341.

**UI:** Working-tree diff (#330): `q` quits in the diff as everywhere (#333), `h`/`Esc` go back; a conflicted file shows git's combined diff folded into one old/new pair (the per-parent columns are dropped), so it does not literally equal `git diff`; a path that is not valid UTF-8 shows a note because `git diff` reads no pathspec file (#344); a failed read is retried only when the status is read again or the options change.
