# Gitunia

Keyboard-first terminal Git client (graph, branches, worktrees, vim-style keys). See README.md for goal and MVP scope.

Design docs are in `docs/` (architecture, git-layer, ux, testing, roadmap, decisions). Work is tracked as GitHub issues grouped by milestone M0-M4 (goals in `docs/roadmap.md`).

## Stack

- C# on .NET 10 (LTS), `net10.0`, SDK pinned in `global.json`. Code in `src/`, tests in `tests/` (layout in `docs/architecture.md`). Published as a single Native AOT binary; it must start in well under 100 ms.
- Git access: spawn the `git` CLI and parse its output; no libgit2 without a logged decision.
- UI: Terminal.Gui v2 (decided in M0, see `docs/decisions.md` for settings and pitfalls). Tests: xUnit with real `git` on temp repos.

## Rules

- Gitunia is a standalone multi-repo client with tabs. Never depend on the process working directory: every git call takes an explicit repo/worktree path, and all repo state is scoped to a tab. No static or global "current repo".
- Cross-platform (Linux, Windows, macOS; macOS lowest priority, Linux is built and tested first). No platform-specific APIs outside the platform abstraction; anything platform-specific gets a branch for every supported OS or an explicit "unsupported" path. Paths via `Path` APIs, no hardcoded separators, `/dev/null` or `/tmp`. Details: `docs/architecture.md`.
- Keep to the MVP scope in README.md. Don't add features beyond it without asking.
- Native AOT from day one: no reflection or runtime codegen, every dependency AOT/trimming-compatible. Run the AOT publish after adding one (details: `docs/architecture.md`).
- The Git layer returns typed results and never touches the UI. The state layer is pure (`(State, Command) -> State` plus effects) and runs without a terminal. Only the rendering layer touches the console. Layering details: `docs/architecture.md`.
- No `InternalsVisibleTo`: test through public API. If something needs testing, make it a small public type or parameter (e.g. an injected env lookup), not an internal exposed to tests.
- Never block the UI thread on a git process. Every git call is async, cancellable and has a timeout.
- Be conservative with destructive operations (reset --hard, force push, branch/worktree delete, stash drop, discarding changes): confirm in the UI and keep an undo path (reflog hint) where git allows.
- Respect the user's git config, hooks, credential helpers and worktree layout (`docs/git-layer.md` lists the only env vars we set).
- Names are spelled out: no truncated or single-letter names (`platform`, `dictionary`, `selection`, `buffer`, not `p`, `d`, `sel`, `buf`); loop indexes and well-known domain terms excepted. This applies to variables, parameters, members and test locals.
- Locals are declared with `var` (built-in types, apparent and non-apparent types alike); enforced as a warning (error in the build) by `.editorconfig`. Explicit types stay in signatures, fields and members.
- Member order: private methods come after all public/protected methods (fields, constructors and properties stay on top). Applies to test classes too: test methods first, private helpers last.
- One type per file, named after the type. Models (records, enums, plain data) and classes with logic live in separate files; unrelated models get separate files too, unless they are closely related or inherited from each other.
- Scripts (bash and PowerShell) live in `scripts/`; `tools/` holds small C# helper programs.
- Every key binding goes in the keymap table in `docs/ux.md` in the same change that adds it. No binding conflicts.
- Don't copy code from other projects verbatim. Ideas and git command patterns are fine; note the source in `docs/decisions.md` if it shaped a decision.

## Git conventions

- Branches: `feature/<issue>-<slug>` for new work, `fix/<issue>-<slug>` for bug fixes. Branch from `main`; when working autonomously, no direct commits to it: changes arrive through merged PRs. Reference the issue in the PR body (`Closes #<n>`).
- Ad-hoc requests from the owner: simple changes (e.g. a `.gitignore` entry, a doc tweak) go straight to `main`, no branch or PR. If it isn't obvious whether a change is simple enough, ask.
- Changes that address PR review comments (code, docs, skills, CLAUDE.md alike) all go through a PR branch, never straight to `main`. The straight-to-`main` exception above applies only to standalone requests from the owner.
- After a PR is merged, nothing of it remains: remove its worktree and delete the branch locally and on the remote (spike branches are the exception and stay).
- Agent identity: every commit, PR body and GitHub comment says which agent (orchestrator, implementer, reviewer) produced it: author-name convention, `Agent:` trailer, header and signature. Details in the `work-task` skill, "Agent identity". Never add the "🤖 Generated with Claude Code" line to PR bodies or comments.
- Commits: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`), optional scope, imperative subject.

## Which doc to read

Read the doc before working in its area; don't rely on memory of it.

| Working on | Read |
|---|---|
| Solution layout, layering, rendering, AOT, performance | `docs/architecture.md` |
| Running git, parsers, graph model, worktrees, refresh/watching | `docs/git-layer.md` |
| Screens, modes, tabs/repo picker, key bindings, workflows, mouse | `docs/ux.md` |
| Any test, fixtures, snapshot tests | `docs/testing.md` |
| Milestone goals and open questions | `docs/roadmap.md` |
| Anything you are about to ask the owner | `docs/decisions.md` first |
| Prior art and other clients (local only, gitignored; may be absent) | `docs/private/competitor-research.md` |
| Why a feature is in or out | `README.md` |

## Working autonomously

Follow the `work-task` skill (`.claude/skills/work-task/`) for the procedure. Rules that must hold regardless:

- Take the lowest-numbered unblocked open issue in the earliest open milestone (`Blocked by:` lines decide). Don't start M1+ until the M0 decision issue is closed.
- `spike`, `destructive` (can lose user data) and `needs-review` issues are never merged by an agent: spikes stay on unmerged `spike/` branches and close with findings; the others stop at an open PR for the owner. Keep working on other unblocked issues meanwhile.
- Every PR gets the reviewer's findings posted as a PR comment (and a follow-up comment on what was fixed); the review must be visible on GitHub, not only in the session.
- Independent unblocked issues run in parallel as subagents in separate worktrees (see the skill).
- Everything else: `gh pr merge <n> --merge --delete-branch` once CI is green and the reviewer (subagent) has no open blockers. Merge commits only, never squash or rebase, never force-push.
- Don't block on questions: decide from the docs or take the recommended default, log it in `docs/decisions.md` or `docs/roadmap.md` open questions, and say so in the PR.
- Never: spend money or create paid resources/accounts, delete branches you didn't create, touch anything outside this repo and the scratchpad, or experiment in the owner's real repos (use the `testing` skill's throwaway repos).
- Build, test and lint commands (run from the repo root):
  - Build (analyzers on, warnings are errors, also the lint step): `dotnet build`
  - Test: `dotnet test`
  - AOT publish (fails on any `IL*` warning): `dotnet publish src/Gitunia.Tui -c Release -o <outdir>`; smoke run: `<outdir>/gitunia`
  - Formatting is enforced by `.editorconfig` during build (`EnforceCodeStyleInBuild`).
