#!/usr/bin/env bash
# Dumps the review evidence for /retrospective into one condensed markdown file and prints its path and a summary.
# Usage: collect.sh <out-file> [since-pr-number] [limit]   (default: last 40 merged PRs)
# Each comment is tagged OWNER (no <!-- agent: marker, written by the owner), REVIEWER, IMPLEMENTER or OTHER;
# bodies are cut to 1500 characters and the agent signature lines are dropped.
set -euo pipefail

out="${1:?usage: collect.sh <out-file> [since-pr-number] [limit]}"
since="${2:-0}"
limit="${3:-40}"

gh pr list --state merged --limit "$limit" --json number,title \
    | jq -r --argjson since "$since" '.[] | select(.number > $since) | .number' | sort -n > "$out.numbers"

: > "$out"
while read -r number; do
    gh pr view "$number" --json number,title,comments,reviews | jq -r '
        def role: if (.body | test("<!-- agent: reviewer")) then "REVIEWER"
            elif (.body | test("<!-- agent: implementer")) then "IMPLEMENTER"
            elif (.body | test("<!-- agent:")) then "OTHER"
            else "OWNER" end;
        def clean: .body | gsub("<sub>[^\n]*</sub>"; "") | gsub("<!--[^>]*-->"; "") | gsub("\n{2,}"; "\n") | .[0:1500];
        "## PR #\(.number) \(.title)",
        ([.comments[], (.reviews[] | select(.body != ""))] | .[] | "### \(role)\n\(clean)")'  >> "$out"
done < "$out.numbers"

echo "file: $out"
echo "PRs: $(wc -l < "$out.numbers") ($(head -1 "$out.numbers"), up to $(tail -1 "$out.numbers"))"
for role in OWNER REVIEWER IMPLEMENTER; do
    echo "$role comments: $(grep -c "^### $role" "$out" || true)"
done
rm -f "$out.numbers"
