# Measure cold start and RSS of gitunia restoring many tabs (Windows; PowerShell 7, psmux, git).
# PowerShell twin of scripts/measure-tabs.sh: builds throwaway repos and a session file in a temp state directory,
# never touches real repos or state. Times are wall-clock ms from polling `capture-pane` (psmux: ~50 ms per poll).
#
# Usage: scripts/measure-tabs.ps1 <binary> [-Tabs N] [-Commits N] [-Runs N] [-Size WxH] [-Timeout SEC]
# The active tab is the first one; each repo's newest subject is "Head of tab-NN", used as the marker.
param(
    [Parameter(Mandatory, Position = 0)][string]$Binary,
    [int]$Tabs = 12, [int]$Commits = 300, [int]$Runs = 5, [string]$Size = '120x40', [int]$Timeout = 10)

$ErrorActionPreference = 'Stop'
if (-not (Get-Command psmux -ErrorAction SilentlyContinue)) { [Console]::Error.WriteLine("missing tool 'psmux': winget install marlocarlo.psmux"); exit 3 }
$Binary = (Resolve-Path $Binary).Path
$cols, $rows = $Size -split 'x' | ForEach-Object { [int]$_ }

$work = Join-Path ([System.IO.Path]::GetTempPath()) "gtt-$PID"
$sock = "gtt-$PID"
function T { psmux -L $sock @args }

$emptyConfig = Join-Path $work 'empty.gitconfig'
New-Item -ItemType Directory -Force $work | Out-Null
New-Item -ItemType File -Force $emptyConfig | Out-Null
$env:GIT_CONFIG_GLOBAL = $emptyConfig; $env:GIT_CONFIG_SYSTEM = $emptyConfig
# The server inherits this environment, and so does the app. USERPROFILE covers apps that resolve the home directory.
$env:HOME = Join-Path $work 'home'; $env:USERPROFILE = $env:HOME
$env:XDG_STATE_HOME = Join-Path $work 'state'; $env:XDG_CONFIG_HOME = Join-Path $work 'config'; $env:XDG_CACHE_HOME = Join-Path $work 'cache'
New-Item -ItemType Directory -Force $env:HOME, (Join-Path $env:XDG_STATE_HOME 'gitunia') | Out-Null
$session = Join-Path $env:XDG_STATE_HOME 'gitunia/session.json'
$template = Join-Path $work 'session.template'

