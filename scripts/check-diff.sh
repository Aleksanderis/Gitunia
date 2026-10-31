#!/usr/bin/env bash
# Mechanical checks on the current branch against main that the build cannot do: docs/decisions.md is append-only and dated,
# test hygiene (home dir, wall-clock), broken relative doc links, commits without the agent author name or Agent: trailer.
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
# Exception: a removed line that is added verbatim to docs/history/decisions.md in the same diff is an archive move.
skip_dated=""
archived="$(mktemp)"
trap 'rm -f "$archived"' EXIT
git diff -U0 "$base"...HEAD -- docs/history/decisions.md | grep '^+[^+]' | sed 's/^+//' > "$archived" || true
removed="$(git diff -U0 "$base"...HEAD -- docs/decisions.md | grep '^-[^-]' | sed 's/^-//' | grep -cvxF -f "$archived" || true)"
archive_removed="$(git diff -U0 "$base"...HEAD -- docs/history/decisions.md | grep -c '^-[^-]' || true)"
# One-off owner-approved exception (#919): DECISIONS_EDIT_APPROVED=<issue> skips the append-only checks for that cleanup PR only.
if [[ "${DECISIONS_EDIT_APPROVED:-}" == "919" ]]; then
    archive_removed=0
    removed=0
    skip_dated=1
fi
if (( archive_removed > 0 )); then
    report "docs/history/decisions.md: $archive_removed existing line(s) removed or changed, the archive is append-only"
fi
if (( removed > 0 )); then
    report "docs/decisions.md: $removed existing line(s) removed or changed, the file is append-only (archive moves go verbatim to docs/history/decisions.md in the same diff)"
fi
today="$(date +%F)"

while IFS=$'\t' read -r line heading; do
    if [[ -z "$skip_dated" && "$heading" != "## $today" ]]; then
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

# Relative markdown links in docs/ and the root docs must resolve to a file.
while IFS= read -r md; do
    dir="$(dirname "$md")"
    while IFS= read -r target; do
        target="${target%%#*}"
        [[ -z "$target" || "$target" == *://* || "$target" == mailto:* ]] && continue
        [[ -e "$dir/$target" ]] || report "$md: broken link '$target'"
    done < <(grep -o '\]([^)]*)' "$md" | sed 's/^](//; s/)$//' || true)
done < <(ls README.md CLAUDE.md docs/*.md docs/history/*.md 2>/dev/null)

echo "$problems problem(s)"
(( problems == 0 ))
