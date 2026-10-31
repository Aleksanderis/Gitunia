# Gitunia user guide

How to install and use Gitunia. This guide covers the common paths only; for keys see "Keys" below.

## Install

Requirements: `git` 2.43 or newer on your `PATH`. Any terminal works; truecolor or 256 colours look best, with a fallback to 16 colours or plain styles (and ASCII when Unicode is missing).

**From a release archive.** Release archives for linux-x64 and win-x64 are attached to the draft releases built on version tags (see `docs/architecture.md`; none are published yet while the project is in development). Unpack the archive and put the `gitunia` binary (`gitunia.exe` on Windows) on your `PATH`. The `.sha256` file next to the archive lets you check the download. `gitunia --version` prints the release it was built from (`0.0.0-dev` for a local build).

**From source.** Install the .NET SDK version pinned in `global.json` (.NET 10) and the Native AOT build prerequisites (Linux: `clang` and the zlib development headers, e.g. `zlib1g-dev`; Windows: the Visual Studio "Desktop development with C++" workload). Then from the repository root:

```
dotnet publish src/Gitunia.Tui -c Release -o publish
./publish/gitunia
```

The publish produces a single Native AOT binary (`gitunia.exe` on Windows); copy it anywhere on your `PATH`. `dotnet build` and `dotnet test` run the build and the tests.

## Start and open a repository

- `gitunia` restores your last session (open tabs, the selection and focus of each tab). With no session it shows the repository picker.
- `gitunia <path>` opens that path as a tab, from any directory; a subdirectory resolves to its repository root.
- `Ctrl-t` or `<space>oo` opens the picker in a new tab: type to fuzzy-filter open, pinned, recent and discovered repositories, or type a path (`~/`, `./`, `/`, a drive). `Enter` opens, `Tab` completes, `Esc` closes. `Alt-c` clones, `Alt-i` runs `git init` in a folder, `Alt-p` pins, `Alt-d` removes a recent entry.
- Switch tabs with `gt` / `gT`, `3gt` or `Alt-1`..`Alt-9`; `Ctrl-w c` closes a tab.

## The screen

History view: sidebar on the left (branches, remotes, tags, stashes, worktrees), commit graph in the centre, detail pane at the bottom (Details, Changes, Files). Move between panes with `Tab` or `Ctrl-w h/j/k/l`; `<space>z` zooms the focused pane. `<space>s` switches to the working-tree view of the tab and back.

## Keys

Gitunia is modal and vim-style; `<space>` is the leader key. `?` shows the keys of the focused view, `:` or `Ctrl-p` opens the command palette, `<space>,` opens the settings screen whose Key reference category lists every binding (rebinds included), `q` closes an overlay or quits. Once you know the keys, the Keys category of the settings turns the which-key popup and the status bar hints down (`hints.whichKey`, `hints.statusBar`); `?` and `:` always work. The command level (`commandLevel`: `everyday` by default, `advanced`, `advanced+`; Settings, Keys, or `<space>*` to cycle) hides the less frequent commands from help, the palette, the which-key popup, the actions menus and the status bar hints; hidden commands keep their keys and still run, and the end of the help says how many are hidden. The complete table is `docs/keymap.md`. To change keys, see "Customise keys" below.

## Everyday tasks

