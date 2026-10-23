# Screenshots of the real UI for pull requests (Windows; PowerShell 7, psmux, resvg, dotnet, git; `gh` for publish/clean).
# PowerShell twin of scripts/screenshot.sh: same commands, options and scenario names. The real binary runs in a private
# psmux server on a throwaway repo, `capture-pane -p -e` gives the ANSI frame, tools/AnsiToSvg turns it into an SVG and
# resvg into a PNG (rsvg-convert has no native Windows build; see docs/decisions.md). Headless, no network.
# See the screenshots skill (.claude/skills/screenshots/SKILL.md). psmux: `winget install marlocarlo.psmux`;
# resvg: `winget install linebender.resvg`.
#
# Usage:
#   scripts/screenshot.ps1 render  -Binary <gitunia.exe> [-Out <dir>] [-Scenarios default,diff,narrow,light] [-Repo <dir>]
#   scripts/screenshot.ps1 publish -Pr <number> [-Out <dir>]    push the PNGs of `render` to the screenshots repository, print markdown
#   scripts/screenshot.ps1 clean                                remove images of merged/closed PRs from the screenshots repository
# Scenario names, the screenshots repository (GITUNIA_SCREENSHOTS_REPO) and the throwaway repositories are the same as in
# screenshot.sh (its header describes every scenario); -Repo is for scenarios that do not need the dirty/commit/rebase copies.
# Each key is sent on its own: psmux drops the second key of "g g" sent in one send-keys call.
param(
    [Parameter(Mandatory, Position = 0)][ValidateSet('render', 'publish', 'clean')][string]$Command,
    [string]$Binary = '', [string]$Out = '', [string[]]$Scenarios = @('default', 'diff', 'narrow', 'light'), [string]$Repo = '', [string]$Pr = '')

#requires -Version 7
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$repoRoot = Split-Path -Parent $PSScriptRoot
$cacheDir = Join-Path ($env:LOCALAPPDATA ?? (Join-Path $HOME '.cache')) 'gitunia/screenshot'
if (-not $Out) { $Out = Join-Path $cacheDir 'out' }
$branchName = 'main'
$agentRole = $env:GITUNIA_AGENT_ROLE ?? 'implementer'
$work = ''
$sock = "gtshot-$PID"

function Write-Err([string]$text) { [Console]::Error.WriteLine($text) }
function Stop-Script([string]$text, [int]$code) { Write-Err $text; $script:exitCode = $code; throw 'stopped' }
function Find-Tool([string]$name, [string]$hint) {
    if (Get-Command $name -ErrorAction SilentlyContinue) { return }
    # winget adds its links or package folders to the user PATH, which a shell started earlier does not see yet.
    $winget = Join-Path $env:LOCALAPPDATA 'Microsoft/WinGet'
    $found = Get-ChildItem (Join-Path $winget 'Links'), (Join-Path $winget 'Packages/*/*') -Filter "$name.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $env:PATH = "$($found.DirectoryName)$([IO.Path]::PathSeparator)$env:PATH"; return }
    Write-Err "missing tool '$name': $hint"; $script:exitCode = 3; throw 'missing tool'
}
function Invoke-Git([string]$dir, [string[]]$gitArgs) { & git -C $dir @gitArgs; if ($LASTEXITCODE -ne 0) { throw "git $($gitArgs -join ' ') failed in $dir" } }

