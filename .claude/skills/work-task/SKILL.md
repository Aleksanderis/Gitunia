---
name: work-task
description: Deliver one Gitunia roadmap task end to end (pick, branch, implement, test, review, commit). Use when asked to work on the next task, a named task, or to run autonomously through the roadmap.
---

# Work a task

## 1. Pick

- If given a task, use it. Otherwise take the first unfinished task in the earliest unfinished milestone in `docs/roadmap.md`. Don't start M1+ until the M0 decision (renderer) is logged in `docs/decisions.md`.
- Read `CLAUDE.md`'s routing table and the docs for the area you are touching before writing code. If a doc contradicts the roadmap's "done when", the roadmap wins; fix the doc in the same change.

## 2. Unblock without asking

- Settle questions from the docs, `docs/decisions.md` or a sensible default; note the assumption in the commit body or final summary.
- If a real owner decision is needed, add it to "Open questions" in `docs/roadmap.md` with your recommended default and what changes if it is wrong, then continue with the default or move to another task. Don't wait.
- If the same build or test failure persists after three different attempts, stop and report what you tried.

## 3. Implement

- Branch from `master` as `feature/<slug>` or `fix/<slug>`; one task per branch. If a missing prerequisite appears, add it to the roadmap instead of widening the change.
- Respect the layering in `docs/architecture.md` (Git -> Core -> Tui, no upward references). Parsers get fixture tests; state logic gets pure tests; destructive ops get integration tests on temp repos.
- New key bindings: add the row to the keymap table in `docs/ux.md` in the same change.
- Run the AOT publish if you added a dependency or touched anything reflection-adjacent.
- Update stale docs in the same change. Log cross-cutting decisions in `docs/decisions.md`.

## 4. Deliver

- Build, test and (if relevant) AOT-publish; report real output, including failures.
- Run the `reviewer` subagent on the branch. Fix every `blocker` and `should-fix`, or justify why not.
- Commit with a Conventional Commit message. Don't push, merge to `master` or open PRs unless asked.
- Tick the task in `docs/roadmap.md`, then continue with the next one if running autonomously.
