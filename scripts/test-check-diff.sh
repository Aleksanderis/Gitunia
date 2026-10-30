#!/usr/bin/env bash
# Proves the decisions.md append-only check of check-diff.sh: an archive move (removal copied verbatim to
# docs/history-decisions.md in the same diff) passes, an edit or an unrelated deletion is flagged.
# Usage: test-check-diff.sh   (exit code 1 on failure)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

git init -q -b main "$work"
mkdir -p "$work/scripts" "$work/docs"
cp "$root/scripts/check-diff.sh" "$work/scripts/"
cd "$work"
git config user.name test
git config user.email test@example.com
printf '# Decisions\n\n## 2026-10-10\n\n**Stack:** one\n\n**Stack:** two\n\n**Stack:** three\n' > docs/decisions.md
git add -A
git commit -q -m base

# expect <name> <message fragment> <1 = must appear, 0 = must not>
expect() {
    local out
    out="$(bash scripts/check-diff.sh main 2>&1 || true)"
    if { [[ "$out" == *"$2"* ]] && [[ "$3" == 1 ]]; } || { [[ "$out" != *"$2"* ]] && [[ "$3" == 0 ]]; }; then
        echo "ok: $1"
    else
        echo "FAIL: $1"
        echo "$out"
        fail=1
    fi
}

fresh_branch() {
    git checkout -q main
    git branch -q -D work 2>/dev/null || true
    git checkout -q -b work
}

fresh_branch
printf '# Decisions\n\n## 2026-10-10\n\n**Stack:** one\n\n**Stack:** three\n' > docs/decisions.md
printf '# History\n\n## 2026-10-10\n\n**Stack:** two\n' > docs/history-decisions.md
git add -A
git commit -q -m archive
expect "archive move is accepted" "append-only" 0

fresh_branch
printf '# Decisions\n\n## 2026-10-10\n\n**Stack:** one\n\n**Stack:** three\n' > docs/decisions.md
git add -A
git commit -q -m delete
expect "unrelated deletion is flagged" "1 existing line(s) removed" 1

fresh_branch
printf '# Decisions\n\n## 2026-10-10\n\n**Stack:** ONE\n\n**Stack:** two\n\n**Stack:** three\n' > docs/decisions.md
git add -A
git commit -q -m edit
expect "edit of an existing line is flagged" "append-only" 1

fresh_branch
printf '# Decisions\n\n## 2026-10-10\n\n**Stack:** one\n\n**Stack:** three\n' > docs/decisions.md
printf '# History\n\n## 2026-10-10\n\n**Stack:** other\n' > docs/history-decisions.md
git add -A
git commit -q -m mismatch
expect "removal not found in the archive is flagged" "append-only" 1

exit "$fail"
