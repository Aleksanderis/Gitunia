---
name: work-orchestrator
description: Orchestrate a Gitunia milestone (or a slice of it) by delegating issues to subagents in bounded batches. Use when asked to work a milestone, run the backlog, or continue the milestone after a previous orchestrator session. Keeps the session small; state lives on GitHub, not in the conversation.
---

# Work a milestone

You are the orchestrator, not the implementer. Each issue is delivered by a subagent following the `work-task` skill (read it once; it holds the per-issue procedure, the parallelism cap, the lean-prompt rules and the rate-limit rules). This skill only decides what to run, when to stop, and how to hand over.

## 1. Rebuild state from GitHub (never from memory)

Run these, nothing else, to see where things are:

- `gh issue list --state open --milestone "<milestone>" --json number,title,labels,body` and the `Blocked by:` lines (a blocker is closed when `gh issue view <n> --json state` says CLOSED). The `Blocked by:` line is the order.
- `gh pr list --state open` (work awaiting merge or owner review) and `git worktree list` plus `git branch -a` (unfinished agents; their branches may hold pushed work-in-progress).
- Skip `spike`, `destructive` and `needs-review` issues as `work-task` says.

If a worktree or feature branch exists for an issue with no merged PR, that issue is in flight: resume it (`work-task`'s rate-limit rules), do not start it over.

## 2. Run one bounded batch

- Pick up to 3 independent unblocked issues (different files, no shared contracts or keymap rows). Prefer finishing in-flight issues first. If issues touch the same shell files, run fewer at once.
- Launch one subagent per issue with the lean prompt from `work-task`. Do not paste docs or earlier reports into prompts.
- A batch is at most 3 issues in total, not 3 at a time: when 3 have merged (or stopped for the owner), stop launching and go to step 4. This keeps the session short.
- As each agent reports (max 10 lines, details are in its PR comments): confirm the PR is merged (`gh pr view <n> --json state`), stop the agent (`TaskStop`; ignore "not running"), then clean up: `git worktree remove -f -f <path>`, delete its local feature branch and its `worktree-agent-*` branch only after `git branch --merged main` confirms they are merged, then `git fetch -p`. Pull `main`. Then start the next queued issue.
- Do not read PR diffs or agent output files yourself; trust the PR comments and CI. Open the PR only if an agent reports a problem.

## 3. Rate limits and failures

- Follow `work-task`: on a limit error launch nothing new, keep the failed agents' worktrees and branches, schedule a wake-up for the reset time, then resume the same agents with `SendMessage`.
- If the same failure repeats after three different attempts, comment on the issue and move on (`work-task` step 2).

## 4. Stop and hand over

Stop when any of these holds: the batch is done, nothing is unblocked, the session context passes about 150k tokens (finish the running issues first, launch no new ones), or the owner asked you to stop.

Before ending:

1. Check `git worktree list`, `git branch -a` and `git fetch -p`: nothing of a merged PR may remain, and unmerged work is listed.
2. Post a status comment as the final message (also on the milestone's tracking issue if one exists): issues merged this session with PR links, issues in flight with branch names, issues waiting on the owner (spike, destructive, needs-review), what is unblocked next.
3. Tell the owner: "start a new session with `work-orchestrator` to continue" and nothing else is needed, since GitHub holds the state.

## Rules

- No long summaries of agent work in the conversation; link PRs instead.
- Never implement an issue yourself in the orchestrator session unless it is a trivial fix to something an agent just merged.
- Never decide a milestone is done without checking that every issue in it is closed or waiting for the owner.
