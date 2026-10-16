---
name: work-task
description: Deliver one Gitunia GitHub issue end to end (pick, branch, implement, test, review, PR). Use when asked to work on the next issue or a specific issue number (the work-orchestrator skill runs it through the backlog).
model: sonnet
effort: medium
---

# Work an issue

## 1. Pick

- If given an issue number, use it. Otherwise take the lowest-numbered open issue in the earliest open milestone that is unblocked: `gh issue list --state open --json number,title,milestone,body`.
- Unblocked means every issue in the body's `Blocked by:` line is closed (`gh issue view <n> --json state`). That line is the single source of truth for order. If it is missing or forms a cycle, fix the issue body and say so in the PR.
- Issues awaiting the owner's merge don't block you: continue with any other unblocked issue. Stop and summarize only when nothing is unblocked.
- Read the issue, then the docs in `CLAUDE.md`'s routing table for the area it touches. If a doc contradicts the issue's Acceptance line, the issue wins; fix the doc in the same PR.

## 2. Unblock without asking

- Settle questions from the docs, `docs/decisions.md`, issue comments or a sensible default; note the assumption in the PR.
- If a real owner decision is needed, add it to "Open questions" in `docs/roadmap.md` (or open an issue labeled `needs-owner`) with your recommended default and what changes if it is wrong, then continue with the default or move to another issue. Don't wait.
- Owner feedback is never dropped. Every owner comment (issue, PR, inline review comment, review) gets an agent-marked reply that states its fate: `Fixed in #<PR>` (what changed), `Registered as #<issue>` (when it is deferred, with why), or `Not done: <reason>` (an argument, not silence). Reply before you close the issue or merge the PR, and quote the owner's point in one line so the reply reads alone. Check with `.claude/skills/work-orchestrator/scripts/owner-feedback.sh unanswered`: nothing you touched may stay listed. A marked reply after the owner's comment is what settles it; there is no other state, and a newer owner comment reopens it. The PR body lists the owner comments it addresses under "Owner feedback". The issues and PRs the owner wrote or commented on carry the `owner` label (set by the orchestrator), so they can be found with `label:owner`.
- Label for the owner: `needs-owner` marks anything only the owner can settle: a decision (now or for a later milestone) or a manual check only the owner can do (a real terminal, clipboard). State in the issue what is asked, the recommended default and whether work is blocked. The owner answers in a comment and does not touch the label; the label is not removed automatically either. An issue counts as answered when its latest comment has no `<!-- agent:` marker (agents and the owner post from the same GitHub account, so the marker is the only way to tell them apart). The next agent that sees such an issue acts on the comment (implement, log it in `docs/decisions.md`, or close the issue) and replies with an agent-marked comment saying what it did; that reply makes the issue "unanswered" again, so it is not picked up twice. A later owner comment re-triggers it. Close the issue when the answer settles it ("good enough", "leave as is").
- If the same failure persists after three different attempts, comment on the issue with what you tried and move on.

## 3. Implement

- Branch from `main` as `feature/<n>-<slug>` or `fix/<n>-<slug>`; one issue per branch. Spikes use `spike/<n>-<slug>` and are never merged.
- When you run as a subagent of the `work-orchestrator` skill (parallel worktree agent): commit and push work-in-progress to your feature branch regularly so an interruption never loses work; stay inside the paths you were given; do not leave background processes (CI watches, servers) running when you finish; you cannot remove your own worktree, so report `merged, cleanup pending` with your worktree path and branch names; keep the final report to at most 5 lines in the format the orchestrator gave you (PR number and state, follow-up issue numbers, what the owner must know), since details belong in the PR comments. If a missing prerequisite appears, open a new issue instead of widening the PR.
- Code on a hot path (git output parsers, graph layout, rendering, caches, startup) follows the `performance` skill: load it before implementing and put measured numbers in the PR.
- Tests follow the `testing` skill; layering and cross-platform rules are in `CLAUDE.md`; style, Git-layer and UI rules are in `.claude/rules/` (loaded when you touch matching files).
- New key bindings: add the row to the keymap table in `docs/ux.md` in the same PR.
- Run the AOT publish if you added a dependency or touched anything reflection-adjacent.
- Loose ends must not be lost. Anything you notice and do not fix in this PR (a known limit, a deferred case, an unwired piece, a manual check you could not run, a decision for the owner, a missing prerequisite for a later issue) gets registered before you finish, in exactly one of these ways: add it to the Acceptance or body of the existing issue that will own it (`gh issue edit` or a comment), or open a new issue (right milestone, area label, `Blocked by:` line if it depends on something; label `needs-owner` for an owner decision or a manual check). A note in `docs/decisions.md`, a PR comment or your report is not registration. List every registered item with its issue number in the PR body under "Follow-ups". If a later issue's prerequisite is missing, add the new issue to that later issue's `Blocked by:` line.
- Update stale docs in the same PR. Log cross-cutting decisions in `docs/decisions.md` as a short entry under the dated heading: bold topic tag, 1-3 lines (decision and why), no implementation detail (that goes in the area doc), blank line between entries.

