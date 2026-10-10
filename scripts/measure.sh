#!/usr/bin/env bash
# Measure a Gitunia-style TUI binary inside a private tmux server (Linux/macOS, bash >= 5, tmux).
# Contract the binary must meet is documented in docs/testing.md ("Measurement harness").
#
# Usage: scripts/measure.sh <binary> <repo> [options]
#   --runs N          cold-start repetitions (default 5)
#   --keys N          key-latency samples (default 20)
#   --marker TEXT     text of the newest commit subject (default: read from <repo> HEAD)
#   --oldest TEXT     text of the oldest commit subject (default: read from <repo> root)
#   --size WxH        terminal size (default 120x40)
#   --only-start      stop after cold start + size (for binaries without navigation)
#   --timeout SEC     per-wait timeout (default 10)
# Output: `key=value` lines on stdout, human-readable progress on stderr.
# Times are wall-clock milliseconds measured by polling `tmux capture-pane`; they
# include one tmux client round trip (~1-3 ms) per poll, so compare binaries with
# the same harness, don't treat them as absolute.
set -euo pipefail

if [ -z "${EPOCHREALTIME:-}" ]; then
  echo "bash >= 5 required (EPOCHREALTIME)" >&2
  exit 2
fi
if [ $# -lt 2 ]; then
  sed -n '2,16p' "$0" >&2
  exit 2
fi
bin="$1"; repo="$2"; shift 2
runs=5 keys=20 marker="" oldest="" cols=120 rows=40 only_start=0 timeout=10
while [ $# -gt 0 ]; do
  case "$1" in
    --runs) runs="$2"; shift 2 ;;
    --keys) keys="$2"; shift 2 ;;
    --marker) marker="$2"; shift 2 ;;
    --oldest) oldest="$2"; shift 2 ;;
    --size) cols="${2%x*}"; rows="${2#*x}"; shift 2 ;;
    --only-start) only_start=1; shift ;;
    --timeout) timeout="$2"; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
if [ -z "$marker" ]; then
  marker="$(git -C "$repo" log -1 --format=%s HEAD)"
fi
if [ -z "$oldest" ]; then
  oldest="$(git -C "$repo" log --max-parents=0 -1 --format=%s HEAD)"
fi

sock="gtm-$$"
t() { tmux -L "$sock" -f /dev/null "$@"; }
cleanup() { t kill-server 2>/dev/null || true; }
trap cleanup EXIT

now_us() { local n="$EPOCHREALTIME"; echo "${n/./}"; }
pane() { t capture-pane -t m -p -e; }
ms() { # microseconds -> milliseconds with one decimal
  awk -v us="$1" 'BEGIN { printf "%.1f", us / 1000 }'
}
stats() { # reads ms values on stdin: prints median p95 max
  sort -n | awk '{ v[NR] = $1 } END {
    if (NR == 0) { print "nan nan nan"; exit }
    printf "%.1f %.1f %.1f\n", v[int((NR + 1) / 2)], v[int(NR * 0.95 + 0.999999)], v[NR] }'
}

start_server() {
  t start-server \; set -g exit-empty off \; set -g remain-on-exit on
}
start_app() { # starts the app in session "m"; the caller times around this
  t new-session -d -s m -x "$cols" -y "$rows" "exec $(printf '%q %q' "$bin" "$repo")"
}
stop_app() { t kill-session -t m 2>/dev/null || true; }

# wait_for TEXT: polls until the pane contains TEXT; sets WAIT_US, returns 1 on timeout.
wait_for() {
  local text="$1" t0 limit
  t0=$(now_us); limit=$((t0 + timeout * 1000000))
  while :; do
    if pane | grep -qF -- "$text"; then WAIT_US=$(( $(now_us) - t0 )); return 0; fi
    [ "$(now_us)" -gt "$limit" ] && return 1
  done
}
# wait_change BASELINE: polls until the pane differs from BASELINE (escape codes included).
wait_change() {
  local base="$1" t0 limit
  t0=$(now_us); limit=$((t0 + timeout * 1000000))
  while :; do
    [ "$(pane)" != "$base" ] && { WAIT_US=$(( $(now_us) - t0 )); return 0; }
    [ "$(now_us)" -gt "$limit" ] && return 1
  done
}
# wait_stable: polls until 3 consecutive captures are identical; sets WAIT_US to time of last change.
wait_stable() {
  local prev cur same=0 t0 last limit
  t0=$(now_us); limit=$((t0 + timeout * 1000000)); last=$t0; prev="$(pane)"
  while [ "$same" -lt 3 ]; do
    cur="$(pane)"
    if [ "$cur" = "$prev" ]; then same=$((same + 1)); else same=0; last=$(now_us); prev="$cur"; fi
    [ "$(now_us)" -gt "$limit" ] && return 1
  done
  WAIT_US=$((last - t0))
}

