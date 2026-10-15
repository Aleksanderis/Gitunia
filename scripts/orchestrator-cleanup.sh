#!/usr/bin/env bash
# Cleans up after one finished orchestrator subagent whose PR is merged; prints one line.
# Usage: scripts/orchestrator-cleanup.sh <pr-number> <agent-id>
#   <agent-id> is the id the subagent was launched with (the worktree is .claude/worktrees/agent-<agent-id>).
# Removes the worktree, deletes the local branches it held (the feature branch and worktree-agent-<agent-id>) only when merged
# into main, then fetches with prune and fast-forwards main. Never touches anything when the PR is not merged.
set -euo pipefail

if [ $# -ne 2 ]; then
  sed -n '2,6p' "$0" >&2
  exit 2
fi
pull_request="$1"
agent_identifier="$2"

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

state="$(gh pr view "$pull_request" --json state --jq .state)"
if [ "$state" != "MERGED" ]; then
  echo "PR #$pull_request is $state, nothing cleaned up"
  exit 1
fi

worktree_path="$repo_root/.claude/worktrees/agent-$agent_identifier"
harness_branch="worktree-agent-$agent_identifier"
feature_branch=""
if [ -d "$worktree_path" ]; then
  feature_branch="$(git -C "$worktree_path" branch --show-current 2>/dev/null || true)"
  git worktree remove -f -f "$worktree_path"
fi

git fetch -p -q
git pull -q --ff-only || { sleep 2; git fetch -q; git pull -q --ff-only; }

kept=""
for branch in "$feature_branch" "$harness_branch"; do
  [ -n "$branch" ] || continue
  git show-ref --verify --quiet "refs/heads/$branch" || continue
  if git branch --merged main | grep -qx "[* ] *$branch"; then
    git branch -d "$branch" >/dev/null
  else
    kept="$kept $branch"
  fi
done

if [ -n "$kept" ]; then
  echo "PR #$pull_request merged; worktree removed; NOT merged into main, kept:$kept"
else
  echo "PR #$pull_request merged; worktree and local branches removed; main up to date"
fi
