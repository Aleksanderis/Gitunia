# Gitunia

Keyboard-first terminal Git client (graph, branches, worktrees, vim-style keys). See README.md for goal and MVP scope.

Design docs are in `docs/` (architecture, git-layer, ux, testing, roadmap, decisions). Work is tracked as GitHub issues grouped by milestone M0-M4 (goals in `docs/roadmap.md`).

## Stack

- C# on .NET 10 (LTS), `net10.0`, SDK pinned in `global.json`. Code in `src/`, tests in `tests/` (layout in `docs/architecture.md`). Published as a single Native AOT binary; it must start in well under 100 ms.
- Git access: spawn the `git` CLI and parse its output; no libgit2 without a logged decision.
- UI: Terminal.Gui v2 (decided in M0, see `docs/architecture.md` for settings and pitfalls). Tests: xUnit with real `git` on temp repos.

## Rules

- Gitunia is a standalone multi-repo client with tabs. Never depend on the process working directory: every git call takes an explicit repo/worktree path, and all repo state is scoped to a tab. No static or global "current repo".
- Cross-platform (Linux, Windows, macOS; macOS lowest priority, Linux is built and tested first). No platform-specific APIs outside the platform abstraction; anything platform-specific gets a branch for every supported OS or an explicit "unsupported" path. Paths via `Path` APIs, no hardcoded separators, `/dev/null` or `/tmp`. Details: `docs/architecture.md`.
- Keep to the MVP scope in README.md. Don't add features beyond it without asking.
- Native AOT from day one: no reflection or runtime codegen, every dependency AOT/trimming-compatible; run the AOT publish after adding one (rules in `.claude/rules/csharp-style.md`).
- The Git layer returns typed results and never touches the UI. The state layer is pure (`(State, Command) -> State` plus effects) and runs without a terminal. Only the rendering layer touches the console. Layering details: `docs/architecture.md`.
- Never block the UI thread on a git process. Every git call is async, cancellable and has a timeout.
- Be conservative with destructive operations (reset --hard, force push, branch/worktree delete, stash drop, discarding changes): confirm in the UI and keep an undo path (reflog hint) where git allows.
- Respect the user's git config, hooks, credential helpers and worktree layout (`docs/git-layer.md` lists the only env vars we set).
- Code style, naming, folders, static vs instance and AOT rules: `.claude/rules/csharp-style.md`, loaded when you touch `.cs` files. Git layer rules: `.claude/rules/git-layer.md`. UI, keys and colour rules: `.claude/rules/ui-changes.md`. Read them before writing code there.
- Scripts (bash and PowerShell) live in `scripts/`; `tools/` holds small C# helper programs.
- Don't copy code from other projects verbatim. Ideas and git command patterns are fine; note the source in `docs/decisions.md` only if it shaped a non-obvious decision.

## Git conventions

