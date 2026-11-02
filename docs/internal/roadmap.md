# Roadmap

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Milestone goals and exit criteria. Tasks are GitHub issues grouped by these milestones; order within a milestone comes from each issue's `Blocked by:` line. The status line under each heading is updated when a milestone changes state; the open issues are the source of truth.

## M0 - Technology spike (throwaway code allowed)

Status: done. Terminal.Gui v2 chosen (`decisions.md`, 2026-10-10); spike code deleted.

Goal: settle the renderer and prove Native AOT works for this app.

- Prove standalone launch: run from a non-repository directory with `gitunia <path>` and with no args.
- Skeleton solution (layout in `architecture.md`), `PublishAot=true`, analyzers on.
- Spike A: commit list from streamed `git log` with vim navigation in Terminal.Gui v2.
- Spike B: same in a minimal custom renderer (cell buffer, diff flush, VT input parser, mouse optional).
- Measure: AOT publish size, cold start, key-to-frame latency, resize handling, scroll on a 100k-commit repository.
- Done when: a decision is logged in `decisions.md` (renderer chosen), the build/test/publish commands are in `CLAUDE.md`, and spike code is either promoted or deleted.

## M1 - Read-only browser

Status: done.

Workspace first: repository picker, tabs (open/close/switch), recents/pins, session persistence, launch from any directory or with a path.
Graph view (incremental lanes), sidebar with branches/remotes/tags/worktrees, commit details, file list, diff viewer, search, command palette and keymap infrastructure.
Done when: you can open several repositories in tabs from outside any repository, restart and get them back, and navigate a real mid-size repository entirely by keyboard and the graph matches `git log --graph` topology.

## M2 - Working tree

Status: done.

Status view, stage/unstage files, hunks, lines, commit, amend, discard with confirm.
Done when: a full commit cycle works without leaving the app, including partial staging.

## M3 - Branches and worktrees (the reason this project exists)

Status: done (key flows in `docs/ux-workflows.md`, workflows 3-7).

Branch create/rename/delete/checkout/upstream/reset, merge, rebase, cherry-pick, revert; worktree add/switch/remove/lock/prune with dirty state across worktrees; stash; tags (create/delete/push); reflog view.
Done when: the owner can use Gitunia for all daily branch and worktree work.

## M4 - Remotes and polish

Status: done.

Fetch/pull/push with progress and cancel (keys and buttons, prune, force-with-lease, delete remote branch), interactive rebase editor (reorder/reword/squash/fixup/drop), remote management, clone/init, credential prompt handling, config file for keys/theme, packaging (AOT binaries per OS), docs for users.
Done when: usable as a daily driver on Linux; Windows binaries build in CI (macOS CI and binary are in milestone M6, #455).

## M5 - Keymap and discoverability

Status: done (tracking issue #867).

Goal (#833, owner feedback): fewer keys to learn and every command findable. New leader layout (one prefix per noun, no unrelated case pairs, tabs and repositories together), three command levels (Everyday, Advanced, Advanced+) with a fast toggle, help with all-commands tabs and a Custom tab, optional hints, the keymap as a data file with a generated reference, Ctrl-c always quits, key customisation in the user guide.
Done when: the owner approved the new layout (#840), `?` lists every bound command once, hints and advanced commands can be turned off, and the keymap reference is generated from the data file.

## M6 - Additional, unreviewed

Status: in progress (tracking issue #893; the open issues are the source of truth).

Backlog of reviewed owner items, release and packaging: help search and wording, stash file restore, older-console readability, release version stamp and packaging flow, idle CPU measurement, conflict editor tests, manual and Windows checks, macOS CI and binary (#455), package managers (#62).
Done when: the first `v*` tag produces verified release archives (#517) and the listed owner checks are done.

## M7 - Effects-as-data architecture

Status: planned (tracking issue #990; the open issues are the source of truth). Owner decision 2026-11-01, grand code review.

Goal: all app logic in pure Core reducers (state + command -> state + effects), git and file work run by one effect runner in a new `Gitunia.App` project, `Gitunia.Tui` limited to views, rendering and input translation. Removes delegate dependencies (#965), `ActionRouter`, the `TuiApplication` god class and the largest controllers. The design spec (#967, `docs/architecture.md`, "Effects as data") writes down the architecture the owner approved and is merged before migration starts; no owner review gate on its PR.
Done when: every M7 issue is closed; feature logic is covered by synchronous Core tests; no delegate dependencies, late `null!` wiring, constructors over five parameters or files over ~500 lines in `src/`; startup and key latency unchanged.

## M8 - Going public

Status: in progress (tracking issue #903; the open issues are the source of truth). Comes after M7 (owner, 2026-11-01): the code is reworked before it is published.

Publishing the repository and the release: pre-publication check and contributor docs (#902, done), landing page (#900), package managers (#62: winget #943, AUR #944, Arch package #966, Homebrew #945), macOS signing (#952), the release plan (#749, blocked by M7's tracking issue #990).
Done when: the repository is public and the first release is installable through the listed package managers.

## M9 - History and blame

Status: planned (owner request 2026-11-02; the open issues are the source of truth, tracking issue listed there).

Goal: answer "how did this file or these lines get here" without leaving the app: file history (`git log --follow`), line-range history (`git log -L`) and blame (`git blame`), each with a diff and a jump to the commit in the graph. This moves blame out of the README's "Not included" list; the README is updated when the feature ships, not before. New Git readers and parsers, Core state/commands/effects, views, keymap rows, large-file performance, docs and Windows/macOS checks.
Done when: from the graph's commit file list or the Changes view you can open a file's history, a line range's history and a blame view of a large file by keyboard and mouse, jump from a blame line to its commit in the graph, and the user guide, keymap reference and README describe it.

## Post-MVP candidates

Submodules, PR/issue integration (blame shipped in #1117).

## Open questions

