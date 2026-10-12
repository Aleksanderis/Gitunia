---
name: work-task
description: Deliver one Gitunia GitHub issue end to end (pick, branch, implement, test, review, PR). Use when asked to work on the next issue or a specific issue number (the work-orchestrator skill runs it through the backlog).
---

# Work an issue

## 1. Pick

- If given an issue number, use it. Otherwise take the lowest-numbered open issue in the earliest open milestone that is unblocked: `gh issue list --state open --json number,title,milestone,body`.
- Unblocked means every issue in the body's `Blocked by:` line is closed (`gh issue view <n> --json state`). That line is the single source of truth for order. If it is missing or forms a cycle, fix the issue body and say so in the PR.
- Issues awaiting the owner's merge don't block you: continue with any other unblocked issue (M1+ still waits for the M0 decision). Stop and summarize only when nothing is unblocked.
- Read the issue, then the docs in `CLAUDE.md`'s routing table for the area it touches. If a doc contradicts the issue's Acceptance line, the issue wins; fix the doc in the same PR.

## 2. Unblock without asking

- Settle questions from the docs, `docs/decisions.md`, issue comments or a sensible default; note the assumption in the PR.
- If a real owner decision is needed, add it to "Open questions" in `docs/roadmap.md` (or open an issue labeled `question`) with your recommended default and what changes if it is wrong, then continue with the default or move to another issue. Don't wait.
- If the same failure persists after three different attempts, comment on the issue with what you tried and move on.

## 3. Implement

- Branch from `main` as `feature/<n>-<slug>` or `fix/<n>-<slug>`; one issue per branch. Spikes use `spike/<n>-<slug>` and are never merged.
- Run independent unblocked issues in parallel, at most 3 agents at a time (token budget): one subagent per issue, each in its own worktree (`isolation: "worktree"`), each following this skill end to end including review and merge. Queue the rest and start the next only when one finishes. Not for the same files, shared contracts or the same keymap rows. If a missing prerequisite appears, open a new issue instead of widening the PR.
- Give each subagent a lean prompt, never the orchestrator's context: the issue number, the branch name, the few docs sections it needs (name sections, not whole files), a two-line summary of the current state of the code it builds on, the paths it may touch, and the rules in this skill. Ask for a report of at most 10 lines (PR URL, merged yes/no, what the owner must know); details belong in the PR comments.
- Subagents commit and push work-in-progress to their feature branch regularly, so an interruption never loses work.
- Rate limits: a limit error kills running agents and the message names the reset time. Do not launch anything new, and never delete the failed agents' worktrees or branches. After the reset, resume the same agent with `SendMessage` (it keeps its context); only if that is impossible, relaunch it with a prompt that says to continue from its branch's existing commits. Schedule a wake-up for the reset time instead of idling.
- Tests follow the `testing` skill; layering and cross-platform rules are in `CLAUDE.md`.
- New key bindings: add the row to the keymap table in `docs/ux.md` in the same PR.
- Run the AOT publish if you added a dependency or touched anything reflection-adjacent.
- Update stale docs in the same PR. Log cross-cutting decisions in `docs/decisions.md`.

## 4. Deliver

- Build, test and (if relevant) AOT-publish; report real output, including failures.
- Run the `reviewer` subagent on the branch with the issue number. Fix every `blocker` and `should-fix`, or justify why not in the PR.
- Make the review visible on GitHub: after opening the PR, post the reviewer's findings (verdict, then findings by severity) as a PR comment with `gh pr comment <n> --body-file <file>`, and after fixing, post a second comment saying which findings were fixed (with the commit) and which were skipped and why. The PR body carries only the one-line verdict. The owner reads reviews on the PR, not in the session. Reviewing the work-task's own PR with no reviewer run is not allowed, including for style or docs-only changes.
- Everything you write on GitHub or in git carries the agent identity (see "Agent identity" below).
- Open a PR titled like the issue, body `Closes #<n>`, what changed, assumptions, and the reviewer verdict.
- `spike`: don't merge or open a PR; post measured findings as an issue comment, push the `spike/` branch and close the issue.
- `destructive` or `needs-review`: stop after opening the PR; the owner merges.
- Otherwise, when CI is green and the reviewer has no open blockers: `gh pr merge <n> --merge --delete-branch` then update local `main`, remove the issue's worktree (`git worktree remove <path>`) and delete the local branch. Once a PR is merged, nothing of it may remain: no worktree, no local branch, no remote branch. A subagent cannot remove the worktree it runs in (and the harness locks it), so a parallel agent only merges and reports "merged, cleanup pending" with its worktree path and branch names; the caller (orchestrating session) then removes each finished agent's worktree (`git worktree remove -f -f <path>`) and deletes its local branches, including the harness's `worktree-agent-*` branch, after checking they are merged into `main` (`git branch --merged main`). Run `git fetch -p` and confirm no stale worktrees or branches remain before ending the session. Spike branches are the exception: they stay (remote) by design.
- Then continue with the next issue if running autonomously.

## Agent identity

All agents act through the owner's GitHub login, so every artifact must say which agent made it. Roles: `orchestrator` (the main session: picks issues, opens PRs, merges), `implementer` (a per-issue subagent: code, tests, fix commits) and `reviewer` (the `reviewer` subagent).

- **Commits:** author name is the role, email stays the owner's from git config (so GitHub still links the commit): `GIT_AUTHOR_NAME="Claude (implementer)" git commit ...`. The committer stays the owner. Add an `Agent: <role>` trailer above the `Co-Authored-By` line.
- **PR comments, reviews, issue comments:** start with a header line, end with a signature line, and include a hidden marker so they can be filtered:

  ```
  ### 🔍 Reviewer agent: findings
  <!-- agent: reviewer -->

  ...body...

  ---
  <sub>🔍 Reviewer agent · Claude · PR #<n> @ <short sha></sub>
  ```

  Icons: orchestrator 🧭, implementer 🛠, reviewer 🔍. Header titles: `findings`, `fixes for review`, `spike findings`, `blocked`, etc. The signature names the role, "Claude", and the PR or issue plus commit it refers to.
- **PR body:** the last line reads `Opened by 🧭 orchestrator agent` (or `🛠 implementer agent` when a subagent opens it). No "Generated with Claude Code" footer anywhere.
- **Reviewer output:** the `reviewer` subagent is read-only, so it returns the complete comment (header, marker, body, signature) and whoever runs it posts it verbatim with `gh pr comment`. The fix-up comment is posted by the implementer with its own header and signature.
