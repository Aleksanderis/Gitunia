#!/usr/bin/env bash
# Screenshots of the real UI for pull requests (Linux/macOS; bash, tmux, rsvg-convert, dotnet, git; `gh` for publish/clean).
# Pipeline: the real binary runs in a private tmux server on a throwaway repo, `tmux capture-pane -p -e`
# gives the ANSI frame, tools/AnsiToSvg turns it into an SVG and rsvg-convert into a PNG. Headless, no network.
# Windows is unsupported (no tmux): the script says so and exits 2. See docs/testing.md ("Screenshots").
#
# Usage:
#   scripts/screenshot.sh render  --binary <gitunia> [--out <dir>] [--scenarios default,diff,narrow,light] [--repo <dir>]
#   scripts/screenshot.sh publish --pr <number> [--out <dir>]     push the PNGs of `render` to the orphan `screenshots` branch,
#                                                                  print markdown for a PR comment
#   scripts/screenshot.sh clean                                    remove images of merged/closed PRs from `screenshots`
# The repo is private: links are github.com/<repo>/raw/<commit>/... (render for logged-in viewers; raw.githubusercontent.com needs
# a token). They are pinned to the screenshots-branch commit, so later pushes never break them; `clean` deletes the files of
# merged PRs from the branch tip, but old commits stay reachable until GitHub garbage-collects them, so old links may keep working for a while.
# Scenarios: default (120x36), diff (diff pane open), narrow (64x30), light (light terminal background).
# Without --repo a deterministic throwaway repo is generated under a temp dir (never a real repo).
# Fonts differ from the owner's terminal, so glyph widths stay a manual check (#196).
set -euo pipefail

command_name="${1:-}"
[ $# -gt 0 ] && shift

case "$(uname -s)" in
  Linux*|Darwin*) ;;
  *) echo "screenshot.sh is unsupported on this OS (needs tmux); use the snapshot tests or --print-frame instead" >&2; exit 2 ;;
esac

usage() { sed -n '2,13p' "$0" >&2; exit 2; }
need() { # need TOOL HINT
  command -v "$1" >/dev/null 2>&1 || { echo "missing tool '$1': $2" >&2; exit 3; }
}

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cache_dir="${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/screenshot"
out_dir="$cache_dir/out"
binary="" scenarios="default,diff,narrow,light" source_repo="" pull_request=""
while [ $# -gt 0 ]; do
  case "$1" in
    --binary) binary="$2"; shift 2 ;;
    --out) out_dir="$2"; shift 2 ;;
    --scenarios) scenarios="$2"; shift 2 ;;
    --repo) source_repo="$2"; shift 2 ;;
    --pr) pull_request="$2"; shift 2 ;;
    *) echo "unknown option $1" >&2; usage ;;
  esac
done

branch_name="screenshots"
work="" socket=""

make_throwaway_repo() { # make_throwaway_repo DIR: small deterministic repo with branches, a merge, tags and real diffs
  local dir="$1" day=0
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  git init -q -b main "$dir"
  commit() { # commit MESSAGE
    day=$((day + 1))
    export GIT_AUTHOR_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z" GIT_COMMITTER_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z"
    git -C "$dir" add -A
    git -C "$dir" commit -q -m "$1"
  }
  printf '# Demo\n\nA small demo project.\n' >"$dir/README.md"
  mkdir -p "$dir/src"
  printf 'int Add(int left, int right) => left + right;\n' >"$dir/src/math.txt"
  commit "Initial commit"
  printf 'int Subtract(int left, int right) => left - right;\n' >>"$dir/src/math.txt"
  commit "Add subtract"
  git -C "$dir" tag v0.1
  git -C "$dir" switch -q -c feature/multiply
  printf 'int Multiply(int left, int right) => left * right;\n' >"$dir/src/multiply.txt"
  commit "Add multiply"
  printf 'Multiply is exact for small values.\n' >"$dir/src/multiply.md"
  commit "Document multiply"
  git -C "$dir" switch -q main
  printf 'int Divide(int left, int right) => left / right;\n' >>"$dir/src/math.txt"
  printf 'Division truncates.\n' >>"$dir/README.md"
  commit "Add divide with a very long subject that has to be truncated when the terminal is narrow"
  export GIT_AUTHOR_DATE="2026-03-20T12:00:00Z" GIT_COMMITTER_DATE="2026-03-20T12:00:00Z"
  git -C "$dir" merge -q --no-ff -m "Merge feature/multiply" feature/multiply
  printf 'int Modulo(int left, int right) => left %% right;\n' >>"$dir/src/math.txt"
  commit "Add modulo"
  git -C "$dir" switch -q -c fix/overflow
  git -C "$dir" switch -q main
}

