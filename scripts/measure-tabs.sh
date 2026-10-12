#!/usr/bin/env bash
# Measure cold start and RSS of gitunia restoring many tabs (Linux/macOS, bash >= 5, tmux, git).
# Builds throwaway repos and a session file in a temp state directory; never touches real repos or state.
#
# Usage: scripts/measure-tabs.sh <binary> [options]
#   --tabs N       restored tabs (default 12)
#   --commits N    commits per repo (default 300)
#   --runs N       cold-start repetitions (default 5)
#   --size WxH     terminal size (default 120x40)
#   --timeout SEC  per-wait timeout (default 10)
# Output: key=value lines. Times are wall-clock ms from polling `tmux capture-pane` (1-3 ms floor).
# The active tab is the first one; each repo's newest subject is "Head of tab-NN", used as the marker.
set -euo pipefail

if [ -z "${EPOCHREALTIME:-}" ]; then echo "bash >= 5 required (EPOCHREALTIME)" >&2; exit 2; fi
if [ $# -lt 1 ]; then sed -n '2,13p' "$0" >&2; exit 2; fi
bin="$(realpath "$1")"; shift
tabs=12 commits=300 runs=5 cols=120 rows=40 timeout=10
while [ $# -gt 0 ]; do
  case "$1" in
    --tabs) tabs="$2"; shift 2 ;;
    --commits) commits="$2"; shift 2 ;;
    --runs) runs="$2"; shift 2 ;;
    --size) cols="${2%x*}"; rows="${2#*x}"; shift 2 ;;
    --timeout) timeout="$2"; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

work="$(mktemp -d)"
sock="gtt-$$"
t() { tmux -L "$sock" -f /dev/null "$@"; }
cleanup() { t kill-server 2>/dev/null || true; rm -rf "$work"; }
trap cleanup EXIT

export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
export HOME="$work/home" XDG_STATE_HOME="$work/state" XDG_CONFIG_HOME="$work/config" XDG_CACHE_HOME="$work/cache"
mkdir -p "$HOME" "$XDG_STATE_HOME/gitunia"
session="$XDG_STATE_HOME/gitunia/session.json"

now_us() { local n="$EPOCHREALTIME"; echo "${n/./}"; }
ms() { awk -v us="$1" 'BEGIN { printf "%.1f", us / 1000 }'; }
pane() { t capture-pane -t m -p; }
wait_for() {
  local text="$1" t0 limit
  t0=$(now_us); limit=$((t0 + timeout * 1000000))
  while :; do
    if pane | grep -qF -- "$text"; then WAIT_US=$(( $(now_us) - t0 )); return 0; fi
    [ "$(now_us)" -gt "$limit" ] && return 1
  done
}
stats() { sort -n | awk '{ v[NR] = $1 } END {
  if (NR == 0) { print "nan nan nan"; exit }
  printf "%.1f %.1f %.1f\n", v[int((NR + 1) / 2)], v[int(NR * 0.95 + 0.999999)], v[NR] }'; }
rss_kib() {
  local pid
  pid="$(t display-message -t m -p '#{pane_pid}')"
  ps -o rss= -p "$pid" 2>/dev/null | tr -d " " | grep . || echo nan
}

# Throwaway repos: linear history, one tiny file change per commit, built with fast-import.
entries=""
for index in $(seq 0 $((tabs - 1))); do
  name="$(printf 'tab-%02d' "$index")"
  repo="$work/repos/$name"
  mkdir -p "$repo"
  git -C "$repo" init -q -b main
  {
    for n in $(seq 1 "$commits"); do
      subject="Commit $n of $name"
      [ "$n" -eq "$commits" ] && subject="Head of $name"
      printf 'commit refs/heads/main\nmark :%d\ncommitter T <t@example.com> %d +0000\ndata %d\n%s\n' "$n" "$((1700000000 + n))" "${#subject}" "$subject"
      [ "$n" -gt 1 ] && printf 'from :%d\n' "$((n - 1))"
      printf 'M 644 inline f.txt\ndata %d\n%s\n\n' "${#n}" "$n"
    done
  } | git -C "$repo" fast-import --quiet
  git -C "$repo" reset -q --hard main
  entries+="{\"workingTreePath\":\"$repo\",\"gitDirectory\":\"$repo/.git\",\"commonDirectory\":\"$repo/.git\",\"focusedPane\":\"Graph\"},"
done
printf '{"version":1,"activeTabIndex":0,"tabs":[%s],"recents":[],"pins":[]}\n' "${entries%,}" > "$work/session.template"

echo "bin=$bin"
echo "tabs=$tabs"
echo "commits_per_repo=$commits"

t start-server \; set -g exit-empty off \; set -g remain-on-exit on
start_app() { t new-session -d -s m -x "$cols" -y "$rows" "exec $(printf '%q' "$bin")"; }

vals=""
rss_vals=""
for _ in $(seq "$runs"); do
  cp "$work/session.template" "$session"
  t0=$(now_us)
  start_app
  if wait_for "Head of tab-00"; then
    vals+="$(ms $(( $(now_us) - t0 )))"$'\n'
    sleep 1
    rss_vals+="$(rss_kib)"$'\n'
  else
    echo "cold start: marker not seen within ${timeout}s" >&2
  fi
  t kill-session -t m 2>/dev/null || true
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "cold_start_ms_median=$med"
echo "cold_start_ms_p95=$p95"
echo "cold_start_ms_max=$max"
read -r med _ max <<<"$(printf '%s' "$rss_vals" | stats)"
echo "rss_kib_after_start_median=$med"
echo "rss_kib_after_start_max=$max"

# Visit every tab in turn (gt): time to the tab's newest subject, then RSS with all tabs loaded.
cp "$work/session.template" "$session"
start_app
wait_for "Head of tab-00" || { echo "app never showed the first tab" >&2; exit 1; }
sleep 1
vals=""
for index in $(seq 1 $((tabs - 1))); do
  t0=$(now_us)
  t send-keys -t m g t
  if wait_for "Head of $(printf 'tab-%02d' "$index")"; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
done
read -r med _ max <<<"$(printf '%s' "$vals" | stats)"
echo "first_visit_switch_ms_median=$med"
echo "first_visit_switch_ms_max=$max"
sleep 1
echo "rss_kib_all_visited=$(rss_kib)"

# Revisit: jump back to the first tab (already loaded).
t0=$(now_us)
t send-keys -t m 1 g t
if wait_for "Head of tab-00"; then echo "revisit_switch_ms=$(ms "$(( $(now_us) - t0 ))")"; else echo "revisit_switch_ms=nan"; fi
