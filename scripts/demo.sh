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

# Throwaway repo: about 300 commits, three authors, a git-flow history with straight lanes (main takes only release merges, develop is
# one line, short feature bubbles one after another, releases tagged and merged back), three short open branches, and a working-tree change in README.md.
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
    for ((i = 2; i <= n; i++)); do commit "$ref" "${verbs[$(((mark + i) % 8))]} ${title#Add } (part $i)"; done
  }

  # A git-flow history drawn to read well: main is one straight lane that only receives release merges, develop is a second
  # straight lane, features are short bubbles that go one after another off develop, and every release branch is merged
  # into main (tagged) and back into develop. Nothing dangles except two short open branches near the tip.
  topics=(sidebar-filter graph-lanes ref-badges stash-list diff-wrap rebase-todo tag-dialog remote-fetch config-keys status-bar
          worktree-prune commit-search file-tree hunk-stage reflog-view theme-colors mouse-wheel tab-restore
          conflict-editor cherry-pick revert-flow push-confirm pull-rebase bisect-hint help-overlay session-save)
  lens=(2 3 4 3 2 4 3 2)
  commit main "Initial commit"; main_tip=$last
  commit develop "Start the develop line" "$main_tip"; dev_tip=$last
  t_i=0
  for ((rel = 1; rel <= 7; rel++)); do
    for ((f = 0; f < 8; f++)); do
      topic=${topics[$((t_i % ${#topics[@]}))]}; t_i=$((t_i + 1))
      base=$dev_tip
      # Develop moves on by one commit while the feature is open, so each feature is a bubble beside the develop lane.
      commit develop "${verbs[$((mark % 8))]} the ${areas[$((mark % 8))]} view" "$dev_tip"; dev_tip=$last
      branch "feature/$topic" "$base" "${lens[$f]}" "Add $topic"; feature_tip=$last
      commit develop "Merge feature/$topic" "$dev_tip" "$feature_tip"; dev_tip=$last
    done
    if [ "$rel" -eq 7 ]; then
      # Open work near the tip (feature/palette is merged): short branches off develop that stay unmerged (fix/overflow and feature/multiply are used by demo.tape).
      base=$dev_tip
      commit develop "Cover the graph view" "$dev_tip"; dev_tip=$last
      branch feature/palette "$base" 3 "Add palette"; feature_tip=$last
      commit develop "Merge feature/palette" "$dev_tip" "$feature_tip"; dev_tip=$last
      branch feature/multiply "$dev_tip" 2 "Add multiply"
      branch fix/overflow "$dev_tip" 1 "Add a checked variant"
    fi
    # Release cut from develop, merged into main (tagged) and back into develop.
    commit "release/0.$rel" "Prepare release 0.$rel" "$dev_tip"
    commit "release/0.$rel" "Update the changelog for 0.$rel"; rel_tip=$last
    commit main "Merge release/0.$rel" "$main_tip" "$rel_tip"; main_tip=$last
    tag "v0.$rel.0" "$main_tip"
    if [ "$rel" -lt 7 ]; then
      commit develop "Merge release/0.$rel back into develop" "$dev_tip" "$main_tip"; dev_tip=$last
    fi
  done
  printf 'done\n' >>"$stream"
  git -C "$repo" fast-import --quiet <"$stream"
  git -C "$repo" checkout -q -f main
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
