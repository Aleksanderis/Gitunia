#!/usr/bin/env bash
# Tests packaging/aur/update.sh with a fake release archive (no network, no push).
# With makepkg (Arch) it also checks .SRCINFO and builds the package from the fake archive.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

mkdir -p "$tmp/src/gitunia-1.2.3-linux-x64"
printf 'fake-elf' > "$tmp/src/gitunia-1.2.3-linux-x64/gitunia"
printf 'MIT' > "$tmp/src/gitunia-1.2.3-linux-x64/LICENSE"
tar -C "$tmp/src" -czf "$tmp/gitunia-1.2.3-linux-x64.tar.gz" gitunia-1.2.3-linux-x64
sha="$(sha256sum "$tmp/gitunia-1.2.3-linux-x64.tar.gz" | awk '{print $1}')"

"$here/update.sh" 1.2.3 "$tmp/out" --sha256 "$sha" >/dev/null 2>&1
grep -q '^pkgver=1.2.3$' "$tmp/out/PKGBUILD" || fail "version not filled"
grep -q "sha256sums=('$sha')" "$tmp/out/PKGBUILD" || fail "hash not filled"
if grep -q '@' "$tmp/out/PKGBUILD" && grep -v '^#' "$tmp/out/PKGBUILD" | grep -q '@'; then fail "placeholder left"; fi
if "$here/update.sh" 1.2.3 "$tmp/bad" --sha256 nothex >/dev/null 2>&1; then fail "bad hash accepted"; fi
if "$here/update.sh" 1.2.3-rc1 "$tmp/bad" --sha256 "$sha" >/dev/null 2>&1; then fail "prerelease accepted"; fi

if command -v makepkg >/dev/null 2>&1; then
  grep -q 'pkgname = gitunia-bin' "$tmp/out/.SRCINFO" || fail ".SRCINFO missing name"
  grep -q 'depends = git' "$tmp/out/.SRCINFO" || fail ".SRCINFO missing git"
  # Build against the local fake archive: only /usr/bin/gitunia and the licence may be installed.
  mkdir -p "$tmp/build"
  cp "$tmp/out/PKGBUILD" "$tmp/gitunia-1.2.3-linux-x64.tar.gz" "$tmp/build/"
  (cd "$tmp/build" && sed -i 's|^source=.*|source=("gitunia-$pkgver-linux-x64.tar.gz")|' PKGBUILD && makepkg -f --nodeps --skipinteg >/dev/null 2>&1) || fail "makepkg failed"
  list="$(tar -tf "$tmp"/build/gitunia-bin-1.2.3-1-x86_64.pkg.tar.zst | grep -v '/$' | grep -v '^\.' | sort)"
  [ "$list" = $'usr/bin/gitunia\nusr/share/licenses/gitunia-bin/LICENSE' ] || fail "unexpected package files: $list"
else
  echo "makepkg not found: skipped .SRCINFO and package build checks"
fi
echo "ok"
