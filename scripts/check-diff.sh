#!/usr/bin/env bash
# Mechanical checks on the current branch against main that the build cannot do: docs/decisions.md is append-only and dated,
# test hygiene (home dir, wall-clock), commits without the agent author name or Agent: trailer.
# Run it before asking for review; exit code 1 when anything is found.
# Usage: check-diff.sh [base-ref]   (default: main)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
base="${1:-main}"
cd "$root"
problems=0

report() {
    echo "$1"
    problems=$((problems + 1))
}

# decisions.md is append-only: no removed or changed lines, and new entries go under today's dated heading.
removed="$(git diff -U0 "$base"...HEAD -- docs/decisions.md | grep -c '^-[^-]' || true)"
if (( removed > 0 )); then
    report "docs/decisions.md: $removed existing line(s) removed or changed, the file is append-only"
fi
today="$(date +%F)"
while IFS=$'\t' read -r line heading; do
    if [[ "$heading" != "## $today" ]]; then
        report "docs/decisions.md:$line: entry under '$heading', add it under '## $today'"
    fi
done < <(git diff -U0 "$base"...HEAD -- docs/decisions.md | awk '
    /^@@/ { match($0, /\+[0-9]+/); line = substr($0, RSTART + 1, RLENGTH - 1) - 1; next }
    /^\+\*\*/ { line++; print line; next }
    /^\+/ { line++ }' | while read -r n; do
        printf '%s\t%s\n' "$n" "$(git show HEAD:docs/decisions.md | head -n "$n" | grep '^## ' | tail -n 1)"
    done)

# Test hygiene: no real home directory and no wall-clock assertions in added test lines.
while IFS=$'\t' read -r file line text; do
    if [[ "$text" == *SpecialFolder.UserProfile* ]]; then
        report "$file:$line: tests must not touch the real home directory, use a temp dir"
    fi
    if [[ "$text" =~ Assert\..*(ElapsedMilliseconds|Stopwatch|\.Elapsed) || "$text" =~ ElapsedMilliseconds[[:space:]]*(\<|\>) ]]; then
        report "$file:$line: wall-clock assertion is flaky, assert behaviour or use a fake clock"
    fi
done < <(git diff -U0 "$base"...HEAD --diff-filter=AM -- 'tests/*.cs' | awk '
    /^\+\+\+ b\// { file = substr($0, 7); next }
    /^@@/ { match($0, /\+[0-9]+/); line = substr($0, RSTART + 1, RLENGTH - 1) - 1; next }
    /^\+/ { line++; print file "\t" line "\t" substr($0, 2) }')

# Agent identity on every commit of the branch (CLAUDE.md, work-task skill "Agent identity").
while IFS=$'\t' read -r sha author; do
    if [[ "$author" != Claude\ \(* ]]; then
        report "$sha: author '$author' is not 'Claude (<role>)'"
    fi
    if ! git show -s --format=%B "$sha" | grep -q '^Agent: '; then
        report "$sha: no 'Agent: <role>' trailer"
    fi
done < <(git log --no-merges --format=$'%h\t%an' "$base"..HEAD)

echo "$problems problem(s)"
(( problems == 0 ))
