#!/usr/bin/env bash
# Records the README demo: the real binary on a throwaway repo, scripted by scripts/demo.tape (VHS + ttyd), Linux only.
# Usage: scripts/demo.sh [--binary <gitunia>] [--out <dir>]
#   --binary  the AOT binary (default: publishes src/Gitunia.Tui into a temp dir first)
#   --out     output directory (default: docs/media): demo.gif (README) and demo.webm (landing page)
# Needs vhs, ttyd, ffmpeg, git (and dotnet unless --binary). Nothing here runs in CI.
# See docs/user-guide.md, "Regenerating the README demo". Separate from the screenshots flow (scripts/screenshot.sh).
set -euo pipefail

case "$(uname -s)" in
  Linux*) ;;
  *) echo "demo.sh is supported on Linux only" >&2; exit 2 ;;
esac
for tool in vhs ttyd ffmpeg git; do
  command -v "$tool" >/dev/null 2>&1 || { echo "missing tool '$tool' (vhs needs ttyd and ffmpeg)" >&2; exit 3; }
done

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
binary="" out_dir="$repo_root/docs/media"
while [ $# -gt 0 ]; do
  case "$1" in
    --binary|--out)
      [ $# -ge 2 ] || { echo "$1 needs a value" >&2; exit 2; }
      if [ "$1" = "--binary" ]; then binary="$2"; else out_dir="$2"; fi
      shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
if [ -z "$binary" ]; then
  command -v dotnet >/dev/null 2>&1 || { echo "missing tool 'dotnet' (or pass --binary)" >&2; exit 3; }
  dotnet publish "$repo_root/src/Gitunia.Tui" -c Release -o "$work/bin" -nologo -v quiet >&2
  binary="$work/bin/gitunia"
fi
[ -x "$binary" ] || { echo "binary '$binary' not found" >&2; exit 3; }

# Throwaway repo: a small history with a merged feature branch, a second branch, and a working-tree change in two places.
repo="$work/demo-repo"
(
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  day=0
  commit() {
    day=$((day + 1))
    export GIT_AUTHOR_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z" GIT_COMMITTER_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z"
    git -C "$repo" add -A
    git -C "$repo" commit -q -m "$1"
  }
  git init -q -b main "$repo"
  mkdir -p "$repo/src"
  printf '# Demo\n\nA small calculator.\n\n## Usage\n\nCall the functions from src.\n\n## License\n\nMIT.\n' >"$repo/README.md"
  printf 'int Add(int a, int b) => a + b;\n\nint Subtract(int a, int b) => a - b;\n' >"$repo/src/math.txt"
  commit "Initial commit"
  git -C "$repo" switch -q -c feature/multiply
  printf 'int Multiply(int a, int b) => a * b;\n' >"$repo/src/multiply.txt"
  commit "Add multiply"
  printf 'Multiply is exact for small values.\n' >>"$repo/README.md"
  commit "Document multiply"
  git -C "$repo" switch -q main
  printf '\nint Divide(int a, int b) => a / b;\n' >>"$repo/src/math.txt"
  commit "Add divide"
  day=$((day + 1))
  export GIT_AUTHOR_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z" GIT_COMMITTER_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z"
  git -C "$repo" merge -q --no-ff -m "Merge feature/multiply" feature/multiply
  git -C "$repo" switch -q -c fix/overflow
  printf '\nint AddChecked(int a, int b) => checked(a + b);\n' >>"$repo/src/math.txt"
  commit "Add a checked variant"
  git -C "$repo" switch -q main
  # Uncommitted edits in README.md (it is identical on every branch, so switching branches stays possible):
  # added lines in one hunk, so the demo can stage only some of them.
  sed -i 's/^Call the functions from src\.$/&\n\nDivide truncates toward zero.\nModulo keeps the sign of the dividend.\nPower is not implemented yet./' "$repo/README.md"
)

mkdir -p "$out_dir" "$work/config" "$work/state"
export DEMO_BIN="$binary" DEMO_REPO="$repo"
export XDG_CONFIG_HOME="$work/config" XDG_STATE_HOME="$work/state"
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
cd "$work"
vhs "$script_dir/demo.tape" >&2
mv "$work/demo.gif" "$out_dir/demo.gif"
mv "$work/demo.webm" "$out_dir/demo.webm"
ls -l "$out_dir/demo.gif" "$out_dir/demo.webm" >&2