- **Browse history.** Move in the graph; `Enter` focuses the commit details, `]` and `[` cycle Details, Changes and Files; `<space>h` jumps to HEAD, `<space>J` to a ref or hash, `/` searches (`author:`, `hash:`, `body:` prefixes).
- **Read a diff.** Select a file in Changes and press `Enter` or `l`. `]c` and `[c` jump between hunks; `<space>vs` toggles side-by-side, `<space>vw` word wrap, `<space>vi` ignores whitespace, `<space>vc` shows whitespace characters.
- **Commit.** `<space>s` opens the working-tree view. Stage files with `<space>a` (in the diff, a hunk, or in Visual mode selected lines), write the message with `<space>c`, commit with `Ctrl-g`. `Alt-a` toggles amend. `<space>ec` commits with `$EDITOR` instead.
- **Conflicts.** `Enter` on a `!` file in the working-tree view opens the full-screen conflict editor: the whole file as base, ours and theirs side by side (KDiff3 order) with the result below, `Tab` moves between the panes, `j`/`k` move the cursor and `n`/`N` jump between the conflicts, `<space>a` takes the pane's version for the current conflict (`o`, `t`, `b` and `m` take ours, theirs, the base or both) and `<space>u` untakes it again, `v` then `<space>a` (or `<space>u`) takes (or removes) only the selected lines, a click puts the cursor on a pane or row and a double-click on a conflict takes that version, the result is an editor of its own (`e` or `i` types, `Esc` stops), `u`/`Ctrl-r` undo and redo, `Ctrl-x` re-creates the conflict, `Ctrl-s` saves and stages the file when no conflict is left (the status bar names how many files are still conflicted; `<space>gc` continues once none is). Files it cannot take (delete/modify, binary) go to the mergetool (`<space>em`).
- **Branches, tags, worktrees, stashes.** Select the branch, tag, worktree or stash in the sidebar (or a commit in the graph) and use the `<space>b`, `<space>t`, `<space>w` and `<space>k` chords, or open the actions menu with `<space>.`.
- **Rebase interactively.** `<space>bri` opens the todo editor: `p r s f d` set the action, `K` / `J` move the commit, `Enter` starts after a confirmation.
- **Operation in progress.** A merge, rebase, cherry-pick or revert shows a banner; `<space>gc` continues, `<space>ga` aborts, `<space>gs` skips.
- **Undo.** Destructive actions ask for a confirmation that names the undo where one exists (discarding changes cannot be undone). The reflog (`<space>l`) lets you check out or reset to an earlier state of HEAD or a branch.

## Configuration

Config is a hand-editable JSON file with comments, `config.json` in `~/.config/gitunia` (`$XDG_CONFIG_HOME/gitunia` overrides). Session state lives in `~/.local/state/gitunia` (`$XDG_STATE_HOME`). The settings screen (`<space>,`) edits the same file and lists every key and action id. Options (theme, scan folders, key bindings) and the file format are described in "Config file" in `docs/ux-look.md`.

