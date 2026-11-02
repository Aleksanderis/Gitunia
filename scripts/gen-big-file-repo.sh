#!/usr/bin/env bash
# Generate a deterministic repo with one large file for blame performance checks (Linux/macOS only).
# Never committed: output goes outside the project tree.
#
# Usage: scripts/gen-big-file-repo.sh [dir] [lines] [commits]
#   dir      default: ${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/bigfile-<lines>
#   lines    default: 50000 (lines of big.txt)
#   commits  default: 500 (commits after the first; each rewrites every 50th line from a rotating offset)
# The last stdout line is the repo path. An existing non-empty dir is refused.
set -euo pipefail

lines="${2:-50000}"
commits="${3:-500}"
dir="${1:-${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/bigfile-$lines}"
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null LC_ALL=C
export GIT_AUTHOR_NAME=Perf GIT_AUTHOR_EMAIL=perf@example.com GIT_COMMITTER_NAME=Perf GIT_COMMITTER_EMAIL=perf@example.com

if [ -d "$dir/.git" ]; then
  echo "$dir"
  exit 0
fi
if [ -e "$dir" ] && [ -n "$(ls -A "$dir" 2>/dev/null)" ]; then
  echo "refusing to overwrite non-empty $dir" >&2
  exit 1
fi
mkdir -p "$dir"
git init -q -b main "$dir"
awk -v n="$lines" 'BEGIN { for (i = 1; i <= n; i++) printf "line %d: some typical source text value=%d;\n", i, i }' >"$dir/big.txt"
git -C "$dir" add big.txt
git -C "$dir" commit -q -m "Add big.txt"
for ((c = 1; c <= commits; c++)); do
  awk -v c="$c" -v off="$((c % 50))" 'NR % 50 == off { printf "line %d: edited in commit %d;\n", NR, c; next } { print }' "$dir/big.txt" >"$dir/big.tmp"
  mv "$dir/big.tmp" "$dir/big.txt"
  git -C "$dir" commit -q -am "Edit $c"
done
echo "$dir"
