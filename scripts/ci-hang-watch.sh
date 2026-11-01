#!/usr/bin/env bash
# TEMPORARY diagnostics for #959: while a test host runs, report the process tree and stack samples of anything stuck.
# Usage: scripts/ci-hang-watch.sh &   (prints to stdout, which is the CI step log)
set -u

seconds_of() { # etime [[dd-]hh:]mm:ss -> seconds
  local t="$1" d=0 h=0 m=0 s=0
  [[ "$t" == *-* ]] && { d="${t%%-*}"; t="${t#*-}"; }
  IFS=: read -r -a p <<<"$t"
  case ${#p[@]} in
    3) h=${p[0]}; m=${p[1]}; s=${p[2]};;
    2) m=${p[0]}; s=${p[1]};;
    *) s=${p[0]};;
  esac
  echo $(( 10#$d*86400 + 10#$h*3600 + 10#$m*60 + 10#$s ))
}

report=0
while sleep 8; do
  ps -axo pid=,ppid=,etime=,stat=,command= > /tmp/ps-now.txt
  hosts=$(awk '$5 ~ /Gitunia\.[A-Za-z]+\.Tests/ && $5 !~ /dotnet$/ && $0 !~ / test / {print $1}' /tmp/ps-now.txt)
  [ -z "$hosts" ] && continue
  stuck=""
  for host in $hosts; do
    while read -r pid ppid etime stat command; do
      if [ "$ppid" = "$host" ] && [ "$(seconds_of "$etime")" -ge 25 ]; then stuck="$stuck $pid"; fi
    done < /tmp/ps-now.txt
  done
  for host in $hosts; do
    hostetime=$(awk -v h="$host" '$1 == h {print $3}' /tmp/ps-now.txt)
    [ "$(seconds_of "$hostetime")" -ge 100 ] && stuck="$stuck $host"
  done
  [ -z "$stuck" ] && continue
  report=$((report + 1))
  [ "$report" -gt 3 ] && continue
  echo "::group::hang-watch report $report at $(date -u +%T): hosts=$hosts stuck=$stuck"
  ps -axo pid,ppid,pgid,stat,etime,command | grep -E "PID|git|fakegit|sleep|Tests|dotnet" | grep -v grep | cut -c1-260
  for pid in $stuck; do
    echo "--- lsof $pid"; lsof -p "$pid" 2>&1 | head -40
  done
  for pid in $(echo $stuck | tr ' ' '\n' | head -3) $hosts; do
    echo "--- sample $pid"; sample "$pid" 3 -file "/tmp/sample-$pid-$report.txt" >/dev/null 2>&1; head -150 "/tmp/sample-$pid-$report.txt" 2>&1
  done
  echo "::endgroup::"
  sleep 20
done
