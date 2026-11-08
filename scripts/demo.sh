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

# Throwaway repo: about 300 commits, three authors, main/develop/release branches and feature branches of mixed length
# growing in parallel with main (a few crossings; merged ones keep their label, a few stay open), tags, and a working-tree change in README.md.
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
  # Feature branches of mixed length (1-8 commits) grow in parallel with main: at most two are open at once, so lanes
  # cross where a merge cuts over another branch. The last one of each 24 stays unmerged.
  lens=(2 6 1 4 8 2 3 5 1 7)
  act_ref=() act_tip=() act_left=() act_n=()
  for ((w = 0; w < 64; w++)); do
    topic=${topics[$((w % ${#topics[@]}))]}
    commit main "${verbs[$((w % 8))]} the ${areas[$((w % 8))]} view" "$main_tip"; main_tip=$last
    # Advance every open branch by one commit; merge it into main when it is complete.
    n_ref=() n_tip=() n_left=() n_n=()
    for i in "${!act_ref[@]}"; do
      ref=${act_ref[$i]} tip=${act_tip[$i]} left=${act_left[$i]} cnt=${act_n[$i]}
      if [ "$left" -gt 0 ]; then
        cnt=$((cnt + 1))
        commit "$ref" "${verbs[$(((mark + cnt) % 8))]} ${ref#feature/} (part $cnt)" "$tip"; tip=$last; left=$((left - 1))
      fi
      if [ "$left" -eq 0 ]; then
        commit main "Merge $ref" "$main_tip" "$tip"; main_tip=$last
      else
        n_ref+=("$ref"); n_tip+=("$tip"); n_left+=("$left"); n_n+=("$cnt")
      fi
    done
    act_ref=("${n_ref[@]}") act_tip=("${n_tip[@]}") act_left=("${n_left[@]}") act_n=("${n_n[@]}")
    if [ $((w % 24)) -eq 23 ]; then
      branch "feature/$topic" "$main_tip" 3 "Add $topic"
    elif [ "${#act_ref[@]}" -lt 2 ] && [ $((w % 4)) -ne 1 ]; then
      commit "feature/$topic" "Add $topic" "$main_tip"
      act_ref+=("feature/$topic"); act_tip+=("$last"); act_left+=("$((lens[w % 10] - 1))"); act_n+=(1)
    fi
    # Develop is a single line that catches up with main now and then,.
    if [ $((w % 6)) -eq 5 ]; then commit develop "Merge main into develop" "$dev_tip" "$main_tip"; dev_tip=$last; fi
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
