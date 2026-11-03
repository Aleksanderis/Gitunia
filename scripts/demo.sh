#!/usr/bin/env bash
# Records the README demo: the real binary on a throwaway repo, scripted by scripts/demo.tape (VHS + ttyd), Linux only.
# Usage: scripts/demo.sh [--binary <gitunia>] [--out <dir>]
#   --binary  the AOT binary (default: publishes src/Gitunia.Tui into a temp dir first)
#   --out     output directory (default: $GITUNIA_LANDING_MEDIA, else ~/Projects/Gitunia-Landing/media, the landing page checkout
#             of gitunia.github.io): demo.gif and demo.webm. The README links to the landing copy; this repo does not carry them.
# Needs vhs, ttyd, ffmpeg, git (and dotnet unless --binary). Nothing here runs in CI.
# See docs/testing.md, "Regenerating the README demo". Separate from the screenshots flow (scripts/screenshot.sh).
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
binary="" out_dir="${GITUNIA_LANDING_MEDIA:-$HOME/Projects/Gitunia-Landing/media}"
while [ $# -gt 0 ]; do
  case "$1" in
    --binary|--out)
      [ $# -ge 2 ] || { echo "$1 needs a value" >&2; exit 2; }
      if [ "$1" = "--binary" ]; then binary="$2"; else out_dir="$2"; fi
      shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

# A fixed, short work dir keeps the paths visible in the GIF (the worktree path dialog, the status line) identical on every run.
work="/tmp/gitunia-demo"
# Only one recording at a time: the fixed dir is shared. Paths passed to --binary/--out must live outside it.
case "$binary $out_dir" in *"$work"*) echo "--binary and --out must not point inside $work" >&2; exit 2 ;; esac
mkdir /tmp/gitunia-demo.lock 2>/dev/null || { echo "another demo recording is running (or remove /tmp/gitunia-demo.lock)" >&2; exit 3; }
trap 'rmdir /tmp/gitunia-demo.lock' EXIT
[ ! -e "$work" ] || [ -e "$work/.gitunia-demo" ] || { echo "$work exists and is not a demo work dir; remove it first" >&2; exit 3; }
rm -rf "$work"
mkdir -p "$work"
touch "$work/.gitunia-demo"
trap 'rm -rf "$work"; rmdir /tmp/gitunia-demo.lock' EXIT
if [ -z "$binary" ]; then
  command -v dotnet >/dev/null 2>&1 || { echo "missing tool 'dotnet' (or pass --binary)" >&2; exit 3; }
  dotnet publish "$repo_root/src/Gitunia.Tui" -c Release -o "$work/bin" -nologo -v quiet >&2
  binary="$work/bin/gitunia"
fi
[ -x "$binary" ] || { echo "binary '$binary' not found" >&2; exit 3; }

