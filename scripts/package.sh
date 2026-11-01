#!/usr/bin/env bash
# Packs an AOT publish directory into a release archive plus a SHA-256 file.
# Usage: scripts/package.sh <rid> <publish-dir> <version> <out-dir>
# Non-Windows rids give gitunia-<version>-<rid>.tar.gz, win-* rids give .zip
# plus the bare gitunia-<version>-<rid>.exe and its .sha256 for a portable winget package (#941). Used by the release job in ci.yml.
# Works with GNU tools and with macOS (shasum, bsdtar) (#455).
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
    if tar --version 2>/dev/null | grep -q "GNU tar"; then owner=(--owner=0 --group=0); else owner=(--uid 0 --gid 0 --uname root --gname root); fi
    (cd "$stage" && tar "${owner[@]}" -czf "$out/$archive" "$name") ;;
esac

if command -v sha256sum >/dev/null 2>&1; then sha() { sha256sum "$@"; }; else sha() { shasum -a 256 "$@"; }; fi
(cd "$out" && sha "$archive" > "$archive.sha256")
case "$rid" in
  win-*)
    # Bare binary for a portable winget package (#941).
    bare="$name.exe"
    cp "$src/$exe" "$out/$bare"
    (cd "$out" && sha "$bare" > "$bare.sha256") ;;
esac
echo "$out/$archive"
