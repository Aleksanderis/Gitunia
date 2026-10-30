#!/usr/bin/env bash
# Regenerates docs/keymap.md from src/Gitunia.Core/Keymap/keymap.default.json (run after changing the JSON or an action title).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$root/tools/KeymapDoc" -- --json "$root/src/Gitunia.Core/Keymap/keymap.default.json" --out "$root/docs/keymap.md"
