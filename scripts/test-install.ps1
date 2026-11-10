# Tests scripts/install.ps1 against a fake release on disk (no network, user PATH untouched): install, update, checksum
# mismatch, unsupported architecture, missing asset (#1248), and download retries against a loopback HTTP server (#1281). Run on the Windows CI legs (needs Windows PowerShell or pwsh).
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
    if ((Get-Content -LiteralPath (Join-Path $tmp 'bin\gitunia.install-source') -Raw).Trim() -ne 'script') { Fail 'install-source marker not written (#1291)' }

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
    # Transient download failures: a local HTTP server answers 503 twice per file, then serves the release.
    $web = Join-Path $tmp 'web'
    New-Release '1.4.0' $web 'x64'
    $port = Get-Random -Minimum 20000 -Maximum 40000
    $prefix = "http://127.0.0.1:$port/"
    $json = Get-Content -LiteralPath (Join-Path $web 'release.json') -Raw | ConvertFrom-Json
    foreach ($asset in $json.assets) { $asset.browser_download_url = $prefix + (Split-Path -Leaf $asset.browser_download_url) }
    $json | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $web 'release.json')
    $listener = [Net.HttpListener]::new()
    $listener.Prefixes.Add($prefix)
    $listener.Start()
    $server = [powershell]::Create()
    [void]$server.AddScript({
        param($listener, $dir, $failures)
        $seen = @{}
        while ($listener.IsListening) {
            try { $context = $listener.GetContext() } catch { break }
            $name = $context.Request.Url.AbsolutePath.TrimStart('/')
            $seen[$name] = 1 + [int]$seen[$name]
            if ($seen[$name] -le $failures) { $context.Response.StatusCode = 503 } else {
                $bytes = [IO.File]::ReadAllBytes((Join-Path $dir $name))
                $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            }
            $context.Response.Close()
        }
    }).AddArgument($listener).AddArgument($web).AddArgument(2)
    [void]$server.BeginInvoke()
    $env:GITUNIA_RETRY_DELAY = '0'
    try {
        Remove-Item -LiteralPath $exe -ErrorAction SilentlyContinue
        $env:GITUNIA_RELEASE_URL = "${prefix}release.json"
        $env:GITUNIA_INSTALL_DIR = Join-Path $tmp 'bin'
        $env:GITUNIA_NO_PATH = '1'
        $env:GITUNIA_ARCH = 'x64'
        $out = & (Join-Path $root 'scripts\install.ps1') 6>&1 | Out-String
        if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.4.0') { Fail "no install after two failed downloads: $out" }
        if ($out -notlike '*retrying*') { Fail "no retry message: $out" }
    } finally {
        $listener.Stop()
        $server.Dispose()
    }

    # Always failing: nothing listens on the port now.
    $failed = $null
    try { [void](& (Join-Path $root 'scripts\install.ps1') 6>&1) } catch { $failed = $_.Exception.Message }
    if (-not $failed -or $failed -notlike "*download failed after 4 attempts: ${prefix}release.json*" -or $failed -notlike '*github.com/Aleksanderis/Gitunia/releases*') { Fail "bad failure message: $failed" }
    Write-Host 'ok'
} finally {
    Remove-Item Env:GITUNIA_RELEASE_URL, Env:GITUNIA_INSTALL_DIR, Env:GITUNIA_NO_PATH, Env:GITUNIA_ARCH, Env:GITUNIA_RETRY_DELAY -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
