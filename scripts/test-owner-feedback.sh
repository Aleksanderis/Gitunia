#!/usr/bin/env bash
# Proves the agent-marker detection of owner-feedback.sh: a marker at the very start of a comment body counts like one after
# a newline, and plain owner text does not. Uses the real `def agent` line from the script.
# Usage: test-owner-feedback.sh   (exit code 1 on failure)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
def="$(grep -m1 -o 'def agent:.*;' "$root/.claude/skills/work-orchestrator/scripts/owner-feedback.sh")"
fail=0

# check <name> <body> <expected true|false>
check() {
    local got
    got="$(jq -n --arg body "$2" "$def"' $body | agent')"
    if [ "$got" != "$3" ]; then echo "FAIL: $1 (got $got, want $3)"; fail=1; else echo "ok: $1"; fi
}

check "marker at start of body" $'Agent: orchestrator\n\nDone.' true
check "marker after newline" $'Fixed.\n\nAgent: implementer' true
check "bold marker at start" '**Agent: implementer**' true
check "html marker" '<!-- agent: reviewer -->' true
check "owner text" 'looks good, agent: nope inline' false
check "empty body" '' false
exit $fail
