# Runs JetBrains inspectcode (the Rider inspections) over the solution and prints the warnings.
# These are quality checks (captured disposed/modified variables, unused members, ...) that .editorconfig and the
# compiler analyzers do not report. Exit code 1 when anything is found.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path ([System.IO.Path]::GetTempPath()) "gitunia-inspect-$([guid]::NewGuid()).xml"

Push-Location $root
try {
    dotnet tool restore | Out-Null
    dotnet build --nologo -v quiet | Out-Null
    dotnet jb inspectcode Gitunia.slnx --output=$output --format=Xml --severity=WARNING --no-build --verbosity=ERROR | Out-Null

    $issues = @(([xml](Get-Content -Raw $output)).SelectNodes('//Issue'))
    foreach ($issue in $issues) {
        Write-Output "$($issue.File -replace '\\', '/'):$($issue.Line): $($issue.TypeId): $($issue.Message)"
    }
    Write-Output "$($issues.Count) issue(s)"
    if ($issues.Count -gt 0) { exit 1 }
}
finally {
    Pop-Location
}
