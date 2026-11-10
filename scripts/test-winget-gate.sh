#!/usr/bin/env bash
# Tests scripts/winget-gate.sh: pre-release, switch and token combinations (no network, #1297).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $*" >&2; exit 1; }
gate() { EVENT="$1" PRERELEASE="$2" SWITCH="$3" HAS_TOKEN="$4" TAG=v1.0 "$root/scripts/winget-gate.sh"; }

[ -z "$(gate release false true true)" ] || fail "release with switch and token was skipped"
gate release true true true | grep -q "pre-release" || fail "pre-release not skipped"
gate release false "" true | grep -q "WINGET_SUBMIT" || fail "unset switch not skipped"
gate release false false true | grep -q "WINGET_SUBMIT" || fail "switch=false not skipped"
gate release false true false | grep -q "WINGET_TOKEN" || fail "missing token not skipped"
[ -z "$(gate workflow_dispatch false "" true)" ] || fail "manual run was stopped by the switch"
[ -z "$(gate workflow_dispatch true "" true)" ] || fail "manual run was stopped by the pre-release flag"
gate workflow_dispatch false "" false | grep -q "WINGET_TOKEN" || fail "manual run without token not skipped"
echo "ok"
