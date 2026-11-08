#!/usr/bin/env bash
# Builds the .deb, .rpm and Arch .pkg.tar.zst packages (plus .sha256 files) from the AOT binary with nfpm.
# Usage: scripts/package-linux.sh <publish-dir> <version> <out-dir> [amd64|arm64]   (default amd64; run from the repository root; needs nfpm on PATH)
# Used by the linux-x64 and linux-arm64 legs in ci.yml on release tags (#942, #1247).
set -euo pipefail

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
  echo "usage: $0 <publish-dir> <version> <out-dir> [amd64|arm64]" >&2
  exit 2
fi
src="$1"; version="$2"; out="$3"; arch="${4:-amd64}"
case "$arch" in amd64|arm64) ;; *) echo "unknown architecture: $arch" >&2; exit 2 ;; esac
[ -f "$src/gitunia" ] || { echo "missing $src/gitunia" >&2; exit 1; }
[ -f LICENSE ] || { echo "missing LICENSE (run from the repository root)" >&2; exit 1; }
command -v nfpm >/dev/null 2>&1 || { echo "nfpm not found on PATH" >&2; exit 1; }

mkdir -p "$out"
out="$(cd "$out" && pwd)"
export VERSION="$version" ARCH="$arch"
BINARY="$(cd "$src" && pwd)/gitunia"
export BINARY

for format in deb rpm archlinux; do
  nfpm package --config scripts/nfpm.yaml --packager "$format" --target "$out"
done

for f in "$out"/*.deb "$out"/*.rpm "$out"/*.pkg.tar.zst; do
  name="$(basename "$f")"
  (cd "$out" && sha256sum "$name" > "$name.sha256")
  echo "$f"
done