# --- throwaway repositories (deterministic; a private git config and identity, the caller's environment is restored)
$gitVars = 'GIT_CONFIG_GLOBAL', 'GIT_CONFIG_SYSTEM', 'GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL', 'GIT_AUTHOR_DATE', 'GIT_COMMITTER_DATE'
function Use-GitIdentity([scriptblock]$body) {
    $saved = @{}
    foreach ($name in $gitVars) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        $env:GIT_CONFIG_GLOBAL = $script:emptyConfig; $env:GIT_CONFIG_SYSTEM = $script:emptyConfig
        $env:GIT_AUTHOR_NAME = 'Ada Lovelace'; $env:GIT_AUTHOR_EMAIL = 'ada@example.com'
        $env:GIT_COMMITTER_NAME = 'Ada Lovelace'; $env:GIT_COMMITTER_EMAIL = 'ada@example.com'
        & $body
    }
    finally { foreach ($name in $gitVars) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
}
function Set-GitDate([string]$date) { $env:GIT_AUTHOR_DATE = $date; $env:GIT_COMMITTER_DATE = $date }
function Write-File([string]$path, [string]$text, [switch]$Append) {
    $encoding = [System.Text.UTF8Encoding]::new($false)
    if ($Append) { [IO.File]::AppendAllText($path, $text, $encoding) } else { [IO.File]::WriteAllText($path, $text, $encoding) }
}

function New-ThrowawayRepo([string]$dir) {
    Use-GitIdentity {
        $script:day = 0
        function Save-Commit([string]$message) {
            $script:day++
            Set-GitDate ('2026-03-{0:00}T12:00:00Z' -f $script:day)
            Invoke-Git $dir 'add', '-A'; Invoke-Git $dir 'commit', '-q', '-m', $message
        }
        Invoke-Git $PWD.Path 'init', '-q', '-b', 'main', $dir
        Write-File "$dir/README.md" "# Demo`n`nA small demo project.`n"
        New-Item -ItemType Directory -Force "$dir/src" | Out-Null
        Write-File "$dir/src/math.txt" "int Add(int left, int right) => left + right;`n"
        Save-Commit 'Initial commit'
        Write-File "$dir/src/math.txt" "int Subtract(int left, int right) => left - right;`n" -Append
        Save-Commit 'Add subtract'
        Invoke-Git $dir 'tag', 'v0.1'
        Invoke-Git $dir 'switch', '-q', '-c', 'feature/multiply'
        Write-File "$dir/src/multiply.txt" "int Multiply(int left, int right) => left * right;`n"
        Save-Commit 'Add multiply'
        Write-File "$dir/src/multiply.md" "Multiply is exact for small values.`n"
        Save-Commit 'Document multiply'
        Invoke-Git $dir 'switch', '-q', 'main'
        Write-File "$dir/src/math.txt" "int Divide(int left, int right) => left / right;`n" -Append
        Write-File "$dir/README.md" "Division truncates.`n" -Append
        Save-Commit 'Add divide with a very long subject that has to be truncated when the terminal is narrow'
        $script:day++
        Set-GitDate ('2026-03-{0:00}T12:00:00Z' -f $script:day)
        Invoke-Git $dir 'merge', '-q', '--no-ff', '-m', 'Merge feature/multiply', 'feature/multiply'
        Write-File "$dir/src/math.txt" "int Modulo(int left, int right) => left % right;`n" -Append
        Save-Commit 'Add modulo'
        Invoke-Git $dir 'switch', '-q', '-c', 'fix/overflow'
        Invoke-Git $dir 'switch', '-q', 'main'
    }
}

function New-DirtyRepo([string]$source, [string]$dir) { # a copy with a merge conflict and every kind of change
    Copy-Item -Recurse $source $dir
    Use-GitIdentity {
        Set-GitDate '2026-04-01T12:00:00Z'
        Invoke-Git $dir 'switch', '-q', '-c', 'side', 'fix/overflow'
        Write-File "$dir/src/math.txt" "int Add(long left, long right) => left + right;`n"
        Invoke-Git $dir 'commit', '-q', '-am', 'Widen Add'
        Invoke-Git $dir 'switch', '-q', 'main'
        Write-File "$dir/src/math.txt" "int Add(int left, int right) => checked(left + right);`n"
        Invoke-Git $dir 'commit', '-q', '-am', 'Check Add for overflow'
        & git -C $dir merge -q side *>$null # the conflict is the point; a non-zero exit is expected
        Invoke-Git $dir 'mv', 'src/multiply.md', 'src/multiply-notes.md'
        Write-File "$dir/README.md" "Division truncates toward zero.`n" -Append
        Invoke-Git $dir 'add', 'README.md'
        Write-File "$dir/src/power.txt" "int Power(int value, int exponent) => 1;`n"
        Invoke-Git $dir 'add', 'src/power.txt'
        Write-File "$dir/README.md" "Edited again.`n" -Append
        Remove-Item "$dir/src/multiply.txt"
        New-Item -ItemType Directory -Force "$dir/notes" | Out-Null
        Write-File "$dir/notes/todo.txt" "todo`n"
        Write-File "$dir/Ünïcode name.txt" "x`n"
    }
}

