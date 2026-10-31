# Decisions

Short dated log of live, non-obvious settled choices that cut across docs. Add an entry only when the choice is not already evident from `ux.md` and its parts (`ux-*.md`), `architecture.md` or the code, or when it overrides an earlier decision; a behaviour change documented in `ux.md` needs none. Agents follow these without asking. Open questions live in `roadmap.md`. Superseded, closed and purely historical entries are archived word for word in `history/decisions.md` (not required reading).

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Git layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. The full text of long entries, implementation detail, measurements and limits live in `docs/history/` (moved there, same wording), the area docs (`architecture.md`, `git-layer.md`, `ux.md`, `testing.md`), or the issue or PR. When a decision changes, append a new entry that supersedes the old one; archive the old entry once the new one states the outcome (see 2026-10-30).

## 2026-10-10

**Stack:** C# on .NET with Native AOT (Rust was the runner-up). Why: the owner is a .NET developer doing heavy agentic development, so the code must stay reviewable by them.

**Git layer:** Git access via the `git` CLI, not libgit2. Why: it matches the user's real config, hooks, credentials and new git features such as worktrees.

**Architecture:** Standalone multi-repo client (tabs, picker, session restore), not bound to the current directory. Why: retrofitting multi-repo later is expensive, so the Git API is repository-scoped from the start.

**Platform:** Cross-platform from day one (Linux first; macOS lowest priority and droppable). Platform-specific code only behind a platform abstraction.

**Stack:** Minimum git 2.43, `net10.0`, JSON config with source generation, MIT licence, binary name `gitunia`, config in a `gitunia` dir (location: see the 2026-10-16 Platform entry). Why: LTS on current distros and AOT-safe.

**Process:** Other open-source Git clients are sources of ideas, never of copied code. Why: licence hygiene.

**Process:** Spikes live on unmerged `spike/` branches and close with findings; `needs-review` issues are merged by the owner while agents continue elsewhere.

**Process:** PRs are merged with a merge commit (never squash or rebase) and the branch is deleted afterwards. Why: the owner wants the full branch history visible in the graph.

