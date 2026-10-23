# Gitunia

A keyboard-first terminal Git client in the spirit of graphical Git clients: a real commit graph, first-class branches and worktrees, vim-style modal navigation, mouse optional.

Status: in development. Milestone progress is in `docs/roadmap.md`.

## Why

Terminal Git tools tend to be great for quick staging and commits but awkward for heavier work: managing many branches, worktrees and navigating a large commit graph. Graphical clients get that model right (graph, branches, worktrees, stashes, interactive rebase as first-class views) but need a mouse and a desktop. Gitunia aims for that model with a mouseless terminal UX.

## Goal

A standalone client for all your repositories, not a tool you launch from inside one: start it from anywhere, open several repositories in tabs, and have them back next time. A daily-driver client for developers who live in the terminal and do non-trivial Git work: many branches, several worktrees, rebases, cherry-picks, large repositories.

## MVP scope

In:
- Standalone, multi-repository workspace: launch from any directory (or with `gitunia [path]`), repository tabs, open/recent/pinned repository picker with fuzzy search, session restore (open tabs, selection per tab), per-tab state that survives switching.
- Commit graph (scrollable, searchable, handles 100k+ commits).
- Working tree: status, stage/unstage files, hunks and lines, commit, amend.
- Diff viewer (unified and side-by-side), also between any two commits (mark one, select the other).
- Branches: list local/remote, checkout, create, rename, delete, reset (soft/mixed/hard), merge, rebase, upstream tracking, ahead/behind.
- Tags: create (lightweight and annotated), delete, push and delete on a remote.
- Reflog: browse it and reset or check out an entry (the undo path for every destructive operation).
- Interactive rebase editor: reorder, pick, reword, squash, fixup and drop commits.
- Worktrees: list, add, remove, switch, lock/prune, status per worktree.
- Stash: list, push, apply, pop, drop.
- Fetch (with prune), pull, push with progress and cancellation; force push with lease, set upstream on first push, delete a remote branch; dedicated keys and buttons for fetch and pull on top of automatic refresh.
- Remotes: add, remove, rename, change URL. Clone and init, from the repository picker.
- Built-in Insert-mode commit message editor, plus hand-off to the user's tools: `$EDITOR`, `git difftool`/`mergetool` (TUI suspends and resumes); clipboard through OSC 52.
- Issue links in commit messages from a `.issuetracker` file (VS Code style regex and URL sections), opened in the browser. A link from the message only: no hosting-provider integration, no network access, no API.
- Vim-style modal keymap, command palette, configurable keys. Mouse support is optional, never required.

Out until after MVP (ask before adding): submodules, LFS UI, blame view, free editing in the merge-conflict editor (hunk-wise picks are in), hosting-provider integrations (PRs, issues), further built-in themes (Catppuccin, Tokyo Night and user theme files are in; the rest of the set is tracked as issues), plugins.

## Stack

- C# on .NET 10 (LTS), Native AOT for the published binary. Cross-platform: Linux, Windows, macOS (lowest priority).
- Config lives in `~/.config/gitunia` and session state in `~/.local/state/gitunia` on every OS (`XDG_CONFIG_HOME` / `XDG_STATE_HOME` override), never in a repository. Config is meant to be hand-edited and kept in a dotfiles repository.
- Git access by running the `git` CLI and parsing plumbing/porcelain output (no libgit2 for writes).
- TUI rendering: Terminal.Gui v2. See `docs/architecture.md`.

## Install and first run

Needs `git` 2.43 or newer on your `PATH`.

- Release archives (linux-x64, win-x64) are built on version tags; unpack and put `gitunia` on your `PATH`. None are published yet.
- From source: install the .NET SDK pinned in `global.json`, then the Native AOT prerequisites (Linux: `clang`, `zlib1g-dev`; Windows: the "Desktop development with C++" workload), then `dotnet publish src/Gitunia.Tui -c Release -o publish` and run `publish/gitunia`. Details: `docs/user-guide.md`.

Then:

```
gitunia                 # restore the last session, or show the repository picker
gitunia ~/Projects/foo  # open a repository (any subfolder resolves to its root)
```

In the picker type to filter repositories or type a path, `Enter` opens it in a tab. Press `?` for the keys of the current view, `:` for the command palette, `q` to quit. The user guide is `docs/user-guide.md`; every key is in the settings screen's Key reference (`<space>,`) and the Keymap in `docs/ux.md`.

## Docs

Start with `CLAUDE.md` (rules and routing table), then `docs/`.