function New-CommitRepo([string]$source, [string]$dir, [bool]$hook) { # staged, unstaged and untracked changes; $hook adds a failing pre-commit hook
    Copy-Item -Recurse $source $dir
    Use-GitIdentity {
        Write-File "$dir/README.md" "Division truncates toward zero.`n" -Append
        Write-File "$dir/src/power.txt" "int Power(int value, int exponent) => 1; // TODO`n"
        Invoke-Git $dir 'add', 'README.md', 'src/power.txt'
        Write-File "$dir/src/math.txt" "int Modulo(int left, int right) => left % right; // edited`n" -Append
        Write-File "$dir/notes.txt" "todo`n"
    }
    if ($hook) { # git runs hooks through its own sh on Windows, so the shebang script works as on Linux
        Write-File "$dir/.git/hooks/pre-commit" "#!/bin/sh`necho `"pre-commit: found TODO in src/power.txt`" >&2`necho `"pre-commit: remove it or commit with No-Verify`" >&2`nexit 1`n"
    }
}

function New-RebaseRepo([string]$source, [string]$dir) { # a linear branch of five commits on top of v0.1
    Copy-Item -Recurse $source $dir
    Use-GitIdentity {
        Invoke-Git $dir 'switch', '-q', '-c', 'cleanup', 'v0.1'
        $index = 0
        foreach ($subject in 'Rename Add to Sum', 'Fix typo in README', 'Add Sum tests', 'Remove dead code', 'Update the changelog') {
            $index++
            Set-GitDate "2026-04-0${index}T12:00:00Z"
            Write-File "$dir/cleanup-$index.txt" "change $index`n"
            Invoke-Git $dir 'add', '-A'
            Invoke-Git $dir 'commit', '-q', '-m', $subject, '-m', "Longer explanation of: $subject."
        }
    }
}

function Build-Converter { # returns the converter dll path
    $dll = Join-Path $cacheDir 'AnsiToSvg/AnsiToSvg.dll'
    $program = Join-Path $repoRoot 'tools/AnsiToSvg/Program.cs'
    if (-not (Test-Path $dll) -or (Get-Item $program).LastWriteTimeUtc -gt (Get-Item $dll).LastWriteTimeUtc) {
        Write-Err 'building tools/AnsiToSvg ...'
        & dotnet build (Join-Path $repoRoot 'tools/AnsiToSvg') -c Release -o (Join-Path $cacheDir 'AnsiToSvg') -v quiet -nologo | ForEach-Object { Write-Err $_ }
        if ($LASTEXITCODE -ne 0) { Write-Err 'converter build failed'; $script:exitCode = 3; throw 'converter build failed' }
    }
    $dll
}

