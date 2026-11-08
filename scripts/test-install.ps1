# Tests scripts/install.ps1 against a fake release on disk (no network, user PATH untouched): install, update, checksum
# mismatch, unsupported architecture, missing asset (#1248). Run on the Windows CI legs (needs Windows PowerShell or pwsh).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("gitunia-install-test-" + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $tmp)
function Fail([string]$Message) { throw "FAIL: $Message" }

function New-Release([string]$Version, [string]$Directory, [string]$Architecture) {
    [void](New-Item -ItemType Directory -Force -Path $Directory)
    $name = "gitunia-$Version-win-$Architecture"
    $stage = Join-Path $tmp "stage-$name"
    [void](New-Item -ItemType Directory -Force -Path (Join-Path $stage $name))
    Set-Content -LiteralPath (Join-Path $stage "$name\gitunia.exe") -Value "fake exe $Version"
    $zip = Join-Path $Directory "$name.zip"
    Compress-Archive -Path (Join-Path $stage $name) -DestinationPath $zip
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $name.zip"
    @{ tag_name = "v$Version"; assets = @(@{ browser_download_url = $zip }, @{ browser_download_url = "$zip.sha256" }) } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'release.json')
}

function Invoke-Install([string]$Directory, [string]$Architecture) {
    $env:GITUNIA_RELEASE_URL = Join-Path $Directory 'release.json'
    $env:GITUNIA_INSTALL_DIR = Join-Path $tmp 'bin'
    $env:GITUNIA_NO_PATH = '1'
    $env:GITUNIA_ARCH = $Architecture
    & (Join-Path $root 'scripts\install.ps1') 6>&1 | Out-String
}

try {
    New-Release '1.2.3' (Join-Path $tmp 'r1') 'x64'
    New-Release '1.3.0' (Join-Path $tmp 'r2') 'arm64'

    $out = Invoke-Install (Join-Path $tmp 'r1') 'x64'
    $exe = Join-Path $tmp 'bin\gitunia.exe'
    if (-not (Test-Path -LiteralPath $exe)) { Fail "binary not installed: $out" }
    if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.2.3') { Fail 'wrong binary installed' }

    # The fake exe cannot answer --version, so an update is the same as a first install here.
    $out = Invoke-Install (Join-Path $tmp 'r2') 'arm64'
    if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.3.0') { Fail "not updated: $out" }
    if (Test-Path -LiteralPath "$exe.old") { Fail 'left the old binary behind' }

    Remove-Item -LiteralPath $exe
    Set-Content -LiteralPath (Join-Path $tmp 'r1\gitunia-1.2.3-win-x64.zip.sha256') -Value ('0' * 64)
    $failed = $false
    try { [void](Invoke-Install (Join-Path $tmp 'r1') 'x64') } catch { $failed = $_.Exception.Message -like '*checksum mismatch*' }
    if (-not $failed) { Fail 'checksum mismatch accepted' }
    if (Test-Path -LiteralPath $exe) { Fail 'binary installed despite a bad checksum' }

    $failed = $false
    try { [void](Invoke-Install (Join-Path $tmp 'r2') 'riscv64') } catch { $failed = $_.Exception.Message -like '*unsupported architecture*' }
    if (-not $failed) { Fail 'unsupported architecture accepted' }

    $failed = $false
    try { [void](Invoke-Install (Join-Path $tmp 'r2') 'x64') } catch { $failed = $_.Exception.Message -like '*no win-x64 zip*' }
    if (-not $failed) { Fail 'missing asset accepted' }
    Write-Host 'ok'
} finally {
    Remove-Item Env:GITUNIA_RELEASE_URL, Env:GITUNIA_INSTALL_DIR, Env:GITUNIA_NO_PATH, Env:GITUNIA_ARCH -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
