# Measure a Gitunia-style TUI binary inside a private psmux server (Windows; PowerShell 7, psmux, git).
# PowerShell twin of scripts/measure.sh: same options, same key=value output, same contract (docs/testing.md,
# "Measurement harness"). psmux (`winget install marlocarlo.psmux`) accepts the tmux commands used here.
#
# Usage: scripts/measure.ps1 <binary> <repo> [-Runs N] [-Keys N] [-Marker TEXT] [-Oldest TEXT] [-Size WxH] [-OnlyStart] [-Blame] [-Timeout SEC]
# -Blame (#1137, #1321): after cold start, open the blame view of the file in the newest commit (`<space>gb`) and time
# open, `j`, Ctrl-d, G and the working set instead of the history cases; use a repo from gen-big-file-repo.sh.
# Times are wall-clock ms measured by polling `capture-pane`; psmux polls cost ~50 ms each (tmux: 1-3 ms), so the
# numbers are only comparable between runs of this script, never with measure.sh.
param(
    [Parameter(Mandatory, Position = 0)][string]$Binary,
    [Parameter(Mandatory, Position = 1)][string]$Repo,
    [int]$Runs = 5, [int]$Keys = 20, [string]$Marker = '', [string]$Oldest = '',
    [string]$Size = '120x40', [switch]$OnlyStart, [switch]$Blame, [int]$Timeout = 10)

#requires -Version 7
$ErrorActionPreference = 'Stop'
# The environment changes below live in the caller's process: snapshot them and restore them in `finally`.
$savedEnv = @{}
foreach ($name in 'GIT_CONFIG_GLOBAL', 'GIT_CONFIG_SYSTEM', 'XDG_STATE_HOME', 'XDG_CONFIG_HOME') { $savedEnv[$name] = [Environment]::GetEnvironmentVariable($name) }
if (-not (Get-Command psmux -ErrorAction SilentlyContinue)) { [Console]::Error.WriteLine("missing tool 'psmux': winget install marlocarlo.psmux"); exit 3 }
$Binary = (Resolve-Path $Binary).Path
$Repo = (Resolve-Path $Repo).Path
$cols, $rows = $Size -split 'x' | ForEach-Object { [int]$_ }

# The app truncates long subjects with an ellipsis, so defaults are short prefixes of the HEAD and root subjects: up to the first colon (at most 12 chars when there is none).
# The newest row also carries ref decorations, which cut a subject earlier than the older rows (#1001).
$emptyConfig = [System.IO.Path]::GetTempFileName()
$env:GIT_CONFIG_GLOBAL = $emptyConfig; $env:GIT_CONFIG_SYSTEM = $emptyConfig
function Get-Prefix([string]$text) {
    $i = $text.IndexOf(':')
    if ($i -ge 8 -and $i -le 20) { return $text.Substring(0, $i) }
    $text.Substring(0, [Math]::Min(12, $text.Length))
}
if (-not $Marker) { $Marker = Get-Prefix ((git -C $Repo log -1 --format=%s HEAD) | Select-Object -First 1) }
if (-not $Oldest) { $Oldest = Get-Prefix ((git -C $Repo log --max-parents=0 -1 --format=%s HEAD) | Select-Object -First 1) }

# Private state/config dirs (honoured on every OS): a restored session would change the frame and the RSS.
$isolated = Join-Path ([System.IO.Path]::GetTempPath()) "gtm-$PID"
New-Item -ItemType Directory -Force (Join-Path $isolated 'state'), (Join-Path $isolated 'config') | Out-Null
$sock = "gtm-$PID"
function T { psmux -L $sock @args }
# The server inherits this environment, and so does the app it starts.
$env:XDG_STATE_HOME = Join-Path $isolated 'state'; $env:XDG_CONFIG_HOME = Join-Path $isolated 'config'

