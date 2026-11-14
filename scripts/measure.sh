#!/usr/bin/env bash
# Measure a Gitunia-style TUI binary inside a private tmux server (Linux/macOS, bash >= 5, tmux; Windows: measure.ps1).
# Contract the binary must meet is documented in docs/testing.md ("Measurement harness").
#
# Usage: scripts/measure.sh <binary> <repo> [options]
#   --runs N          cold-start repetitions (default 5)
#   --keys N          key-latency samples (default 20)
#   --marker TEXT     text of the newest commit subject (default: HEAD subject up to the first colon, else 12 chars)
#   --oldest TEXT     text of the oldest commit subject (default: root subject up to the first colon, else 12 chars)
#   --size WxH        terminal size (default 120x40)
#   --only-start      stop after cold start + size (for binaries without navigation)
#   --blame           blame case (#1137): open the blame view of the file in the newest commit (`<space>gb`) and measure
#                     open, `j`, Ctrl-d, G and RSS instead of the history cases; use a repo from gen-big-file-repo.sh
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
  sed -n '2,18p' "$0" >&2
  exit 2
fi
bin="$1"; repo="$2"; shift 2
runs=5 keys=20 marker="" oldest="" cols=120 rows=40 only_start=0 blame=0 timeout=10
while [ $# -gt 0 ]; do
  case "$1" in
    --runs) runs="$2"; shift 2 ;;
    --keys) keys="$2"; shift 2 ;;
    --marker) marker="$2"; shift 2 ;;
    --oldest) oldest="$2"; shift 2 ;;
    --size) cols="${2%x*}"; rows="${2#*x}"; shift 2 ;;
    --only-start) only_start=1; shift ;;
    --blame) blame=1; shift ;;
    --timeout) timeout="$2"; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
# The app truncates long subjects with an ellipsis (the newest row also carries ref decorations),
# so a full subject may never appear on screen, and the decorations cut the newest row shorter
# than the others (#1001). Defaults are short prefixes: up to the first colon, else 12 chars (a colon prefix under 8 chars, like "fix", is not specific enough);
# explicit --marker/--oldest are used as given.
prefix() {
  local s="$1" head="${1%%:*}"
  if [ "$head" != "$s" ] && [ -n "$head" ] && [ "${#head}" -ge 8 ] && [ "${#head}" -le 20 ]; then printf '%s' "$head"; else printf '%s' "${s:0:12}"; fi
}
if [ -z "$marker" ]; then
  marker="$(git -C "$repo" log -1 --format=%s HEAD)"
  marker="$(prefix "$marker")"
fi
if [ -z "$oldest" ]; then
  oldest="$(git -C "$repo" log --max-parents=0 -1 --format=%s HEAD)"
  oldest="$(prefix "$oldest")"
fi

# Private state/config dirs: a restored session (extra tabs, saved scroll position) would change
# both what is on screen and the RSS. Linux honours XDG_*; macOS keeps state under ~/Library (not isolated here).
isolated="$(mktemp -d)"; mkdir -p "$isolated/state" "$isolated/config"
sock="gtm-$$"
t() { tmux -L "$sock" -f /dev/null "$@"; }
cleanup() { t kill-server 2>/dev/null || true; rm -rf "$isolated"; }
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
  t new-session -d -s m -x "$cols" -y "$rows" "exec env XDG_STATE_HOME=$(printf '%q' "$isolated/state") XDG_CONFIG_HOME=$(printf '%q' "$isolated/config") $(printf '%q %q' "$bin" "$repo")"
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

# --- blame case (#1137): open the blame view of the file shown in the newest commit, then key latency inside it
if [ "$blame" -eq 1 ]; then
  start_app
  if ! wait_for "$marker"; then echo "app never showed marker; skipping blame" >&2; exit 1; fi
  wait_stable || true
  # a blamed row: a short hash in the first column, then the line number and `|`
  blame_row='[0-9a-f]{7} .*[0-9]+ \| '
  plain() { t capture-pane -t m -p; }
  t send-keys -t m Tab  # focus the details pane (its file is the one blamed)
  sleep 0.5
  t0=$(now_us)
  t send-keys -t m Space g b
  until plain | grep -qE -- "$blame_row"; do
    [ "$(now_us)" -gt $((t0 + timeout * 1000000)) ] && break
  done
  if plain | grep -qE -- "$blame_row"; then echo "blame_first_rows_ms=$(ms $(( $(now_us) - t0 )))"; else echo "blame_first_rows_ms=nan"; echo "blame view did not open" >&2; exit 1; fi
  sleep 2; wait_stable || true
  vals=""
  for _ in $(seq "$keys"); do
    base="$(pane)"; t0=$(now_us)
    t send-keys -t m j
    if wait_change "$base"; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
  done
  read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
  echo "blame_j_ms_median=$med"; echo "blame_j_ms_p95=$p95"; echo "blame_j_ms_max=$max"
  vals=""
  for _ in $(seq "$keys"); do
    base="$(pane)"; t0=$(now_us)
    t send-keys -t m C-d
    if wait_change "$base"; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
  done
  read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
  echo "blame_page_ms_median=$med"; echo "blame_page_ms_p95=$p95"; echo "blame_page_ms_max=$max"
  base="$(pane)"; t0=$(now_us)
  t send-keys -t m G
  if wait_change "$base"; then echo "blame_jump_end_ms=$(ms $(( $(now_us) - t0 )))"; else echo "blame_jump_end_ms=nan"; fi
  wait_stable || true
  pid="$(t display-message -t m -p '#{pane_pid}')"
  rss=$(ps -o rss= -p "$pid" 2>/dev/null | tr -d ' ' || true)
  echo "blame_rss_kib=${rss:-nan}"
  exit 0