# scenario -> columns, rows, theme, keys, keys after the first frame settled
function Get-Scenario([string]$name) {
    $s = @{ Columns = 120; Rows = 36; Theme = 'dark'; Keys = ''; After = '' }
    switch ($name) {
        'default' { }
        'diff' { $s.Keys = 'Enter Enter' }
        'zoom-both' { $s.Keys = 'Enter Enter Space z' }
        'view-menu' { $s.Keys = 'Enter Enter Space v v' }
        'working-view-menu' { $s.Keys = 'Space s j Enter j Space v v' }
        'list-view-menu' { $s.Keys = 'Enter Space v v' }
        'files-view-menu' { $s.Keys = 'g f Enter Space v v' }
        'working-list-menu' { $s.Keys = 'Space s Space v v' }
        'split' { $s.Columns = 170; $s.Keys = 'Enter Enter S' }
        'narrow' { $s.Columns = 64; $s.Rows = 30 }
        'light' { $s.Theme = 'light' }
        'files' { $s.Keys = 'g f Enter Enter' }
        'files-filter' { $s.Keys = 'g f Enter / m u l' }
        'file-content' { $s.Keys = 'g f Enter Enter j Enter' }
        'palette' { $s.Rows = 20; $s.Keys = ': C-j C-j' }
        'whichkey' { $s.Keys = 'Space' }
        'help' { $s.Rows = 30; $s.Keys = '?' }
        'help-lists' { $s.Rows = 30; $s.Keys = '? Tab Tab' }
        'menu' { $s.Keys = 'j j Space .' }
        'menu-sidebar' { $s.Keys = 'C-w h j j Space .' }
        'sidebar-ref' { $s.Keys = 'C-w h j j j' }
        'prompt' { $s.Keys = 'Space b c f e a t' }
        'prompt-narrow' { $s.Columns = 64; $s.Rows = 30; $s.Keys = 'Space b c f e a t' }
        'jump' { $s.Keys = 'Space j c' }
        'jump-narrow' { $s.Columns = 64; $s.Rows = 30; $s.Keys = 'Space j c' }
        'jump-diff' { $s.Keys = 'Enter Enter Space j e' }
        'jump-working' { $s.Keys = 'Space s Space j e' }
        'working' { $s.Keys = 'Space s' }
        'working-narrow' { $s.Columns = 64; $s.Rows = 30; $s.Keys = 'Space s' }
        'working-diff' { $s.Keys = 'Space s j Enter j' }
        'working-diff-narrow' { $s.Columns = 64; $s.Rows = 30; $s.Keys = 'Space s j Enter' }
        'working-zoom' { $s.Keys = 'Space s j Enter Space z' }
        'working-stage' { $s.Keys = 'Space s Space a Space a' }
        'working-hunk' { $s.Keys = 'Space s j Enter'; $s.After = 'Space a' }
        'working-lines' { $s.Keys = 'Space s j Enter'; $s.After = 'j j j j j v' }
        'working-discard' { $s.Keys = 'Space s j'; $s.After = 'Space d' }
        'working-resolve' { $s.Keys = 'Space s'; $s.After = 'Space a' }
        'settings' { $s.Keys = 'Space ,' }
        'settings-choice' { $s.Keys = 'Space , Enter Enter j' }
        'settings-narrow' { $s.Columns = 64; $s.Rows = 24; $s.Keys = 'Space ,' }
        'settings-keys' { $s.Keys = 'Space , j Enter j j j' }
        'settings-keys-entry' { $s.Keys = 'Space , j Enter j j j Enter' }
        'theme-preview' { $s.Keys = 'Space , Enter Enter j j' }
        'theme-kept' { $s.Keys = 'Space , Enter Enter j j Enter' }
        'theme-tokyo' { $s.Keys = 'Space ,'; $s.After = 'Enter Enter j j j j j j j' }
        'theme-latte' { $s.Keys = 'Space ,'; $s.After = 'Enter Enter j j j j j j' }
        'status-fresh' { $s.Keys = 'Space F' }
        'status-settled' { $s.Keys = 'Space F'; $s.After = 'Space F' }
        { $_ -in 'working-commit', 'working-hook' } { $s.Keys = 'Space s Space a Space a Space c' }
        'rebase-editor' { $s.Keys = 'G Space g i'; $s.After = 'j s j j d' }
        'rebase-reword' { $s.Keys = 'G Space g i'; $s.After = 'j r' }
        'rebase-confirm' { $s.Keys = 'G Space g i'; $s.After = 'j s j j d Enter' }
        'rebase-narrow' { $s.Columns = 64; $s.Rows = 24; $s.Keys = 'G Space g i'; $s.After = 'j s j j d' }
        default { return $null }
    }
    $s
}