$clock = [System.Diagnostics.Stopwatch]::StartNew()
function Get-Now { $clock.Elapsed.TotalMilliseconds }
function Get-Pane { (T capture-pane -t m -p -e) -join "`n" }
function Get-PanePlain { (T capture-pane -t m -p) -join "`n" }
function Format-Ms([double]$value) { $value.ToString('0.0', [cultureinfo]::InvariantCulture) }
function Get-Stats([double[]]$values) { # median p95 max, like stats() in measure.sh
    if (-not $values -or $values.Count -eq 0) { return 'nan', 'nan', 'nan' }
    $sorted = @($values | Sort-Object)
    $count = $sorted.Count
    (Format-Ms $sorted[[int][Math]::Floor(($count + 1) / 2) - 1]), (Format-Ms $sorted[[Math]::Min($count, [int][Math]::Ceiling($count * 0.95)) - 1]), (Format-Ms $sorted[$count - 1])
}
# Wait-Until { condition }: polls until the scriptblock is true; returns elapsed ms or $null on timeout.
function Wait-Until([scriptblock]$condition) {
    $t0 = Get-Now
    while ($true) {
        if (& $condition) { return (Get-Now) - $t0 }
        if ((Get-Now) - $t0 -gt $Timeout * 1000) { return $null }
    }
}
function Wait-For([string]$text) { Wait-Until { (Get-PanePlain).Contains($text) } }
function Wait-Change([string]$baseline) { Wait-Until { (Get-Pane) -ne $baseline } }
function Wait-Stable { # 3 identical captures in a row; returns ms until the last change
    $t0 = Get-Now; $last = $t0; $same = 0; $prev = Get-Pane
    while ($same -lt 3) {
        $cur = Get-Pane
        if ($cur -eq $prev) { $same++ } else { $same = 0; $last = Get-Now; $prev = $cur }
        if ((Get-Now) - $t0 -gt $Timeout * 1000) { return $null }
    }
    $last - $t0
}
function Start-App { T new-session -d -s m -x $cols -y $rows "`"$Binary`" `"$Repo`"" | Out-Null }
function Stop-App { T kill-session -t m 2>$null | Out-Null }
function Write-Metric($name, $value) { "$name=$value" }
function Write-Stats($prefix, [double[]]$values, [switch]$Full) {
    $med, $p95, $max = Get-Stats $values
    Write-Metric "${prefix}_median" $med
    if ($Full) { Write-Metric "${prefix}_p95" $p95 }
    Write-Metric "${prefix}_max" $max
}
function Get-AppRssKib { # RSS of the app: the pane pid, or its gitunia child when the pane runs a shell
    $panePid = [int](T display-message -t m -p '#{pane_pid}')
    $proc = Get-Process -Id $panePid -ErrorAction SilentlyContinue
    $appName = [System.IO.Path]::GetFileNameWithoutExtension($Binary)
    if ($proc -and $proc.ProcessName -ne $appName) {
        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId=$panePid AND Name='$appName.exe'" | Select-Object -First 1
        $proc = if ($child) { Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
    }
    if ($proc) { [long]($proc.WorkingSet64 / 1024) } else { 'nan' }
}
function Get-DetailsHeader([string]$pane) { $pane -split "`n" | Where-Object { $_ -match 'Details ─ Changes' } | Select-Object -First 1 }
function Get-DiffHash([string]$pane) { if ($pane -match 'Diff  [0-9a-f]{7}') { $Matches[0] } else { '' } }
function Test-DetailsReady([string]$base) {
    $cur = Get-PanePlain
    (Get-DetailsHeader $cur) -ne $base -and -not $cur.Contains('Loading...')
}
function Test-DiffReady([string]$base) {
    $cur = Get-PanePlain
    (Get-DiffHash $cur) -ne $base -and -not $cur.Contains('Loading...') -and $cur.Contains('@@')
}
# One `j` per sample, with a 300 ms pause so neighbour prefetch can finish (the forward walk through the history).
function Measure-Steps([string]$kind) {
    $out = @()
    for ($i = 0; $i -lt $Keys; $i++) {
        $base = if ($kind -eq 'details') { Get-DetailsHeader (Get-PanePlain) } else { Get-DiffHash (Get-PanePlain) }
        $t0 = Get-Now
        T send-keys -t m j | Out-Null
        $elapsed = if ($kind -eq 'details') { Wait-Until { Test-DetailsReady $base } } else { Wait-Until { Test-DiffReady $base } }
        if ($null -ne $elapsed) { $out += (Get-Now) - $t0 }
        Start-Sleep -Milliseconds 300
    }
    , $out
}

try {
    Write-Metric bin $Binary
    Write-Metric binary_bytes (Get-Item $Binary).Length
    Write-Metric marker $Marker
    T start-server | Out-Null

    # --- cold start: new-session until the newest commit subject (marker) is on screen
    $vals = @()
    for ($i = 0; $i -lt $Runs; $i++) {
        $t0 = Get-Now
        Start-App
        if ($null -ne (Wait-For $Marker)) { $vals += (Get-Now) - $t0 } else { [Console]::Error.WriteLine("cold start: marker not seen within ${Timeout}s") }
        Stop-App
    }
    Write-Stats cold_start_ms $vals -Full
    Write-Metric cold_start_samples $vals.Count
    if ($OnlyStart) { return }

    # --- blame case: the blame view of the file shown in the newest commit, then key latency inside it
    if ($Blame) {
        Start-App
        if ($null -eq (Wait-For $Marker)) { [Console]::Error.WriteLine('app never showed marker; skipping blame'); exit 1 }
        $null = Wait-Stable
        $blameRow = '[0-9a-f]{7} .*[0-9]+ \| ' # a short hash in the first column, then the line number and `|`
        T send-keys -t m Tab | Out-Null # focus the details pane (its file is the one blamed)
        Start-Sleep -Milliseconds 500
        $t0 = Get-Now
        # psmux drops keys after the first in one call; send the chord key by key.
        T send-keys -t m Space | Out-Null; T send-keys -t m g | Out-Null; T send-keys -t m b | Out-Null
        if ($null -eq (Wait-Until { (Get-PanePlain) -match $blameRow })) {
            Write-Metric blame_first_rows_ms nan; [Console]::Error.WriteLine('blame view did not open'); exit 1
        }
        Write-Metric blame_first_rows_ms (Format-Ms ((Get-Now) - $t0))
        Start-Sleep 2; $null = Wait-Stable
        foreach ($case in @(@{ Key = 'j'; Name = 'blame_j_ms' }, @{ Key = 'C-d'; Name = 'blame_page_ms' })) {
            $vals = @()
            for ($i = 0; $i -lt $Keys; $i++) {
                $base = Get-Pane; $t0 = Get-Now
                T send-keys -t m $case.Key | Out-Null
                if ($null -ne (Wait-Change $base)) { $vals += (Get-Now) - $t0 }
            }
            Write-Stats $case.Name $vals -Full
        }
        $base = Get-Pane; $t0 = Get-Now
        T send-keys -t m G | Out-Null
        Write-Metric blame_jump_end_ms $(if ($null -ne (Wait-Change $base)) { Format-Ms ((Get-Now) - $t0) } else { 'nan' })
        $null = Wait-Stable
        Write-Metric blame_rss_kib (Get-AppRssKib)
        return
    }

    # --- navigation measurements in one session
    Start-App
    if ($null -eq (Wait-For $Marker)) { [Console]::Error.WriteLine('app never showed marker; skipping navigation'); exit 1 }
    $null = Wait-Stable

    $vals = Measure-Steps 'details'
    Write-Stats details_step_ms $vals -Full; Write-Metric details_step_samples $vals.Count
    $vals = Measure-Steps 'diff'
    Write-Stats diff_step_ms $vals -Full; Write-Metric diff_step_samples $vals.Count

    # details while holding `j`: 10 moves in one burst
    $base = Get-DetailsHeader (Get-PanePlain)
    $t0 = Get-Now
    T send-keys -t m -N 10 j | Out-Null
    $elapsed = Wait-Until { Test-DetailsReady $base }
    Write-Metric details_burst_10j_ms $(if ($null -ne $elapsed) { Format-Ms ((Get-Now) - $t0) } else { 'nan' })
    $null = Wait-Stable

    # key-to-frame latency: `j` must change the rendered frame (the selection highlight counts, -e keeps attributes)
    $vals = @()
    for ($i = 0; $i -lt $Keys; $i++) {
        $base = Get-Pane; $t0 = Get-Now
        T send-keys -t m j | Out-Null
        if ($null -ne (Wait-Change $base)) { $vals += (Get-Now) - $t0 }
    }
    Write-Stats key_latency_ms $vals -Full; Write-Metric key_samples $vals.Count

    # page scroll: Ctrl-d x keys, each until the frame changes
    $vals = @()
    for ($i = 0; $i -lt $Keys; $i++) {
        $base = Get-Pane; $t0 = Get-Now
        T send-keys -t m C-d | Out-Null
        if ($null -ne (Wait-Change $base)) { $vals += (Get-Now) - $t0 }
    }
    Write-Stats page_scroll_ms $vals

    # burst: 200 j in one send-keys; time until the frame changed and then stopped changing
    $base = Get-Pane
    T send-keys -t m -N 200 j | Out-Null
    $first = Wait-Change $base
    $settle = if ($null -ne $first) { Wait-Stable }
    Write-Metric burst_200j_settle_ms $(if ($null -ne $settle) { Format-Ms ($first + $settle) } else { 'nan' })

    # Wait for the log to finish streaming (`n/total+` in the status bar) before G, else G lands mid-history (#192).
    $null = Wait-Until { -not ((Get-PanePlain) -match '[0-9]/[0-9]+\+') }
    $null = Wait-Stable
    $t0 = Get-Now
    T send-keys -t m G | Out-Null
    if ($null -ne (Wait-For $Oldest)) { Write-Metric jump_to_end_ms (Format-Ms ((Get-Now) - $t0)) }
    else { Write-Metric jump_to_end_ms nan; [Console]::Error.WriteLine("oldest='$Oldest' not seen") }
    $t0 = Get-Now
    # psmux drops the second key of "g g" sent in one call; send the chord key by key.
    T send-keys -t m g | Out-Null; T send-keys -t m g | Out-Null
    Write-Metric jump_to_start_ms $(if ($null -ne (Wait-For $Marker)) { Format-Ms ((Get-Now) - $t0) } else { 'nan' })

    # resize: the frame must change after the window shrinks and still show the newest subject
    # psmux drops the second key of "g g" sent in one call; send the chord key by key.
    T send-keys -t m g | Out-Null; T send-keys -t m g | Out-Null
    $null = Wait-For $Marker; $null = Wait-Stable
    $base = Get-Pane
    T resize-window -t m -x ($cols - 30) -y ($rows - 10) | Out-Null
    $elapsed = Wait-Change $base
    Write-Metric resize_ms $(if ($null -ne $elapsed -and (Get-PanePlain).Contains($Marker)) { Format-Ms $elapsed } else { 'nan' })

    Write-Metric rss_kib (Get-AppRssKib)
}
finally {
    T kill-server 2>$null | Out-Null
    foreach ($name in $savedEnv.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnv[$name]) }
    Remove-Item -Recurse -Force $isolated, $emptyConfig -ErrorAction SilentlyContinue
}
