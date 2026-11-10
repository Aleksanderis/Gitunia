#!/usr/bin/env bash
# Tests scripts/install.sh against a fake release on disk (no network, no root): archive install, skip when current,
# update, download retries, checksum mismatch, unsupported architecture and system, missing asset (#1248).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

# A fake release: the real package.sh archive of a fake binary that answers --version, plus the release JSON.
make_release() { # <version> <dir>
  local version="$1" dir="$2"
  mkdir -p "$dir" "$tmp/publish"
  printf '#!/bin/sh\necho "gitunia %s"\n' "$version" > "$tmp/publish/gitunia"
  chmod +x "$tmp/publish/gitunia"
  (cd "$root" && scripts/package.sh linux-x64 "$tmp/publish" "$version" "$dir" >/dev/null)
  (cd "$root" && scripts/package.sh linux-arm64 "$tmp/publish" "$version" "$dir" >/dev/null)
  {
    echo "{ \"tag_name\": \"v$version\", \"assets\": ["
    echo "  { \"browser_download_url\": \"file://$dir/gitunia-$version-linux-x64.tar.gz\" },"
    echo "  { \"browser_download_url\": \"file://$dir/gitunia-$version-linux-x64.tar.gz.sha256\" },"
    echo "  { \"browser_download_url\": \"file://$dir/gitunia-$version-linux-arm64.tar.gz\" },"
    echo "  { \"browser_download_url\": \"file://$dir/gitunia-$version-linux-arm64.tar.gz.sha256\" }"
    echo "] }"
  } > "$dir/release.json"
}

run_install() { # <release dir> [env assignments...]; prints combined output
  local dir="$1"; shift
  env "$@" GITUNIA_METHOD=archive GITUNIA_RELEASE_URL="file://$dir/release.json" \
    GITUNIA_INSTALL_DIR="$tmp/bin" PATH="$tmp/bin:$PATH" sh "$root/scripts/install.sh" 2>&1
}

make_release 1.2.3 "$tmp/r1"
make_release 1.3.0 "$tmp/r2"

out="$(run_install "$tmp/r1" GITUNIA_OS=Linux GITUNIA_ARCH=x86_64)" || fail "install failed: $out"
[ -x "$tmp/bin/gitunia" ] || fail "binary not installed"
[ "$("$tmp/bin/gitunia" --version)" = "gitunia 1.2.3" ] || fail "wrong binary installed"
[ "$(cat "$tmp/bin/gitunia.install-source")" = "script" ] || fail "install-source marker not written (#1291)"

out="$(run_install "$tmp/r1" GITUNIA_OS=Linux GITUNIA_ARCH=x86_64)" || fail "second run failed: $out"
grep -q "already installed" <<<"$out" || fail "no skip when current: $out"

out="$(run_install "$tmp/r2" GITUNIA_OS=Linux GITUNIA_ARCH=aarch64)" || fail "update failed: $out"
grep -q "Updating Gitunia 1.2.3 -> 1.3.0" <<<"$out" || fail "no update message: $out"
[ "$("$tmp/bin/gitunia" --version)" = "gitunia 1.3.0" ] || fail "not updated"

rm "$tmp/bin/gitunia"
printf '%064d  x\n' 0 > "$tmp/r1/gitunia-1.2.3-linux-x64.tar.gz.sha256"
if out="$(run_install "$tmp/r1" GITUNIA_OS=Linux GITUNIA_ARCH=x86_64)"; then fail "checksum mismatch accepted"; fi
grep -q "checksum mismatch" <<<"$out" || fail "no checksum message: $out"
[ ! -e "$tmp/bin/gitunia" ] || fail "binary installed despite a bad checksum"