$clock = [System.Diagnostics.Stopwatch]::StartNew()
function Get-Now { $clock.Elapsed.TotalMilliseconds }
function Get-Pane { (T capture-pane -t m -p) -join "`n" }
function Format-Ms([double]$value) { $value.ToString('0.0', [cultureinfo]::InvariantCulture) }
function Get-Stats([double[]]$values) { # median p95 max
    if (-not $values -or $values.Count -eq 0) { return 'nan', 'nan', 'nan' }
    $sorted = @($values | Sort-Object)
    $count = $sorted.Count
    (Format-Ms $sorted[[int][Math]::Floor(($count + 1) / 2) - 1]), (Format-Ms $sorted[[Math]::Min($count, [int][Math]::Ceiling($count * 0.95)) - 1]), (Format-Ms $sorted[$count - 1])
}
function Wait-For([string]$text) { # elapsed ms, or $null on timeout
    $t0 = Get-Now
    while ($true) {
        if ((Get-Pane).Contains($text)) { return (Get-Now) - $t0 }
        if ((Get-Now) - $t0 -gt $Timeout * 1000) { return $null }
    }
}
function Get-RssKib {
    $panePid = [int](T display-message -t m -p '#{pane_pid}')
    $proc = Get-Process -Id $panePid -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -notlike 'gitunia*') {
        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId=$panePid AND Name='gitunia.exe'" | Select-Object -First 1
        $proc = if ($child) { Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
    }
    if ($proc) { [long]($proc.WorkingSet64 / 1024) } else { 'nan' }
}
function Start-App { T new-session -d -s m -x $cols -y $rows "`"$Binary`"" | Out-Null }

try {
    # Throwaway repos: linear history, one tiny file change per commit, built with fast-import.
    $entries = @()
    for ($index = 0; $index -lt $Tabs; $index++) {
        $name = 'tab-{0:00}' -f $index
        $repo = Join-Path $work "repos/$name"
        New-Item -ItemType Directory -Force $repo | Out-Null
        git -C $repo init -q -b main
        $stream = [System.Text.StringBuilder]::new()
        for ($n = 1; $n -le $Commits; $n++) {
            $subject = if ($n -eq $Commits) { "Head of $name" } else { "Commit $n of $name" }
            $null = $stream.Append("commit refs/heads/main`nmark :$n`ncommitter T <t@example.com> $(1700000000 + $n) +0000`ndata $($subject.Length)`n$subject`n")
            if ($n -gt 1) { $null = $stream.Append("from :$($n - 1)`n") }
            $null = $stream.Append("M 644 inline f.txt`ndata $("$n".Length)`n$n`n`n")
        }
        $streamFile = Join-Path $work "$name.stream"
        [System.IO.File]::WriteAllText($streamFile, $stream.ToString(), [System.Text.UTF8Encoding]::new($false))
        # A file redirect keeps the LF bytes; piping a PowerShell string would turn them into CRLF.
        Start-Process git -ArgumentList '-C', "`"$repo`"", 'fast-import', '--quiet' -RedirectStandardInput $streamFile -NoNewWindow -Wait
        git -C $repo reset -q --hard main
        $gitDir = Join-Path $repo '.git'
        $entries += [ordered]@{ workingTreePath = $repo; gitDirectory = $gitDir; commonDirectory = $gitDir; focusedPane = 'Graph' }
    }
    [ordered]@{ version = 1; activeTabIndex = 0; tabs = $entries; recents = @(); pins = @() } |
        ConvertTo-Json -Depth 5 -Compress | Set-Content $template -NoNewline

    "bin=$Binary"
    "tabs=$Tabs"
    "commits_per_repo=$Commits"

    T start-server | Out-Null

    $vals = @(); $rssVals = @()
    for ($i = 0; $i -lt $Runs; $i++) {
        Copy-Item $template $session -Force
        $t0 = Get-Now
        Start-App
        if ($null -ne (Wait-For 'Head of tab-00')) {
            $vals += (Get-Now) - $t0
            Start-Sleep 1
            $rssVals += Get-RssKib
        }
        else { [Console]::Error.WriteLine("cold start: marker not seen within ${Timeout}s") }
        T kill-session -t m 2>$null | Out-Null
    }
    $med, $p95, $max = Get-Stats $vals
    "cold_start_ms_median=$med"; "cold_start_ms_p95=$p95"; "cold_start_ms_max=$max"
    $med, $null, $max = Get-Stats ($rssVals | Where-Object { $_ -ne 'nan' })
    "rss_kib_after_start_median=$med"; "rss_kib_after_start_max=$max"

    # Visit every tab in turn (gt): time to the tab's newest subject, then RSS with all tabs loaded.
    Copy-Item $template $session -Force
    Start-App
    if ($null -eq (Wait-For 'Head of tab-00')) { [Console]::Error.WriteLine('app never showed the first tab'); exit 1 }
    Start-Sleep 1
    $vals = @()
    for ($index = 1; $index -lt $Tabs; $index++) {
        $t0 = Get-Now
        # psmux drops the second key of "g t" sent in one call; send the chord key by key.
        T send-keys -t m g | Out-Null; T send-keys -t m t | Out-Null
        if ($null -ne (Wait-For ('Head of tab-{0:00}' -f $index))) { $vals += (Get-Now) - $t0 }
    }
    $med, $null, $max = Get-Stats $vals
    "first_visit_switch_ms_median=$med"; "first_visit_switch_ms_max=$max"
    Start-Sleep 1
    "rss_kib_all_visited=$(Get-RssKib)"

    # Revisit: jump back to the first tab (already loaded).
    $t0 = Get-Now
    T send-keys -t m 1 | Out-Null; T send-keys -t m g | Out-Null; T send-keys -t m t | Out-Null
    "revisit_switch_ms=$(if ($null -ne (Wait-For 'Head of tab-00')) { Format-Ms ((Get-Now) - $t0) } else { 'nan' })"
}
finally {
    T kill-server 2>$null | Out-Null
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
