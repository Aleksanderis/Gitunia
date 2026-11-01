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

## Post-MVP candidates

Blame, submodules, PR/issue integration.

## Open questions

