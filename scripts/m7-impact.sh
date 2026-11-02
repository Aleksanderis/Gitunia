#!/usr/bin/env bash
# Code metrics for the M7 impact report (#1031). Usage: scripts/m7-impact.sh <repo-or-extracted-tree-root>
# Prints per-project lines, test counts, largest files and structure counts. Read-only.
set -u
cd "${1:-.}" || exit 1
for d in src/* tests/*; do
  [ -d "$d" ] || continue
  f=$(find "$d" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*')
  [ -z "$f" ] && { echo "$d files=0"; continue; }
  n=$(echo "$f" | wc -l)
  raw=$(echo "$f" | xargs cat | wc -l)
  code=$(echo "$f" | xargs cat | grep -vE '^\s*($|//)' | wc -l)
  t=$(echo "$f" | xargs grep -hE '^\s*\[(Fact|Theory)(\(|\])' | wc -l)
  echo "$d files=$n raw=$raw code=$code tests=$t"
done
echo "TOP15"
find src -name '*.cs' -not -path '*/obj/*' | xargs wc -l | grep -v ' total$' | sort -rn | head -15
echo "FILES_OVER_500_src: $(find src -name '*.cs' -not -path '*/obj/*' | xargs wc -l | grep -v ' total$' | awk '$1>500' | wc -l)"
echo "NULL_BANG_src: $(grep -rE 'null!' src --include=*.cs | wc -l)"
echo "ACTION_FUNC_TOKENS_src: $(grep -rE '\b(Action|Func)<' src --include=*.cs | wc -l)"
echo "ACTION_FUNC_PARAMS_src: $(grep -rE '(Action|Func)<[^()]*>\??\s+\w+\s*[,)=]' src --include=*.cs | wc -l)"
echo "ActionRouter/TuiApplication files:"
find src -name 'ActionRouter*.cs' -o -name 'TuiApplication*.cs' | xargs -r wc -l
echo "Controller files: $(find src -name '*Controller*.cs' | wc -l)"