**Key layout change (#840).** The leader chords were regrouped by noun: `<space>b` branches (merge `bm`, cherry-pick `bp`, revert `bv`, reset `bx`, rebase under `<space>br`), `t` tags, `r` remotes (`rf` fetch, `rl` pull, `rp` push; since #918 also `<space>f`, `<space>p`, `<space>P`), `k` stash, `w` worktrees, `e` external tools, `g` an operation in progress (`gc` continue, `ga` abort, `gs` skip, `gr` resolve conflicts), `o` repositories and tabs (`oo` open, `or` refresh). In the conflict editor `B` (take both) is now `m` and `R` (re-create) is `Ctrl-x`. Your `keys` entries keep working because they bind a key to an action id, which did not change. Two cases are reported by the config check, never fatal: an entry that binds a key of your choice to a prefix the defaults now use (for example a leaf `<space>r` or `<space>o`, which are now groups that wait for a second key; the message names both keys), and an entry `none` on an old default key that no longer exists ("nothing to unbind"). `docs/ux-keymap.md` lists every old and new key.

Gitunia uses your own git configuration, hooks, credential helpers and editor; it changes a repository's config only when you ask (upstream, remotes, clone).

## Customise keys

Key bindings are data: you change them with `keys` entries in `config.json`. The defaults are in the [keymap reference](keymap.md); the file format is the reference in "Config file" in [ux-look.md](ux-look.md).

**Where the file is.** `~/.config/gitunia/config.json` (`$XDG_CONFIG_HOME/gitunia/config.json` when that variable is set). The settings screen (`<space>,`, category Config file, row "Open config.json in $EDITOR") opens it in your editor. The file is JSON with comments and trailing commas; create it with `{}` if it does not exist.

**An entry.** `keys` is a list of objects with these fields:

- `action`: the action id, or `none` to unbind.
- `key`: the key or chord to press.
- `view`: where it applies (default `Global`). Names are the headings of the [keymap reference](keymap.md): `Global`, `Lists`, `Graph`, `Sidebar`, `Detail`, `Diff` and so on.
- `mode`: `Normal` (default), `Visual`, `Insert`, `Command` or `Confirm`. One entry binds one mode and one view; to bind a key in Normal and Visual mode, write two entries.

**Bind, replace, unbind.**

```jsonc
{
  "keys": [
    // Bind a free key: J moves down in the commit graph.
    { "view": "Graph", "key": "J", "action": "list.down" },
    // Bind a leader chord: <space> then H jumps to HEAD (the default is <space>h).
    { "view": "Graph", "key": "<space>H", "action": "graph.jump-to-head" },
    // Replace what a key does: q opens the command palette instead of quitting (Normal mode only;
    // q no longer closes overlays then, use Esc).
    { "key": "q", "action": "palette.open" },
    // Unbind: gg no longer goes to the top of lists (it also stops in the graph, which inherits Lists).
    { "view": "Lists", "key": "gg", "action": "none" }
  ]
}
```

An entry on a key that already has a binding in that mode and view replaces it. `none` removes a binding. Moving an action to another key means two entries: bind the new key, then unbind the old one.

**Key notation.** The same as in the keymap reference: single keys (`j`, `J`, `Enter`, `Esc`, `Tab`, `Down`), modifiers (`Ctrl-d`, `Alt-1`, `Shift-Tab`), chords written one key after another (`gg`, `Ctrl-w c`), `<space>` for the leader (`<space>bc`) and `<lt>` for a literal `<` (a plain `<` also works). Letters are case sensitive: `J` is Shift-j.

**Find the action id.** Open the settings (`<space>,`), category Keys, row Key reference: it lists every action per view with its keys, `/` filters by title, key or id, and `Enter` on a line shows its `keys` entry in the status bar, ready to copy (it writes nothing). The command palette (`:` or `Ctrl-p`) lists the actions by title, and the reference tables list the id of every binding. Insert, Command and Confirm keys are not in the Key reference; take their ids from the palette or the reference tables. Actions without a default key (`Actions without a key` in the reference) can be bound the same way.

**How views inherit.** A binding in a view also works in the views below it: `Global` applies everywhere, `Lists` applies to the graph, sidebar, detail panes, diffs, the help and the settings, and so on. A key set in a more specific view wins over the same key in its parent. Unbinding a key in a view uncovers the parent's binding of that key, if any. To change a key everywhere, bind it in `Global` or `Lists`, and bind it again in a more specific view that has its own binding for it.

**Conflicts.** A key cannot be both a complete binding and the start of a longer chord, because Gitunia waits for the rest of a chord without a timeout. Binding `g` while `gg` and `gt` exist is refused, and so is a leaf `<space>r` because `<space>rf` (fetch) exists. The message names both keys; unbind the longer ones first (earlier entry) if you want the short key. Two entries for the same mode, view and key: the first applies and the second is reported. `Ctrl-c` always quits and cannot be remapped.

**Restart.** The keymap is read once at startup. Edit `config.json`, then restart Gitunia; the Key reference shows the keymap that is running.

**Bad entries.** Each entry is checked alone. One with an unknown action, view or mode, a key that does not parse, nothing to unbind (`none` on a key that has no binding) or a chord conflict is skipped, and the others still apply. The first problem appears in the status bar at startup with a count of the others, and `<space>m` keeps the first five messages. Each message starts with the entry (`keys[2]` is the third). A file that is not valid JSON is ignored as a whole and the defaults apply.

**See the result.** `?` shows the keys of the focused view; its `Custom` section lists your own bindings (shown once at least one entry applied).

## Regenerating the README demo

The animation in `README.md` (`docs/media/demo.gif`) is a scripted recording of the real binary on a generated throwaway repository, never a real one. On Linux with `vhs`, `ttyd` and `ffmpeg` installed, run `scripts/demo.sh` (it publishes the AOT binary first; pass `--binary <gitunia>` to reuse one, `--out <dir>` to write elsewhere). It writes `demo.gif` (README, about 1 MB) and `demo.webm` (landing page, not committed) into `docs/media/`. The key script is `scripts/demo.tape`: edit its `Type` and `Sleep` lines when the UI or keys change, then rerun and commit the new GIF (keep it under about 3 MB). It is separate from the screenshots flow of the `screenshots` skill and never runs in CI.

## Not in the MVP

Submodules, LFS, blame, hosting-provider integration, extra themes and plugins. See "MVP scope" in `README.md`.
