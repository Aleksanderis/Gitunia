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

## The four projects

Each project is a layer. It is defined by what it may know about.

| Project | Job | May depend on | Must not touch |
|---|---|---|---|
| `Gitunia.Platform` | OS and environment code only: config/state directory locations, console capability detection, launching external programs (`$EDITOR`, difftool), byte-exact file deletion. It knows nothing about git or the app. | the OS | git, state, UI |
| `Gitunia.Git` | Runs the `git` CLI and turns its output into typed results: process runner, parsers, readers (commits, refs, status, diff, stash, worktrees), the graph model. | Platform | state, UI |
| `Gitunia.Core` | The app's logic: `AppState`, commands, effects, keymap, mouse map, config, session persistence. Pure `(State, Command) -> (State, Effects)`; runs and tests without a terminal. | Git (its types) | the console, Terminal.Gui, spawning processes |
| `Gitunia.Tui` | What you see and touch: Terminal.Gui views, rendering, key and mouse translation, the effect runner, and `Program.Main` (the AOT binary). | everything | nothing is above it |

Dependencies run one way: **Tui -> Core -> Git -> Platform** (Tui also uses Platform directly). The compiler enforces it; for example Core cannot reference Terminal.Gui.

Platform and Core are not the same thing: Platform is the bottom layer ("how do I do this on this OS?") and knows nothing about Gitunia; Core is the middle layer ("what happens when the user presses `j`?") and knows nothing about the OS.

Example, Enter on a commit: Tui turns the key into a key event; Core's keymap makes it a command and the reducer returns a new state plus an effect ("load the diff for repo X, commit Y"); Tui's effect runner executes it through a Git reader; Git spawns `git show` and returns a parsed diff; the result goes back to Core as a command, the state updates and Tui redraws.

## Principles

- **Workspace model.** `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos). Each tab owns an independent `RepositoryState` (graph, selection, scroll, status, worktrees). Commands are routed to a tab by id; effects carry the repo path they run against. The app starts with no repo (the picker shows) and restores the previous session when there is one.
- **Persistence.** Open tabs, recents, pins, per-tab view state and config live in the user config/state dir and are written atomically. A corrupt or missing file never prevents startup.
- **Pure state.** Effects (run a git command, open an editor, copy to clipboard) are returned as data; a runner executes them and feeds the results back as commands.
- **Async git.** Every git call is async, cancellable and time-bounded; the UI never blocks on one.
- **UI toolkit.** Terminal.Gui v2, wrapped by the rendering layer; Core never sees it. Custom drawing (graph, diff) uses custom views, and modal keys and chords are handled at the application level by our keymap engine. Keep Terminal.Gui types out of Core and keep view models and key events renderer-agnostic (no abstraction interface until a second renderer exists; see `docs/decisions.md`). Settings, pitfalls and measured numbers: `docs/history/architecture.md`.

## Solution layout

```
src/
  Gitunia.Platform/   OS-specific code
  Gitunia.Git/        git process runner, parsers, readers, models
  Gitunia.Core/       state, commands, effects, keymap, config (no console, no process)
  Gitunia.Tui/        renderer, input, views, app wiring, entry point (AOT publish target)
tests/                one test project per src project, same names plus `.Tests`
tools/AnsiToSvg/      tmux capture of the live UI to SVG for PR screenshots (local only, never in CI)
scripts/              bash and PowerShell helpers: big-repo generator, measurement harness, inspectcode wrapper, screenshots
```

- Folders are domain first, technical kind second (`Models/` for records and enums); namespaces follow folders. Look at the folders themselves for the current domain list.
- Each test project mirrors the folder and file names of the code it tests (`Commits/CommitLogParserTests.cs`). Test types: Git tests run against real temp repos, Core tests are pure state/keymap tests, Tui tests are snapshot tests of rendered frames. Shared helpers sit in the folder of their domain or in `Fixtures/`.
- Unused usings fail the build (IDE0005, which needs `GenerateDocumentationFile`; the missing-doc, partial-param-doc and broken-cref warnings that brings are silenced in `Directory.Build.props`).
- Build config is in `Directory.Build.props` and `Directory.Packages.props` (central package management); `IL*` (AOT/trim) warnings fail the build through the global `TreatWarningsAsErrors`.

## External tools

`$EDITOR` (via `git commit`), `git difftool` and `git mergetool` need the real terminal, so they never go through `GitRunner` (which redirects every stream).

- `ExternalToolCommands` (Git) builds the git arguments; no env vars are set, the user's config decides editor and tools.
- `IExternalToolLauncher` (`Gitunia.Platform/ExternalTools`) spawns the program without a shell and without redirection (inherited console on Windows, controlling terminal on Linux/macOS; any other OS gets an explicit "not supported" result).
- `IByteExactFileSystem` (`Gitunia.Platform/FileSystem`) deletes a file or an empty directory by raw path bytes for names that are not valid UTF-8: libc `unlink`/`rmdir` on Linux, an explicit "unsupported" result on every other OS.
- `ExternalToolRunner` (Tui) orchestrates: suspend, run, pause for Enter on a non-zero exit (git's own message stays readable), resume, report the exit code in the status bar, refresh the tab quietly.
- `ITerminalSuspender` is implemented by `TuiApplication`: it stops the main loop, disposes the Terminal.Gui application (leaves the alternate screen, restores terminal and console modes, ends the input thread so the tool is the only stdin reader), runs the tool, then creates a fresh `IApplication` (2.5.0 cannot dispose and re-initialise the same instance twice), re-applies the palette and key handler and runs the same window again. The new driver reads the terminal size, so a resize while suspended is handled.
- Background producers (watchers, pollers, timers) post through `SuspendableUiInvoker`: while suspended their work is queued and replayed, in order, on the new application. The tool blocks the UI thread by design; background git work continues.
- The credential-prompt issue reuses `ITerminalSuspender`.

## Native AOT

Rules (no reflection, source generators, dependency checks): `.claude/rules/csharp-style.md`. `PublishAot=true` and the trim/AOT analyzers are on from the first commit, `IL*` warnings are errors. Every dependency addition and every dispatched CI run the AOT publish and a smoke test of the produced binary. Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64.

## Composition root

Static versus instance rules: `.claude/rules/csharp-style.md`. `Program.Main` creates the `GitRunner` and calls `GitServices.Create`, which wires every reader and loader by hand and passes the `GitServices` record to the application. The Tui depends on the reader interfaces (`ICommitLogReader`, `IRefReader`, ...), each with a fake in `tests/Gitunia.Tui.Tests/Fakes`. Tests build the same record (`TestGit.Services`) or swap one part with `with` and a fake.

## Cross-platform

- Linux, Windows and macOS are all supported targets; macOS is the lowest priority and may be dropped if it adds significant complexity. Build and test on Linux first; CI has a Windows leg (tags, on demand), macOS comes in M5.
- Platform-specific code lives behind a small abstraction in one place (console setup: raw mode and VT input/output; resize notification: SIGWINCH vs console events; process spawning and signals; file watching quirks). Every abstraction has an implementation or an explicit "unsupported" result for each OS.
- Config and state locations follow terminal-app convention on every OS, not the OS-native dirs: config in `$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, state in `$XDG_STATE_HOME/gitunia` or `~/.local/state/gitunia`. So the config folder can live in a dotfiles repository, and config stays apart from machine-local state.
- Never hardcode path separators, `/tmp`, `/dev/null` or line endings; handle case-insensitive file systems, long Windows paths and CRLF in diffs.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.

Design history (why things are the way they are, measurements, rejected options): `docs/history/architecture.md`. Not needed for routine work.