function T { psmux -L $sock @args }
function Get-Pane { (T capture-pane -t shot -p -e) -join "`n" }
function Send-Keys([string]$keys) { # one send-keys call per key
    foreach ($key in ($keys -split ' ' | Where-Object { $_ })) { T send-keys -t shot $key | Out-Null }
}
function Wait-Stable { # until three identical captures in a row (about 10 s at most)
    $previous = ''; $same = 0
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        Start-Sleep -Milliseconds 100
        $current = Get-Pane
        if ($current -and $current -eq $previous) { $same++ } else { $same = 0 }
        $previous = $current
        if ($same -ge 3) { return }
    }
    Write-Err 'frame did not settle within 10 s; capturing anyway'
}
function Send-Message { # a subject, a blank line, a body line, then SignOff on (Alt-s) in the open message box
    T send-keys -t shot -l 'Drop stale cache entries on refresh' | Out-Null
    T send-keys -t shot Enter | Out-Null; T send-keys -t shot Enter | Out-Null
    T send-keys -t shot -l 'Entries were never dropped when the status changed.' | Out-Null
    T send-keys -t shot M-s | Out-Null
}

function Invoke-Render {
    Find-Tool psmux 'winget install marlocarlo.psmux'
    Find-Tool resvg 'winget install linebender.resvg'
    Find-Tool dotnet 'install the .NET SDK'
    Find-Tool git 'install git'
    if (-not $Binary) { Stop-Script '-Binary <path to gitunia.exe> is required (build: dotnet build src/Gitunia.Tui -c Release -o <dir>)' 2 }
    if (-not (Test-Path -PathType Leaf $Binary)) { Stop-Script "binary not found: $Binary" 2 }
    $binaryPath = (Resolve-Path $Binary).Path
    $script:work = Join-Path ([IO.Path]::GetTempPath()) "gtshot-$PID"
    New-Item -ItemType Directory -Force $work, $cacheDir, $Out | Out-Null
    $script:emptyConfig = Join-Path $work 'empty.gitconfig'
    New-Item -ItemType File -Force $emptyConfig | Out-Null
    Remove-Item (Join-Path $Out '*.png') -ErrorAction SilentlyContinue # stale images of earlier runs must never be published
    $null = Build-Converter
    $converter = Join-Path $cacheDir 'AnsiToSvg/AnsiToSvg.dll'
    $sourceRepo = $Repo
    if ($sourceRepo) { $sourceRepo = (Resolve-Path $sourceRepo).Path } else { $base = Join-Path $work 'repo'; New-ThrowawayRepo $base; $sourceRepo = $base }
    New-Item -ItemType Directory -Force (Join-Path $work 'state'), (Join-Path $work 'config') | Out-Null

    # The server inherits this environment and so does the app. The caller's values are restored in `finally`.
    $appVars = 'COLORTERM', 'XDG_STATE_HOME', 'XDG_CONFIG_HOME', 'GIT_CONFIG_GLOBAL', 'GIT_CONFIG_SYSTEM', 'GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL'
    $savedApp = @{}
    foreach ($name in $appVars) { $savedApp[$name] = [Environment]::GetEnvironmentVariable($name) }
    $env:COLORTERM = 'truecolor'; $env:XDG_STATE_HOME = Join-Path $work 'state'; $env:XDG_CONFIG_HOME = Join-Path $work 'config'
    $env:GIT_CONFIG_GLOBAL = $emptyConfig; $env:GIT_CONFIG_SYSTEM = $emptyConfig
    # Linux guesses an identity from the account; on Windows git has none and commits fail before the hook runs.
    $env:GIT_AUTHOR_NAME = 'Ada Lovelace'; $env:GIT_AUTHOR_EMAIL = 'ada@example.com'
    $env:GIT_COMMITTER_NAME = 'Ada Lovelace'; $env:GIT_COMMITTER_EMAIL = 'ada@example.com'
    try {
        T start-server | Out-Null
        foreach ($scenario in ($Scenarios -split ',' | Where-Object { $_ })) {
            $s = Get-Scenario $scenario
            if (-not $s) { Stop-Script "unknown scenario '$scenario'" 2 }
            $scenarioRepo = $sourceRepo
            if (-not $Repo) {
                if ($scenario -in 'working-commit', 'working-hook') {
                    $scenarioRepo = Join-Path $work ($scenario -eq 'working-hook' ? 'repo-hook' : 'repo-commit')
                    if (-not (Test-Path $scenarioRepo)) { New-CommitRepo $sourceRepo $scenarioRepo ($scenario -eq 'working-hook') }
                }
                elseif ($scenario -like 'rebase-*') {
                    $scenarioRepo = Join-Path $work 'repo-rebase'
                    if (-not (Test-Path $scenarioRepo)) { New-RebaseRepo $sourceRepo $scenarioRepo }
                }
                elseif ($scenario -like 'working*') {
                    $scenarioRepo = Join-Path $work 'repo-dirty'
                    if (-not (Test-Path $scenarioRepo)) { New-DirtyRepo $sourceRepo $scenarioRepo }
                }
            }
            if ($env:GITUNIA_SCREENSHOT_CONFIG) { # optional config.json for the private config directory
                New-Item -ItemType Directory -Force (Join-Path $work 'config/gitunia') | Out-Null
                Copy-Item $env:GITUNIA_SCREENSHOT_CONFIG (Join-Path $work 'config/gitunia/config.json') -Force
            }
            T kill-session -t shot 2>$null | Out-Null
            T new-session -d -s shot -x $s.Columns -y $s.Rows "`"$binaryPath`" `"$scenarioRepo`"" | Out-Null
            Wait-Stable
            if ($s.Keys) { Send-Keys $s.Keys; Start-Sleep -Milliseconds 500; Wait-Stable }
            if ($scenario -in 'working-commit', 'working-hook') {
                Send-Message
                if ($scenario -eq 'working-hook') { T send-keys -t shot M-Enter | Out-Null }
                Start-Sleep -Seconds 1; Wait-Stable
            }
            if ($s.After) { Send-Keys $s.After; Start-Sleep -Milliseconds 500; Wait-Stable } # keys that need the loaded diff
            if ((T display-message -p -t shot '#{pane_dead}') -eq '1') { Stop-Script "the binary exited during scenario '$scenario'; no screenshot taken" 1 }
            $ansi = Join-Path $work "$scenario.ansi"; $svg = Join-Path $work "$scenario.svg"; $png = Join-Path $Out "$scenario.png"
            [IO.File]::WriteAllText($ansi, ((T capture-pane -t shot -p -e) -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
            & dotnet $converter --theme $s.Theme --columns $s.Columns --rows $s.Rows --in $ansi --out $svg
            if ($LASTEXITCODE -ne 0) { throw "AnsiToSvg failed for $scenario" }
            & resvg --monospace-family Consolas --font-family Consolas $svg $png
            if ($LASTEXITCODE -ne 0) { throw "resvg failed for $scenario" }
            Write-Err "wrote $png"
        }
    }
    finally {
        T kill-server 2>$null | Out-Null
        foreach ($name in $savedApp.Keys) { [Environment]::SetEnvironmentVariable($name, $savedApp[$name]) }
    }
}

# --- publish / clean: gh and git against the screenshots repository, as in screenshot.sh
function Get-RepositorySlug { # owner/name of origin
    (& git -C $repoRoot remote get-url origin) -replace '^.*[:/]([^/:]+/[^/]+)$', '$1' -replace '\.git$', ''
}
function Get-ScreenshotsSlug { $env:GITUNIA_SCREENSHOTS_REPO ?? "$(Get-RepositorySlug)-screenshots" }
function Open-Clone([string]$dir) {
    Find-Tool gh 'install the GitHub CLI'
    & gh repo clone (Get-ScreenshotsSlug) $dir -- -q --branch $branchName --single-branch --depth 1
    if ($LASTEXITCODE -ne 0) { Write-Err "cannot clone $(Get-ScreenshotsSlug); create it first (private, with a $branchName branch)"; $script:exitCode = 4; throw 'clone failed' }
    Invoke-Git $dir 'config', 'user.name', "Claude ($agentRole)"
    Invoke-Git $dir 'config', 'user.email', (& git -C $repoRoot config user.email)
}
function Push-WithRetry([string]$dir) { # plain push, rebased once on a concurrent publish or clean (never forced)
    & git -C $dir push -q origin $branchName 2>$null
    if ($LASTEXITCODE -eq 0) { return }
    Invoke-Git $dir 'pull', '-q', '--rebase', 'origin', $branchName
    Invoke-Git $dir 'push', '-q', 'origin', $branchName
}
function Invoke-Publish {
    if (-not $Pr) { Stop-Script '-Pr <number> is required' 2 }
    $pngs = @(Get-ChildItem (Join-Path $Out '*.png') -ErrorAction SilentlyContinue)
    if ($pngs.Count -eq 0) { Stop-Script "no PNGs in $Out; run render first" 2 }
    $script:work = Join-Path ([IO.Path]::GetTempPath()) "gtshot-$PID"
    New-Item -ItemType Directory -Force $work | Out-Null
    $clone = Join-Path $work 'clone'
    Open-Clone $clone
    $stamp = (& git -C $repoRoot rev-parse --short HEAD)
    $target = "pr-$Pr/$stamp"
    New-Item -ItemType Directory -Force (Join-Path $clone $target) | Out-Null
    Copy-Item $pngs.FullName (Join-Path $clone $target)
    Invoke-Git $clone 'add', '-A'
    & git -C $clone diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        Invoke-Git $clone 'commit', '-q', '-m', "chore: screenshots for PR #$Pr @ $stamp", '-m', "Agent: $agentRole"
        Push-WithRetry $clone
    }
    $pinned = (& git -C $clone rev-parse HEAD)
    $slug = Get-ScreenshotsSlug
    Write-Err "pushed to $slug ($target @ $($pinned.Substring(0, 7)))"
    foreach ($file in $pngs) {
        $name = $file.BaseName
        "**$name**"; ''; "![$name](https://github.com/$slug/raw/$pinned/$target/$name.png)"; ''
    }
}
function Invoke-Clean {
    $script:work = Join-Path ([IO.Path]::GetTempPath()) "gtshot-$PID"
    New-Item -ItemType Directory -Force $work | Out-Null
    $clone = Join-Path $work 'clone'
    Open-Clone $clone
    $removed = 0
    foreach ($directory in Get-ChildItem $clone -Directory -Filter 'pr-*') {
        $number = $directory.Name.Substring(3)
        $state = & gh pr view $number -R (Get-RepositorySlug) --json state -q .state 2>$null
        if ($LASTEXITCODE -ne 0) { $state = 'UNKNOWN' }
        if ($state -in 'MERGED', 'CLOSED') {
            Invoke-Git $clone 'rm', '-rq', "pr-$number"
            $removed++
            Write-Err "removed pr-$number ($state)"
        }
    }
    if ($removed -gt 0) {
        Invoke-Git $clone 'commit', '-q', '-m', 'chore: remove screenshots of merged or closed PRs', '-m', "Agent: $agentRole"
        Push-WithRetry $clone
    }
    else { Write-Err 'nothing to clean' }
}

$exitCode = 0
try {
    switch ($Command) { 'render' { Invoke-Render } 'publish' { Invoke-Publish } 'clean' { Invoke-Clean } }
}
catch {
    if ($exitCode -eq 0) { $exitCode = 1; Write-Err $_.Exception.Message }
}
finally {
    if ($work) { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
}
exit $exitCode
