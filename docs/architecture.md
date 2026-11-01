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

- **Workspace model.** `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos). Each tab owns an independent `RepositoryState` (graph, selection, scroll, status, worktrees). Commands are routed to a tab by id; effects carry the repo path they run against. Only the 5 most recently used tabs keep a loaded graph (about 350 bytes per commit); colder tabs are evicted and reload on activation (`EffectRunner`, docs/internal/performance.md). The app starts with no repo (the picker shows) and restores the previous session when there is one.
- **Persistence.** Open tabs, recents, pins, per-tab view state and config live in the user config/state dir and are written atomically. A corrupt or missing file never prevents startup.
- **Pure state.** Effects (run a git command, open an editor, copy to clipboard) are returned as data; a runner executes them and feeds the results back as commands.
- **Async git.** Every git call is async, cancellable and time-bounded; the UI never blocks on one.
- **Shared prompt components.** Prompts are data, not views: `PromptPopup` (Core) is drawn by the one `PromptPopupFrameBuilder`; every type-to-filter prompt keeps its candidates in a `FilterPicker<T>` (#766). A new picker is a new call into it, never a new copy. Behaviour: `docs/internal/ux.md`, "Prompt popups".
- **Shared key hints and chips.** A "key plus what it does" is drawn by one of two builders in `Rendering/Styling`: `HintBar` for a row of hints and `KeyedChip` for a clickable chip (`ChipStyle` picks the roles, the key comes from the keymap through `ChipKeys`). Footers pass explicit `HintItem` lists to `OverlayBox.Hints`; keys are never guessed from text. Prompt footers are a renderer-agnostic `PromptHint` list in Core (`PromptPopup.Hints`) mapped to `HintItem` by `PromptPopupFrameBuilder`. A new hint row or button uses these; rules: `.claude/rules/ui-changes.md`.
- **UI toolkit.** Terminal.Gui v2, wrapped by the rendering layer; Core never sees it. Custom drawing (graph, diff) uses custom views, and modal keys and chords are handled at the application level by our keymap engine. Keep Terminal.Gui types out of Core and keep view models and key events renderer-agnostic (no abstraction interface until a second renderer exists; see `docs/internal/decisions.md`). Settings, pitfalls and measured numbers: `docs/internal/history/architecture.md` (cell widths: `DisplayWidth` follows Terminal.Gui per character, ZWJ sequences differ).

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
- `IExternalToolLauncher` (`Gitunia.Platform/ExternalTools`) spawns the program without a shell and without redirection (inherited console on Windows, controlling terminal on Linux/macOS; any other OS gets an explicit "not supported" result). `IUrlOpener` (`Gitunia.Platform/Urls`) opens an http or https address in the default browser without waiting (`xdg-open`, `open`, the shell on Windows, "unsupported" elsewhere); the caller copies the address through OSC 52 when it fails. `IByteExactFileSystem` deletes a file or empty directory by raw path bytes for non-UTF-8 names (libc `unlink`/`rmdir` on Linux, "unsupported" elsewhere).
- `ExternalToolRunner` (Tui) orchestrates: suspend, run, pause for Enter on a non-zero exit (git's own message stays readable), resume, report the exit code in the status bar, refresh the tab quietly.
- `ITerminalSuspender` is implemented by `TuiApplication`: it stops the main loop, disposes the Terminal.Gui application (alternate screen, terminal and console modes restored, input thread ended so the tool is the only stdin reader), runs the tool, then creates a fresh `IApplication` (2.5.0 cannot re-initialise one instance) and runs the same window again; a resize while suspended is handled.
- `ITerminalModes` (`Gitunia.Platform/Terminal`) remembers the terminal modes before the UI driver starts (termios on Linux and macOS, console modes on Windows). `TerminalHandOff` puts them back right before the tool starts and after it ends. The capture is the first thing `Run` does, before the light-background query (#743).
- Background producers (watchers, pollers, timers) post through `SuspendableUiInvoker`: while suspended their work is queued and replayed, in order, on the new application. The tool blocks the UI thread by design; background git work continues.
- The credential retry (#45) reuses `ITerminalSuspender` through `ExternalToolRunner.LaunchGit`, which runs any git argument list (`-C <repo>` prefixed) with the real terminal, with no environment set: the user's ssh, askpass and credential helper decide how to prompt.

## Files tab (commit tree)

`CommitTreeStore` (Tui, one per tab, like `CommitDetailsStore`) reads `ls-tree -r -z --long` off the UI thread when the Files tab is shown, with the same debounce, cancels on every selection change and keeps the last four trees. The worker also builds the `FileTreeModel` (Core: nested directories, folded single-child chains, ordinal order), so the UI thread only asks it for the visible rows, which `FileTreeSession` caches until the opened directories or the filter change. A closed directory costs nothing: the rows are produced by walking only the open ones.

A 100 000-file commit loads in 0.6 s with the AOT binary and a worst-case filter keystroke stays near 0.1 s (`docs/internal/performance.md`); lazy per-directory loading is not needed at this size.

## Native AOT

Rules: `.claude/rules/csharp-style.md`. `PublishAot=true` and the trim/AOT analyzers are on from the first commit, `IL*` warnings are errors. Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64. Release archives for linux-x64, win-x64 and osx-arm64 are built on `v*` tags by `ci.yml` (`scripts/package.sh`, draft release; the Windows leg also attaches the bare `gitunia-<version>-win-x64.exe` plus `.sha256` for a portable winget package, #941; test: `scripts/test-package.sh`); the Linux leg also builds a `.deb` and an `.rpm` from the same binary with nfpm (`scripts/package-linux.sh`, `scripts/nfpm.yaml`, #942). The macOS leg (`macos-arm64`, #455) runs on tags and on `gh workflow run ci.yml -f platform=macos`, never in `all` (10x minutes); it always builds, tests (OS-sensitive tests only) and smoke-runs, since the check job covers Linux and Windows only. A tiny `check` job looks for a green full `workflow_dispatch` run of `ci.yml` on the tagged commit: if there is one, the tag run skips build and tests and only packages; otherwise (or if the lookup fails) the same run builds and tests first, then packages and releases. It never starts or awaits another run (#899, #937). On a tag both legs publish with `-p:Version=<tag without v>`; `Directory.Build.props` defaults plain builds to `0.0.0-dev`, and a target in `Gitunia.Tui.csproj` bakes `$(Version)` into the generated constant `BuildVersion.Value` (no reflection, no startup cost). `gitunia --version` prints `gitunia <version>` and exits before any discovery or UI (the check is in `CommandLineParser`).

## Composition root

`Program.Main` creates the `GitRunner` and calls `GitServices.Create`, which wires every reader and loader by hand and passes the `GitServices` record to the application. The Tui depends on the reader interfaces, each with a fake in `tests/Gitunia.Tui.Tests/Fakes`; tests build the same record (`TestGit.Services`) or swap one part with `with`. Static versus instance: `.claude/rules/csharp-style.md`.

## Cross-platform

- Linux, Windows and macOS are supported targets; macOS is the lowest priority. Build and test on Linux first; CI has a Windows leg and a macOS leg (tags, on demand). Platform-specific code and path rules: `CLAUDE.md`.
- The config file is `config.json` in the config directory (format and rules: `docs/internal/ux-look.md`, "Config file"). `ConfigLoader` and `KeymapConfigurator` (Core) read it and build the keymap; `ConfigStartup` (Tui) runs them at startup. `ConfigJsonContext` is the source-generated JSON context; sections are added as properties of `ConfigDocument`. `ConfigStartup.Load` also reads `theme` (`ThemeConfigurator`) and `tabs.showBranch`, `scanRoots`, `labelCaps` and `hints.whichKey`/`hints.statusBar` (`OptionsConfigurator`, result `ConfigOptions` in `StartupConfig`); `PaletteSettings.WithTheme` forces the light or dark side, or applies a theme file (`ThemeCatalog`, `BuiltInThemes` and `ThemeFileParser` in `Gitunia.Core.Themes`; built-in files are embedded resources in `Themes/Files`), before `Palette.Configure`; `PaletteTable` turns a `ThemeDefinition` into colours.
- `ConfigWriter` (Core) changes one key of `config.json` with a pure edit, an atomic replace and a content-hash stale check; the Settings screen persistence rules are in `docs/internal/ux-settings.md`.
- Config and state locations follow terminal-app convention on every OS, not the OS-native dirs: config in `$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, state in `$XDG_STATE_HOME/gitunia` or `~/.local/state/gitunia`. So the config folder can live in a dotfiles repository, and config stays apart from machine-local state.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.

Design history (why things are the way they are, measurements, rejected options): `docs/internal/history/architecture.md`. Not needed for routine work.
