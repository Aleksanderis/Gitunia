#!/usr/bin/env bash
# Tests packaging/aur/update.sh with a fake release archive (no network, no push).
# With makepkg (Arch) it also checks .SRCINFO and builds the package from the fake archive.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

for rid in linux-x64 linux-arm64; do
  mkdir -p "$tmp/src/gitunia-1.2.3-$rid"
  printf "fake-elf-$rid" > "$tmp/src/gitunia-1.2.3-$rid/gitunia"
  printf 'MIT' > "$tmp/src/gitunia-1.2.3-$rid/LICENSE"
  tar -C "$tmp/src" -czf "$tmp/gitunia-1.2.3-$rid.tar.gz" gitunia-1.2.3-$rid
done
sha="$(sha256sum "$tmp/gitunia-1.2.3-linux-x64.tar.gz" | awk '{print $1}')"
sha_arm="$(sha256sum "$tmp/gitunia-1.2.3-linux-arm64.tar.gz" | awk '{print $1}')"

"$here/update.sh" 1.2.3 "$tmp/out" --sha256 "$sha" --sha256-arm64 "$sha_arm" >/dev/null
grep -q '^pkgver=1.2.3$' "$tmp/out/PKGBUILD" || fail "version not filled"
grep -q "sha256sums_x86_64=('$sha')" "$tmp/out/PKGBUILD" || fail "x64 hash not filled"
grep -q "sha256sums_aarch64=('$sha_arm')" "$tmp/out/PKGBUILD" || fail "arm64 hash not filled"
if grep -v '^#' "$tmp/out/PKGBUILD" | grep -q '@[A-Z0-9_]*@'; then fail "placeholder left"; fi
if "$here/update.sh" 1.2.3 "$tmp/bad" --sha256 nothex --sha256-arm64 "$sha_arm" >/dev/null 2>&1; then fail "bad hash accepted"; fi
if "$here/update.sh" 1.2.3-rc1 "$tmp/bad" --sha256 "$sha" --sha256-arm64 "$sha_arm" >/dev/null 2>&1; then fail "prerelease accepted"; fi

if command -v makepkg >/dev/null 2>&1; then
  # Push path against a local bare repo standing in for the AUR: first push, repeat (no-op), new version.
  git init -q --bare -b master "$tmp/aur.git"
  printf 'fake-key' > "$tmp/key"
  export GITUNIA_AUR_URL="$tmp/aur.git" AUR_SSH_KEY_FILE="$tmp/key"
  "$here/update.sh" 1.2.3 "$tmp/p1" --sha256 "$sha" --sha256-arm64 "$sha_arm" --push >/dev/null || fail "first push failed"
  [ "$(git -C "$tmp/aur.git" rev-list --count master)" = 1 ] || fail "first push should make one commit"
  git -C "$tmp/aur.git" show master:.SRCINFO | grep -q 'pkgver = 1.2.3' || fail "pushed .SRCINFO lacks version"
  "$here/update.sh" 1.2.3 "$tmp/p2" --sha256 "$sha" --sha256-arm64 "$sha_arm" --push >/dev/null || fail "repeat push failed"
  [ "$(git -C "$tmp/aur.git" rev-list --count master)" = 1 ] || fail "repeat push should be a no-op"
  "$here/update.sh" 1.2.4 "$tmp/p3" --sha256 "$sha" --sha256-arm64 "$sha_arm" --push >/dev/null || fail "update push failed"
  [ "$(git -C "$tmp/aur.git" rev-list --count master)" = 2 ] || fail "new version should add a commit"
  unset GITUNIA_AUR_URL AUR_SSH_KEY_FILE

  grep -q 'pkgname = gitunia-bin' "$tmp/out/.SRCINFO" || fail ".SRCINFO missing name"
  grep -q 'depends = git' "$tmp/out/.SRCINFO" || fail ".SRCINFO missing git"
  # Build against the local fake archive: only /usr/bin/gitunia and the licence may be installed.
  mkdir -p "$tmp/build"
  cp "$tmp/out/PKGBUILD" "$tmp/gitunia-1.2.3-linux-x64.tar.gz" "$tmp/build/"
  (cd "$tmp/build" && sed -i 's|^source_x86_64=.*|source_x86_64=("gitunia-$pkgver-linux-x64.tar.gz")|' PKGBUILD && makepkg -f --nodeps --skipinteg >/dev/null 2>&1) || fail "makepkg failed"
  list="$(tar -tf "$tmp"/build/gitunia-bin-1.2.3-1-x86_64.pkg.tar.zst | grep -v '/$' | grep -v '^\.' | sort)"
  [ "$list" = $'usr/bin/gitunia\nusr/share/licenses/gitunia-bin/LICENSE' ] || fail "unexpected package files: $list"
else
  echo "makepkg not found: skipped .SRCINFO and package build checks"
fi
echo "ok"
