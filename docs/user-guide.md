# Gitunia user guide

How to install and use Gitunia. This guide covers the common paths only; for keys see "Keys" below.

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

## Keys

Gitunia is modal and vim-style; `<space>` is the leader key. `?` shows the keys of the focused view, `:` or `Ctrl-p` opens the command palette, `<space>,` opens the settings screen whose Key reference category lists every binding (rebinds included), `q` closes an overlay or quits. Once you know the keys, the Keys category of the settings turns the which-key popup and the status bar hints down (`hints.whichKey`, `hints.statusBar`); `?` and `:` always work. The complete table is the Keymap section of `docs/ux-keymap.md`.

## Everyday tasks

- **Browse history.** Move in the graph; `Enter` focuses the commit details, `]` and `[` cycle Details, Changes and Files; `<space>h` jumps to HEAD, `<space>f` to a ref or hash, `/` searches (`author:`, `hash:`, `body:` prefixes).
- **Read a diff.** Select a file in Changes and press `Enter` or `l`. `]c` and `[c` jump between hunks; `<space>vs` toggles side-by-side, `<space>vw` word wrap, `<space>vi` ignores whitespace, `<space>vc` shows whitespace characters.
- **Commit.** `<space>s` opens the working-tree view. Stage files with `<space>a` (in the diff, a hunk, or in Visual mode selected lines), write the message with `<space>c`, commit with `Ctrl-g`. `Alt-a` toggles amend. `<space>te` commits with `$EDITOR` instead.
- **Conflicts.** `Enter` on a `!` file in the working-tree view opens the full-screen conflict editor: the whole file as base, ours and theirs side by side (KDiff3 order) with the result below, `Tab` moves between the panes, `j`/`k` move the cursor and `n`/`N` jump between the conflicts, `<space>a` takes the pane's version for the current conflict (`o`, `t`, `b` and `B` take ours, theirs, the base or both) and `<space>u` untakes it again, `v` then `<space>a` (or `<space>u`) takes (or removes) only the selected lines, a click puts the cursor on a pane or row and a double-click on a conflict takes that version, the result is an editor of its own (`e` or `i` types, `Esc` stops), `u`/`Ctrl-r` undo and redo, `R` re-creates the conflict, `Enter` saves and offers to mark the file resolved. Files it cannot take (delete/modify, binary) go to the mergetool (`<space>tm`).
- **Branches, tags, worktrees, stashes.** Select the branch, tag, worktree or stash in the sidebar (or a commit in the graph) and use the `<space>b`, `<space>T`, `<space>w` and `<space>S` chords, or open the actions menu with `<space>.`.
- **Rebase interactively.** `<space>gi` opens the todo editor: `p r s f d` set the action, `K` / `J` move the commit, `Enter` starts after a confirmation.
- **Operation in progress.** A merge, rebase, cherry-pick or revert shows a banner; `<space>gc` continues, `<space>ga` aborts, `<space>gk` skips.
- **Undo.** Destructive actions ask for a confirmation that names the undo where one exists (discarding changes cannot be undone). The reflog (`<space>l`) lets you check out or reset to an earlier state of HEAD or a branch.

## Configuration

Config is a hand-editable JSON file with comments, `config.json` in `~/.config/gitunia` (`$XDG_CONFIG_HOME/gitunia` overrides). Session state lives in `~/.local/state/gitunia` (`$XDG_STATE_HOME`). The settings screen (`<space>,`) edits the same file and lists every key and action id. Options (theme, scan folders, key bindings) and the file format are described in "Config file" in `docs/ux-look.md`.

Remap a key by adding an entry to `keys`:

```jsonc
{ "keys": [ { "view": "Graph", "key": "J", "action": "list.down" } ] }
```

Gitunia uses your own git configuration, hooks, credential helpers and editor; it changes a repository's config only when you ask (upstream, remotes, clone).

## Not in the MVP

Submodules, LFS, blame, hosting-provider integration, extra themes and plugins. See "MVP scope" in `README.md`.