- Branches: `feature/<issue>-<slug>` for new work, `fix/<issue>-<slug>` for bug fixes. Branch from `main`; when working autonomously, no direct commits to it: changes arrive through merged PRs. Reference the issue in the PR body (`Closes #<n>`).
- Ad-hoc requests from the owner: simple changes (e.g. a `.gitignore` entry, a doc tweak) go straight to `main`, no branch or PR. If it isn't obvious whether a change is simple enough, ask.
- Pushing: never push to any remote without the owner's confirmation. Ad-hoc changes (to `main`, a branch or docs alike) are committed locally; end the task with "committed locally, not pushed" and a one-line summary, and push everything pending in one push once the owner says so (this keeps CI runs down). The autonomous PR flow (agents pushing their `feature/`/`fix/` branches, opening and merging PRs, and `scripts/screenshot.sh publish`/`clean` pushing to the `<repo>-screenshots` repository, plain pushes only) is unaffected. GitHub issue and comment edits are not pushes and need no confirmation.
- Syncing unpushed local commits with `origin/main`: `git fetch` then `git rebase origin/main`. Never `git merge origin/main` or a bare `git pull` (merge commits are for PR merges only); if a merge slipped in, redo it as a rebase.
- Changes that address PR review comments (code, docs, skills, CLAUDE.md alike) all go through a PR branch, never straight to `main`. The straight-to-`main` exception above applies only to standalone requests from the owner.
- After a PR is merged, nothing of it remains: remove its worktree and delete the branch locally and on the remote (spike branches are the exception and stay).
- Agent identity: every commit, PR body and GitHub comment says which agent (orchestrator, implementer, reviewer) produced it: author-name convention, `Agent:` trailer, header and signature. Details in the `work-task` skill, "Agent identity". Never add the "🤖 Generated with Claude Code" line to PR bodies or comments.
- Commits: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`), optional scope, imperative subject.

## Which doc to read

Read the doc before working in its area; don't rely on memory of it.

| Working on | Read |
|---|---|
| Solution layout, layering, rendering, cross-platform | `docs/architecture.md` |
| Performance targets and measured numbers | `docs/performance.md` (and the `performance` skill) |
| Running git, parsers, graph model, worktrees, refresh/watching | `docs/git-layer.md` |
| Screens, modes, tabs/repo picker, key bindings, workflows, mouse | `docs/ux.md` (index; links the parts: workflows, keymap, mouse, settings, look) |
| Any test, fixtures, snapshot tests | `docs/testing.md` and the `testing` skill |
| Milestone goals and open questions | `docs/roadmap.md` |
| Anything you are about to ask the owner | `docs/decisions.md` first |
| Why a feature is in or out | `README.md` |

## Build, test and lint

Run from the repo root:

- Build (analyzers on, warnings are errors, also the lint step): `dotnet build`
- Test: `dotnet test`
- AOT publish (fails on any `IL*` warning): `dotnet publish src/Gitunia.Tui -c Release -o <outdir>`; smoke run: `<outdir>/gitunia`
- Formatting is enforced by `.editorconfig` during build (`EnforceCodeStyleInBuild`).
- Branch checks the build cannot see (decisions.md append-only and dated, test hygiene, agent author and trailer): `scripts/check-diff.sh` (instant; archive moves to `docs/history/decisions.md` are allowed; `scripts/test-check-diff.sh` tests the check). Run it before review; the reviewer does too.
- Quality inspections the build cannot see (captured disposed or modified variables, unused members, redundant arguments): `scripts/inspect.sh` (or `scripts/inspect.ps1`) runs JetBrains inspectcode, the same engine as Rider. It is not part of the edit loop: run `scripts/inspect.sh --changed` (~10 s) after finishing a change that touches lambdas, events or `using`, and the full run (~40 s) once before opening a PR (the reviewer does the same). Fix what it reports; the tool is pinned in `dotnet-tools.json`.

## Working autonomously

Follow the `work-task` skill (`.claude/skills/work-task/`) for the procedure. To work a whole milestone or the backlog, follow the `work-orchestrator` skill (`.claude/skills/work-orchestrator/`); `work-readiness` prevalidates a milestone first. They hold the issue order, review, PR, merge, loose-end and owner-feedback procedure. Invariants that hold regardless:

- `spike` and `needs-review` issues are never merged by an agent: spikes stay on unmerged `spike/` branches and close with findings; `needs-review` stops at an open PR for the owner.
- Merge commits only, never squash or rebase, never force-push.
- Every PR carries the reviewer's findings as a PR comment; every owner comment gets an agent-marked reply; loose ends are registered as issues, not left in notes.
- Don't block on questions: decide from the docs or take the recommended default, log it, and say so in the PR.
- Models: use Sonnet for every role, not Opus. Cost is tuned with reasoning effort, set in the skill and agent frontmatter, not repeated in prose.
- Never: spend money or create paid resources/accounts, delete branches you didn't create, touch anything outside this repo and the scratchpad, or experiment in the owner's real repos (use the `testing` skill's throwaway repos).