# Throwaway repo: about 300 commits, three authors, main/develop/release branches and short feature branches
# merged back one at a time (a mostly straight graph; merged ones are deleted, a few stay open), tags, and a working-tree change in README.md.
# Built with git fast-import from a generated stream: deterministic (no clock, no randomness) and fast.
repo="$work/demo-repo"
(
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  git init -q -b main "$repo"
  stream="$work/stream"
  : >"$stream"
  t=$(date -u -d 2026-01-05T09:00:00Z +%s)
  mark=0 last=0
  authors=("Ada Lovelace <ada@example.com>" "Grace Hopper <grace@example.com>" "Alan Turing <alan@example.com>")
  areas=(graph branches worktrees staging diff stash tags config)
  verbs=(Add Fix Refine Extend Cover Simplify Document Rework)
  readme=$'# Demo\n\nA small calculator.\n\n## Usage\n\nCall the functions from src.\n\n## License\n\nMIT.\n'
  # commit <ref> <message> [from-mark] [merge-mark]: sets $last to the new mark; time advances 11-19 hours per commit.
  commit() {
    local ref=$1 msg=$2 from=${3:-} merge=${4:-} area author body
    mark=$((mark + 1)); last=$mark
    t=$((t + 39600 + (mark * 7919) % 29000))
    area=${areas[$((mark % 8))]}
    author=${authors[$(((mark / 3 + mark) % 3))]}
    body="revision $mark of $area"
    {
      printf 'commit refs/heads/%s\nmark :%d\n' "$ref" "$mark"
      printf 'author %s %d +0000\ncommitter %s %d +0000\n' "$author" "$t" "$author" "$t"
      printf 'data %d\n%s\n' "${#msg}" "$msg"
      [ -z "$from" ] || printf 'from :%d\n' "$from"
      [ -z "$merge" ] || printf 'merge :%d\n' "$merge"
      if [ "$mark" -eq 1 ]; then
        printf 'M 100644 inline README.md\ndata %d\n%s\n' "${#readme}" "$readme"
      fi
      printf 'M 100644 inline src/%s.txt\ndata %d\n%s\n\n' "$area" "${#body}" "$body"
    } >>"$stream"
  }
  tag() { printf 'reset refs/tags/%s\nfrom :%d\n\n' "$1" "$2" >>"$stream"; }
  # branch <ref> <base-mark> <count> <title>: a side branch of <count> commits; its tip lands in $last.
  branch() {
    local ref=$1 base=$2 n=$3 title=$4 i
    commit "$ref" "$title" "$base"
    for ((i = 2; i <= n; i++)); do commit "$ref" "${verbs[$(((mark + i) % 8))]} $title (part $i)"; done
  }

  commit main "Initial commit"; main_tip=$last
  branch feature/multiply "$main_tip" 2 "Add multiply"; multiply_tip=$last
  commit main "Add divide" "$main_tip"; main_tip=$last
  commit main "Merge feature/multiply" "$main_tip" "$multiply_tip"; main_tip=$last
  branch fix/overflow "$main_tip" 1 "Add a checked variant"
  commit develop "Start the develop line" "$main_tip"; dev_tip=$last
  tag v0.1.0 "$main_tip"

  topics=(sidebar-filter graph-lanes ref-badges stash-list diff-wrap rebase-todo tag-dialog remote-fetch config-keys status-bar
          worktree-prune commit-search file-tree hunk-stage reflog-view palette theme-colors mouse-wheel tab-restore
          conflict-editor cherry-pick revert-flow push-confirm pull-rebase bisect-hint help-overlay session-save)
  for ((w = 0; w < 48; w++)); do
    topic=${topics[$((w % ${#topics[@]}))]}
    # Mostly straight: main advances, a short feature branch is cut and merged back before the next one starts, so at most
    # one side lane is open at a time. Every 24th feature stays open, every 4th merges into develop instead of main.
    commit main "${verbs[$((w % 8))]} the ${areas[$((w % 8))]} view" "$main_tip"; main_tip=$last
    branch "feature/$topic" "$main_tip" $((2 + w % 3)) "Add $topic"; feat_tip=$last
    commit main "${verbs[$(((w + 3) % 8))]} ${areas[$(((w + 3) % 8))]} errors" "$main_tip"; main_tip=$last
    if [ $((w % 24)) -ne 23 ]; then
      commit main "Merge feature/$topic" "$main_tip" "$feat_tip"; main_tip=$last
    fi
    # Develop is a single line that catches up with main now and then, plus one open fix branch.
    if [ $((w % 6)) -eq 5 ]; then commit develop "Merge main into develop" "$dev_tip" "$main_tip"; dev_tip=$last; fi
    if [ $((w % 16)) -eq 9 ]; then branch "fix/crash-$w" "$dev_tip" 2 "Fix crash in ${areas[$((w % 8))]} ($w)"; fi
    # Releases: a tag on main every 16 iterations and a release branch cut from it with one hotfix.
    if [ $((w % 16)) -eq 15 ]; then
      rel=$((w / 16 + 1))
      tag "v0.$((rel + 1)).0" "$main_tip"
      commit "release/0.$((rel + 1))" "Prepare release 0.$((rel + 1))" "$main_tip"
      commit "release/0.$((rel + 1))" "Hotfix for 0.$((rel + 1)).1"
      tag "v0.$((rel + 1)).1" "$last"
      commit main "Bump version to 0.$((rel + 2)).0-dev" "$main_tip"; main_tip=$last
    fi
  done
  printf 'done\n' >>"$stream"
  git -C "$repo" fast-import --quiet <"$stream"
  git -C "$repo" checkout -q -f main
  # Merged feature branches are gone in a tidy repo; keep feature/multiply (the demo creates a worktree on it) and the open ones.
  git -C "$repo" for-each-ref --merged main --format='%(refname:short)' refs/heads/feature |
    grep -vx feature/multiply | xargs -r git -C "$repo" branch -q -d
  # Uncommitted edits in README.md (it is identical on every branch, so switching branches stays possible):
  # added lines in one hunk, so the demo can stage only some of them.
  sed -i 's/^Call the functions from src\.$/&\n\nDivide truncates toward zero.\nModulo keeps the sign of the dividend.\nPower is not implemented yet./' "$repo/README.md"
)

[ -d "$out_dir" ] || [ -d "$(dirname "$out_dir")" ] || { echo "output directory $out_dir not found: pass --out or set GITUNIA_LANDING_MEDIA to the landing page media folder" >&2; exit 2; }
mkdir -p "$out_dir" "$work/config" "$work/state"
export DEMO_BIN="$binary" DEMO_REPO="$repo"
# Deliberate isolation of the recorded app from the owner config and git identity (the app itself sets none of these).
export XDG_CONFIG_HOME="$work/config" XDG_STATE_HOME="$work/state"
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
cd "$work"
vhs "$script_dir/demo.tape" >&2
mv "$work/demo.gif" "$out_dir/demo.gif"
mv "$work/demo.webm" "$out_dir/demo.webm"
ls -l "$out_dir/demo.gif" "$out_dir/demo.webm" >&2
