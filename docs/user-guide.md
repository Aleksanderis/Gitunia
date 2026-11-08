# Gitunia user guide

## Install

Needs `git` 2.43 or newer on your `PATH`. Any terminal works. Truecolor looks best.

- **One line (Linux):** `curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh` installs the latest release and updates it when run again. It picks the native package (pacman, apt or dnf, needs root or sudo) and otherwise puts the binary in `~/.local/bin`; every download is checked against its `.sha256`. `GITUNIA_METHOD=archive` forces the archive, `GITUNIA_INSTALL_DIR` sets its folder, `GITUNIA_FORCE=1` reinstalls. Read the script first if you prefer: `scripts/install.sh`.
- **One line (Windows):** `irm https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.ps1 | iex` installs `gitunia.exe` to `%LOCALAPPDATA%\Gitunia`, adds it to your user `PATH` (no admin rights) and updates when run again. `GITUNIA_INSTALL_DIR`, `GITUNIA_NO_PATH=1` and `GITUNIA_FORCE=1` work as above.
- **Package (linux-x64, linux-arm64):** `sudo apt install ./gitunia_<version>_amd64.deb` (`_arm64.deb` on arm64) or `sudo dnf install ./gitunia-<version>-1.x86_64.rpm` (`.aarch64.rpm` on arm64). Installs `/usr/bin/gitunia`.
- **Windows (winget):** `winget install Gitunia.Gitunia` puts `gitunia` on your `PATH`. The binary is unsigned, so SmartScreen may warn at first. Available once the first release is in winget.
- **Archive (linux-x64, linux-arm64, win-x64, win-arm64):** unpack and put `gitunia` on your `PATH`. Check the download with the `.sha256` file.
- **macOS archive (osx-arm64, Apple silicon):** unpack and put `gitunia` on your `PATH`. It should work but is not fully tested yet, and it is unsigned, so Gatekeeper asks you to allow it (System Settings, Privacy & Security).
- **Arch Linux / Omarchy:** `sudo pacman -U gitunia-<version>-1-x86_64.pkg.tar.zst` (`aarch64` on arm64; from the release page; pulls in `git`). It is not in the AUR yet. The archive above also works: put `gitunia` in `~/.local/bin`.
- **Source:**
  - Prerequisites: the .NET SDK from `global.json` and the Native AOT prerequisites.
    - Linux (Debian/Ubuntu): `clang` and `zlib1g-dev`.
    - Arch: `sudo pacman -S --needed git clang zlib dotnet-sdk`.
    - Windows: "Desktop development with C++".
  - Build: `dotnet publish src/Gitunia.Tui -c Release -o publish`.
  - Run: `publish/gitunia`.

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
