#!/usr/bin/env bash
# Unpacks a release archive and runs the binary from it, checking `--version` prints the stamped version (#1296).
# Usage: scripts/smoke-archive.sh <archive> <version>   (.tar.gz or .zip as built by scripts/package.sh; run on the archive's own OS)
# Used by the build legs of release.yml. Private XDG dirs keep the run away from the caller's state.
set -euo pipefail
if [ "$#" -ne 2 ]; then echo "usage: $0 <archive> <version>" >&2; exit 2; fi
archive="$1"; version="$2"
[ -f "$archive" ] || { echo "missing $archive" >&2; exit 1; }
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
case "$archive" in
  *.tar.gz) tar -xzf "$archive" -C "$tmp"; exe=gitunia ;;
  *.zip)
    exe=gitunia.exe
    if command -v unzip >/dev/null 2>&1; then unzip -q "$archive" -d "$tmp"
    elif command -v 7z >/dev/null 2>&1; then 7z x -bd -o"$tmp" "$archive" >/dev/null
    else tar -xf "$archive" -C "$tmp"; fi ;;
  *) echo "unknown archive type: $archive" >&2; exit 2 ;;
esac
bin="$(find "$tmp" -name "$exe" -type f | head -1)"
[ -n "$bin" ] || { echo "FAIL: $exe not found in $archive" >&2; exit 1; }
chmod +x "$bin"
export XDG_STATE_HOME="$tmp/state" XDG_CONFIG_HOME="$tmp/config"
out="$("$bin" --version)"
echo "$(basename "$archive"): $out"
grep -qF "$version" <<<"$out" || { echo "FAIL: --version of $archive does not print $version" >&2; exit 1; }
