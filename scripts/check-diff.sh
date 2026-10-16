#!/usr/bin/env bash
# Mechanical checks on the current branch against main that the build cannot do and reviewers keep finding:
# added C# lines over 120 columns, fully qualified System.* names, over-long docs/decisions.md entries, commits
# without the agent author name or Agent: trailer. Run it before asking for review; exit code 1 when anything is found.
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

# Added lines only (the diff against the merge base), so untouched old code is never reported.
while IFS=$'\t' read -r file line text; do
    if (( ${#text} > 120 )); then
        report "$file:$line: over 120 columns (${#text})"
    fi
    if [[ "$text" =~ (^|[^A-Za-z0-9_.\"])System\.[A-Z][A-Za-z]+\.[A-Z] && ! "$text" =~ ^[[:space:]]*(using|namespace|//|\*) ]]; then
        report "$file:$line: fully qualified name, use a using import"
    fi
done < <(git diff -U0 "$base"...HEAD --diff-filter=AM -- '*.cs' | awk '
    /^\+\+\+ b\// { file = substr($0, 7); next }
    /^@@/ { match($0, /\+[0-9]+/); line = substr($0, RSTART + 1, RLENGTH - 1) - 1; next }
    /^\+/ { line++; print file "\t" line "\t" substr($0, 2) }')

# decisions.md entries are one or two lines (the format in its header); long ones belong in the area doc.
while IFS= read -r text; do
    if (( ${#text} > 400 )); then
        report "docs/decisions.md: entry over 400 characters, move the detail to the area doc: ${text:0:60}..."
    fi
done < <(git diff -U0 "$base"...HEAD -- docs/decisions.md | grep '^+\*\*' | cut -c2- || true)

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
