#!/usr/bin/env bash
# Installs the .deb, .rpm and Arch packages of a release in clean distro containers, runs `gitunia --version`
# (must print the stamped version; git must be there as the declared dependency), removes the package and checks the
# binary is gone (#1296). Needs docker; plain distro images, nothing is built.
# Usage: scripts/smoke-packages.sh <dist-dir> <version>   (the packages of the machine's own architecture)
# Used by the Linux legs of release.yml. Arch Linux publishes an x86_64 image only: on arm64 that package is skipped
# with a notice.
set -euo pipefail
if [ "$#" -ne 2 ]; then echo "usage: $0 <dist-dir> <version>" >&2; exit 2; fi
dist="$(cd "$1" && pwd)"; version="$2"
command -v docker >/dev/null || { echo "docker not found" >&2; exit 1; }
case "$(uname -m)" in
  x86_64) deb_arch=amd64; rpm_arch=x86_64; arch_arch=x86_64 ;;
  aarch64|arm64) deb_arch=arm64; rpm_arch=aarch64; arch_arch=aarch64 ;;
  *) echo "unsupported machine $(uname -m)" >&2; exit 1 ;;
esac
failed=0

# Runs inside the container; INSTALL and REMOVE come from the environment.
inside='
set -eu
export XDG_STATE_HOME=/tmp/state XDG_CONFIG_HOME=/tmp/config
sh -c "$INSTALL" >/dev/null
out="$(gitunia --version)"; echo "$out"
case "$out" in *"$VERSION"*) ;; *) echo "version not stamped"; exit 1 ;; esac
git --version
sh -c "$REMOVE" >/dev/null
hash -r
if command -v gitunia >/dev/null 2>&1; then echo "gitunia still installed after removal"; exit 1; fi
'

run() { # <label> <image> <package file> <install command> <remove command>
  local label="$1" image="$2" pkg="$3" install="$4" remove="$5"
  if [ ! -f "$dist/$pkg" ]; then echo "FAIL: $label: missing $dist/$pkg" >&2; failed=1; return; fi
  echo "::group::$label ($image)"
  if docker run --rm -v "$dist:/dist:ro" -e PKG="/dist/$pkg" -e VERSION="$version" -e INSTALL="$install" -e REMOVE="$remove" \
      "$image" bash -c "$inside"; then echo "ok: $label"; else echo "FAIL: $label" >&2; failed=1; fi
  echo "::endgroup::"
}

# nfpm writes the version as given for .deb, with `~` for a pre-release hyphen in .rpm and `_` in Arch.
rpm_version="${version//-/\~}"; arch_version="${version//-/_}"
for image in ubuntu:24.04 debian:stable; do
  run ".deb on $image" "$image" "gitunia_${version}_${deb_arch}.deb" \
    'apt-get update -qq && apt-get install -y -qq --no-install-recommends "$PKG"' 'apt-get remove -y -qq gitunia'
done
run ".rpm on fedora" fedora:latest "gitunia-${rpm_version}-1.${rpm_arch}.rpm" \
  'dnf install -y -q "$PKG"' 'dnf remove -y -q gitunia'
if [ "$arch_arch" = x86_64 ]; then
  run "Arch package on archlinux" archlinux:latest "gitunia-${arch_version}-1-${arch_arch}.pkg.tar.zst" \
    'pacman -Sy --noconfirm && pacman -U --noconfirm "$PKG"' 'pacman -R --noconfirm gitunia'
else
  echo "notice: no archlinux image for $arch_arch, Arch package not installed"
fi
exit "$failed"
