#!/usr/bin/env bash
# Tests scripts/homebrew-formula.sh with placeholder hashes (no release needed, #945). Run by hand.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
a=$(printf 'a%.0s' $(seq 64)); b=$(printf 'b%.0s' $(seq 64)); c=$(printf 'c%.0s' $(seq 64))
gen="$root/scripts/homebrew-formula.sh"

"$gen" 1.2.3 "$tmp/Formula/gitunia.rb" --sha256-osx-arm64 "$a" --sha256-linux-x64 "$b" --sha256-linux-arm64 "$c" >/dev/null
f="$tmp/Formula/gitunia.rb"
grep -q '^class Gitunia < Formula$' "$f" || fail "class"
grep -q 'gitunia-1.2.3-osx-arm64' "$f" || fail "version"
# Each url must pair with its own hash, and name the archives scripts/package.sh builds.
grep -A1 'download/v1.2.3/gitunia-1.2.3-osx-arm64.tar.gz"' "$f" | grep -q "sha256 \"$a\"" || fail "osx-arm64 pair"
grep -A1 'download/v1.2.3/gitunia-1.2.3-linux-x64.tar.gz"' "$f" | grep -q "sha256 \"$b\"" || fail "linux-x64 pair"
grep -A1 'download/v1.2.3/gitunia-1.2.3-linux-arm64.tar.gz"' "$f" | grep -q "sha256 \"$c\"" || fail "linux-arm64 pair"
grep -q 'xattr -d com.apple.quarantine' "$f" || fail "quarantine caveat"
if grep -q '@[A-Z0-9_]*@' "$f"; then fail "placeholder left"; fi
if command -v ruby >/dev/null 2>&1; then ruby -c "$f" >/dev/null || fail "ruby syntax"; fi

if "$gen" v1.2.3 "$tmp/o1" --sha256-osx-arm64 "$a" --sha256-linux-x64 "$b" --sha256-linux-arm64 "$c" 2>/dev/null; then fail "leading v accepted"; fi
if "$gen" 1.2.3-rc1 "$tmp/o2" --sha256-osx-arm64 "$a" --sha256-linux-x64 "$b" --sha256-linux-arm64 "$c" 2>/dev/null; then fail "prerelease accepted"; fi
if "$gen" 1.2.3 "$tmp/o3" --sha256-osx-arm64 abc --sha256-linux-x64 "$b" --sha256-linux-arm64 "$c" 2>/dev/null; then fail "short hash accepted"; fi
[ ! -e "$tmp/o3" ] || fail "file written for bad hash"
echo "ok"
