# Workflow profile

## Docs

- roadmap: `docs/internal/roadmap.md` (milestone goals, open questions)
- decisions: `docs/internal/decisions.md`
- testing: `docs/testing.md`
- routing: CLAUDE.md "Which doc to read" table; read the rows for the area an issue touches
- retrospectives: `docs/internal/retrospectives.md`

## Commands

- build: `dotnet build` (analyzers on, warnings are errors; also the lint step)
- test: `dotnet test`
- test, one project/filter: `dotnet test <project> --filter <expr>`
- lint / inspect: `scripts/inspect.sh` (`--changed` variant, ~10 s; full run ~40 s once before a PR)
- extra gate when dependencies or project files change: `dotnet publish src/Gitunia.Tui -c Release -o <outdir>` (Native AOT, fails on any `IL*` warning)
- start/smoke the real app: `<outdir>/gitunia` (see `tui-smoke-testing`)

## Policy

- default branch: `main`
- branch names: `feature/<issue>-<slug>`, `fix/<issue>-<slug>`, `spike/<issue>-<slug>`
- merge style: merge commits only, never squash or rebase, never force-push
- `needs-review` issues: `merge` (merge as usual, owner reviews afterwards); `spike` issues are never merged by an agent
- autonomy: `autonomous` (never wait for the owner: merge dependencies yourself, decide from the docs, report afterwards)
- push policy: agents push freely, no confirmation needed
- CI: `manual` (decision 2026-10-18: never runs automatically; local build and tests are the gate). Dispatch once before closing a milestone: `gh workflow run ci.yml --ref main`; a run takes about 4-6 minutes
- extra labels: `windows-vm` (needs a Windows VM; the orchestrator never picks it), `tracking` (never picked as work), `owner` (filed by the owner)
- commit message format: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`)
- context limit: 150000

## Hot paths

Git output parsers, graph layout and scrolling, per-frame rendering, caches, startup (well under 100 ms). Budgets and numbers: `docs/internal/performance.md`.

## Agent run-time rules

(owner, 2026-11-01, after hour-long runs; from `.claude/skills/work-orchestrator/SKILL.md`)

- No `scripts/measure.sh` or A/B performance rounds per issue (measure once at the milestone's composition-root issue, if at all).
- One normal `dotnet test`, then rerun only the affected project after a fix; never full-suite loops.
- The `reviewer` runs once and must not run tests or builds.
- Smoke runs of the binary use a private `XDG_STATE_HOME` and `XDG_CONFIG_HOME` (never the owner's real session file).
- After the merge the agent starts no background watch, monitor or polling (no waiting on CI) and ends with its report.
- Verify locally, open the PR ready, push once; no screenshots without a visual change; no empty commits or comment-only nits.

## Self-review additions

- layering: Git -> Core (pure) -> App (effects, no Terminal.Gui) -> Tui (only console)
- no static or global current repo; every git call takes an explicit path, is async, cancellable and has a timeout
- destructive operations confirm in the UI and keep an undo path (reflog hint)
- Native AOT: no reflection or runtime codegen; run the AOT publish after adding a dependency
- keymap doc is generated (`scripts/gen-keymap-doc.sh`) and must be current
- no platform-specific APIs outside the platform abstraction; no hardcoded separators, `/tmp` or `/dev/null`

## Visual verification

`scripts/screenshot.sh` (see the `screenshots` skill); images are hosted in the `<repo>-screenshots` repository.
