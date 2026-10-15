# Gitunia

A keyboard-first terminal Git client in the spirit of graphical Git clients: a real commit graph, first-class branches and worktrees, vim-style modal navigation, mouse optional.

Status: pre-spike (2026-10-10). No code yet. The first deliverable is the technology spike in `docs/roadmap.md` (M0).

## Why

Terminal Git tools tend to be great for quick staging and commits but awkward for heavier work: managing many branches, worktrees and navigating a large commit graph. Graphical clients get that model right (graph, branches, worktrees, stashes, interactive rebase as first-class views) but need a mouse and a desktop. Gitunia aims for that model with a mouseless terminal UX.

## Goal

A standalone client for all your repositories, not a tool you launch from inside one: start it from anywhere, open several repositories in tabs, and have them back next time. A daily-driver client for developers who live in the terminal and do non-trivial Git work: many branches, several worktrees, rebases, cherry-picks, large repositories.

## MVP scope

In:
- Standalone, multi-repository workspace: launch from any directory (or with `gitunia [path]`), repository tabs, open/recent/pinned repository picker with fuzzy search, session restore (open tabs, selection per tab), per-tab state that survives switching.
- Commit graph (scrollable, searchable, handles 100k+ commits).
- Working tree: status, stage/unstage files, hunks and lines, commit, amend.
- Diff viewer (unified and side-by-side).
- Branches: list local/remote, checkout, create, rename, delete, merge, rebase, upstream tracking, ahead/behind.
- Worktrees: list, add, remove, switch, lock/prune, status per worktree.
- Stash: list, push, apply, pop, drop.
- Fetch, pull, push with progress and cancellation.
- Built-in Insert-mode commit message editor, plus hand-off to the user's tools: `$EDITOR`, `git difftool`/`mergetool` (TUI suspends and resumes); clipboard through OSC 52.
- Vim-style modal keymap, command palette, configurable keys. Mouse support is optional, never required.

Out until after MVP (ask before adding): interactive rebase editor, submodules, LFS UI, blame view, merge-conflict editor, hosting-provider integrations (PRs, issues), themes beyond light/dark, plugins.

## Stack

- C# on .NET 10 (LTS), Native AOT for the published binary. Cross-platform: Linux, Windows, macOS (lowest priority).
- Config and session state live in the user config/state dir (XDG on Linux), never in a repository.
- Git access by running the `git` CLI and parsing plumbing/porcelain output (no libgit2 for writes).
- TUI rendering: decided by the M0 spike (Terminal.Gui v2 vs a thin custom renderer). See `docs/architecture.md`.

## Docs

Start with `CLAUDE.md` (rules and routing table), then `docs/`.
