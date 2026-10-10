# Architecture

Status: draft, 2026-10-10. Items marked **Decided** are settled; **Open** items are resolved by the M0 spike.

## Overview

```
Terminal (keys, mouse, resize)
   |  raw input / VT sequences
   v
Rendering layer  <----  View model (what to draw)
   ^                         ^
   | commands                | state
   v                         |
State layer (pure: (State, Command) -> (State, Effects))
   |  effects
   v
Git layer (async, cancellable) ---- spawns `git` ---- repo on disk
```

- **Decided:** standalone workspace model. `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos) and each tab owns an independent `RepoState` (graph, selection, scroll, status, worktrees). Commands are routed to a tab by id; effects carry the repo path they run against. The app starts without any repo (empty workspace shows the repo picker) and restores the previous session when there is one.
- **Decided:** persistence (open tabs, recents, pins, per-tab view state, config) lives in the user config/state dir, written atomically; a corrupt or missing file must never prevent startup.
- **Decided:** three layers, strictly one-way dependencies: Rendering -> State -> Git. Git knows nothing about state or UI; State knows nothing about the console.
- **Decided:** State is pure and testable without a terminal. Effects (run a git command, open an editor, copy to clipboard) are returned as data and executed by a runner that feeds results back as commands.
- **Decided:** all git calls are async, cancellable and time-bounded; the UI never blocks on them.
- **Open:** renderer. Options: (a) Terminal.Gui v2 (full widget toolkit, less control over graph and diff rendering, heavier), (b) thin custom renderer: cell buffer, diff-based flush, VT input parser (~1-2k lines, full control, modal keymap is easy). Leaning to (b); the spike decides.

## Proposed solution layout

```
src/
  Gitunia.Platform/   the only OS-specific code: config/state dirs, console capability detection
  Gitunia.Git/        process runner, parsers, repo/worktree/branch/graph models
  Gitunia.Core/       state, commands, effects, keymap, config (no console, no process)
  Gitunia.Tui/        renderer, input, views, app entry point (AOT publish target)
tests/
  Gitunia.Git.Tests/  against real temp repos
  Gitunia.Core.Tests/ pure state/keymap tests
  Gitunia.Tui.Tests/  snapshot tests of rendered frames
  Gitunia.Platform.Tests/
```

Dependencies: Tui -> Core -> Git -> Platform (Tui also -> Platform). Build config is in `Directory.Build.props` / `Directory.Packages.props` (central package management); the AOT `IL*` warnings-as-errors list is in `Gitunia.Tui.csproj`. Adjust here when the layout changes.

## Native AOT constraints

- No runtime reflection: use source generators (`System.Text.Json` source gen, `LoggerMessage`, etc.). No Newtonsoft, no Castle-style proxies, no DI container needing runtime codegen (plain constructor wiring is enough).
- `PublishAot=true` and trim/AOT analyzers on from the first commit, warnings as errors for `IL*` codes.
- CI (and every dependency addition) runs the AOT publish and a smoke test of the produced binary.
- Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64.

## Performance targets (verify in the spike, then tighten)

- Cold start to first frame: < 100 ms with a warm git cache.
- Commit graph: open a 100k-commit repo and scroll without dropping frames. Load incrementally (`git log` streamed, paged); never materialize the whole graph eagerly in the UI.
- Many tabs: background tabs hold only compact state and are refreshed lazily or on a throttled timer; only the active tab does continuous work. Tab switch is instant (no reload).
- Memory: compact commit model (struct-of-arrays or interned hashes), virtualized lists.
- Render: diff against the previous frame, write one buffered flush per frame.

## Cross-platform

- **Decided:** Linux, Windows and macOS are all supported targets; macOS is the lowest priority and may be dropped if it adds significant complexity. Build and test on Linux first, and add Windows/macOS to CI in M4.
- Platform-specific code lives behind a small abstraction in one place (config/state dirs: XDG / `%APPDATA%` / `~/Library`; console setup: raw mode and VT input/output; resize notification: SIGWINCH vs console events; process spawning and signals; file watching quirks). Every abstraction has an implementation or an explicit "unsupported" result for each OS.
- Never hardcode path separators, `/tmp`, `/dev/null` or line endings; handle case-insensitive file systems, long Windows paths and CRLF in diffs.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.
