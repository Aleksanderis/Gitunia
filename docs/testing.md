# Testing

## Principles

- Test where the risk is: parsing git output, graph layout, destructive operations, worktree handling, key handling, and rendering regressions. Plain wiring gets no tests.
- Real `git` against real temporary repos for the Git layer. No mocking the process runner except for timeout/cancellation/error-path tests.
- State and keymap logic is pure and tested without a terminal.
- No test touches the user's repos, global git config or network. Isolation recipe, naming, waiting rules and fixture shapes are in the `testing` skill.
- Tests run in parallel and git children inherit the process environment: never change process-wide environment variables in a test, except in the `AmbientEnvironmentGroup` collection (runs alone), or concurrent git runs flake (#299).
- No unbounded waits: a blocking wait or gate in a test or fake carries a timeout (30 s), because `TestContext.Current.CancellationToken` only fires when the run ends. `Gitunia.Tui.Tests` also has a 10-minute run watchdog (`RunWatchdog`) that aborts a hung testhost (it cannot help if the stuck process is the `dotnet test` driver; #384).
- A failing test blocks merge. No coverage percentage gate.

## Layers

| Layer | Tools | Covers |
|---|---|---|
| Git parsers | xUnit, captured output fixtures | Every parser incl. odd cases (renames, unicode, detached, unborn, in-progress ops) |
| Git integration | xUnit, temp repos (see `testing` skill) | Commands end to end: branches, worktrees, stash, merge/rebase, partial staging |
| Tui loading and effects | xUnit; fakes of the reader interfaces (`tests/Gitunia.Tui.Tests/Fakes`, wired by `FakeGitServices`) where git is not the subject, real temp repos (`TestGit.Services`) where it is (watchers, refresh, effect runner; each test class says which tests stay real and why) | Stores, pollers and loaders without a process per test; the wiring against real git |
| Graph layout | xUnit, property tests | Lane assignment invariants, determinism, incremental == full |
| State/keymap | xUnit | Command -> state transitions, modal behaviour, no binding conflicts (test reads the keymap table) |
| Rendering | snapshot tests of frames as text (`ScreenComposer`; `gitunia <repo> --print-frame WxH [--diff] [--zoom <pane>] [--tab details\|changes\|files] [--colors truecolor\|16\|none [--light]]` prints the same frame, with ANSI colours when asked) | Layout at several terminal sizes, wide chars, ASCII fallback; `MainViewTests` builds the Terminal.Gui `MainView` without a terminal and checks view-level settings the text frames cannot show (the detail list's panel scheme, which colours its blank cells) |
| AOT smoke | CI script | AOT publish succeeds with no `IL*` warnings; binary starts and renders a frame against a fixture repo |
| Manual | checklist | Real terminals (kitty, alacritty, Windows Terminal, tmux), credential prompts, huge repos; mouse: the Terminal.Gui views (`PaneListView`, `PickerView`) have no headless test, so check click, double-click and wheel in tmux by sending SGR sequences (`tmux send-keys -l $'\e[<0;X;YM\e[<0;X;Ym'`, wheel is button 64/65; a drag is the press `0;X;Y` + `M`, one or more moves `32;X;Y` + `M`, then the release `0;X;Y` + `m`) or with a real mouse; the pure parts (`DragTracker`, `PaneResizer`, the button layouts, `VisualRangeController`) are unit-tested |

CI (`.github/workflows/ci.yml`) runs build, test, AOT publish and the smoke run on linux-x64 on manual dispatch only (`gh workflow run ci.yml`): never on PRs, pushes or a schedule. Local `dotnet build` and `dotnet test` are the gate; dispatch CI before closing a milestone or cutting a release. The OS matrix comes in M4. Why (free-plan minutes): `decisions.md` and `docs/history/testing.md`.

Agents verify locally first (`dotnet build`, `dotnet test`, AOT publish when dependencies or projects changed, `scripts/inspect.sh --changed`), open the PR ready and push once. Code-only PRs rely on the build-time AOT analyzers until the weekly run. After merging, if another PR merged since the last push, run `dotnet build && dotnet test` on updated `main`.

## Performance checks

A benchmark (BenchmarkDotNet, not part of the normal test run) for graph load and layout on a generated 100k-commit repo. Record numbers in `docs/performance.md` when targets are verified.

### Big repo

`scripts/gen-big-repo.sh [dir] [commits]` builds a deterministic repo with `git fast-import` (default 100000 commits, ~5-10 s, linear `main` with a merged 4-commit feature branch every 40 commits, 24 kept branches, a tag per 5000 commits, commit-graph written). Default location `${GITUNIA_BIG_REPO:-${XDG_CACHE_HOME:-~/.cache}/gitunia/big-<commits>}`; it is generated on demand, never committed, and re-running is a no-op. Subjects are `Commit #<n>: ...` / `Merge feature f<k>`, so the newest and oldest commit are recognizable on screen. Linux/macOS only (bash, awk).

### Measurement harness

`scripts/measure.sh <binary> <repo> [--runs N] [--keys N] [--marker T] [--oldest T] [--size WxH] [--only-start]` runs the binary as `<binary> <repo>` in a private tmux server and prints `key=value` lines. Requires bash >= 5 and tmux; Linux/macOS only. The binary runs with a private `XDG_STATE_HOME`/`XDG_CONFIG_HOME` (on Linux), so a restored session of the real user (extra tabs, saved scroll position) cannot change the frame or the RSS; macOS state under `~/Library` is not isolated. Timings are wall-clock ms from polling `capture-pane`, so they carry a 1-3 ms polling floor; compare binaries with the same harness rather than reading them as absolute.

`scripts/measure-tabs.sh <binary> [--tabs N] [--commits N] [--runs N] [--size WxH]` measures a session restore: it builds N throwaway repos and a `session.json` under a temp `XDG_STATE_HOME`/`HOME` (nothing real is touched), runs the binary without arguments and prints cold start, RSS (active tab only, all tabs visited) and tab-switch times. Same tmux requirements and polling floor.

What the harness expects of the binary:

- Invoked as `<binary> <repo-path>`; renders in the alternate screen, fills the terminal.
- First frame shows the commit list with the newest commit subject (HEAD's subject, from `git log -1 --format=%s`) as text. "Cold start" = tmux `new-session` until that text is on screen.
- Keys: `j`/`k` move the selection, `Ctrl-d` half page down, `G` last commit, `gg` first commit. Each must change the rendered frame (a moved highlight counts, since captures keep escape sequences). Jumping to the end must show the oldest commit's subject; `gg` the newest.
- It must re-render on terminal resize (the harness resizes and expects the marker again).
- Binaries that can't do all of this still work with `--only-start --marker <text>`.

Metrics: `binary_bytes`, `cold_start_ms_{median,p95,max}`, `key_latency_ms_{median,p95,max}` (`j` to changed frame), `page_scroll_ms_*` (`Ctrl-d`), `details_step_ms_{median,p95,max}` and `details_burst_10j_ms` (#215: `j` until the details pane header shows another commit and no "Loading..." is left, with 300 ms between steps / 10 `j` in one burst; needs the details pane in its default layout), `diff_step_ms_{median,p95,max}` (#225: `j` until the diff pane beside the Changes file list shows another commit: its title hash changed, no "Loading..." and a hunk header is on screen; 300 ms between steps), `burst_200j_settle_ms` (200 queued `j` until the frame stops changing), `jump_to_end_ms` (`G` after the log finished streaming; the harness waits for the `+` in `n/total+` to vanish, because `G` goes to the last loaded commit), `jump_to_start_ms`, `resize_ms` (frame changes after a window shrink and still shows the newest subject), `rss_kib`. `nan` means the expected frame never appeared within `--timeout`. Each poll costs a tmux round trip, so expect a few ms of floor on every latency figure; run a trivial reference program to calibrate on your machine. `cold_start_samples` and `key_samples` report how many samples produced numbers.

## Screenshots

UI pull requests with a visual change carry one capture (2-3 images at most); none for key labels, hints only, docs or logic. Procedure, flags and limits: the `screenshots` skill.

Design history (CI minutes, screenshot hosting, accepted timer waits): `docs/history/testing.md`.