## 4. Deliver

- Build, test and (if relevant) AOT-publish; report real output, including failures.
- Run the `reviewer` subagent on the branch with the issue number. Fix every `blocker` and `should-fix`, or justify why not in the PR.
- Make the review visible on GitHub: after opening the PR, post the reviewer's findings (verdict, then findings by severity) as a PR comment with `gh pr comment <n> --body-file <file>`, and after fixing, post a second comment saying which findings were fixed (with the commit) and which were skipped and why. The PR body carries only the one-line verdict. The owner reads reviews on the PR, not in the session. Reviewing the work-task's own PR with no reviewer run is not allowed, including for style or docs-only changes.
- Everything you write on GitHub or in git carries the agent identity (see "Agent identity" below).
- Open a PR titled like the issue, body `Closes #<n>`, what changed, assumptions, and the reviewer verdict. Verify locally first, because Actions minutes are limited (decision 2026-10-16): `dotnet build`, `dotnet test`, the AOT publish when dependencies or project files changed, and `scripts/inspect.sh --changed`. Then open the PR ready (`gh pr create`, no draft toggling) after the reviewer has no open blockers, and push once: CI runs once per PR and not on pushes to `main`. Push nothing for comment-only nits, never push an empty commit to trigger CI (only when CI truly did not start), and push again only to fix a failure or a blocker. Wait for the run to finish and merge when it is green; if another PR merged since your last push, also run `dotnet build && dotnet test` on updated `main` afterwards (no CI runs on `main`). If the PR only touches paths CI ignores (`docs/**`, `**/*.md`, `.claude/**`), no CI runs: merge on the reviewer verdict alone. PRs whose base is a `review/**` branch (owner review-only PRs) never run CI and are never merged.
- UI PRs (anything that changes what is rendered; none for key labels, hints only, docs or logic) attach one capture (at most 2-3 images) as a PR comment and run `scripts/screenshot.sh clean` after merging: follow the `screenshots` skill (agent header and signature on the comment; never read the images back; if a tool is missing, say so in the PR).
- `spike`: don't merge or open a PR; post measured findings as an issue comment, push the `spike/` branch and close the issue.
- `destructive` or `needs-review`: stop after opening the PR; the owner merges.
- Otherwise, when CI is green and the reviewer has no open blockers: `gh pr merge <n> --merge --delete-branch` then update local `main`, remove the issue's worktree (`git worktree remove <path>`) and delete the local branch. Once a PR is merged, nothing of it may remain: no worktree, no local branch, no remote branch. A subagent cannot remove the worktree it runs in (and the harness locks it), so a parallel agent only merges and reports "merged, cleanup pending" with its worktree path and branch names; the caller (orchestrating session) then removes each finished agent's worktree (`git worktree remove -f -f <path>`) and deletes its local branches, including the harness's `worktree-agent-*` branch, after checking they are merged into `main` (`git branch --merged main`). Run `git fetch -p` and confirm no stale worktrees or branches remain before ending the session. Spike branches are the exception: they stay (remote) by design.
- Then continue with the next issue if running autonomously.

## Agent identity

All agents act through the owner's GitHub login, so every artifact must say which agent made it. Roles: `orchestrator` (the main session: picks issues, opens PRs, merges), `implementer` (a per-issue subagent: code, tests, fix commits) and `reviewer` (the `reviewer` subagent) and `readiness` (the `work-readiness` skill: reports and issue edits).

- **Commits:** author name is the role, email stays the owner's from git config (so GitHub still links the commit): `GIT_AUTHOR_NAME="Claude (implementer)" git commit ...`. The committer stays the owner. Add an `Agent: <role>` trailer above the `Co-Authored-By` line.
- **PR comments, reviews, issue comments:** start with a header line, end with a signature line, and include a hidden marker so they can be filtered:

  ```
  ### 🔍 Reviewer agent: findings
  <!-- agent: reviewer -->

  ...body...

  ---
  <sub>🔍 Reviewer agent · Claude · PR #<n> @ <short sha></sub>
  ```

  Icons: orchestrator 🧭, implementer 🛠, reviewer 🔍, readiness 📋. Issues and issue comments created by an agent follow the same header and signature rules (a signature on a new issue body; edits to an existing issue body say who added the text). Header titles: `findings`, `fixes for review`, `spike findings`, `blocked`, etc. The signature names the role, "Claude", and the PR or issue plus commit it refers to.
- **PR body:** the last line reads `Opened by 🧭 orchestrator agent` (or `🛠 implementer agent` when a subagent opens it). No "Generated with Claude Code" footer anywhere.
- **Reviewer output:** the `reviewer` subagent is read-only, so it returns the complete comment (header, marker, body, signature) and whoever runs it posts it verbatim with `gh pr comment`. The fix-up comment is posted by the implementer with its own header and signature.
