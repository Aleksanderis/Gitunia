#!/usr/bin/env bash
# Builds gitunia_<version>_amd64.deb and gitunia-<version>-1.x86_64.rpm (plus .sha256 files) from the AOT binary with nfpm.
# Usage: scripts/package-linux.sh <publish-dir> <version> <out-dir>   (run from the repository root; needs nfpm on PATH)
# Used by the Linux leg in ci.yml on release tags (#942). x64 only until an arm64 binary exists.
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <publish-dir> <version> <out-dir>" >&2
  exit 2
fi
src="$1"; version="$2"; out="$3"
[ -f "$src/gitunia" ] || { echo "missing $src/gitunia" >&2; exit 1; }
[ -f LICENSE ] || { echo "missing LICENSE (run from the repository root)" >&2; exit 1; }
command -v nfpm >/dev/null 2>&1 || { echo "nfpm not found on PATH" >&2; exit 1; }

mkdir -p "$out"
out="$(cd "$out" && pwd)"
export VERSION="$version"
BINARY="$(cd "$src" && pwd)/gitunia"
export BINARY

for format in deb rpm; do
  nfpm package --config scripts/nfpm.yaml --packager "$format" --target "$out"
done

for f in "$out"/*.deb "$out"/*.rpm; do
  name="$(basename "$f")"
  (cd "$out" && sha256sum "$name" > "$name.sha256")
  echo "$f"
done