**UI:** Renderer is Terminal.Gui v2 (2.5.0), not a custom renderer (M0 decision #7, owner chose after the spikes). Why: toolkit widgets are far easier to read and maintain, and it is fast enough; accepted costs are startup near the 100 ms budget, a larger binary and a native library. Numbers: `docs/history/architecture.md`.

## 2026-10-11

**Architecture:** The keymap engine (#19) is pure: bindings are rows with a view hierarchy, and a binding that is a strict prefix of a longer one is a conflict (no chord timeout). Key notation is the `ux.md` one.

**UI:** An Esc followed by a key within a few milliseconds arrives as `Alt-<key>` (terminal limitation); scripts must send them separately.

**UI:** Clipboard (#52) uses OSC 52 with env-based detection (injected lookup, tmux and screen passthrough). Why: no platform clipboard APIs; success is a best guess because OSC 52 has no acknowledgement.

## 2026-10-12

**Architecture:** Background tabs load lazily (#91, #111): only the active tab loads, idle preloading was rejected (first visit ~37 ms, ~0.5-1 MB per loaded tab). Background markers come from a status poller (#129).

**UI:** `Ctrl-j`, `Ctrl-^` and `Ctrl-h` arrive as other keys in Terminal.Gui 2.5.0 under tmux; `KeyTranslator` maps `Ctrl-j` and `Ctrl-^` back so the documented bindings work; `Ctrl-h` arrives as `Ctrl-Backspace` and is not mapped.

**UI:** Mouse is optional and the keyboard path stays complete; if Terminal.Gui's mouse is limited over tmux or ssh, document the limits.

**UI:** Drag, resize, buttons and overlays (#100) follow the same rule; drag-select is the existing Visual mode, and pane resizing has keys (`Ctrl-w > < + - =`) because nothing may be mouse-only.

**Git layer:** Background status polling (#135) uses `--untracked-files=normal`. Why: the tab bar only needs to know the tree is dirty, and it is far cheaper on trees with many untracked files.

## 2026-10-13

**UI:** The default palette (#216) is Catppuccin Mocha (dark) and Latte (light), colour values from https://catppuccin.com/palette (MIT, values only); Latte accents are darkened until they pass the contrast test.

**Process:** Screenshots of the real UI (#227) are made from a tmux capture of the live binary with `tools/AnsiToSvg`, local only, never in CI. Why: no extra dependency and it shows real key-driven states.

**Process:** Screenshots are hosted in the separate private repository `Aleksanderis/Gitunia-screenshots` (#229, owner decision); agents may push there with plain pushes. Why: the images' history can be reset without touching the code repository.

## 2026-10-14

**Process:** Decision entries are short: 1-3 lines (decision and why), a bold topic tag, a blank line between entries; detail lives in the area doc or the issue. Why: the log grew to ~62 KB and was hard to read. Done in #253.

**Code structure:** Folders group by domain first, technical kind second (`Models/` inside a domain); test folders mirror the code. Why: one flat folder with 72 files hides what belongs together. Done in #246.

**Code structure:** Pure parsers and formatters are static; classes with I/O, a clock or a cache are instances with constructor injection, wired by hand, no DI container (AOT). Why: static leaves no seam for fakes (#252).

**Code structure:** Git readers are instances holding a `GitRunner` and taking a `RepositoryLocation` per call; `GitServices.Create` is the hand-wired composition root and the Tui sees reader interfaces (#252).

## 2026-10-15

**UI:** User-visible text spells out "Repository", never "Repo" (sidebar title fallback, picker tag, docs; #262, owner decision). Stable ids such as `repo.pin` keep the short form.

**Code structure:** `RepositoryOperationDetector` and `FramePrinter` are instances, not static, so they can be faked (#269).

**Code structure:** Unused usings (IDE0005) fail the build; Roslyn needs `GenerateDocumentationFile` for it, so doc warnings CS1573/1591 are silenced (CS1574/1734 were re-enabled in #295). Small `Models/` folders (Refs, Watching, Execution) stay for consistency (#258).

**UI:** Status bar hints (#287, owner): wording stays short and is not derived from `ActionCatalog` titles; each hint names an `ActionId` per key and a test checks the default keymap binds the key to it.

**Git layer:** Non-UTF-8 paths (#94) are decoded surrogate-escaped and reach git on stdin, never as arguments; line patches of such files are refused and the files staged whole.

**UI:** Commit message editor (#314): an own `MessageEditor` in Core drawn by our renderer, not Terminal.Gui `TextView` (obsolete, its replacement is a separate package to vet for AOT); testable without a terminal.

**UI:** `q` always quits from any pane (#333); `h` and `Esc` step back. Supersedes "q goes back one level".

**UI:** Vim-safe keys (#335, owner): standard vim keys keep their vim meaning; Gitunia actions use `<space>` chords or `Ctrl-`/`Alt-` keys. Mapping in `docs/ux.md` ("Key rule").

**Git layer:** Hunk and line staging (#31): the file's diff is re-read with default context and the picked lines applied with `git apply --cached` (`-R` to unstage), never built from the rows on screen.

**UI:** Discard (#33) is always behind a Confirm dialog that states what is lost; no dangling stash; one file at a time.

## 2026-10-16

**UI:** vim-safe keys (#338, owner): diff display toggles move to the `<space>v` family (`w` `i` `f` `s` `l`), `@` to `<space>h`, `gr` to `<space>f`; `q` `h` `l` `]` `[` `?` `Y` `+` `-` `gd` `gc` `gf` stay as documented exceptions (read-only panes or navigation, muscle memory; list in `docs/ux.md`).

**Platform:** Untracked names that are not valid UTF-8 are deleted on Linux only, by raw bytes through libc `unlink`/`rmdir` (P/Invoke) after `git ls-files -o -z` lists them; Windows and macOS keep the refusal (#358). Why: process arguments cannot carry such bytes and .NET paths are strings.

**UI:** Drafts and quit (#369): a missing tab's draft moves to the replacing tab; `q` quits at once unless the final session save fails with an unsent message. Supersedes the quit-guard removal of #347.

**Platform:** Config and state dirs are the same on every OS: `$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, `$XDG_STATE_HOME/gitunia` or `~/.local/state/gitunia` (supersedes the per-OS dirs, e.g. `%APPDATA%`). Why: Gitunia is a terminal app, config is hand-edited and kept in dotfiles repositories, and config stays apart from machine-local state.

## 2026-10-17

**Git layer:** M3 calls that could open an editor pass `--no-edit` or `-c core.editor=true`. Pull is fast-forward only, fetch prunes, force push is `--force-with-lease` only. Cancel of fetch/pull/push is `Esc`, never `<space>x`.

**Git layer:** A call that runs or advances a merge, rebase, pick or revert returns `OperationResult` (`Completed` or `Conflicted`), decided in one place (`OperationRunner`); editor-capable calls pass `-c core.editor=true`. Why: #38-#40 and the interactive rebase reuse it instead of each guessing "did it stop?".

**Git:** Pick/revert cancelled or timed out midway (#439) is aborted like any other failure, so no half-applied sequence stays; the old tip is in the reflog. Why: the user cancelled the whole action, and a banner for a state they never chose is worse.

## 2026-10-18

**UI:** Prompts that ask for text or a choice are centred popups (#470): one `PromptPopup` record and frame builder, exposed by prompt handlers; the status bar keeps transient messages and find-as-you-type bars (search, jump, filters). Why: a status-bar input is not evident, especially for mouse-started actions. Supersedes the status bar prompts of the reflog entry.

**UI:** Pane focus by `Ctrl-h/j/k/l` (#473, owner): Alt clashes with multiplexers; same actions as `Ctrl-w h/j/k/l`, Normal mode only so Backspace/Enter in text entry are never stolen; `Ctrl-Backspace` is the Ctrl-h fallback.

**UI:** No right-, middle- or Ctrl-click bindings (#479, owner): they feel like a desktop app and multiplexers may swallow them. The mouse path of the row actions menu is a `...` marker at the right edge of the selected graph or sidebar row (not click-on-selected-row: it clashes with sidebar activate and graph double-click). Supersedes the right-click wording of #428, #434, #436 and #471; #477 is moot.

**UI:** Settings (owner, 2026-10-18): one general settings screen (#465), no separate theme picker (#464 folded in); it writes the config file by a minimal text edit of the changed key (comments kept) and refuses to save a file that does not parse. **Stack:** config format stays JSON with comments and trailing commas (source-generated, AOT-safe); TOML and YAML rejected for now.

**UI:** Below 14 rows (no details pane) the graph row menu omits "Open details" and `Enter` shows a notice instead of moving focus (#490). Zooming was rejected: the pane is hidden by design and the graph is the useful view there. Supersedes the "always listed" sentence of the #486 entry.

**Git layer:** Interactive rebase engine (#399) writes the todo itself and hands it to git through `-c sequence.editor` pointing at `gitunia --sequence-editor` (no new env var; a set `GIT_SEQUENCE_EDITOR` is refused); rewords are `exec git commit --amend -F`. Ranges with merge commits are refused and `--root` is supported (owner, 2026-10-18); details in git-layer.md.

**UI:** Merge commits (#95) keep showing the diff and file list against the first parent (what the merge brought into the branch); the Changes heading names that parent. Combined diffs and a per-parent switch were not added: a clean merge has an empty `--cc` diff, and a switch needs a new key (follow-up: #497).

**Git:** Remote calls (#44) set `GIT_SSH_COMMAND` with `-o BatchMode=yes` (the one env var beyond `LC_ALL` and `GIT_TERMINAL_PROMPT`): ssh prompts on the tty, not stdin. Pull is always `--no-rebase --ff-only`. Detail in `git-layer.md`.

**UI:** `DisplayWidth` treats East Asian Width W/F (Unicode 16.0) as two columns, from a constant sorted range table with binary search (#99). Regional indicators and text-presentation symbols stay one column; sequences (flags, ZWJ) are not joined.

**UI:** Force push is `<space>Rf`, remote branch delete `<space>RD` (#457), not `<space>Pd`: the key engine always waits for a longer chord, so a binding that is complete and a prefix never fires. Force is only a lease behind a Confirm.

**UI:** Credentials (#45): remote calls stay in the background with prompts off; on an auth failure a Confirm offers the same command with the terminal handed to git (TUI suspends like `$EDITOR`), not an inline pty. Why: ssh prompts on /dev/tty, so a pty needs a terminal emulator in the TUI. Detail: `git-layer.md`.

**Process:** Release packaging (#48) lives in `ci.yml`, tag runs only: each leg archives its AOT binary with `scripts/package.sh` (tar.gz, zip on Windows, plus SHA-256) and a `release` job attaches them to a draft release. Linux x64 and Windows x64 only; macOS and linux-arm64 follow with #455. Why: no extra workflow or build, so no extra minutes; draft so the owner publishes.

**UI:** Key config (#46): `config.json` has a `keys` list of entries (mode, view, key, action; `none` unbinds) applied over the defaults; each entry is validated alone and bad ones are skipped with a startup warning, so config never blocks startup. An entry that would make a key both complete and a chord prefix is refused (the engine always waits for a longer chord, #457). Detail in `ux.md`.

**Performance:** Cold-tab eviction (#199): only the 5 most recently used tabs keep their graph; older idle tabs reload on activation (view state stays in the workspace, search and diff position do not); a tab with a running operation is never evicted. Why 5: recent tabs stay instant, RSS stays bounded. Soft row limit deferred to #515 with numbers.

**UI:** Theme setting (#47): `theme` in `config.json` is `auto` (default, follows the detected terminal background), `dark` or `light`; it only forces which side of the existing Catppuccin Mocha/Latte and 16-colour tables is used. Unknown values warn and mean `auto`. `auto` still uses `COLORFGBG` only; the OSC 11 query is #527. Named themes stay in #61.

## 2026-10-19

**Architecture:** Config writer (#522): `ConfigWriter` (Core) sets one key through `ConfigTextEditor`, a pure minimal text edit on top of `ConfigKeyLocator.Locate` (last duplicate wins). Writes go through `AtomicFileWriter`; a file that does not parse is never written; a changed file (content hash) is reported as stale. Rules in `ux.md`, "Settings screen".

**UI:** Theme auto asks the terminal for its background with OSC 11 before the UI starts (#527), 50 ms timeout, falling back to `COLORFGBG`, then dark; skipped when not interactive and on Windows (unsupported there). Why: most terminals do not set `COLORFGBG`. Supersedes the COLORFGBG-only detection of #47.

**Testing:** Startup smoke test (#532): `TuiApplication.Run` runs headless on the `ansi` driver in `dotnet test` (public `DriverName` and `ExitAfterFirstIteration`, no InternalsVisibleTo). Why: two startup crashes passed green unit tests. Details: `testing.md`.

**Performance:** No throttle for the refresh-triggered `body:` re-run (#509): `log --grep` takes ~0.4-0.8 s on 100k commits, a restart cancels the earlier run and runs off the UI thread. Numbers: `performance.md`.

**UI:** A zero-width-joiner emoji sequence measures as one wide cell, as Terminal.Gui does for the sequences in `TerminalGuiWidthComparisonTests` (#547): `WidthCounter` joins an emoji to an emoji in `DisplayWidth.Of(string)`, the per-character wrap and fit loops and the title cells. Supersedes the "ZWJ sequences still differ" note in the #503 entry.

**Performance:** Soft row limit (#515, finishes #199 step 4): a tab loads its newest 100,000 commits and the rest on demand (`G`, search reaching the end, jumps). Measured on 1M commits: ~76 MB instead of ~390 MB until the end is asked for. Supersedes the "deferred to #515" part of the #199 entry. Details: `performance.md`.

**UI:** Bare-key exception for modal lists (owner, #549): the settings screen's `x` `a` `d` `J` `K` `+` `-` (#523) and the rebase editor's `p` `r` `s` `f` `d` `J` `K` (#458) stay bare, because a modal list is not a buffer and no vim normal-mode command applies there. Supersedes the #335 rule for these two views only; every other view keeps the rule.

**Process:** User guide (#50, `docs/user-guide.md`) holds a hand-written subset of the keymap and points to `docs/ux.md` for the full table, instead of generating a key reference; a rebinding of a listed key updates both.

## 2026-10-23

**UI:** Themes (#61) are JSON files (format and 16-colour rule: `docs/ux.md`, "Theme files"), built-ins embedded in the binary. Published values are nudged where they fail contrast (`docs/ux.md`).
**Git layer:** Interactive rebase keeps merge commits (#461; supersedes "interactive rebase refuses merges, `--rebase-merges` post-MVP"): git prepares the `--rebase-merges` todo, read through a sequence editor that prints it and fails; label, reset and merge lines are kept and only reordered. Why: the todo is git's to get right; cost is one aborted `rebase` start per read.

**Git layer:** Refresh polls by default (#543; supersedes the ignore-aware watcher of #83/#504 as the default): the active tab reads a fingerprint every 2 s and the status every 5 s, with back-off on slow repositories; file watching is opt-in (`watchFiles`). Why: each `FileSystemWatcher` takes a scarce inotify instance and other clients poll too. Detail: `git-layer.md`.
**UI:** File content follows the tree cursor (#635; supersedes "opened with `Enter`, closes when the commit or tab changes" in the #511 entry): beside the tree the pane is always there with a placeholder, a commit change shows the placeholder until the new tree loads, `Esc` returns focus and keeps the content; reads start 40 ms after the cursor rests so a held key does not spawn one per row.

## 2026-10-24

**UI:** Conflict editor (#689) is in the MVP, with per-hunk picks only (supersedes "post-MVP"). Hunks come from the markers in the working-tree file, not from `show :2:`/`:3:`, so hand edits are kept. Detail: `ux.md` workflow 4.
**Testing:** Windows screenshots use `scripts/screenshot.ps1` with resvg (`winget install linebender.resvg`, a single exe) instead of rsvg-convert (no native Windows build). Why: smallest dependency that renders the existing AnsiToSvg output with the same options; `tools/AnsiToSvg` gained `--in`/`--out` file options because redirected Windows stdio is not UTF-8.

## 2026-10-28

**UI:** File lists scroll the diff beside them with `D`/`U` (half page) and `J`/`K` (line) (#755, owner): bare `D`/`U` break the vim-clash rule on purpose, so `Ctrl-d`/`Ctrl-u` keep paging the focused list.

**UI:** Graph lane order (#757, owner): the branch with the newest tip is leftmost, the next newest to its right, with no exception for the checked-out branch (supersedes the lane 0 rule of #308). Why: the most compact graph; HEAD keeps its `◆` mark and label. The HEAD reads (`ReadHeadLayoutAsync`, the above-HEAD walk) are gone, one git call less per graph load.

## 2026-10-29

**Issue links** (#213, reopened by the owner; supersedes the source of the earlier #213 entry): the `[issuetracker "name"]` sections are read from the effective git config (repository, global, system) as well as from `.issuetracker`; a section of the same name in the file wins. Why: the owner's Jira section was not detected and other clients read it from the git config; the file as quoted in the issue already parsed. Cost: one more `git config --list -z` per load.

**UI:** Graph colours (#761, owner): one colour per branch name (remote prefix ignored), a hash of the name moved to a free slot. Why: colours that follow the name, not the position; local and remote of one branch match. Detail: `git-layer.md`.

**UI:** Graph lanes (#759, owner): a branch keeps one lane and colour from tip to fork and joins at the fork commit; names on a line are labels; where lanes meet the leftmost (the newest tip's) goes on to the bottom and the others, main included, bend into it. Why: the owner's reference image and review of #793. Detail: `ux.md`, `git-layer.md`.

**UI:** Bare `h`/`j`/`k`/`l` stay inside a pane, `Ctrl-h`/`Ctrl-j`/`Ctrl-k`/`Ctrl-l` move between panes everywhere (#834, owner, agreed in #748; supersedes the `h`/`l` navigation exception of #338 and the `l`/`h` focus moves of #198, finishes the commit-box part of #818). Bare `h`/`l` keep their within-pane meanings (fold and unfold a tree, collapse and expand the sidebar, cursor in the message box); `Enter` goes into a pane and `Esc` back. Modal screens that replace the panes (reflog, rebase todo, conflict editor, settings, help) keep `h` as close or back, since they are not panes. Detail: `ux.md`, "Focus model and key style".

## 2026-10-30

**UI:** Conflict editor redesign (#742, owner; supersedes the one-hunk-at-a-time editor of #689/#690/#726 and the `B` base toggle, the `]c`/`[c` and `Ctrl-d`/`Ctrl-u` side scrolling, `b` for both and the under-width stacked layouts), modelled on KDiff3: full screen (the frame covers the tab row and the banner, the status bar stays), the whole file in base, ours and theirs side by side (rows aligned, filler rows pad the shorter versions) and the whole result below as a real text editor (the commit box's `MessageEditor` and Normal/Insert keys). The cursor moves like the working-tree diff (`j`/`k`, `n`/`N` conflict jumps, `v` Visual lines) and `<space>a` takes the pane's version (or the selected lines), the staging key; `o` `t` `b` `B` `O` `T` stay as shortcuts (`b` is now the base, `B` both). Why: the owner's request; a hunk with two lines of context could not show where a conflict sits in the file or let the user mix lines.
**UI:** Typing in the result and picks (#742): each conflict is a tracked region of the result (a line diff after every key moves the regions after the change; a change inside a region makes it `edited`; lines opened just before or after a region stay outside it). A later pick or reset on a conflict replaces its region wholesale (undo brings the typed text back); picks on other conflicts and text outside regions are never touched; an edited conflict counts as resolved once none of its lines is a conflict marker. A Visual line pick replaces an unresolved conflict's markers and is appended to a conflict that already has lines. Why: the conservative reading of "manual edits survive": edits are lost only where the user picks again on the very conflict they edited, and one `u` restores them. Line endings: the result is written with the file's dominant ending and keeps a missing final newline; a file that mixes CRLF and LF is written in the dominant one everywhere and the status bar warns when it opens (an editor with one line list cannot keep each line's own ending; refusing such files was the alternative).
**UI:** Conflict editor keymap views (#742): `ConflictEditor` (all panes), `ConflictSide` (the three versions) and `ConflictResult` (the result editor, reusing the message box's `working-tree.message-*` actions, retitled "Message box and conflict result"). `n`/`N` and `r` are bound bare in the result pane on purpose: the editor has no search or replace-character command. Narrow terminals show two (from 66 columns) or one version pane and `Tab` switches. Not in this change: clicking a pane row (#861; the wheel and the chips cover the mouse) and horizontal scrolling of long lines in the version panes (#862; they are cut with an ellipsis).
**UI:** Conflict editor interaction (#742, owner feedback; supersedes parts of the keymap entry above, which stays as history): one focus concept, so the `[taken]` marker on a version pane's header is gone and the state of a conflict sits on the conflict itself (margin marks: a check on the version taken, a dot on the others, `~` for picked lines or typed text, `?` unresolved) and in the position strip; `<space>u` untakes as `<space>a` takes (the whole conflict, or the selected lines in Visual) and replaces `r`, whose key is freed (`u` stays vim's undo); a click on a header, version row or result line moves the cursor and focus there and a double-click on a conflict row of a version pane takes that version, because a click selects and a double-click acts everywhere else in the mouse model (clicking a pane row, #861, is done); `e` is kept and renamed "edit in result" (it already moved into the result at the conflict in Insert mode; the unclear part was its name and the missing feedback). Why: owner feedback after trying it.
**UI:** Branch chips are coloured per branch, not per lane (#849, owner; supersedes the "label takes its lane's colour" rule of #759/#760 for tips in the middle of another branch's lane). Different branches get different colours even when stacked on one lane (`test` on `main`); only a local branch and its remote-tracking branches of the same short name share one. Lane line and node keep the lane colour. Why: the owner read same-coloured chips as related branches. Detail: `ux.md`, `git-layer.md`.

**UI:** Keymap rework design (#833, proposal until the owner approves the tables in the PR; built by #839 and #840): one noun per `<space>` group (`b` branch with rebase as `br`, `t` tag, `w` worktree, `k` stash, `r` remote, `o` repositories and tabs, `g` operation in progress, `e` external tools, `v` view options), no unrelated lowercase/uppercase pairs, aliases on one row, Everyday/Advanced/Advanced+ level per action (hidden, never disabled; default Everyday), fast toggle `<space>*` cycling the levels. Resolves #834 (nothing to change: `Ctrl-h/j/k/l` main keys of pane focus, bare `h`/`l` in-pane) and #742 (`conflict.open` moves from `<space>to` to `<space>gr`). Stash gets `k` because `s` stays the working tree and `S` would be an unrelated pair. Tags and worktrees keep their own groups instead of moving under `b` (deviation from the issue wording, listed for the owner). Existing `keys` entries bind keys to action ids, and no id changes. Detail and the tables: `ux.md`, "Keymap rework".

**Docs:** `docs/ux.md` is split into flat files (#860, owner): `ux.md` (index, principles, workspace, modes, focus), `ux-workflows.md`, `ux-keymap.md`, `ux-mouse.md`, `ux-settings.md`, `ux-look.md` (look, colours, config file); no subfolder. Why: the single file was over 2000 lines; the tests read the keymap, mouse and settings tables from their own files.

**Docs:** Closed decisions are archived (#865, owner-reviewed audit): an entry that is superseded, closed or purely historical moves word for word, under its dated heading and in order, from `decisions.md` to `history/decisions.md` (flat file, no subfolder); a superseding chain keeps the latest entry live and archives the older ones only when the live entry states the outcome. `scripts/check-diff.sh` accepts a removal from `decisions.md` only when the same line is added to `history/decisions.md` in the same diff. Why: the log passed 480 lines and agents read it before asking the owner anything.

**Keymap:** The default key bindings live in an embedded data file, `src/Gitunia.Core/Keymap/keymap.default.json` (#857, owner), read by `DefaultKeymap` with `Utf8JsonReader` (hand-written reader, no reflection, AOT-safe; source-generated System.Text.Json was not needed). One row per action, view and mode set: `{action, view, modes, keys}` with the main key first and aliases after; it is a superset of the user's `keys` entry (`mode`, `view`, `key`, `action`) with plural `modes` and `keys`; aligning the two (for example accepting `keys` lists in the config) is left to #840/#838. Other properties (level, label, group, help cluster) are skipped by the reader and added by the consumers (#838 help, #839 level) so each field arrives with the code that reads it. Why: the key layout change of #840 is written once, in the data file. Parity with the former C# tables was checked binding for binding (550 identical mode, view, key, action tuples at the time, 551 with the Ctrl-c row of #856; the test `KeymapRowReaderTests` keeps that listing as a snapshot). The view hierarchy (`ViewParents`) stays in code. Build-time C# generation was not needed: parsing is far below the startup budget (`performance.md`).

**UI:** Conflict editor keys beyond the #847 table (#840, owner): take-both is `m` (was `B`) and re-create is `Ctrl-x` (was `R`); `b` stays take-base. Why: the Shift-letter pairs were easy to hit by mistake and `R` is destructive. Existing `keys` entries are unaffected (they bind action ids).

## 2026-10-31

**Process:** Release tags only package (#899): the `v*` legs skip the build and test steps (the AOT publish still builds with warnings as errors), and the `release` job fails unless a successful `workflow_dispatch` run of `ci.yml` with both legs green exists for the tagged commit. Before tagging, run `gh workflow run ci.yml` and wait for green. Why: Native AOT cannot cross-compile, so the legs stay separate, but repeating the slow Windows suite on every tag buys nothing.

## 2026-11-01

**Process:** The project keeps the name "Gitunia" (#896, owner). Why: the same-named macOS client has little traction, is single-platform and open source, so the collision risk is small and a rename stays cheap before the first release. Re-check that client and the package registries right before the repository goes public; `gitkeel` is the fallback.

**Process:** Supersedes the reasoning of the 2026-11-01 name entry (#919, owner): the third-party description is dropped; the decision stands, the name "Gitunia" is kept and `gitkeel` stays the fallback. Check the package registries right before the repository goes public.
