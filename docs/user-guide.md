# Gitunia user guide

## Install

Needs `git` 2.43 or newer on your `PATH`. Any terminal works. Truecolor looks best.

- **Package (linux-x64):** `sudo apt install ./gitunia_<version>_amd64.deb` or `sudo dnf install ./gitunia-<version>-1.x86_64.rpm`. Installs `/usr/bin/gitunia`.
- **Archive (linux-x64, win-x64):** unpack and put `gitunia` on your `PATH`. Check the download with the `.sha256` file.
- **Arch Linux / Omarchy:** `sudo pacman -U gitunia-<version>-1-x86_64.pkg.tar.zst` (from the release page; pulls in `git`). It is not in the AUR yet. The archive above also works: put `gitunia` in `~/.local/bin`.
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
- Tabs: `gt` / `gT`, `Alt-1`..`Alt-9`, `Ctrl-w c` closes.
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
- **Merge or rebase in progress:** `<space>gc` continue, `ga` abort, `gs` skip.
- **Blame:** `<space>gb` on a file in the details pane (the file is read at the shown commit) or in the Changes view lists (the working tree file). Each line shows the short id (`^` marks a boundary commit), author and age; `Enter` closes the view and selects the line's commit in the graph, `/` searches, `Esc` closes.
- **Undo:** destructive actions ask first and name the undo. The reflog (`<space>l`) restores earlier states.

## Configuration

- **Config:** `config.json` in `~/.config/gitunia`. Set `$XDG_CONFIG_HOME` to override.
- **Session state:** `~/.local/state/gitunia`.

Settings (`<space>,`) edits the same file. The format is in [ux-look.md](internal/ux-look.md).

## Git config is respected

Gitunia uses your Git configuration, hooks, credential helpers and editor, and changes a repository's config only when you ask (upstream, remotes, clone).
