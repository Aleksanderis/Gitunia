# Installs or updates Gitunia from the latest GitHub release (#1248).
#
#   irm https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.ps1 | iex
#
# Windows (x64, arm64), Windows PowerShell 5.1 and pwsh. Downloads the release zip, checks it against its .sha256 file,
# puts gitunia.exe in %LOCALAPPDATA%\Gitunia and adds that folder to your user PATH. No admin rights. Running it again
# updates; it does nothing when the installed version is already the latest.
#
# Environment (all optional):
#   GITUNIA_INSTALL_DIR  install folder (default: %LOCALAPPDATA%\Gitunia)
#   GITUNIA_FORCE        1 = install even when the latest version is already installed
#   GITUNIA_NO_PATH      1 = do not touch the user PATH
#   GITUNIA_RELEASE_URL  release JSON to read (default: the GitHub API's latest release; a local file path works, for tests)
#   GITUNIA_RETRY_DELAY  seconds before the first download retry, grows per retry (default 2; 0 in tests)
#   GITUNIA_ARCH         x64 | arm64 (default: detected; for tests)
#
# Everything is in one function called on the last line, so a cut-off download runs nothing.
function Get-GituniaFile([string]$Source, [string]$Destination) {
    # A URL is downloaded, up to 4 tries with a growing pause (GitHub answers 502/504 now and then); anything else is a local path (tests).
    if ($Source -notmatch '^https?://') {
        Copy-Item -LiteralPath $Source -Destination $Destination
        return
    }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $delay = if ($env:GITUNIA_RETRY_DELAY) { [int]$env:GITUNIA_RETRY_DELAY } else { 2 }
    $previous = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        for ($attempt = 1; ; $attempt++) {
            try { Invoke-WebRequest -UseBasicParsing -Uri $Source -OutFile $Destination; return } catch {
                if ($attempt -ge 4) { throw "download failed after $attempt attempts: $Source ($($_.Exception.Message)); releases: https://github.com/Aleksanderis/Gitunia/releases" }
                Write-Host "download failed, retrying in $($attempt * $delay)s: $Source"
                Start-Sleep -Seconds ($attempt * $delay)
            }
        }
    } finally { $ProgressPreference = $previous }
}

# Adds a folder to the user PATH without changing how the existing value is stored: read it unexpanded (it may hold
# %VARIABLES%) and write it back as an expandable string, which Environment.SetEnvironmentVariable would not do.
function Add-ToUserPath([string]$Folder) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        $current = [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $wanted = $Folder.TrimEnd('\')
        $present = $current -split ';' | Where-Object { $_ } |
            Where-Object { [Environment]::ExpandEnvironmentVariables($_).TrimEnd('\') -ieq $wanted }
        if ($present) { return }
        $value = if ($current) { "$($current.TrimEnd(';'));$Folder" } else { $Folder }
        $key.SetValue('Path', $value, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    } finally { $key.Dispose() }
    # Writing the registry directly sends no change notice; setting and removing a variable does, so new terminals see the PATH.
    [Environment]::SetEnvironmentVariable('GITUNIA_PATH_REFRESH', '1', 'User')
    [Environment]::SetEnvironmentVariable('GITUNIA_PATH_REFRESH', $null, 'User')
    $env:Path = "$env:Path;$Folder"
    Write-Host "Added $Folder to your user PATH (open a new terminal for other windows to see it)."
}

function Install-Gitunia {
    # Under `irm | iex` this runs in the caller's session: set the preference here and put it back afterwards.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Stop'
    try { Invoke-InstallCore } finally { $ErrorActionPreference = $previousPreference }
}

function Invoke-InstallCore {
    $repository = 'Aleksanderis/Gitunia'
    $releaseUrl = if ($env:GITUNIA_RELEASE_URL) { $env:GITUNIA_RELEASE_URL } else { "https://api.github.com/repos/$repository/releases/latest" }
    $architecture = if ($env:GITUNIA_ARCH) { $env:GITUNIA_ARCH } else {
        switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
            'X64' { 'x64' }
            'Arm64' { 'arm64' }
            default { throw "unsupported architecture: $_ (releases exist for x64 and arm64)" }
        }
    }
    if ($architecture -ne 'x64' -and $architecture -ne 'arm64') { throw "unsupported architecture: $architecture" }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("gitunia-install-" + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $work)
    try {
        $jsonPath = Join-Path $work 'release.json'
        Get-GituniaFile $releaseUrl $jsonPath
        $release = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
        if (-not $release.tag_name) { throw "could not read the latest release from $releaseUrl" }
        $latest = ([string]$release.tag_name).TrimStart('v')

        $target = if ($env:GITUNIA_INSTALL_DIR) { $env:GITUNIA_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'Gitunia' }
        $executable = Join-Path $target 'gitunia.exe'
        if ((Test-Path -LiteralPath $executable) -and $env:GITUNIA_FORCE -ne '1') {
            # A broken or foreign gitunia.exe must not block the update.
            try { $installed = ((& $executable --version 2>$null) -split '\s+' | Select-Object -Skip 1 -First 1) } catch { $installed = $null }
            if ($installed -eq $latest) { Write-Host "Gitunia $latest is already installed."; return }
            if ($installed) { Write-Host "Updating Gitunia $installed -> $latest" }
        }

        $suffix = "-win-$architecture.zip"
        $asset = @($release.assets | Where-Object { $_.browser_download_url -like "*$suffix" }) | Select-Object -First 1
        if (-not $asset) { throw "this release has no win-$architecture zip yet; see https://github.com/$repository/releases" }
        $zip = Join-Path $work ($asset.browser_download_url -split '[/\\]')[-1]
        Get-GituniaFile $asset.browser_download_url $zip
        Get-GituniaFile "$($asset.browser_download_url).sha256" "$zip.sha256"
        $expected = ((Get-Content -LiteralPath "$zip.sha256" -TotalCount 1).Trim() -split '\s+')[0]
        if (-not $expected) { throw "no checksum for $(Split-Path -Leaf $zip)" }
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
        if ($expected -ne $actual) { throw "checksum mismatch for $(Split-Path -Leaf $zip) (expected $expected, got $actual)" }

        $unpacked = Join-Path $work 'unpacked'
        Expand-Archive -LiteralPath $zip -DestinationPath $unpacked
        $binary = Get-ChildItem -LiteralPath $unpacked -Recurse -Filter 'gitunia.exe' | Select-Object -First 1
        if (-not $binary) { throw 'the archive holds no gitunia.exe' }

        [void](New-Item -ItemType Directory -Force -Path $target)
        # A running gitunia.exe cannot be overwritten but can be renamed away, so an update works while it runs.
        $old = "$executable.old"
        Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $executable) { Move-Item -LiteralPath $executable -Destination $old -Force }
        try {
            Copy-Item -LiteralPath $binary.FullName -Destination $executable
        } catch {
            if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination $executable -Force }
            throw
        }
        Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue
        # The install-source marker (#1291): tells `gitunia update` that this binary may replace itself.
        Set-Content -LiteralPath (Join-Path $target 'gitunia.install-source') -Value 'script'
        Write-Host "Installed $executable"

        if ($env:GITUNIA_NO_PATH -ne '1') { Add-ToUserPath $target }
        Write-Host "Gitunia $latest installed. Run: gitunia"
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-Gitunia