echo "bin=$bin"
if stat -c%s "$bin" >/dev/null 2>&1; then size=$(stat -c%s "$bin"); else size=$(stat -f%z "$bin"); fi
echo "binary_bytes=$size"
echo "marker=$marker"

# --- cold start: new-session until the newest commit subject (marker) is on screen
start_server
vals=""
for _ in $(seq "$runs"); do
  t0=$(now_us)
  start_app
  if wait_for "$marker"; then
    vals+="$(ms $(( $(now_us) - t0 )))"$'\n'
  else
    echo "cold start: marker not seen within ${timeout}s" >&2
  fi
  stop_app
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "cold_start_ms_median=$med"
echo "cold_start_ms_p95=$p95"
echo "cold_start_ms_max=$max"
echo "cold_start_samples=$(printf '%s' "$vals" | grep -c . || true)"

if [ "$only_start" -eq 1 ]; then exit 0; fi

# --- navigation measurements in one session
start_app
if ! wait_for "$marker"; then echo "app never showed marker; skipping navigation" >&2; exit 1; fi
wait_stable || true

# key-to-frame latency: `j` must change the rendered frame (selection highlight counts, -e keeps attributes)
vals=""
for _ in $(seq "$keys"); do
  base="$(pane)"
  t0=$(now_us)
  t send-keys -t m j
  if wait_change "$base"; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "key_latency_ms_median=$med"
echo "key_latency_ms_p95=$p95"
echo "key_latency_ms_max=$max"
echo "key_samples=$(printf '%s' "$vals" | grep -c . || true)"

# page scroll: Ctrl-d x keys, each until the frame changes
vals=""
for _ in $(seq "$keys"); do
  base="$(pane)"; t0=$(now_us)
  t send-keys -t m C-d
  if wait_change "$base"; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "page_scroll_ms_median=$med"
echo "page_scroll_ms_max=$max"

# burst: 200 j in one send-keys; time from send until the frame changed and then stopped changing
base="$(pane)"
t0=$(now_us)
t send-keys -t m -N 200 j
if wait_change "$base" && first=$WAIT_US && wait_stable; then
  echo "burst_200j_settle_ms=$(ms $((first + WAIT_US)))"
else
  echo "burst_200j_settle_ms=nan"
fi

# jump to the end of history (G) until the oldest commit is visible, then back (gg)
t0=$(now_us)
t send-keys -t m G
if wait_for "$oldest"; then echo "jump_to_end_ms=$(ms $(( $(now_us) - t0 )))"; else echo "jump_to_end_ms=nan"; fi
t0=$(now_us)
t send-keys -t m g g
if wait_for "$marker"; then echo "jump_to_start_ms=$(ms $(( $(now_us) - t0 )))"; else echo "jump_to_start_ms=nan"; fi

# resize: the frame must change after the window shrinks and still show the newest subject
t send-keys -t m g g
wait_for "$marker" || true
wait_stable || true
base="$(pane)"
t0=$(now_us)
t resize-window -t m -x $((cols - 30)) -y $((rows - 10))
if wait_change "$base" && pane | grep -qF -- "$marker"; then echo "resize_ms=$(ms "$WAIT_US")"; else echo "resize_ms=nan"; fi

# memory of the app process (RSS, KiB), if the pane pid is still alive
pid="$(t display-message -t m -p '#{pane_pid}')"
rss=$(ps -o rss= -p "$pid" 2>/dev/null | tr -d ' ' || true)
echo "rss_kib=${rss:-nan}"
