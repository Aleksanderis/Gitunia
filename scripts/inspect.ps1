# Runs JetBrains inspectcode (the Rider inspections) over the solution and prints the warnings.
# These are quality checks (captured disposed/modified variables, unused members, ...) that .editorconfig and the
# compiler analyzers do not report. Exit code 1 when anything is found.
# Usage: inspect.ps1 [-Changed | glob...]. No argument inspects everything (~40 s); -Changed or globs (~10 s)
# limit the report to those files (-Changed: files changed on the branch since origin/main, committed or not).
param([switch]$Changed, [Parameter(ValueFromRemainingArguments)][string[]]$Globs)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path ([System.IO.Path]::GetTempPath()) "gitunia-inspect-$([guid]::NewGuid()).xml"

Push-Location $root
try {
    $include = @()
    if ($Changed) {
        $base = git merge-base HEAD origin/main 2>$null
        if (-not $base) { $base = 'HEAD' }
        $files = @(git diff --name-only $base -- '*.cs')
        if ($files.Count -eq 0) { Write-Output '0 issue(s)'; exit 0 }
        $include = @("--include=$($files -join ';')")
    }
    elseif ($Globs) {
        $include = @("--include=$($Globs -join ';')")
    }

    dotnet tool restore | Out-Null
    dotnet build --nologo -v quiet | Out-Null
    dotnet jb inspectcode Gitunia.slnx --output=$output --format=Xml --severity=WARNING --no-build --verbosity=ERROR @include | Out-Null

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
