# Gitunia user guide

## Install

Needs `git` 2.43 or newer on your `PATH`. Any terminal works. Truecolor looks best.

One line, installs the latest release and updates it when run again. Downloads are retried a few times if one fails.

**Linux (x64, arm64):**

```sh
curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh
```

It picks the native package (pacman, apt or dnf, needs root or sudo) and otherwise puts the binary in `~/.local/bin`; every download is checked against its `.sha256`. Options are environment variables on the `sh` side of the pipe: `curl -fsSL <url> | GITUNIA_METHOD=archive sh` forces the archive (no root), `GITUNIA_INSTALL_DIR` sets its folder, `GITUNIA_FORCE=1` reinstalls. The checksum comes from the same release, so it catches a damaged download, not a compromised release. The Linux builds need glibc (not Alpine). Read the script first if you prefer: `scripts/install.sh`.

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.ps1 | iex
```

It installs `gitunia.exe` to `%LOCALAPPDATA%\Gitunia`, adds it to your user `PATH` (no admin rights) and updates when run again. Set options first, in the same PowerShell session: `$env:GITUNIA_FORCE = '1'; irm <url> | iex` (also `GITUNIA_INSTALL_DIR` and `GITUNIA_NO_PATH`).

Other ways:

- **Package (linux-x64, linux-arm64):** `sudo apt install ./gitunia_<version>_amd64.deb` (`_arm64.deb` on arm64) or `sudo dnf install ./gitunia-<version>-1.x86_64.rpm` (`.aarch64.rpm` on arm64). Installs `/usr/bin/gitunia`.
- **Windows (winget):** `winget install Gitunia.Gitunia` puts `gitunia` on your `PATH`. The binary is unsigned, so SmartScreen may warn at first. Available once its winget-pkgs submission is approved.
- **Archive (linux-x64, linux-arm64, win-x64, win-arm64):** unpack and put `gitunia` on your `PATH`. Check the download with the `.sha256` file.
- **Homebrew (macOS Apple silicon, Linux x64 and arm64):** `brew install aleksanderis/tap/gitunia` (or `brew tap aleksanderis/tap` once, then `brew install gitunia`). The macOS binary is not signed or notarised: if macOS blocks it, run `xattr -d com.apple.quarantine "$(which gitunia)"`. Available once the first release is published and the tap exists.
- **macOS archive (osx-arm64, Apple silicon):** unpack and put `gitunia` on your `PATH`. It should work but is not fully tested yet, and it is unsigned, so Gatekeeper asks you to allow it (System Settings, Privacy & Security).
- **Arch Linux / Omarchy:** `sudo pacman -U gitunia-<version>-1-x86_64.pkg.tar.zst` (`aarch64` on arm64; from the release page; pulls in `git`). From the AUR (planned, not published yet): `yay -S gitunia-bin` (or `paru -S gitunia-bin`; x86_64 and aarch64); it installs the release binary and `git`. The archive above also works: put `gitunia` in `~/.local/bin`.
- **Source:**
  - Prerequisites: the .NET SDK from `global.json` and the Native AOT prerequisites.
    - Linux (Debian/Ubuntu): `clang` and `zlib1g-dev`.
    - Arch: `sudo pacman -S --needed git clang zlib dotnet-sdk`.
    - Windows: "Desktop development with C++".
  - Build: `dotnet publish src/Gitunia.Tui -c Release -o publish`.
  - Run: `publish/gitunia`.

## Update

`gitunia update` compares your version with the latest release and updates it, whichever way you installed:

- **Installed by the install script** (`~/.local/bin`, `%LOCALAPPDATA%\Gitunia`): it downloads the release archive for your system, checks it against the release's `.sha256` file, runs the new binary with `--version`, and only then replaces the old one. If any step fails, nothing is changed and the command prints the archive address and the manual steps. On Windows it needs no PowerShell: the binary downloads the archive itself and renames the running `gitunia.exe` aside (the leftover `gitunia.exe.old` is removed the next time Gitunia starts). It follows your system proxy settings.
- **Installed by a package manager** (Homebrew, winget, `.deb`, `.rpm`, pacman, AUR): it prints the command to run (`brew upgrade gitunia`, `winget upgrade Gitunia.Gitunia`, or the install script for the Linux packages) and changes nothing, so root-owned files are never touched.
- **Anything else** (a development build, an archive you unpacked yourself): it prints where to download the release and does not replace the binary.

`gitunia update --check` only reports: exit code 0 when you are up to date, 10 when a newer release exists, 1 when the check failed (no network, no release). The check reads GitHub's latest-release page; no token, account or machine id is sent, only the `User-Agent` (`gitunia/<version>`). Behind a corporate proxy that asks for Windows or Kerberos sign-in, your signed-in user's credentials go to the proxy, as for any program that follows the system proxy. `GITUNIA_RELEASE_URL` points `update` at another release JSON (tests, mirrors); the archive and its checksum then come from the same place, so it is no authenticity check.

The install scripts write a one-line marker file `gitunia.install-source` next to the binary; it is how `update` knows a script installed that copy. A copy without the marker is still recognised in the script's default folder (`~/.local/bin`, `%LOCALAPPDATA%\Gitunia`) and, for the package managers, by its path (for example `/usr/bin` or Homebrew's prefix); a script install in another folder needs the install script run once more (`GITUNIA_FORCE=1` for a current version) to get the marker.

### Update hint

Gitunia looks for a newer release in the background, at most once a day (the answer is kept in `update-check.json` in the state directory, the only file the check writes), and then the right end of the status bar says `Gitunia 0.9.0 available: gitunia update` (`brew upgrade gitunia` or `winget upgrade Gitunia.Gitunia` for those installs; the shorter `Gitunia 0.9.0 available` when the terminal is narrow, nothing under about 70 columns). It starts about a second after the first frame, so it never slows startup, and a failed check (offline, no answer) shows nothing and is not tried again before the next day. Only the `User-Agent` is sent, as for `gitunia update`; development builds never check. Turn it off with `Check for updates` in Settings (`<space>,`, Updates), `"updateCheck": false` in `config.json`, or by setting the `GITUNIA_NO_UPDATE_CHECK` environment variable to any value (`GITUNIA_RELEASE_URL` applies here too).

`gitunia update` is always the update command, so a repository folder named `update` is opened with `gitunia ./update`.

## Start and open a repository

- `gitunia` restores your last session, or shows the repository picker.
- `gitunia <path>` opens a repository in a tab. A subfolder resolves to its root.
- `Ctrl-t` opens the picker: type to filter or type a path, `Enter` opens, `Esc` closes.
- Picker keys: `Alt-c` clone, `Alt-i` init, `Alt-p` pin, `Alt-d` forget.
- Tabs: `gt` / `gT`, `Alt-1`..`Alt-9`, `Ctrl-w c` closes. `<space>o <` / `<space>o>` move the active tab one place left or right (`2<space>o>` moves two); with the mouse, press a tab and drag it along the row (`Esc` or releasing outside the row cancels). A plain click still switches. The order is kept in the session.
- Lost? `?` shows the keys of the current view, `:` opens the command palette, `q` quits.

## The screen

- Sidebar (branches, remotes, tags, stashes, worktrees), commit graph, detail pane (Details, Changes, Files).
- `Tab` or `Ctrl-w h/j/k/l` moves between panes. `<space>z` zooms one.
- `<space>s` toggles the Changes view.

| Key | Does |
|---|---|
| `?` | Keys of the focused view |
| `:` or `Ctrl-p` | Command palette |
| `<space>,` | Settings and full key reference |
| `<space>*` | Cycle command level (hides rarer commands) |
| `q` | Close overlay or quit |

Full table: [keymap.md](keymap.md). To rebind: [customising-keys.md](customising-keys.md).

## Everyday tasks

- **Browse:** `Enter` focuses details, `]` / `[` cycle tabs, `<space>h` jumps to HEAD, `/` searches.
- **Read a diff:** `Enter` on a file, `]c` / `[c` jump hunks. `<space>vs` side-by-side.
- **Commit:** `<space>s`, stage with `<space>a` (file, hunk or selected lines), message `<space>c`, commit `Ctrl-g`, `Alt-a` amend.
- **Conflicts:** `Enter` on a `!` file opens the conflict editor. Press `?` there for its keys.
- **Branches, tags, worktrees, stashes:** select in the sidebar, then `<space>b`, `t`, `w`, `k`, or `<space>.` for a menu.
- **Interactive rebase:** `<space>bri` opens the todo editor.
- **Line history:** in a diff (or the blame view, on the highlighted line), select lines with `v` (or stay on one line) and press `<space>L` to see the commits that changed exactly those lines, with each commit's hunk of the range; lines that shift position and renames are followed (a block cut and pasted elsewhere shows as added by the commit that moved it). In the Changes view the lines are numbered as `HEAD` has them, and uncommitted lines have no history. Keys as in the file history.
- **Select and copy text:** in a diff, `v` (or a drag) selects whole lines and `Alt-v` selects by character: move with `h` `l` `w` `b` `0` `$` (and `j` `k` across lines), `iw` / `il` select the word or the line's text, and `y` copies exactly the selected characters through the terminal clipboard (OSC 52), without line numbers or `+`/`-` markers. With the mouse, drag across the text, double-click a word, or triple-click a line in the history diff (the Changes view diff has the double-click only; staging there is `<space>a` / `<space>u` or the chips). It works with word wrap and side-by-side on (a side-by-side selection stays on the side you started on and copies only that side's text), and on the text of the commit's Details tab (a triple-click selects a row there; a double-click opens an issue link). The blame view selects by character too (see Blame below); the lists do not yet. If your terminal keeps mouse reporting, its own selection needs its bypass modifier (usually Shift).
- **File history:** `<space>H` on a file in the commit's Changes or Files list, or in the Changes view, opens the commits that touched it, renames followed, with that commit's diff of the file beside the list. `j`/`k` pick a commit, `J`/`K` scroll the diff, `Enter` selects the commit in the graph, `Esc` returns.
- **Merge or rebase in progress:** `<space>gc` continue, `ga` abort, `gs` skip.
- **Blame:** `<space>gb` on a file in the details pane (the file is read at the shown commit) or in the Changes view lists (the working tree file). Each line shows the short id (`^` marks a boundary commit), author and age; `Enter` closes the view and selects the line's commit in the graph, `/` searches, `Alt-p` blames the file at the parent of the line's commit (`Esc` steps back), `<space>H` opens the history of the file at the blamed revision (the view closes), `<space>L` opens the history of the highlighted line, starting at the commit that wrote it (an uncommitted line has none), `Alt-w`/`Alt-m`/`Alt-c` or the `-w`, `-M` and `-C` chips under the view toggle git's switches of those names (shown in the footer; `blame.ignoreRevsFile` from your git config is honored and announced), `v` or a drag from the gutter selects whole lines, `Alt-v` or a drag from the code selects characters (`h` `l` `w` `b` `0` `$`, `iw` and `il`, or a double or triple click, select a word or a line), `y` copies the code without the gutter and `<space>L` takes the selected lines for the line history, `Esc` closes. The lines show at once and the commit columns fill in while git runs (`blaming 40%` in the footer). If your git config sets `diff.algorithm = histogram`, blame of a large file can take many seconds, because git's blame uses that algorithm; Gitunia keeps your config, so the view stays usable meanwhile.
- **Undo:** destructive actions ask first and say in plain words whether and how they can be undone. The exact restore command of the last actions is in the message history (`<space>m`); the reflog (`<space>l`) restores earlier states.

## Configuration

- **Config:** `config.json` in `~/.config/gitunia`. Set `$XDG_CONFIG_HOME` to override.
- **Session state:** `~/.local/state/gitunia`.

Settings (`<space>,`) edits the same file. The format is in [ux-look.md](internal/ux-look.md).

## Git config is respected

Gitunia uses your Git configuration, hooks, credential helpers and editor, and changes a repository's config only when you ask (upstream, remotes, clone).
