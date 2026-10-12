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

- standalone workspace model. `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos) and each tab owns an independent `RepoState` (graph, selection, scroll, status, worktrees). Commands are routed to a tab by id; effects carry the repo path they run against. The app starts without any repo (empty workspace shows the repo picker) and restores the previous session when there is one.
- persistence (open tabs, recents, pins, per-tab view state, config) lives in the user config/state dir, written atomically; a corrupt or missing file must never prevent startup.
- three layers, strictly one-way dependencies: Rendering -> State -> Git. Git knows nothing about state or UI; State knows nothing about the console.
- State is pure and testable without a terminal. Effects (run a git command, open an editor, copy to clipboard) are returned as data and executed by a runner that feeds results back as commands.
- all git calls are async, cancellable and time-bounded; the UI never blocks on them.
- Terminal.Gui v2 is the UI toolkit; the rendering layer wraps it and the state layer never sees it. Custom drawing (commit graph, diff) goes in custom views; modal keys and chords are handled at the application level by our keymap engine. Settings and pitfalls (iteration rate, streaming list source, key handling) and the measured numbers are in `docs/decisions.md`. The custom-renderer spikes were rejected on readability and maintenance cost, not speed. A custom renderer may be added later as a separate project, so keep Terminal.Gui types out of Core and keep view models and key events renderer-agnostic (no abstraction interface until a second renderer exists; see `docs/decisions.md`).

## Solution layout

```
src/
  Gitunia.Platform/   the only OS-specific code: config/state dirs, console capability detection
  Gitunia.Git/        process runner, parsers, repo/worktree/branch/graph models
  Gitunia.Core/       state, commands, effects, keymap, config (no console, no process)
  Gitunia.Tui/        renderer, input, views, app entry point (AOT publish target); folders: Rendering (no Terminal.Gui types: styled lines, graph and sidebar glyphs, frame composer), Views (Terminal.Gui views, palette), Input (key translation, keymap controller), Loading (streaming from git), App (wiring: workspace host, effect runner, action router)
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

Dependencies: Tui -> Core -> Git -> Platform (Tui also -> Platform). Build config is in `Directory.Build.props` / `Directory.Packages.props` (central package management); `IL*` (AOT/trim) warnings fail the build through the global `TreatWarningsAsErrors`. Adjust here when the layout changes.

## Native AOT constraints

- No runtime reflection: use source generators (`System.Text.Json` source gen, `LoggerMessage`, etc.). No Newtonsoft, no Castle-style proxies, no DI container needing runtime codegen (plain constructor wiring is enough).
- `PublishAot=true` and trim/AOT analyzers on from the first commit, warnings as errors for `IL*` codes.
- CI (and every dependency addition) runs the AOT publish and a smoke test of the produced binary.
- Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64.

## Performance targets

- Cold start to first frame: < 100 ms with a warm git cache.
- Commit graph: open a 100k-commit repo and scroll without dropping frames. Load incrementally (`git log` streamed, paged); never materialize the whole graph eagerly in the UI.
- Many tabs: background tabs hold only compact state and are refreshed lazily or on a throttled timer; only the active tab does continuous work. Tab switch is instant (no reload).
- Memory: compact commit model (struct-of-arrays or interned hashes), virtualized lists.
- Render: diff against the previous frame, write one buffered flush per frame.

### Verified numbers (graph view, #22)

Native AOT linux-x64 binary (9.9 MB plus the 564 KB native library), 100k-commit repo from `scripts/gen-big-repo.sh`, `scripts/measure.sh` medians (they include 1-3 ms of tmux polling), Terminal.Gui at 100 iterations per second:

| Terminal | Cold start to first rows | `j` latency | `Ctrl-d` | 200 queued `j` settle | RSS with all 100k loaded |
|---|---|---|---|---|---|
| 80x24 | ~72 ms (p95 106) | ~7.5 ms | ~7 ms | ~15 ms | ~69 MB |
| 120x40 | ~80 ms (p95 101) | ~7.3 ms | ~7.6 ms | ~14 ms | ~73 MB |
| 200x60 | ~89 ms (p95 106) | ~8.8 ms | ~11.7 ms | ~16 ms | ~73 MB |

The first 150 commits are on screen at once; the whole log streams in and is laid out in ~0.8 s without blocking input (the status bar shows `n/total+` while loading). Idle CPU after loading is ~1%. Memory per loaded commit is ~250 bytes (a compact `GraphEntry`, not the full `CommitInfo`); frame cost depends on the visible rows only, not on the number of loaded commits. A narrow window cuts the newest subject, so the harness's resize check cannot find its marker there (`resize_ms=nan` at 80x24 is that, not a failure to redraw). Known limit: the wide-character table in `TextWidth` covers CJK and the main emoji blocks only, so some newer emoji count as one column and can make a row one cell too wide.

## Cross-platform

- Linux, Windows and macOS are all supported targets; macOS is the lowest priority and may be dropped if it adds significant complexity. Build and test on Linux first, and add Windows/macOS to CI in M4.
- Platform-specific code lives behind a small abstraction in one place (config/state dirs: XDG / `%APPDATA%` / `~/Library`; console setup: raw mode and VT input/output; resize notification: SIGWINCH vs console events; process spawning and signals; file watching quirks). Every abstraction has an implementation or an explicit "unsupported" result for each OS.
- Never hardcode path separators, `/tmp`, `/dev/null` or line endings; handle case-insensitive file systems, long Windows paths and CRLF in diffs.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.