build_converter() { # prints the converter dll path
  local dll="$cache_dir/AnsiToSvg/AnsiToSvg.dll"
  if [ ! -f "$dll" ] || [ "$repo_root/tools/AnsiToSvg/Program.cs" -nt "$dll" ]; then
    echo "building tools/AnsiToSvg ..." >&2
    dotnet build "$repo_root/tools/AnsiToSvg" -c Release -o "$cache_dir/AnsiToSvg" -v quiet -nologo >&2
  fi
  echo "$dll"
}

render() {
  need tmux "install tmux"
  need rsvg-convert "install librsvg (rsvg-convert)"
  need dotnet "install the .NET SDK"
  need git "install git"
  [ -n "$binary" ] || { echo "--binary <path to gitunia> is required (build: dotnet build src/Gitunia.Tui -c Release -o <dir>)" >&2; exit 2; }
  [ -x "$binary" ] || { echo "binary not found or not executable: $binary" >&2; exit 2; }
  binary="$(cd "$(dirname "$binary")" && pwd)/$(basename "$binary")"
  mkdir -p "$out_dir" "$cache_dir"
  local converter repo
  converter="$(build_converter)"
  work="$(mktemp -d)"
  socket="gtshot-$$"
  repo="$source_repo"
  tmux_cmd() { tmux -L "$socket" -f /dev/null "$@"; }
  cleanup() { tmux_cmd kill-server 2>/dev/null || true; rm -rf "$work"; }
  trap cleanup EXIT
  if [ -z "$repo" ]; then repo="$work/repo"; make_throwaway_repo "$repo"; fi
  mkdir -p "$work/state" "$work/config"
  tmux_cmd start-server \; set -g exit-empty off \; set -g remain-on-exit on \; set -g escape-time 0 \; set -ga terminal-features ',*:RGB' \; set -ga terminal-overrides ',*:Tc'

  local scenario
  for scenario in ${scenarios//,/ }; do
    local columns=120 rows=36 theme=dark keys=""
    case "$scenario" in
      default) ;;
      diff) keys="Enter Enter" ;;
      narrow) columns=64 rows=30 ;;
      light) theme=light ;;
      *) echo "unknown scenario '$scenario'" >&2; exit 2 ;;
    esac
    tmux_cmd kill-session -t shot 2>/dev/null || true
    tmux_cmd new-session -d -s shot -x "$columns" -y "$rows" "exec env COLORTERM=truecolor XDG_STATE_HOME=$(printf '%q' "$work/state") XDG_CONFIG_HOME=$(printf '%q' "$work/config") GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null $(printf '%q %q' "$binary" "$repo")"
    wait_stable "$socket" 0
    if [ -n "$keys" ]; then
      # shellcheck disable=SC2086
      tmux_cmd send-keys -t shot $keys
      wait_stable "$socket" 1
    fi
    tmux_cmd capture-pane -t shot -p -e >"$work/$scenario.ansi"
    dotnet "$converter" --theme "$theme" --columns "$columns" --rows "$rows" <"$work/$scenario.ansi" >"$work/$scenario.svg"
    rsvg-convert "$work/$scenario.svg" -o "$out_dir/$scenario.png"
    echo "wrote $out_dir/$scenario.png" >&2
  done
}