fi

# --- navigation measurements in one session
start_app
if ! wait_for "$marker"; then echo "app never showed marker; skipping navigation" >&2; exit 1; fi
wait_stable || true

# details latency: `j`, then the details pane header (`Details  Changes  Files  <hash>  n files`) must show another commit
# and the pane must no longer say "Loading..." (#215). The pause between steps is a user reading before the next move, so
# neighbour prefetch has time to finish; the step is the forward walk through the history.
detail_header() { t capture-pane -t m -p | grep -m1 'Details ─ Changes' || true; }
details_ready() { # $1 = header before the key; true once another commit's details are shown
  local cur
  cur="$(t capture-pane -t m -p)"
  [ "$(printf '%s' "$cur" | grep -m1 'Details ─ Changes' || true)" != "$1" ] && ! printf '%s' "$cur" | grep -qF 'Loading...'
}
vals=""
for _ in $(seq "$keys"); do
  base="$(detail_header)"
  t0=$(now_us)
  t send-keys -t m j
  until details_ready "$base"; do
    [ "$(now_us)" -gt $((t0 + timeout * 1000000)) ] && break
  done
  if [ "$(now_us)" -le $((t0 + timeout * 1000000)) ]; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
  sleep 0.3
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "details_step_ms_median=$med"
echo "details_step_ms_p95=$p95"
echo "details_step_ms_max=$max"
echo "details_step_samples=$(printf '%s' "$vals" | grep -c . || true)"

# diff latency (#225, #1390): `j`, then the diff pane (beside the Changes file list) must show the diff of another commit:
# its text changed, no "Loading..." and a hunk header is on screen. The pane title no longer names the commit (#1380), so
# the key is the pane's text: the rows under the `┬─ Diff` title, right of the column of that `┬`, up to the next border row.
# Same 300 ms pause between steps as the details metric.
diff_key() { # $1 = plain pane capture; prints the diff pane's text (empty without a diff pane)
  # C.UTF-8 so gawk and mawk both count characters; no bracket class on the box glyphs (bytes in a byte-based awk)
  printf '%s\n' "$1" | LC_ALL=C.UTF-8 awk '
    !found { i = index($0, "┬─ Diff"); if (i) { found = 1; col = i } next }
    index($0, "└") == 1 || index($0, "├") == 1 || index($0, "╰") == 1 { exit }
    { print substr($0, col + 1) }'
}
diff_ready() { # $1 = diff pane text before the key
  local cur
  cur="$(t capture-pane -t m -p)"
  [ "$(diff_key "$cur")" != "$1" ] && ! printf '%s' "$cur" | grep -qF 'Loading...' && printf '%s' "$cur" | grep -qF '@@'
}
vals=""
keys_diff="$keys"
if ! t capture-pane -t m -p | grep -qF '┬─ Diff'; then
  # without a diff pane (a narrow terminal) every step would wait out its timeout
  echo "no diff pane on screen; skipping the diff steps" >&2
  keys_diff=0
fi
for _ in $(seq "$keys_diff"); do
  base="$(diff_key "$(t capture-pane -t m -p)")"
  t0=$(now_us)
  t send-keys -t m j
  until diff_ready "$base"; do
    [ "$(now_us)" -gt $((t0 + timeout * 1000000)) ] && break
  done
  if [ "$(now_us)" -le $((t0 + timeout * 1000000)) ]; then vals+="$(ms $(( $(now_us) - t0 )))"$'\n'; fi
  sleep 0.3
done
read -r med p95 max <<<"$(printf '%s' "$vals" | stats)"
echo "diff_step_ms_median=$med"
echo "diff_step_ms_p95=$p95"
echo "diff_step_ms_max=$max"
echo "diff_step_samples=$(printf '%s' "$vals" | grep -c . || true)"

# details while holding `j`: 10 moves in one burst, time until the details of the commit it ended on are shown
base="$(detail_header)"
t0=$(now_us)
t send-keys -t m -N 10 j
sleep 0.05
until details_ready "$base"; do
  [ "$(now_us)" -gt $((t0 + timeout * 1000000)) ] && break
done
if [ "$(now_us)" -le $((t0 + timeout * 1000000)) ]; then echo "details_burst_10j_ms=$(ms $(( $(now_us) - t0 )))"; else echo "details_burst_10j_ms=nan"; fi
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

# G goes to the last *loaded* commit, and the status bar shows `n/total+` while the log still streams
# in (~1 s on 100k commits). Wait for the `+` to go away first, or G lands mid-history (the cause of
# jump_to_end_ms=nan in #192); the measured jump is then the pure jump.
loaded_deadline=$(( $(now_us) + timeout * 1000000 ))
while pane | grep -qE '[0-9]/[0-9]+\+' && [ "$(now_us)" -lt "$loaded_deadline" ]; do :; done
if pane | grep -qE '[0-9]/[0-9]+\+'; then echo "log still streaming after ${timeout}s; jump_to_end_ms will be unreliable" >&2; fi
wait_stable || true
# jump to the end of history (G) until the oldest commit is visible, then back (gg)
t0=$(now_us)
t send-keys -t m G
if wait_for "$oldest"; then echo "jump_to_end_ms=$(ms $(( $(now_us) - t0 )))"; else echo "jump_to_end_ms=nan"; echo "oldest='$oldest' not seen; pane:" >&2; pane >&2; fi
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
