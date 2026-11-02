#!/usr/bin/env bash
# Builds the static landing page into <outdir> (default: site-out) and prints how to preview it.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/site-out}"
case "$out" in
  ""|/|"$HOME"|"$root"|.|..) echo "refusing to clear '$out'" >&2; exit 1 ;;
esac
rm -rf "$out"
mkdir -p "$out/assets" "$out/media"
cp "$root/site/index.html" "$out/"
cp "$root"/docs/assets/* "$out/assets/"
cp "$root/docs/media/demo.gif" "$out/media/"
touch "$out/.nojekyll"
echo "Built $out. Preview: python3 -m http.server -d '$out' 8000"
