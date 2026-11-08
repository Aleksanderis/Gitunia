#!/usr/bin/env bash
# Fills the Homebrew formula template packaging/homebrew/gitunia.rb.in for one release (#945).
# Usage: scripts/homebrew-formula.sh <version> <out-file> [--sha256-osx-arm64 H --sha256-linux-x64 H --sha256-linux-arm64 H]
#   <version>  without the leading v (1.2.3, stable releases only)
#   hashes     default: downloaded from the release's .tar.gz.sha256 assets ($GITUNIA_REPO overrides Aleksanderis/Gitunia)
# Writes the formula (Formula/gitunia.rb of the tap Aleksanderis/homebrew-tap) to <out-file>.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="${GITUNIA_REPO:-Aleksanderis/Gitunia}"

version=""; out=""; h_mac=""; h_x64=""; h_arm=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --sha256-osx-arm64) h_mac="${2:?--sha256-osx-arm64 needs a value}"; shift 2 ;;
    --sha256-linux-x64) h_x64="${2:?--sha256-linux-x64 needs a value}"; shift 2 ;;
    --sha256-linux-arm64) h_arm="${2:?--sha256-linux-arm64 needs a value}"; shift 2 ;;
    -*) echo "unknown option $1" >&2; exit 2 ;;
    *) if [ -z "$version" ]; then version="$1"; elif [ -z "$out" ]; then out="$1"; else echo "too many arguments" >&2; exit 2; fi; shift ;;
  esac
done
[ -n "$version" ] && [ -n "$out" ] || { echo "usage: $0 <version> <out-file> [--sha256-osx-arm64 H --sha256-linux-x64 H --sha256-linux-arm64 H]" >&2; exit 2; }
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "version must look like 1.2.3 (stable releases only, no leading v)" >&2; exit 2; }

fetch_sha() { # <rid>
  local url="https://github.com/$repo/releases/download/v$version/gitunia-$version-$1.tar.gz.sha256"
  curl -fsSL "$url" | awk '{print $1}' || { echo "cannot download $url" >&2; return 1; }
}
[ -n "$h_mac" ] || h_mac="$(fetch_sha osx-arm64)"
[ -n "$h_x64" ] || h_x64="$(fetch_sha linux-x64)"
[ -n "$h_arm" ] || h_arm="$(fetch_sha linux-arm64)"
for h in "$h_mac" "$h_x64" "$h_arm"; do
  [[ "$h" =~ ^[0-9a-f]{64}$ ]] || { echo "not a lower-case SHA-256 hash: '$h'" >&2; exit 1; }
done

mkdir -p "$(dirname "$out")"
sed -e "s/@VERSION@/$version/g" -e "s/@SHA256_OSX_ARM64@/$h_mac/" -e "s/@SHA256_LINUX_X64@/$h_x64/" -e "s/@SHA256_LINUX_ARM64@/$h_arm/" \
  "$here/../packaging/homebrew/gitunia.rb.in" > "$out"
if grep -q '@[A-Z0-9_]*@' "$out"; then echo "unfilled placeholder left in $out" >&2; exit 1; fi
echo "$out"
