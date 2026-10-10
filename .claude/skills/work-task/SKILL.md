---
name: work-task
description: Deliver one Gitunia GitHub issue end to end (pick, branch, implement, test, review, PR). Use when asked to work on the next issue, a specific issue number, or to run autonomously through the backlog.
---

# Work an issue

## 1. Pick

- If given an issue number, use it. Otherwise take the lowest-numbered open issue in the earliest open milestone that is unblocked: `gh issue list --state open --json number,title,milestone,body`.
- Unblocked means every issue in the body's `Blocked by:` line is closed (`gh issue view <n> --json state`). That line is the single source of truth for order. If it is missing or forms a cycle, fix the issue body and say so in the PR.
- Don't start M1+ until the M0 decision issue (renderer) is closed.
- Read the issue, then the docs in `CLAUDE.md`'s routing table for the area it touches. If a doc contradicts the issue's Acceptance line, the issue wins; fix the doc in the same PR.

## 2. Unblock without asking

- Settle questions from the docs, `docs/decisions.md`, issue comments or a sensible default; note the assumption in the PR.
- If a real owner decision is needed, add it to "Open questions" in `docs/roadmap.md` (or open an issue labeled `question`) with your recommended default and what changes if it is wrong, then continue with the default or move to another issue. Don't wait.
- If the same failure persists after three different attempts, comment on the issue with what you tried and move on.

## 3. Implement

- Branch from `main` as `feature/<n>-<slug>` or `fix/<n>-<slug>`; one issue per branch. If a missing prerequisite appears, open a new issue instead of widening the PR.
- Respect the layering in `docs/architecture.md` (Git -> Core -> Tui, no upward references). Tests follow the `testing` skill.
- New key bindings: add the row to the keymap table in `docs/ux.md` in the same PR.
- Run the AOT publish if you added a dependency or touched anything reflection-adjacent.
- Update stale docs in the same PR. Log cross-cutting decisions in `docs/decisions.md`.

## 4. Deliver

- Build, test and (if relevant) AOT-publish; report real output, including failures.
- Run the `reviewer` subagent on the branch with the issue number. Fix every `blocker` and `should-fix`, or justify why not in the PR.
- Open a PR titled like the issue, body `Closes #<n>`, what changed, assumptions, and the reviewer verdict.
- Labeled `destructive` or `spike`: stop after opening the PR; the owner merges. Otherwise merge on green CI with no open reviewer blockers.
- Then continue with the next issue if running autonomously.