wait_stable() { # wait_stable SOCKET MINIMUM_POLLS: until three identical captures in a row (about 10 s at most)
  local socket="$1" previous="" current same=0 attempt
  for attempt in $(seq 100); do
    sleep 0.1
    current="$(tmux -L "$socket" capture-pane -t shot -p -e 2>/dev/null || true)"
    if [ -n "$current" ] && [ "$current" = "$previous" ]; then same=$((same + 1)); else same=0; fi
    previous="$current"
    [ "$same" -ge 3 ] && return 0
  done
  echo "frame did not settle within 10 s; capturing anyway" >&2
}

with_branch() { # with_branch DIR: checks the orphan screenshots branch out into DIR (a temporary clone, never touches this worktree)
  need gh "install the GitHub CLI"
  local dir="$1" remote_url
  remote_url="$(git -C "$repo_root" remote get-url origin)"
  if git -C "$repo_root" ls-remote --exit-code --heads origin "$branch_name" >/dev/null 2>&1; then
    git clone -q --branch "$branch_name" --single-branch --depth 1 "$remote_url" "$dir"
  else
    git init -q -b "$branch_name" "$dir"
    git -C "$dir" remote add origin "$remote_url"
  fi
  git -C "$dir" config user.name "$(git -C "$repo_root" config user.name)"
  git -C "$dir" config user.email "$(git -C "$repo_root" config user.email)"
}

repository_slug() { gh repo view --json nameWithOwner -q .nameWithOwner; }

publish() {
  [ -n "$pull_request" ] || { echo "--pr <number> is required" >&2; exit 2; }
  ls "$out_dir"/*.png >/dev/null 2>&1 || { echo "no PNGs in $out_dir; run render first" >&2; exit 2; }
  local slug stamp target file pinned
  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  with_branch "$work/clone"
  slug="$(repository_slug)"
  stamp="$(git -C "$repo_root" rev-parse --short HEAD)"
  target="pr-$pull_request/$stamp"
  mkdir -p "$work/clone/$target"
  cp "$out_dir"/*.png "$work/clone/$target/"
  git -C "$work/clone" add -A
  if ! git -C "$work/clone" diff --cached --quiet; then
    git -C "$work/clone" commit -q -m "chore: screenshots for PR #$pull_request @ $stamp"
    git -C "$work/clone" push -q origin "$branch_name"
  fi
  pinned="$(git -C "$work/clone" rev-parse HEAD)"
  echo "pushed to $branch_name ($target @ ${pinned:0:7})" >&2
  for file in "$out_dir"/*.png; do
    local name; name="$(basename "$file" .png)"
    echo "**$name**"
    echo
    echo "![$name](https://github.com/$slug/raw/$pinned/$target/$name.png)"
    echo
  done
}

clean() {
  local directory number state removed=0
  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  with_branch "$work/clone"
  for directory in "$work"/clone/pr-*; do
    [ -d "$directory" ] || continue
    number="${directory##*/pr-}"
    state="$(gh pr view "$number" --json state -q .state 2>/dev/null || echo UNKNOWN)"
    if [ "$state" = "MERGED" ] || [ "$state" = "CLOSED" ]; then
      git -C "$work/clone" rm -rq "pr-$number"
      removed=$((removed + 1))
      echo "removed pr-$number ($state)" >&2
    fi
  done
  if [ "$removed" -gt 0 ]; then
    git -C "$work/clone" commit -q -m "chore: remove screenshots of merged or closed PRs"
    git -C "$work/clone" push -q origin "$branch_name"
  else
    echo "nothing to clean" >&2
  fi
}

case "$command_name" in
  render) render ;;
  publish) publish ;;
  clean) clean ;;
  *) usage ;;
esac
