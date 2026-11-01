<p align="center"><img src="docs/assets/logo.svg" width="96" alt="Gitunia logo"></p>

<h1 align="center">Gitunia</h1>

A keyboard-first terminal Git client: a real commit graph, first-class branches and worktrees, vim-style modal navigation, the mouse is optional but fully functional.

![Gitunia demo: graph, branch switch, worktree, staging lines, commit](docs/media/demo.gif)

## Why

Terminal Git tools are great for quick staging and commits but awkward for many branches, worktrees and large graphs. Graphical clients get that model right but need a mouse and a desktop. Gitunia brings that model to the terminal.

## Features

- Standalone multi-repository client: tabs, repository picker, session restore
- Commit graph that scrolls and searches through 100k+ commits
- Stage files, hunks and single lines; then commit or amend
- Built-in conflict editor with base, ours and theirs side by side
- Unified and side-by-side diffs, also between any two commits
- Branches, tags, merge, rebase, reset, cherry-pick and revert
- Interactive rebase editor
- Worktrees and stashes as first-class views
- Fetch, pull and push with progress, clone, init and remotes
- Reflog browser as the undo path for destructive actions
- Command palette and configurable vim-style keys

Not included: submodules, LFS UI, blame, hosting-provider integration, plugins.

## Install

Needs `git` 2.43 or newer on your `PATH`.

- Release archives (linux-x64, win-x64) and Linux packages are attached to the releases: unpack and put `gitunia` on your `PATH`.
- From source: install the .NET SDK pinned in `global.json` and the Native AOT prerequisites, then `dotnet publish src/Gitunia.Tui -c Release -o publish`. Details: [user guide](docs/user-guide.md).

## Quick start

```
gitunia                 # restore the last session, or show the repository picker
gitunia ~/Projects/foo  # open a repository (any subfolder resolves to its root)
```

In the picker type to filter or type a path; `Enter` opens it in a tab. `?` shows the keys of the current view, `:` opens the command palette, `q` quits.

## Docs

- [User guide](docs/user-guide.md) and [customising keys](docs/customising-keys.md)
- [Key reference](docs/keymap.md)
- Design documents are in `docs/`
- Project conventions are in [CLAUDE.md](CLAUDE.md)

## Contributing, security, license

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Licensed under the MIT License ([LICENSE](LICENSE)). This covers the logo too.
