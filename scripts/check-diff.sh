#!/usr/bin/env bash
# Mechanical checks on the current branch against main that the build cannot do and reviewers keep finding:
# added C# lines over 120 columns, nested or wrapped ?: (one per line), added doc lines over 600 characters, fully qualified System.* names, over-long, rewritten or misdated docs/decisions.md entries, test hygiene (home dir, wall-clock), commits
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

# Ternaries: no nested ?: and no ?: wrapped over several lines (rule in .claude/rules/csharp-style.md).
while IFS=$'\t' read -r file line text; do
    trimmed="${text#"${text%%[![:space:]]*}"}"
    if [[ "$trimmed" == //* || "$trimmed" == \** || "$trimmed" == /\** ]]; then
        continue
    fi
    if [[ "$trimmed" == '? '* || "$trimmed" == *' ?' ]]; then
        report "$file:$line: multi-line ?:, use if/return, a switch expression or a helper"
    elif [[ "$trimmed" =~ \ \?\ .*\ \?\ .*\ :\  ]]; then
        report "$file:$line: nested ?:, use if/return, a switch expression or a helper"
    fi
done < <(git diff -U0 "$base"...HEAD --diff-filter=AM -- '*.cs' | awk '
    /^\+\+\+ b\// { file = substr($0, 7); next }
    /^@@/ { match($0, /\+[0-9]+/); line = substr($0, RSTART + 1, RLENGTH - 1) - 1; next }
    /^\+/ { line++; print file "\t" line "\t" substr($0, 2) }')

# Long doc lines make parallel edits conflict on the whole line: one sentence or bullet per line (table rows excepted).
while IFS=$'\t' read -r file line text; do
    if (( ${#text} > 600 )) && [[ "$text" != '|'* ]]; then
        report "$file:$line: doc line over 600 characters (${#text}), split it into one sentence per line"
    fi
done < <(git diff -U0 "$base"...HEAD --diff-filter=AM -- 'docs/*.md' | awk '
    /^\+\+\+ b\// { file = substr($0, 7); next }
    /^@@/ { match($0, /\+[0-9]+/); line = substr($0, RSTART + 1, RLENGTH - 1) - 1; next }
    /^\+/ { line++; print file "\t" line "\t" substr($0, 2) }')

# decisions.md entries are one or two lines (the format in its header); long ones belong in the area doc.
while IFS= read -r text; do
    if (( ${#text} > 400 )); then
        report "docs/decisions.md: entry over 400 characters, move the detail to the area doc: ${text:0:60}..."
    fi
done < <(git diff -U0 "$base"...HEAD -- docs/decisions.md | grep '^+\*\*' | cut -c2- || true)

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
