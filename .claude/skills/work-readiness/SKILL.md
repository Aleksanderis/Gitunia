---
name: work-readiness
description: Check whether a Gitunia milestone is ready to start (issue quality, blockers, open decisions, missing prerequisites, file overlap, stale docs) and post a readiness report. Use before work-orchestrator starts a milestone, or when asked to prevalidate, groom or refine the next phase. Read-only except for issue edits the owner approves.
model: sonnet
effort: high
---

# Milestone readiness

Invoke as `/work-readiness M2` (resolve the milestone title with `gh api repos/{owner}/{repo}/milestones --jq '.[]|"\(.number) \(.title)"'`; goals are in `docs/roadmap.md`). Do the whole job in this one session, cheaply: read GitHub and only the docs and code areas the milestone touches. Do not implement anything and do not launch implementation agents.

## 1. Gather

- All issues of the milestone: `gh issue list --milestone "<title>" --state all --json number,title,state,labels,body`, plus the issues they reference in `Blocked by:` lines.
- Open PRs, in-flight worktrees and branches (`gh pr list`, `git worktree list`, `git branch -a`).
- The milestone's section in `docs/roadmap.md` (goal and "Done when") and its open questions; `docs/decisions.md` entries about the area; the workflow rows in `docs/ux.md` the milestone relies on.
- For each issue, `ls`/`grep` the code it will build on (do not read whole files) to see what exists.

## 2. Check

1. **Issue quality:** each issue has a clear scope and an `Acceptance:` line that can be tested; a `Blocked by:` line that is consistent (every blocker exists, none is a cycle, closed blockers are not listed as open); correct labels (`spike`, `destructive`, `needs-review`); a milestone.
2. **Owner-gated work:** list issues that will stop at an open PR (`destructive`, `needs-review`) or never merge (`spike`), so the owner can plan reviews.
3. **Open decisions:** open questions in `docs/roadmap.md` and unsettled choices in `docs/decisions.md` that this milestone depends on. For each, propose a default and say what changes if it is wrong, so agents are not blocked mid-run.
4. **Prerequisites and assumptions:** what each issue assumes about existing code versus what exists; missing pieces; unwired work from earlier milestones (follow-up issues such as session or clipboard wiring) that this milestone needs or that could break it.
5. **Overlap:** which issues touch the same files, shared contracts or keymap rows (check the keymap table in `docs/ux.md`), so the orchestrator knows which can run together (max 3 agents) and in what order.
6. **Docs:** design issues still to be written (flows in `docs/ux.md`), docs that are stale for this area, keymap rows that will be needed.
7. **Size and cost:** issues too big for one agent run (propose a split), and which large docs agents will have to load (suggest the sections to name in their prompts).
8. **Loose ends:** known limits, deferred work and manual checks from earlier milestones that exist only in `docs/decisions.md`, PR comments or code comments (grep `docs/decisions.md` for 'known limit', 'follow-up', 'not wired', 'TODO'); each must be an issue or part of one. Register missing ones (new issue or edit an existing one) and list them in the report.
9. **Environment:** the build, tests and AOT publish are green on `main`; CI is passing; nothing from the previous milestone is half-merged (stray worktrees, branches, open PRs awaiting the owner).

## 3. Report

Post one comment on the milestone's tracking issue (find it with `gh issue list --milestone "<title>" --label tracking --state open`; if missing, create it with `gh issue create --title "<milestone title>: tracking" --label tracking --milestone "<title>"` (create the `tracking` label first if it does not exist) and a one-line body saying it holds readiness reports and orchestrator status), and tell the owner the link: verdict `ready`, `ready with conditions` or `not ready`; then sections: blockers to fix first, decisions needed (with proposed defaults), proposed issue edits, suggested batches in order (up to 3 independent issues each, with the reason), risks and cost notes. Keep it under about 60 lines and link instead of quoting.

## 4. Fix what you may

- Issue-body fixes that are mechanical (a wrong `Blocked by:` line, a missing label) may be applied directly and listed in the report.
- Anything that changes scope, splits an issue or answers a decision waits for the owner's approval. Log accepted defaults in `docs/decisions.md` or `docs/roadmap.md` only when the owner approves.

## 5. Hand over

End with: the verdict, the decisions the owner must make, and "run `/work-orchestrator <milestone>` when ready". The orchestrator will refuse to start on a `not ready` verdict and starts without asking again on `ready` when the report is less than a few days old.
