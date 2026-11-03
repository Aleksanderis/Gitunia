#!/usr/bin/env bash
# Tests scripts/winget-manifest.sh with placeholder hashes (no release exists to test against, #943).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
h=$(printf 'a%.0s' $(seq 64))
H=$(printf 'A%.0s' $(seq 64))

"$root/scripts/winget-manifest.sh" 1.2.3 "$h" "$tmp/out" 2030-01-02 >/dev/null
[ "$(ls "$tmp/out" | wc -l)" -eq 3 ] || fail "expected three manifests"
i="$tmp/out/Gitunia.Gitunia.installer.yaml"
grep -q '^PackageVersion: 1.2.3$' "$i" || fail "version"
grep -q "InstallerSha256: $H$" "$i" || fail "hash not upper case"
grep -q 'download/v1.2.3/gitunia-1.2.3-win-x64.zip$' "$i" || fail "url"
grep -q '^InstallerType: zip$' "$i" || fail "zip"
grep -q '^NestedInstallerType: portable$' "$i" || fail "nested portable"
grep -qF 'RelativeFilePath: gitunia-1.2.3-win-x64\gitunia.exe' "$i" || fail "nested path"
grep -q '^  PortableCommandAlias: gitunia$' "$i" || fail "alias"
grep -q '^ReleaseDate: 2030-01-02$' "$i" || fail "date"
grep -q 'releases/tag/v1.2.3$' "$tmp/out/Gitunia.Gitunia.locale.en-US.yaml" || fail "release notes url"
if grep -rq '@[A-Z0-9]*@' "$tmp/out"; then fail "placeholder left"; fi

printf '%s  gitunia-1.2.3-win-x64.zip\n' "$h" > "$tmp/x.sha256"
"$root/scripts/winget-manifest.sh" 1.2.3 "$tmp/x.sha256" "$tmp/out2" >/dev/null || fail "sha256 file input"
grep -q "InstallerSha256: $H$" "$tmp/out2/Gitunia.Gitunia.installer.yaml" || fail "hash from file"

if "$root/scripts/winget-manifest.sh" v1.2.3 "$h" "$tmp/o3" 2>/dev/null; then fail "leading v accepted"; fi
if "$root/scripts/winget-manifest.sh" 1.2.3 abc "$tmp/o4" 2>/dev/null; then fail "short hash accepted"; fi
echo "ok"
