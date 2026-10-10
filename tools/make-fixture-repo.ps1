param([Parameter(Mandatory = $true)][string]$Path)
# Small deterministic fixture repo for the Windows smoke test (40 commits; newest has SUBJ-LATEST, the one before has non-ASCII).
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Path | Out-Null
git -C $Path init -q -b main
$utf8 = New-Object System.Text.UTF8Encoding($false)
$msgFile = Join-Path ([IO.Path]::GetTempPath()) 'gitunia-msg.txt'
for ($i = 1; $i -le 40; $i++) {
    $subject = "commit $i"
    if ($i -eq 39) { $subject = "caf$([char]0x00e9) $([char]0x2713)" }
    if ($i -eq 40) { $subject = 'SUBJ-LATEST' }
    [IO.File]::WriteAllText($msgFile, $subject, $utf8)
    git -C $Path -c user.name=Test -c user.email=test@example.com commit -q --allow-empty -F $msgFile
}
git -C $Path log --oneline | Select-Object -First 3
