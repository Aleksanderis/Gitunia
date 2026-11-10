<p align="center"><img src="docs/assets/logo.svg" width="96" alt="Gitunia logo"></p>

<h1 align="center">Gitunia</h1>

<p align="center">
<a href="https://github.com/Aleksanderis/Gitunia/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Aleksanderis/Gitunia/ci.yml?branch=main&logo=github&label=CI" alt="CI status"></a>
<a href="https://github.com/Aleksanderis/Gitunia/releases/latest"><img src="https://img.shields.io/github/v/release/Aleksanderis/Gitunia?label=release" alt="Latest release"></a>
<a href="LICENSE"><img src="https://img.shields.io/github/license/Aleksanderis/Gitunia" alt="MIT license"></a>
<img src="https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-informational" alt="Platforms: Linux, Windows, macOS">
<img src="https://img.shields.io/badge/.NET-10%20Native%20AOT-512BD4?logo=dotnet" alt=".NET 10 Native AOT">
</p>

A keyboard-first terminal Git client: a real commit graph, first-class branches and worktrees, vim-style modal navigation, the mouse is optional but fully functional.

[![Gitunia demo: scrolling a large graph, branch switch, worktree, staging lines, commit](https://gitunia.github.io/media/demo.gif)](https://gitunia.github.io)

## Why

Terminal Git tools are great for quick staging and commits but awkward for many branches, worktrees and large graphs. Graphical clients get that model right but need a mouse and a desktop. Gitunia brings that model to the terminal.

## Features

- Commit graph that scrolls and searches through 100k+ commits
- Stage files, hunks and single lines, then commit or amend
- Branches, merge, rebase, reset, cherry-pick and revert
- Fetch, pull and push with progress
- Unified and side-by-side diffs, also between any two commits
- Standalone multi-repository client: tabs, repository picker, session restore
- Stashes as a first-class view
- 3-way merge conflict resolution
- Worktrees as a first-class view
- Tags, clone, init and remotes
- Interactive rebase editor
- Reflog browser as the undo path for destructive actions
- Blame view: who last changed each line of a file, at a commit or in the working tree
- `gitunia update`: updates itself from the latest release (checksum-verified, no PowerShell on Windows) or prints the package manager's command; `gitunia update --check` is for scripts; the status bar tells when a newer release exists (checked once a day, off with one setting or `GITUNIA_NO_UPDATE_CHECK`)
- Command palette, configurable vim-style keys and full mouse support
- Select and copy exactly the text you want from a diff, by character or by line, with keys or the mouse (copies through the terminal clipboard, also over ssh)

Not included: submodules, LFS UI, hosting-provider integration, plugins.

## Install

Needs Git 2.43 or newer on your `PATH`.

One line, installs the latest release (re-run it to update).

Linux (x64, arm64):

```sh
curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh
```

Windows (PowerShell):

```powershell
irm https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.ps1 | iex
```

Other ways to install: Linux `.deb`/`.rpm`, Arch/Omarchy `.pkg.tar.zst` (x64 and arm64) (the AUR package `gitunia-bin` is planned, not published yet), Windows `winget install Gitunia.Gitunia` (once its winget-pkgs submission is approved), Homebrew `brew install aleksanderis/tap/gitunia` (macOS and Linux), a Windows or Linux archive (x64 and arm64), a macOS archive (Apple silicon; should work but is not fully tested yet, and unsigned, so Gatekeeper asks you to allow it), or from source. For more details look in the [user guide](docs/user-guide.md#install).

## Quick start

```
gitunia                 # restore the last session, or show the repository picker
gitunia ~/Projects/foo  # open a repository (any subfolder resolves to its root)
```

In the picker type to filter or type a path. `Enter` opens it in a tab. `?` shows the keys of the current view, `:` opens the command palette, `q` quits.

## Docs

- [Customising keys](docs/customising-keys.md) and [key reference](docs/keymap.md)
- Design documents are in `docs/` (maintainer notes in `docs/internal/`)
- Project conventions are in [CLAUDE.md](CLAUDE.md)

## Contributing, security, license

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Licensed under the MIT License ([LICENSE](LICENSE)). This covers the logo too.

## Support

If Gitunia saves you time, you can buy me a coffee:

[![Buy me a coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black)](https://buymeacoffee.com/aleksanderis)
