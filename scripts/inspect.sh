#!/usr/bin/env bash
# Runs JetBrains inspectcode (the Rider inspections) over the solution and prints the warnings.
# These are quality checks (captured disposed/modified variables, unused members, ...) that .editorconfig and the
# compiler analyzers do not report. Exit code 1 when anything is found.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$(mktemp -d)/inspect.xml"

cd "$root"
dotnet tool restore >/dev/null
dotnet build --nologo -v quiet >/dev/null
dotnet jb inspectcode Gitunia.slnx --output="$output" --format=Xml --severity=WARNING --no-build --verbosity=ERROR >/dev/null

python3 - "$output" <<'PY'
import sys
import xml.etree.ElementTree as ET

issues = list(ET.parse(sys.argv[1]).iter("Issue"))
for issue in issues:
    file = issue.get("File").replace("\\", "/")
    print(f"{file}:{issue.get('Line')}: {issue.get('TypeId')}: {issue.get('Message')}")
print(f"{len(issues)} issue(s)")
sys.exit(1 if issues else 0)
PY
