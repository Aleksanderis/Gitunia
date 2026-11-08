#!/usr/bin/env bash
# Prints the URL of the first open winget-pkgs PR in a `gh pr list --json number,title,url` file whose title names
# the package Gitunia.Gitunia (a prefix such as "New version: " is fine); prints nothing when there is none (#1275). Used by .github/workflows/winget.yml.
# Usage: winget-open-pr.sh <pr-list.json>
set -euo pipefail
[ $# -eq 1 ] && [ -f "$1" ] || { echo "usage: $0 <pr-list.json>" >&2; exit 2; }
jq -r '[.[] | select(.title | test("(^|[^A-Za-z0-9.])Gitunia\\.Gitunia( |$)"))][0].url // empty' "$1"
