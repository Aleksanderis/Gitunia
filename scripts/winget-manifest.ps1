# Fills the winget manifest templates in packaging/winget for one release (#943).
# PowerShell twin of scripts/winget-manifest.sh (Windows PowerShell 5.1 and pwsh): same arguments, checks and output files.
# Usage: scripts/winget-manifest.ps1 <version> <sha256 | path to the .sha256 file> <out-dir> [release-date]
# <version> has no leading "v". The installer URL is the bare gitunia-<version>-win-x64.exe release asset (#941).
# Writes the three files winget wants (version, installer, defaultLocale) into <out-dir>.
param(
    [Parameter(Mandatory, Position = 0)][string]$Version,
    [Parameter(Mandatory, Position = 1)][string]$Hash,
    [Parameter(Mandatory, Position = 2)][string]$Out,
    [Parameter(Position = 3)][string]$Date = ([DateTime]::UtcNow.ToString('yyyy-MM-dd')))

$ErrorActionPreference = 'Stop'
function Stop-Script([string]$Message) { [Console]::Error.WriteLine($Message); exit 1 }

$root = Split-Path -Parent $PSScriptRoot
if ($Version -eq '' -or $Version.StartsWith('v')) { Stop-Script "version must not start with v: $Version" }
if ($Version -cnotmatch '^[0-9]+(\.[0-9]+)*(-[0-9A-Za-z.]+)?$') { Stop-Script "bad version: $Version" }
if ($Date -cnotmatch '^[0-9]{4}-[0-9]{2}-[0-9]{2}$') { Stop-Script "bad date: $Date" }
$hashPath = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($Hash)
if (Test-Path -LiteralPath $hashPath -PathType Leaf) {
    $first = (Get-Content -LiteralPath $hashPath -TotalCount 1)
    $Hash = if ($first) { ($first.Trim() -split '\s+')[0] } else { '' }
}
$Hash = $Hash.ToUpperInvariant()
if ($Hash -cnotmatch '^[0-9A-F]+$') { Stop-Script "not a SHA-256: $Hash" }
if ($Hash.Length -ne 64) { Stop-Script "SHA-256 must be 64 hex digits: $Hash" }

$url = "https://github.com/Aleksanderis/Gitunia/releases/download/v$Version/gitunia-$Version-win-x64.exe"
$outDir = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($Out)
[void](New-Item -ItemType Directory -Force -Path $outDir)
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($t in Get-ChildItem -LiteralPath (Join-Path $root 'packaging/winget') -Filter '*.yaml') {
    $text = [IO.File]::ReadAllText($t.FullName)
    $text = $text.Replace('@VERSION@', $Version).Replace('@URL@', $url).Replace('@SHA256@', $Hash).Replace('@DATE@', $Date)
    [IO.File]::WriteAllText((Join-Path $outDir $t.Name), $text, $utf8)
}
foreach ($f in Get-ChildItem -LiteralPath $outDir -Filter '*.yaml') {
    if ([IO.File]::ReadAllText($f.FullName) -cmatch '@[A-Z0-9]*@') { Stop-Script "unfilled placeholder left in $outDir" }
}
Write-Output $Out
