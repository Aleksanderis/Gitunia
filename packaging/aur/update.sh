#!/usr/bin/env bash
# Renders the AUR package gitunia-bin for a release and optionally pushes it to the AUR (#944).
# Usage: packaging/aur/update.sh <version> <out-dir> [--sha256 <x64-hash> --sha256-arm64 <arm64-hash>] [--push]
#   <version>  without the leading v (0.8 or 1.2.3, as in the tag)
#   --sha256, --sha256-arm64   archive hashes (linux-x64, linux-arm64); default: downloaded from the release's .tar.gz.sha256 assets
#   --push     commit and push <out-dir> to ssh://aur@aur.archlinux.org/gitunia-bin.git;
#              the private key comes from $AUR_SSH_KEY_FILE (a file path); $GITUNIA_AUR_URL overrides the remote (tests)
# pkgrel is always 1: if the template changes for an already published version, bump pkgrel in PKGBUILD.in by hand.
# Writes <out-dir>/PKGBUILD and, when makepkg exists (Arch), <out-dir>/.SRCINFO.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="${GITUNIA_REPO:-Aleksanderis/Gitunia}"

version=""; out=""; sha=""; sha_arm=""; push=0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --sha256) sha="${2:?--sha256 needs a value}"; shift 2 ;;
    --sha256-arm64) sha_arm="${2:?--sha256-arm64 needs a value}"; shift 2 ;;
    --push) push=1; shift ;;
    -*) echo "unknown option $1" >&2; exit 2 ;;
    *) if [ -z "$version" ]; then version="$1"; elif [ -z "$out" ]; then out="$1"; else echo "too many arguments" >&2; exit 2; fi; shift ;;
  esac
done
[ -n "$version" ] && [ -n "$out" ] || { echo "usage: $0 <version> <out-dir> [--sha256 <hash> --sha256-arm64 <hash>] [--push]" >&2; exit 2; }
[[ "$version" =~ ^[0-9]+\.[0-9]+(\.[0-9]+)?$ ]] || { echo "version must look like 1.2 or 1.2.3 (stable releases only; pkgver cannot contain '-')" >&2; exit 2; }

fetch_sha() { # <rid>
  local url="https://github.com/$repo/releases/download/v$version/gitunia-$version-$1.tar.gz.sha256"
  curl -fsSL "$url" | awk '{print $1}' || { echo "cannot download $url" >&2; return 1; }
}
[ -n "$sha" ] || sha="$(fetch_sha linux-x64)"
[ -n "$sha_arm" ] || sha_arm="$(fetch_sha linux-arm64)"
for h in "$sha" "$sha_arm"; do
  [[ "$h" =~ ^[0-9a-f]{64}$ ]] || { echo "not a SHA-256 hash: '$h'" >&2; exit 1; }
done

mkdir -p "$out"
sed -e "s/@VERSION@/$version/" -e "s/@SHA256_X64@/$sha/" -e "s/@SHA256_ARM64@/$sha_arm/" "$here/PKGBUILD.in" > "$out/PKGBUILD"
if command -v makepkg >/dev/null 2>&1; then
  (cd "$out" && makepkg --printsrcinfo > .SRCINFO)
elif [ "$push" -eq 1 ]; then
  echo "makepkg is needed to write .SRCINFO before a push (Arch container or runner)" >&2; exit 1
else
  echo "makepkg not found: .SRCINFO not written" >&2
fi

if [ "$push" -eq 1 ]; then
  if [ -z "${AUR_SSH_KEY_FILE:-}" ] || [ ! -f "$AUR_SSH_KEY_FILE" ]; then
    echo "set AUR_SSH_KEY_FILE to the private key file" >&2; exit 1
  fi
  work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT
  export GIT_SSH_COMMAND="ssh -i $AUR_SSH_KEY_FILE -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new"
  git clone -q "${GITUNIA_AUR_URL:-ssh://aur@aur.archlinux.org/gitunia-bin.git}" "$work/aur"
  cp "$out/PKGBUILD" "$out/.SRCINFO" "$work/aur/"
  cd "$work/aur"
  git add PKGBUILD .SRCINFO
  if git diff --cached --quiet; then echo "AUR already up to date"; exit 0; fi
  git -c user.name="Gitunia release" -c user.email="noreply@users.noreply.github.com" commit -q -m "Update to $version"
  git push -q origin HEAD:master
  echo "pushed gitunia-bin $version"
fi
echo "$out/PKGBUILD"
