#!/usr/bin/env bash
# Tests scripts/package.sh with a fake publish dir (no CI dispatch exists, decision 2026-10-18).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

mkdir -p "$tmp/win" "$tmp/lin"
printf 'fake-exe' > "$tmp/win/gitunia.exe"
printf 'fake-elf' > "$tmp/lin/gitunia"
cd "$root"
scripts/package.sh win-x64 "$tmp/win" 1.2.3 "$tmp/out" >/dev/null
scripts/package.sh linux-x64 "$tmp/lin" 1.2.3 "$tmp/out" >/dev/null

for f in gitunia-1.2.3-win-x64.zip gitunia-1.2.3-win-x64.zip.sha256 gitunia-1.2.3-win-x64.exe gitunia-1.2.3-win-x64.exe.sha256 \
         gitunia-1.2.3-linux-x64.tar.gz gitunia-1.2.3-linux-x64.tar.gz.sha256; do
  [ -f "$tmp/out/$f" ] || fail "missing $f"
done
cmp "$tmp/win/gitunia.exe" "$tmp/out/gitunia-1.2.3-win-x64.exe" || fail "exe differs from publish output"
(cd "$tmp/out" && sha256sum -c gitunia-1.2.3-win-x64.exe.sha256 >/dev/null) || fail "exe sha256 does not verify"
[ "$(ls "$tmp/out" | wc -l)" -eq 6 ] || fail "unexpected asset count (linux must not get a bare binary)"
echo "ok"
