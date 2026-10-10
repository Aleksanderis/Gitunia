# Roadmap

Status: 2026-10-10. Milestone goals and exit criteria. Tasks are GitHub issues grouped by these milestones; order within a milestone comes from each issue's `Blocked by:` line.

## M0 - Technology spike (throwaway code allowed)

Goal: settle the renderer and prove Native AOT works for this app.

- Prove standalone launch: run from a non-repo directory with `gitunia <path>` and with no args.
- Skeleton solution (layout in `architecture.md`), `PublishAot=true`, analyzers on.
- Spike A: commit list from streamed `git log` with vim navigation in Terminal.Gui v2.
- Spike B: same in a minimal custom renderer (cell buffer, diff flush, VT input parser, mouse optional).
- Measure: AOT publish size, cold start, key-to-frame latency, resize handling, scroll on a 100k-commit repo.
- Done when: a decision is logged in `decisions.md` (renderer chosen), the build/test/publish commands are in `CLAUDE.md`, and spike code is either promoted or deleted.

## M1 - Read-only browser

Workspace first: repo picker, tabs (open/close/switch), recents/pins, session persistence, launch from any directory or with a path.
Graph view (incremental lanes), sidebar with branches/remotes/tags/worktrees, commit details, file list, diff viewer, search, command palette and keymap infrastructure.
Done when: you can open several repos in tabs from outside any repo, restart and get them back, and navigate a real mid-size repo entirely by keyboard and the graph matches `git log --graph` topology.

## M2 - Working tree

Status view, stage/unstage files, hunks, lines, commit, amend, discard with confirm.
Done when: a full commit cycle works without leaving the app, including partial staging.

## M3 - Branches and worktrees (the reason this project exists)

Branch create/rename/delete/checkout/upstream, merge, rebase, cherry-pick, revert; worktree add/switch/remove/lock/prune with dirty state across worktrees; stash.
Done when: the owner can use Gitunia for all daily branch and worktree work.

## M4 - Remotes and polish

Fetch/pull/push with progress and cancel, credential prompt handling, config file for keys/theme, packaging (AOT binaries per OS), docs for users.
Done when: usable as a daily driver on Linux; macOS/Windows binaries build in CI.

## Post-MVP candidates

Interactive rebase editor, blame, conflict resolution view, side-by-side diff, submodules, PR/issue integration.

## Open questions

- Terminal.Gui v2 vs custom renderer: M0 answers this.
- Interactive credential/ssh prompts: suspend the TUI and give git the terminal, or run with a pty and render prompts inline? Recommendation: suspend for fetch/pull/push when git needs input.
- Large-repo graph layout: incremental lanes vs precomputed via `commit-graph`; decide with spike data.
- Pane focus: `Tab` cycling vs `Ctrl-w` + direction vs both.
- Key style: per-view single keys for frequent actions plus a `<space>` leader for global ones (recommended) vs leader-only.