if out="$(run_install "$tmp/r2" GITUNIA_OS=Linux GITUNIA_ARCH=riscv64)"; then fail "unsupported arch accepted"; fi
grep -q "unsupported architecture" <<<"$out" || fail "no arch message: $out"
if out="$(run_install "$tmp/r2" GITUNIA_OS=FreeBSD GITUNIA_ARCH=x86_64)"; then fail "unsupported system accepted"; fi
grep -q "unsupported system" <<<"$out" || fail "no system message: $out"
if out="$(run_install "$tmp/r2" GITUNIA_OS=Darwin GITUNIA_ARCH=arm64)"; then fail "missing macOS asset accepted"; fi
grep -q "no osx-arm64 archive" <<<"$out" || fail "no missing-asset message: $out"
# The package path with a fake pacman and sudo (no root): installs the verified file, and a bad checksum stops the script
# before the package manager runs (the reviewer's set -e case).
mkdir -p "$tmp/stubs"
printf '#!/bin/sh\nexec "$@"\n' > "$tmp/stubs/sudo"
printf '#!/bin/sh\necho "pacman $*" >> "%s/pacman.log"\n' "$tmp" > "$tmp/stubs/pacman"
chmod +x "$tmp/stubs/sudo" "$tmp/stubs/pacman"
printf 'fake package' > "$tmp/r2/gitunia-1.3.0-1-x86_64.pkg.tar.zst"
(cd "$tmp/r2" && sha256sum gitunia-1.3.0-1-x86_64.pkg.tar.zst > gitunia-1.3.0-1-x86_64.pkg.tar.zst.sha256)
sed -i "s|\] }|  ,{ \"browser_download_url\": \"file://$tmp/r2/gitunia-1.3.0-1-x86_64.pkg.tar.zst\" },\n  { \"browser_download_url\": \"file://$tmp/r2/gitunia-1.3.0-1-x86_64.pkg.tar.zst.sha256\" }\n] }|" "$tmp/r2/release.json"
rm -f "$tmp/bin/gitunia"

run_package() {
  env "$@" GITUNIA_RELEASE_URL="file://$tmp/r2/release.json" GITUNIA_OS=Linux GITUNIA_ARCH=x86_64 \
    PATH="$tmp/stubs:$PATH" sh "$root/scripts/install.sh" 2>&1
}
out="$(run_package GITUNIA_METHOD=package)" || fail "package install failed: $out"
grep -q "^pacman -U --noconfirm .*gitunia-1.3.0-1-x86_64.pkg.tar.zst$" "$tmp/pacman.log" || fail "pacman not called as expected: $(cat "$tmp/pacman.log")"

rm "$tmp/pacman.log"
printf '%064d  x\n' 0 > "$tmp/r2/gitunia-1.3.0-1-x86_64.pkg.tar.zst.sha256"
if out="$(run_package GITUNIA_METHOD=package)"; then fail "package checksum mismatch accepted: $out"; fi
grep -q "checksum mismatch" <<<"$out" || fail "no package checksum message: $out"
[ ! -e "$tmp/pacman.log" ] || fail "pacman ran despite a bad checksum"
# Transient download failures: a fake curl in front of the real one fails the first N calls for each URL.
real_curl="$(command -v curl)" || fail "need curl for the retry test"
PATH="$tmp/shim:$PATH"
mkdir -p "$tmp/shim"
cat > "$tmp/shim/curl" <<SHIM
#!/bin/sh
for last; do :; done
key="$tmp/count.\$(printf %s "\$last" | tr -c 'A-Za-z0-9' _)"
n=\$(cat "\$key" 2>/dev/null || echo 0)
echo \$((n + 1)) > "\$key"
[ "\$n" -lt "\$(cat "$tmp/curl.fail")" ] && exit 22
exec "$real_curl" "\$@"
SHIM
chmod +x "$tmp/shim/curl"
rm -rf "$tmp/bin"
rm -f "$tmp"/count.*; echo 2 > "$tmp/curl.fail"
out="$(run_install "$tmp/r2" GITUNIA_OS=Linux GITUNIA_ARCH=aarch64 GITUNIA_RETRY_DELAY=0)" || fail "no install after two failed downloads: $out"
[ -x "$tmp/bin/gitunia" ] || fail "binary missing after retried downloads"
grep -q "retrying" <<<"$out" || fail "no retry message: $out"
rm -rf "$tmp/bin"; rm -f "$tmp"/count.*; echo 1000 > "$tmp/curl.fail"
if out="$(run_install "$tmp/r2" GITUNIA_OS=Linux GITUNIA_ARCH=aarch64 GITUNIA_RETRY_DELAY=0)"; then fail "always-failing download accepted"; fi
grep -q "download failed after 4 attempts: file://$tmp/r2/release.json" <<<"$out" || fail "failure lacks the URL: $out"
grep -q "github.com/Aleksanderis/Gitunia/releases" <<<"$out" || fail "failure lacks the releases page: $out"
n="$(cat "$tmp"/count.*release_json)"; [ "$n" = 4 ] || fail "expected 4 attempts, got $n"
echo "ok"
