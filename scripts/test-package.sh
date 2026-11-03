#!/usr/bin/env bash
# Tests scripts/package.sh with a fake publish dir (no CI dispatch exists, decision 2026-10-18).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

mkdir -p "$tmp/win" "$tmp/lin" "$tmp/mac"
printf 'fake-exe' > "$tmp/win/gitunia.exe"
printf 'fake-elf' > "$tmp/lin/gitunia"
printf 'fake-macho' > "$tmp/mac/gitunia"
cd "$root"
scripts/package.sh win-x64 "$tmp/win" 1.2.3 "$tmp/out" >/dev/null
scripts/package.sh linux-x64 "$tmp/lin" 1.2.3 "$tmp/out" >/dev/null
scripts/package.sh osx-arm64 "$tmp/mac" 1.2.3 "$tmp/out" >/dev/null

for f in gitunia-1.2.3-win-x64.zip gitunia-1.2.3-win-x64.zip.sha256 \
         gitunia-1.2.3-linux-x64.tar.gz gitunia-1.2.3-linux-x64.tar.gz.sha256 \
         gitunia-1.2.3-osx-arm64.tar.gz gitunia-1.2.3-osx-arm64.tar.gz.sha256; do
  [ -f "$tmp/out/$f" ] || fail "missing $f"
done
(cd "$tmp/out" && sha256sum -c gitunia-1.2.3-win-x64.zip.sha256 >/dev/null) || fail "zip sha256 does not verify"
# The winget manifest points at gitunia-<version>-win-x64/gitunia.exe inside the zip (#1156).
unzip -p "$tmp/out/gitunia-1.2.3-win-x64.zip" gitunia-1.2.3-win-x64/gitunia.exe | cmp - "$tmp/win/gitunia.exe" || fail "zip does not hold gitunia.exe from the publish output"
[ "$(ls "$tmp/out" | wc -l)" -eq 6 ] || fail "unexpected asset count (no bare binary any more, #1156)"
echo "ok"
