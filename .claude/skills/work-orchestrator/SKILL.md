---
name: work-orchestrator
description: Orchestrate a Gitunia milestone (or a slice of it) by delegating issues to subagents in bounded batches. Use when asked to work a milestone, run the backlog, or continue the milestone after a previous orchestrator session. Keeps the session small; state lives on GitHub, not in the conversation.
---

# Work a milestone

Invoke as `/work-orchestrator start working on M2` (any milestone: `M1`..`M4`, or a title; no other prompt is needed). Resolve the argument to the milestone title with `gh api repos/{owner}/{repo}/milestones --jq '.[]|"\(.number) \(.title)"'`. Goals per milestone are in `docs/roadmap.md`. Do not start M1+ until the M0 decision issue is closed, and do not start a milestone while an earlier one still has open, unblocked issues unless the owner says so.

You are the orchestrator, not the implementer. Each issue is delivered by a subagent following the `work-task` skill (read it once; it holds the per-issue procedure and what an agent must do). This skill decides what to run, how many at once, what each agent is told, and when to stop.

## Delegation rules

- At most 3 subagents at a time (token budget); queue the rest and start the next only when one finishes. One subagent per issue, each in its own worktree (`isolation: "worktree"`). Only issues that are independent run together: different files, no shared contracts, not the same keymap rows; issues that touch the same shell files run fewer at once.
- Lean prompts, never the orchestrator's context: the issue number, the branch name (`feature/<n>-<slug>`), the few docs sections the agent needs (name sections, not whole files), a two-line summary of the current state of the code it builds on, the paths it may touch, "follow the `work-task` skill and CLAUDE.md", and a request for a report of at most 10 lines.
- Rate limits: a limit error kills running agents and names the reset time. Launch nothing new and never delete the failed agents' worktrees or branches. After the reset resume the same agent with `SendMessage` (it keeps its context); only if impossible, relaunch it with a prompt that says to continue from its branch's existing commits. Schedule a wake-up for the reset time instead of idling.

## 0. Readiness

Look for a readiness report from the `work-readiness` skill for this milestone (the latest comment containing "Verdict:" on the milestone's tracking issue: the open issue labeled `tracking` in that milestone). If none exists from the last few days, tell the owner to run `/work-readiness <milestone>` first (or run it yourself if the owner said to just go) and stop on a `not ready` verdict. Use the report's suggested batches and answered decisions; do not redo its analysis.

## 1. Rebuild state from GitHub (never from memory)

Run these, nothing else, to see where things are:

- `gh issue list --state open --milestone "<milestone>" --json number,title,labels,body` and the `Blocked by:` lines (a blocker is closed when `gh issue view <n> --json state` says CLOSED). The `Blocked by:` line is the order.
- `gh pr list --state open` (work awaiting merge or owner review) and `git worktree list` plus `git branch -a` (unfinished agents; their branches may hold pushed work-in-progress).
- Skip `spike`, `destructive` and `needs-review` issues as `work-task` says, and never pick the `tracking` issue as work.

If a worktree or feature branch exists for an issue with no merged PR, that issue is in flight: resume it (`work-task`'s rate-limit rules), do not start it over.

## 2. Run one bounded batch

- Pick up to 3 independent unblocked issues (different files, no shared contracts or keymap rows). Prefer finishing in-flight issues first. If issues touch the same shell files, run fewer at once.
- Launch one subagent per issue with the lean prompt above. Do not paste docs or earlier reports into prompts.
- A batch is at most 3 issues in total, not 3 at a time: when 3 have merged (or stopped for the owner), stop launching and go to step 4. This keeps the session short.
- As each agent reports (max 10 lines, details are in its PR comments): confirm the PR is merged (`gh pr view <n> --json state`), stop the agent (`TaskStop`; ignore "not running"), then clean up: `git worktree remove -f -f <path>`, delete its local feature branch and its `worktree-agent-*` branch only after `git branch --merged main` confirms they are merged, then `git fetch -p`. Pull `main`. Then start the next queued issue.
- Do not read PR diffs or agent output files yourself; trust the PR comments and CI. Open the PR only if an agent reports a problem.

## 3. Rate limits and failures

- Apply the rate-limit rule above.
- If the same failure repeats after three different attempts, comment on the issue and move on (`work-task` step 2).

## 4. Stop and hand over

Stop when any of these holds: the batch is done, nothing is unblocked, the session context passes about 150k tokens (finish the running issues first, launch no new ones), or the owner asked you to stop.

Before ending:

1. Check `git worktree list`, `git branch -a` and `git fetch -p`: nothing of a merged PR may remain, and unmerged work is listed.
2. Post a status comment on the milestone's tracking issue (create it as `work-readiness` describes if missing) and give the same summary as the final message: issues merged this session with PR links, issues in flight with branch names, issues waiting on the owner (spike, destructive, needs-review), what is unblocked next.
3. Tell the owner: "start a new session with `work-orchestrator` to continue" and nothing else is needed, since GitHub holds the state.

## Rules

- No long summaries of agent work in the conversation; link PRs instead.
- Never implement an issue yourself in the orchestrator session unless it is a trivial fix to something an agent just merged.
- Never decide a milestone is done without checking that every issue in it is closed or waiting for the owner.
