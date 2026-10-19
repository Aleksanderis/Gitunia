#!/usr/bin/env bash
# Packs an AOT publish directory into a release archive plus a SHA-256 file.
# Usage: scripts/package.sh <rid> <publish-dir> <version> <out-dir>
# Non-Windows rids give gitunia-<version>-<rid>.tar.gz, win-* rids give .zip. Used by the release job in ci.yml.
# Needs sha256sum (macOS will need a shasum fallback with #455).
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 <rid> <publish-dir> <version> <out-dir>" >&2
  exit 2
fi
rid="$1"; src="$2"; version="$3"; out="$4"

case "$rid" in
  win-*) exe="gitunia.exe" ;;
  *) exe="gitunia" ;;
esac
[ -f "$src/$exe" ] || { echo "missing $src/$exe" >&2; exit 1; }

name="gitunia-${version}-${rid}"
mkdir -p "$out"
out="$(cd "$out" && pwd)"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
mkdir -p "$stage/$name"
cp "$src/$exe" "$stage/$name/"
if [ -f LICENSE ]; then cp LICENSE "$stage/$name/"; fi
cp README.md "$stage/$name/"

case "$rid" in
  win-*)
    archive="$name.zip"
    if command -v 7z >/dev/null 2>&1; then (cd "$stage" && 7z a -tzip -bd "$out/$archive" "$name" >/dev/null)
    elif command -v zip >/dev/null 2>&1; then (cd "$stage" && zip -qr "$out/$archive" "$name")
    else echo "need 7z or zip to build $archive" >&2; exit 1; fi ;;
  *)
    archive="$name.tar.gz"
    (cd "$stage" && tar --owner=0 --group=0 -czf "$out/$archive" "$name") ;;
esac

(cd "$out" && sha256sum "$archive" > "$archive.sha256")
echo "$out/$archive"
