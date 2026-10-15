# Architecture

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

- standalone workspace model. `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos) and each tab owns an independent `RepositoryState` (graph, selection, scroll, status, worktrees). Commands are routed to a tab by id; effects carry the repo path they run against. The app starts without any repo (empty workspace shows the repo picker) and restores the previous session when there is one.
- persistence (open tabs, recents, pins, per-tab view state, config) lives in the user config/state dir, written atomically; a corrupt or missing file must never prevent startup.
- three layers, strictly one-way dependencies: Rendering -> State -> Git. Git knows nothing about state or UI; State knows nothing about the console.
- State is pure and testable without a terminal. Effects (run a git command, open an editor, copy to clipboard) are returned as data and executed by a runner that feeds results back as commands.
- all git calls are async, cancellable and time-bounded; the UI never blocks on them.
- Terminal.Gui v2 is the UI toolkit; the rendering layer wraps it and the state layer never sees it. Custom drawing (commit graph, diff) goes in custom views; modal keys and chords are handled at the application level by our keymap engine. Settings and pitfalls (iteration rate, streaming list source, key handling) and the measured numbers are in `docs/history/architecture.md`. The custom-renderer spikes were rejected on readability and maintenance cost, not speed. A custom renderer may be added later as a separate project, so keep Terminal.Gui types out of Core and keep view models and key events renderer-agnostic (no abstraction interface until a second renderer exists; see `docs/decisions.md`).

## Solution layout

```
src/
  Gitunia.Platform/   the only OS-specific code: config/state dirs, console capability detection
  Gitunia.Git/        process runner, parsers, readers (instance classes with an interface where the Tui consumes them), repo/worktree/branch/graph models; folders by domain: Commits, Diff, Execution (runner, version, exceptions), Refs, Repositories, Status, Stash, Watching, Worktrees; each has a `Models/` subfolder where records and enums are many; shared `ObjectId` and `ParseResult` stay in the root
  Gitunia.Core/       state, commands, effects, keymap, mouse map, config (no console, no process); folders by domain (Keymap and Search with a `Models/` subfolder, Workspaces with Panes, Repositories and Tabs subfolders, ...)
  Gitunia.Tui/        renderer, input, views, app entry point (AOT publish target); folders: Rendering (no Terminal.Gui types; subfolders Details, Diff, Graph, Panes, Screen, Sidebar, Styling, Tabs, Overlays), Views (Terminal.Gui views; subfolders Lists, Theme), Input (key and mouse translation, keymap and mouse controllers; Drag subfolder), Loading (streaming from git; subfolders Commits, Diff, Graph), Search (graph search and jump-to-ref prompts), App (wiring: workspace host, effect runner, action router; subfolders Clipboard, CommandLine, Details, Overlays, Panes, Sidebar, Tabs)
  Gitunia.CustomRenderTui/  parked alternative renderer (cell buffer, VT input, raw terminal); not referenced by the app, not actively maintained
samples/              runnable demos and reference spikes (not referenced by the app, not maintained)
tests/
  Gitunia.Git.Tests/  against real temp repos
  Gitunia.Core.Tests/ pure state/keymap tests
  Gitunia.Tui.Tests/  snapshot tests of rendered frames
  Gitunia.Platform.Tests/
  Gitunia.CustomRenderTui.Tests/  cell buffer diff and input parser tests
tools/
  ConPtyHarness/      Windows pseudoconsole smoke harness (standalone, used by the custom renderer workflow)
scripts/              helper scripts (bash and PowerShell): fixture and big-repo generators, measurement harness
```

Folder rule: domain first, technical kind second (`Models/` for records and enums), namespaces follow folders, and every test project mirrors the folder and file names of the code it tests (`Commits/CommitLogParserTests.cs`); shared test helpers sit in the folder of their domain or in `Fixtures/`. Unused usings fail the build (IDE0005; it needs `GenerateDocumentationFile`, so the missing-doc, partial-param-doc and broken-cref warnings that brings are silenced in `Directory.Build.props`). Small `Models/` folders (Refs, Watching, Execution) stay for consistency.

Dependencies: Tui -> Core -> Git -> Platform (Tui also -> Platform). Build config is in `Directory.Build.props` / `Directory.Packages.props` (central package management); `IL*` (AOT/trim) warnings fail the build through the global `TreatWarningsAsErrors`. Adjust here when the layout changes.

## External tools

`$EDITOR` (via `git commit`), `git difftool` and `git mergetool` need the real terminal, so they never go through `GitRunner` (which redirects every stream). `ExternalToolCommands` (Git) builds the git arguments, `IExternalToolLauncher` (`Gitunia.Platform/ExternalTools`) spawns the program without a shell and without redirection (`UseShellExecute=false`, inherited console on Windows, controlling terminal on Linux/macOS; an OS outside the three gets an explicit "not supported" result) and `ExternalToolRunner` (Tui) orchestrates. Suspending is `ITerminalSuspender`, implemented by `TuiApplication`: it stops the main loop, disposes the Terminal.Gui application (which leaves the alternate screen, restores the terminal and console modes and ends its input thread, so the tool is the only reader of stdin), runs the tool, then creates a fresh `IApplication` (a disposed instance cannot be disposed and re-initialised twice in 2.5.0), re-applies the palette and key handler, and runs the same window again. The new driver reads the terminal size on start, so a resize while suspended is picked up. The tool blocks the UI thread by design (the terminal is given away); background git work continues. Afterwards the tab is refreshed quietly and the exit code reported in the status bar. The credential-prompt issue reuses `ITerminalSuspender`.

## Native AOT

Rules (no reflection, source generators, dependency checks): `.claude/rules/csharp-style.md`. `PublishAot=true` and the trim/AOT analyzers are on from the first commit, `IL*` warnings are errors. CI and every dependency addition run the AOT publish and a smoke test of the produced binary. Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64.

## Composition root

Static versus instance rules: `.claude/rules/csharp-style.md`. `Program.Main` creates the `GitRunner` and calls `GitServices.Create`, which wires every reader and loader by hand and passes the `GitServices` record to the application. The Tui depends on the reader interfaces (`ICommitLogReader`, `IRefReader`, ...), each with a fake in `tests/Gitunia.Tui.Tests/Fakes`. Tests build the same record (`TestGit.Services`) or swap one part with `with` and a fake.

## Cross-platform

- Linux, Windows and macOS are all supported targets; macOS is the lowest priority and may be dropped if it adds significant complexity. Build and test on Linux first, and add Windows/macOS to CI in M4.
- Platform-specific code lives behind a small abstraction in one place (config/state dirs: XDG / `%APPDATA%` / `~/Library`; console setup: raw mode and VT input/output; resize notification: SIGWINCH vs console events; process spawning and signals; file watching quirks). Every abstraction has an implementation or an explicit "unsupported" result for each OS.
- Never hardcode path separators, `/tmp`, `/dev/null` or line endings; handle case-insensitive file systems, long Windows paths and CRLF in diffs.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.

Design history (why things are the way they are, measurements, rejected options): `docs/history/architecture.md`. Not needed for routine work.
