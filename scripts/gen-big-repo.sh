#!/usr/bin/env bash
# Generate a deterministic large git repo for performance checks (Linux/macOS only).
# Never committed: output goes outside the project tree.
#
# Usage: scripts/gen-big-repo.sh [dir] [commits]
#   dir      default: ${GITUNIA_BIG_REPO:-${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/big-<commits>}
#   commits  default: 100000 (total, merge commits included)
# The last stdout line is the repo path. Re-running on an existing repo with at
# least that many commits is a no-op; a non-empty unrelated dir is refused.
#
# Shape: linear `main`; every 40 commits a 4-commit feature branch is forked
# and merged back (so ~2500 merges per 100k). Every 100th feature branch is
# kept as a ref (feature/f100, ...), a lightweight tag v<k> every 5000 commits.
# Subjects: "Commit #<n>: ..." and "Merge feature f<k>"; HEAD is main.
set -euo pipefail

commits="${2:-100000}"
dir="${1:-${GITUNIA_BIG_REPO:-${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/big-$commits}}"

# Isolation: ignore the user's git config; fixed author/committer inside the stream.
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null LC_ALL=C

if [ -d "$dir/.git" ] && [ "$(git -C "$dir" rev-list --count --all 2>/dev/null || echo 0)" -ge "$commits" ]; then
  echo "exists: $dir" >&2
  echo "$dir"
  exit 0
fi
if [ -e "$dir" ] && [ -n "$(ls -A "$dir" 2>/dev/null)" ]; then
  echo "refusing to overwrite non-empty $dir" >&2
  exit 1
fi

mkdir -p "$dir"
git init -q -b main "$dir"

awk -v total="$commits" '
function emit(ref, mark, from, merge, subj, path, body) {
  printf "commit %s\nmark :%d\nauthor %s %d +0000\ncommitter %s %d +0000\n", ref, mark, who, base + mark * 60, who, base + mark * 60
  printf "data %d\n%s\n", length(subj), subj
  if (from != 0) printf "from :%d\n", from
  if (merge != 0) printf "merge :%d\n", merge
  printf "M 100644 inline %s\ndata %d\n%s\n\n", path, length(body), body
}
BEGIN {
  base = 1700000000; m = 0; main = 0; n = 0; k = 0
  who = "Test <test@example.com>"
  while (m < total) {
    if (main != 0 && m % 40 == 0 && m + 5 <= total) {
      k++
      ref = (k % 100 == 0) ? "refs/heads/feature/f" k : "refs/heads/tmp"
      prev = main
      for (j = 1; j <= 4; j++) {
        m++; n++
        emit(ref, m, prev, 0, "Commit #" n ": feature f" k " step " j, "feature/f" k ".txt", "f" k " " j)
        prev = m
      }
      m++
      emit("refs/heads/main", m, main, prev, "Merge feature f" k, "merges.txt", "merge " k)
    } else {
      m++; n++
      emit("refs/heads/main", m, main, 0, "Commit #" n ": update file " (n % 97), "src/file" (n % 97) ".txt", "n=" n)
    }
    main = m
    if (m % 5000 == 0) printf "reset refs/tags/v%d\nfrom :%d\n\n", m / 5000, m
  }
  printf "reset refs/heads/tmp\n\n"
}' | git -C "$dir" fast-import --quiet

git -C "$dir" reset -q --hard main
git -C "$dir" commit-graph write --reachable
git -C "$dir" rev-list --count --all >&2
echo "$dir"
