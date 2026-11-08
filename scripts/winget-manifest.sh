#!/usr/bin/env bash
# Fills the winget manifest templates in packaging/winget for one release (#943).
# Usage: scripts/winget-manifest.sh <version> <sha256 | path to the .sha256 file> <out-dir> [release-date]
# <version> has no leading "v". The installer URL is the gitunia-<version>-win-x64.zip release asset (#1156); pass its .sha256 file.
# Writes the three files winget wants (version, installer, defaultLocale) into <out-dir>.
set -euo pipefail

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
  echo "usage: $0 <version> <sha256|sha256-file> <out-dir> [release-date]" >&2
  exit 2
fi
version="$1"; hash="$2"; out="$3"; date="${4:-$(date -u +%Y-%m-%d)}"
root="$(cd "$(dirname "$0")/.." && pwd)"

case "$version" in v*|"") echo "version must not start with v: $version" >&2; exit 1 ;; esac
if ! printf '%s' "$version" | grep -Eq '^[0-9]+(\.[0-9]+)*(-[0-9A-Za-z.]+)?$'; then echo "bad version: $version" >&2; exit 1; fi
if ! printf '%s' "$date" | grep -Eq '^[0-9]{4}-[0-9]{2}-[0-9]{2}$'; then echo "bad date: $date" >&2; exit 1; fi
if [ -f "$hash" ]; then hash="$(awk '{print $1; exit}' "$hash")"; fi
hash="$(printf '%s' "$hash" | tr 'a-f' 'A-F')"
case "$hash" in
  *[!0-9A-F]*|"") echo "not a SHA-256: $hash" >&2; exit 1 ;;
esac
[ "${#hash}" -eq 64 ] || { echo "SHA-256 must be 64 hex digits: $hash" >&2; exit 1; }

url="https://github.com/Aleksanderis/Gitunia/releases/download/v${version}/gitunia-${version}-win-x64.zip"
mkdir -p "$out"
for t in "$root"/packaging/winget/*.yaml; do
  sed -e "s|@VERSION@|$version|g" -e "s|@URL@|$url|g" -e "s|@SHA256@|$hash|g" -e "s|@DATE@|$date|g" "$t" > "$out/$(basename "$t")"
done
if grep -l '@[A-Z0-9]*@' "$out"/*.yaml >/dev/null 2>&1; then echo "unfilled placeholder left in $out" >&2; exit 1; fi
echo "$out"
