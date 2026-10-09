# Gitunia

Keyboard-first terminal Git client (graph, branches, worktrees, vim-style keys). See README.md for goal and MVP scope. Pre-spike: there is no code yet, so build/test commands don't exist.

Design docs are in `docs/` (architecture, git-layer, ux, testing, roadmap, decisions). Work is tracked in `docs/roadmap.md` (milestones M0-M4) until a GitHub remote exists.

## Stack

- C# on current .NET LTS. Published as a single Native AOT binary; it must start in well under 100 ms.
- Git access: spawn the `git` CLI and parse its output. Don't use libgit2/LibGit2Sharp for anything that writes, and don't add it for reads without a decision logged in `docs/decisions.md`.
- UI: pure state/model layer plus a thin rendering layer. The renderer choice (Terminal.Gui v2 or a custom cell-buffer renderer) is decided by the M0 spike.
- Tests: xUnit; real `git` against temporary repos for the Git layer.

## Rules

- Gitunia is a standalone multi-repo client with tabs. Never depend on the process working directory: every git call takes an explicit repo/worktree path, and all repo state is scoped to a tab. No static or global "current repo".
- Keep to the MVP scope in README.md. Don't add features beyond it without asking.
- Native AOT from day one. No reflection-based serialization or DI containers that need runtime codegen; use source generators (`System.Text.Json` source gen, etc.). Every new dependency must be AOT/trimming-compatible; run the AOT publish before adding it to main.
- The Git layer returns typed results and never touches the UI. The state layer is pure (`(State, Command) -> State` plus effects) and runs without a terminal. Only the rendering layer touches the console. Don't break this layering; it is what makes agent work testable.
- Never block the UI thread on a git process. Every git call is async, cancellable and has a timeout.
- Be conservative with destructive operations (reset --hard, force push, branch/worktree delete, stash drop, discarding changes): confirm in the UI and keep an undo path (reflog hint) where git allows.
- Respect the user's git config, hooks, credential helpers and worktree layout. Don't set `GIT_*` env vars beyond what `docs/git-layer.md` lists.
- Every key binding goes in the keymap table in `docs/ux.md` in the same change that adds it. No binding conflicts.
- Don't copy code from other projects verbatim. Ideas and git command patterns are fine; note the source in `docs/decisions.md` if it shaped a decision.

## Git conventions

- Branches: `feature/<slug>` for new work, `fix/<slug>` for bug fixes. Branch from `master`; never commit to it directly. Once issues exist, use `feature/<issue>-<slug>`.
- Commits: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`), optional scope, imperative subject.

## Which doc to read

Read the doc before working in its area; don't rely on memory of it.

| Working on | Read |
|---|---|
| Solution layout, layering, rendering, AOT, performance | `docs/architecture.md` |
| Running git, parsers, graph model, worktrees, refresh/watching | `docs/git-layer.md` |
| Screens, modes, tabs/repo picker, key bindings, workflows, mouse | `docs/ux.md` |
| Any test, fixtures, snapshot tests | `docs/testing.md` |
| Milestone order and "done when" | `docs/roadmap.md` |
| Anything you are about to ask the owner | `docs/decisions.md` first |
| Prior art and other clients (local only, gitignored; may be absent) | `docs/private/competitor-research.md` |
| Why a feature is in or out | `README.md` |

## Working autonomously

Use the `work-task` skill (`.claude/skills/work-task/`). Hard rules:

- Take tasks from `docs/roadmap.md` in milestone order. Don't skip ahead of M0 (the spike) until its decision is logged.
- Decisions go in `docs/decisions.md` (and the relevant area doc). Open questions go in the "Open questions" section of `docs/roadmap.md`. Don't block on them; take the recommended default and say so.
- Use the `reviewer` subagent before declaring a task done.
- Use the `git-fixture` skill to create throwaway repos for tests and manual checks; never experiment on the user's real repos.
- Build, test and lint commands: add them here once the solution exists (M0).
