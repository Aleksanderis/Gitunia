# Gitunia user guide

How to install and use Gitunia. The complete key list is the Keymap table in `docs/ux.md` (the single source of truth); inside the app, `?` shows the keys of the focused view and `:` or `Ctrl-p` opens the command palette, which lists every action with its binding. This guide covers the common paths only.

## Install

Requirements: `git` 2.43 or newer on your `PATH`. Any terminal works; truecolor or 256 colours look best, with a fallback to 16 colours or plain styles (and ASCII when Unicode is missing).

**From a release archive.** Release archives for linux-x64 and win-x64 are attached to the draft releases built on version tags (see `docs/architecture.md`; none are published yet while the project is in development). Unpack the archive and put the `gitunia` binary (`gitunia.exe` on Windows) on your `PATH`. The `.sha256` file next to the archive lets you check the download.

**From source.** Install the .NET SDK version pinned in `global.json` (.NET 10) and the Native AOT build prerequisites (Linux: `clang` and the zlib development headers, e.g. `zlib1g-dev`; Windows: the Visual Studio "Desktop development with C++" workload). Then from the repository root:

```
dotnet publish src/Gitunia.Tui -c Release -o publish
./publish/gitunia
```

The publish produces a single Native AOT binary (`gitunia.exe` on Windows); copy it anywhere on your `PATH`. `dotnet build` and `dotnet test` run the build and the tests.

## Start and open a repository

- `gitunia` restores your last session (open tabs, the selection and focus of each tab). With no session it shows the repository picker.
- `gitunia <path>` opens that path as a tab, from any directory; a subdirectory resolves to its repository root.
- `Ctrl-t` or `<space>o` opens the picker in a new tab: type to fuzzy-filter open, pinned, recent and discovered repositories, or type a path (`~/`, `./`, `/`, a drive). `Enter` opens, `Tab` completes, `Esc` closes. `Alt-c` clones, `Alt-i` runs `git init` in a folder, `Alt-p` pins, `Alt-d` removes a recent entry.
- Switch tabs with `gt` / `gT`, `3gt` or `Alt-1`..`Alt-9`; `Ctrl-w c` closes a tab.

## The screen

History view: sidebar on the left (branches, remotes, tags, stashes, worktrees), commit graph in the centre, detail pane at the bottom (Details, Changes, Files). Move between panes with `Tab` or `Ctrl-w h/j/k/l`; `<space>z` zooms the focused pane. `<space>s` switches to the working-tree view of the tab and back.

## Keys in short

Gitunia is modal and vim-style. Normal mode navigates and runs chords; `<space>` is the leader key.

| Keys | Does |
|---|---|
| `j` `k` `gg` `G` `Ctrl-d` `Ctrl-u` | Move, top, bottom, half page (arrows and PageUp/Down also work; counts like `5j`) |
| `/` `n` `N` | Search, next, previous |
| `?` | Keys of the current view |
| `:` / `Ctrl-p` | Command palette |
| `v` then `y` | Select a range, copy it (OSC 52 clipboard) |
| `Esc` | Cancel a chord, a search or Insert mode |
| `q` | Close an overlay, else quit |

Frequent actions (`<space>` first). Branch, merge, rebase, reset and reflog chords work from the graph and the sidebar, `Sa` `Sp` act on the stash selected in the sidebar, the stage chords in the working-tree view:

| Keys | Does |
|---|---|
| `bs` `bc` | Switch branch, create a branch |
| `F` `L` `P` | Fetch (with prune), pull (fast-forward only), push |
| `s` | Working-tree view |
| `a` `A` `u` `U` | Stage file, stage all, unstage file, unstage all (working-tree lists) |
| `c` | Write the commit message; `Alt-Enter` or `Ctrl-g` commits |
| `Ss` `Sa` `Sp` | Stash, apply, pop |
| `gm` `gr` `gi` | Merge, rebase, interactive rebase onto the selected commit |
| `gp` `gv` | Cherry-pick, revert |
| `gx` | Reset the current branch to the selected commit (soft, mixed, hard) |
| `l` | Reflog |
| `.` | Actions menu of the row under the cursor |
| `,` | Settings |

The actions menu and the palette reach every action without remembering a chord, and the mouse works everywhere (click, double-click, wheel, drag dividers), but nothing requires it.

## Everyday tasks

- **Browse history.** Move in the graph; `Enter` focuses the commit details, `]` and `[` cycle Details, Changes and Files; `<space>h` jumps to HEAD, `<space>f` to a ref or hash, `/` searches (`author:`, `hash:`, `body:` prefixes).
- **Read a diff.** Select a file in Changes and press `Enter` or `l`. `]c` and `[c` jump between hunks; `<space>vs` toggles side-by-side, `<space>vw` word wrap, `<space>vi` ignores whitespace.
- **Commit.** `<space>s` opens the working-tree view. Stage files with `<space>a` (in the diff, a hunk, or in Visual mode selected lines), write the message with `<space>c`, commit with `Alt-Enter`. `Alt-a` toggles amend. `<space>te` commits with `$EDITOR` instead.
- **Branches, tags, worktrees, stashes.** Select the branch, tag, worktree or stash in the sidebar (or a commit in the graph) and use the `<space>b`, `<space>T`, `<space>w` and `<space>S` chords, or open the actions menu with `<space>.`.
- **Rebase interactively.** `<space>gi` opens the todo editor: `p r s f d` set the action, `K` / `J` move the commit, `Enter` starts after a confirmation.
- **Operation in progress.** A merge, rebase, cherry-pick or revert shows a banner; `<space>gc` continues, `<space>ga` aborts, `<space>gk` skips.
- **Undo.** Destructive actions ask for a confirmation that names the undo where one exists (discarding changes cannot be undone). The reflog (`<space>l`) lets you check out or reset to an earlier state of HEAD or a branch.

## Configuration

Config is a hand-editable JSON file with comments, `config.json` in `~/.config/gitunia` (`$XDG_CONFIG_HOME/gitunia` overrides). Session state lives in `~/.local/state/gitunia` (`$XDG_STATE_HOME`). The settings screen (`<space>,`) edits the same file and lists every key and action id. Options (theme, frame style, scan folders, key bindings) and the file format are described in "Config file" in `docs/ux.md`.

Remap a key by adding an entry to `keys`:

```jsonc
{ "keys": [ { "view": "Graph", "key": "J", "action": "list.down" } ] }
```

Gitunia uses your own git configuration, hooks, credential helpers and editor; it changes a repository's config only when you ask (upstream, remotes, clone).

This guide keeps a hand-written subset of the Keymap in `docs/ux.md`; when you rebind a key listed here, update both.

## Not in the MVP

Submodules, LFS, blame, a merge-conflict editor, hosting-provider integration, extra themes and plugins. See "MVP scope" in `README.md`.
