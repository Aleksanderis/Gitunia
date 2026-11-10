# Testing

## Principles

- Test where the risk is: parsing git output, graph layout, destructive operations, worktree handling, key handling, and rendering regressions. Plain wiring gets no tests.
- Real `git` against real temporary repos for the Git layer. No mocking the process runner except for timeout/cancellation/error-path tests.
- State and keymap logic is pure and tested without a terminal.
- No test touches the user's repos, global git config or network. Isolation recipe, naming, waiting rules and fixture shapes are in the `testing` skill. A runner for production code under test comes from `IsolatedGit.CreateRunner()` (or `TestRepository.CreateIsolatedRunner()`), never `new GitRunner()`: the machine's global git config (e.g. `core.autocrlf=true` on Windows CI) must not leak in (#608).
- Process spawns dominate test time on Windows (#624). Test runners therefore get `TestGitSpeed`: the real `mingw64\bin\git.exe` instead of the `cmd\git.exe` shim, and `gc.auto=0`, `maintenance.auto=false` and `core.fsync=none`. New fixtures that build a runner use it; keep the number of git calls per fixture low.
- Tests run in parallel and git children inherit the process environment: never change process-wide environment variables in a test, except in the `AmbientEnvironmentGroup` collection (runs alone), or concurrent git runs flake (#299).
- Parallelism: `tests/xunit.runner.json` caps xUnit at 8 threads per assembly, parallel across test classes (no method-level parallelism: split a slow class instead). The Windows CI leg runs the test projects one after another and stops at the first failing one (#634). Test git runners get a 2-minute default timeout (`TestGitSpeed.Timeout`) and waits for background events are generous (30-60 s); do not write a test whose pass depends on a timeout below ~1 s.
- No unbounded waits: a blocking wait or gate in a test or fake carries a timeout (30 s), because `TestContext.Current.CancellationToken` only fires when the run ends. `Gitunia.Tui.Tests` also has a 10-minute run watchdog (`RunWatchdog`) that aborts a hung testhost and first prints the tests still in flight with their run time (`RunningTests`, #1254; it cannot help if the stuck process is the `dotnet test` driver; #384)). The Linux CI test step adds `--blame-hang-timeout 3m` (names a hung test; it must stay below the step timeout or the step is cancelled first) and a 10-minute step timeout (covers a host that died with no test running; #666). A callback that signals a test from a background thread uses `TestSignal`, not a disposed `SemaphoreSlim`: a late `Release` on a disposed semaphore throws on a pool thread and can end the host.
- One test per behaviour, one theory row per equivalence class or boundary (#676): a new case says which class or edge it adds, and prefers a row in the sibling theory over a new `[Fact]`. A row that repeats a class (another code point in the same range, the same input plus a trailing ASCII char) is deleted, not kept "to be safe". Cases that differ only by data are a `[Theory]` (`[InlineData]`, or `[MemberData]` for larger tables and non-constant data), never a hand-written `foreach` with a `failures` list in a `[Fact]`: a theory reports and reruns each row on its own and counts as one test-logic method. A loop is allowed only when the per-case setup is expensive (a real repo or process per case), with a one-line comment saying so. A property test runs 3 seeds, not 40 (#1072, #1084). A test is also dropped when a lower-layer test fails for the same defect (an App pipeline test that only repeats what its effect handler test and the Core reducer tests prove): break the guarded line, see the lower test fail, then drop.
- No tests of constants, defaults or banner strings, and none that recompute the production expression (the `IsSupported` case): they restate the code and can only fail when the code is edited on purpose.
- A timeout test injects a short timeout (tens of ms) instead of waiting seconds; the floor of ~1 s above is for tests that wait on real background events, not for timeout semantics.
- The #676 audit kept the "unrelated action is not handled" tests per handler and separate tests for `CommitDetailsCache` and `DiffCache`. File watching is off by default, so tab tests poll: inject `PollOptions` with short intervals.
- A failing test blocks merge. No coverage percentage gate.

## Layers

| Layer | Tools | Covers |
|---|---|---|
| Git parsers | xUnit, captured output fixtures | Every parser incl. odd cases (renames, unicode, detached, unborn, in-progress ops) |
| Git integration | xUnit, temp repos (see `testing` skill) | Commands end to end: branches, worktrees, stash, merge/rebase, partial staging |
| App loading and effects (`tests/Gitunia.App.Tests`; Tui for what needs the terminal) | xUnit; fakes of the reader interfaces (`tests/Gitunia.Tui.Tests/Fakes`, linked into App.Tests, `FakeGitServices`) where git is not the subject, real temp repos (`TestGit.Services`) where it is | Stores, pollers and loaders without a process per test; the wiring against real git |
| Graph layout | xUnit, property tests | Lane assignment invariants, determinism, incremental == full |
| State/keymap | xUnit; `TestShell` (`tests/Gitunia.Tui.Tests/Fakes`) gives a view or shell test the real shell state (key mode, confirm dialog, open prompt, status, output) with `Answer()`/`Decline()` | Command -> state transitions (`ShellReducer`, `PromptReducer`, `AppReducer`: dialogs, prompts, status aging, resize), modal behaviour, no binding conflicts (tests read `keymap.default.json` and check the generated `docs/keymap.md` is current) |
| Rendering | snapshot tests of frames as text (`ScreenComposer`); `gitunia <repo> --print-frame WxH` prints the same frame (flags: `CommandLineParser.Usage`) | Layout at several terminal sizes, wide chars, ASCII fallback; `MainViewTests` checks view-level settings the text frames cannot show |
| Startup smoke | `StartupSmokeTests` runs `TuiApplication.Run` on the headless `ansi` driver with temp config/state dirs, with a repository and with none | The whole shell is constructed as `Program.Main` does and its first frame is drawn, so a constructor-order crash fails `dotnet test`. It cannot see rendering or key handling: UI issues also get a tmux run of the real binary |
| AOT smoke | CI script | AOT publish succeeds with no `IL*` warnings; binary starts and renders a frame against a fixture repo |
| Manual | checklist | Real terminals (kitty, alacritty, Windows Terminal, tmux), credential prompts, huge repos; mouse in the Terminal.Gui views (`PaneListView`, `PickerView`) is checked in tmux by sending SGR sequences (`tmux send-keys -l $'\e[<0;X;YM\e[<0;X;Ym'`; wheel is button 64/65; a drag is the press `0;X;Y` + `M`, moves `32;X;Y` + `M`, then the release `0;X;Y` + `m`) or with a real mouse; the pure parts (`DragTracker`, `PaneResizer`, `VisualReducer`, `DiffPaneReducer`) are unit-tested |

Windows interactive runs: drive the real `gitunia.exe` with psmux (same `new-session`/`send-keys`/`capture-pane` commands; caveats in the `testing` skill, "Driving the real binary"). Snapshot and smoke tests stay the gate.

Windows runs only the OS-sensitive tests (#637): every test class marked `[Trait("Os", "Independent")]` (pure logic on in-memory data) is excluded there with `--filter "Os!=Independent"`; Linux runs all. An unmarked test runs on Windows, so a forgotten mark costs minutes, never coverage. `OsTraitGuardTests` fails if a marked file uses real git, paths, the file system, processes or the environment. Author rules: `testing` skill, "Mark OS-independent tests".

Test projects that compare paths with what git returns link `Fixtures/RealTempDirectory.cs`: its module initializer points `TMPDIR` at the symlink-free temp directory, because macOS's `/var/folders/...` is a link to `/private/var/...` and git reports the latter (#959). On the macOS leg, `scripts/ci-hang-watch.sh` runs beside the tests and prints the process tree and stack samples when no test finishes for 45 s or a child of the host lives over 25 s (#960; remove it once a stall is explained).

CI (`.github/workflows/ci.yml`) runs build, test, the install script tests (`scripts/test-install.sh`, and `scripts/test-install.ps1` in Windows PowerShell and pwsh; a fake release on disk, no internet; the PowerShell test also starts a loopback HTTP server for the download retries, #1248, #1281), AOT publish and the smoke run on linux-x64 and windows-x64 on every pull request (not drafts), every push to `main` and on manual dispatch (macos-arm64 only with `-f platform=macos`, never in `all`; it always builds and tests; #455), the `linux-arm64` and `windows-arm64` legs (native arm64 runners; `-f platform=arm64` only, never in `all`; they always build and test; #1247) (`gh workflow run ci.yml`). Release packaging is not part of it: `release.yml` runs on `v*` tags and reuses or dispatches a full run of `ci.yml` for the tagged commit (see `docs/architecture.md`, "Releases"; #1295). Re-enabled when the repository went public (see `decisions.md`); local `dotnet build` and `dotnet test` stay the first gate. Tests that need Linux-only file names (`*`, `:` or a tab) or shell-script fakes skip themselves on Windows. Why (free-plan minutes): `decisions.md` and `docs/internal/history/testing.md`.

`packaging/aur/test.sh` tests the AUR update script (fake archives, push to a local bare repo; the `makepkg` parts run only on Arch) and is run by hand, no workflow runs it. `scripts/test-homebrew-formula.sh` does the same for the Homebrew formula generator, and `scripts/test-winget-open-pr.sh` for the winget open-PR guard (canned `gh pr list` JSON).

Local verification and push rules for agents: `.claude/workflow.md` (Agent run-time rules, Policy). A change to `ci.yml` that moves build or output paths is tried locally first with the same environment variables (`UseArtifactsOutput=true`, `ArtifactsPath=<dir outside the repo>`, then `dotnet test`); tests must not assume the binaries sit inside the repo (#663).

## Performance checks

A benchmark (BenchmarkDotNet, not part of the normal test run) for graph load and layout on a generated 100k-commit repo. Record numbers in `docs/internal/performance.md` when targets are verified.

### Big repo

`scripts/gen-big-repo.sh [dir] [commits]` builds a deterministic repo with `git fast-import` (default 100000 commits, ~5-10 s, linear `main` with a merged 4-commit feature branch every 40 commits, 24 kept branches, a tag per 5000 commits, commit-graph written). Default location `${GITUNIA_BIG_REPO:-${XDG_CACHE_HOME:-~/.cache}/gitunia/big-<commits>}`; it is generated on demand, never committed, and re-running is a no-op. Subjects are `Commit #<n>: ...` / `Merge feature f<k>`, so the newest and oldest commit are recognizable on screen. Linux/macOS only (bash, awk).

`scripts/gen-big-file-repo.sh [dir] [lines] [commits]` builds a repo with one large file `big.txt` for blame checks (default 50000 lines and 500 edit commits, each rewriting every 50th line from a rotating offset, ~9 s). Default location `${XDG_CACHE_HOME:-~/.cache}/gitunia/bigfile-<lines>-<commits>`; never committed, re-running is a no-op. Linux/macOS only.

### Measurement harness

`scripts/measure.sh <binary> <repo> [--runs N] [--keys N] [--marker T] [--oldest T] [--size WxH] [--only-start] [--blame]` runs the binary as `<binary> <repo>` in a private tmux server and prints `key=value` lines. Requires bash >= 5 and tmux on Linux/macOS. `--blame` (#1137) opens the blame view of the file in the newest commit and times `j`, `Ctrl-d`, `G` and RSS in it; it needs a `scripts/gen-big-file-repo.sh` repo and has no `measure.ps1` equivalent. On Windows use `scripts/measure.ps1 <binary> <repo> [-Runs N] [-Keys N] [-Marker T] [-Oldest T] [-Size WxH] [-OnlyStart]` (PowerShell 7 and psmux, same metrics and output; each poll costs ~50 ms, so its numbers are comparable only with other `measure.ps1` runs). The binary runs with a private `XDG_STATE_HOME`/`XDG_CONFIG_HOME` (on Linux and, in the `.ps1` twins, Windows), so a restored session of the real user (extra tabs, saved scroll position) cannot change the frame or the RSS; macOS state under `~/Library` is not isolated. Timings are wall-clock ms from polling `capture-pane`, so they carry a 1-3 ms polling floor; compare binaries with the same harness rather than reading them as absolute.

`scripts/measure-tabs.sh <binary> [--tabs N] [--commits N] [--runs N] [--size WxH]` measures a session restore: it builds N throwaway repos and a `session.json` under a temp `XDG_STATE_HOME`/`HOME` (nothing real is touched), runs the binary without arguments and prints cold start, RSS (active tab only, all tabs visited) and tab-switch times. Same tmux requirements and polling floor; `scripts/measure-tabs.ps1 <binary> [-Tabs N] [-Commits N] [-Runs N] [-Size WxH]` is the psmux twin for Windows; its RSS is the Windows working set, only loosely comparable with Linux RSS. `scripts/screenshot.ps1` is the psmux twin of `screenshot.sh` (same commands and scenario names, PNG via resvg instead of rsvg-convert; see the `screenshots` skill).

What the harness expects of the binary:

- Invoked as `<binary> <repo-path>`; renders in the alternate screen, fills the terminal.
- First frame shows the commit list with the newest commit subject (HEAD's subject, from `git log -1 --format=%s`) as text. "Cold start" = tmux `new-session` until that text is on screen.
- Keys: `j`/`k` move the selection, `Ctrl-d` half page down, `G` last commit, `gg` first commit. Each must change the rendered frame (a moved highlight counts, since captures keep escape sequences). Jumping to the end must show the oldest commit's subject; `gg` the newest.
- It must re-render on terminal resize (the harness resizes and expects the marker again).
- Binaries that can't do all of this still work with `--only-start --marker <text>`.


Metrics (`key=value`; `nan` means the expected frame never appeared within `--timeout`): `binary_bytes`, `cold_start_ms_*`, `key_latency_ms_*` (`j` to changed frame), `page_scroll_ms_*` (`Ctrl-d`), `details_step_ms_*` and `details_burst_10j_ms` (#215), `diff_step_ms_*` (#225), `burst_200j_settle_ms`, `jump_to_end_ms` (after the log finished streaming), `jump_to_start_ms`, `resize_ms`, `rss_kib`, plus `cold_start_samples` and `key_samples`. Each poll costs a tmux round trip, so expect a few ms of floor on every latency figure.

## Screenshots

UI pull requests with a visual change carry one capture (2-3 images at most); none for key labels, hints only, docs or logic. Procedure, flags and limits: the `screenshots` skill.

### Regenerating the README demo

The animation in `README.md` is a scripted recording of the real binary on a generated throwaway repository (about 300 commits, three authors, main/develop/release branches and feature branches of mixed length, tags), never a real one. It is not committed here: the README links to the landing page copy (`https://gitunia.github.io/media/demo.gif`). On Linux with `vhs`, `ttyd` and `ffmpeg` installed, run `scripts/demo.sh` (it publishes the AOT binary first; pass `--binary <gitunia>` to reuse one). It writes `demo.gif` and `demo.webm` into the landing page checkout (`gitunia.github.io`, `media/`): `$GITUNIA_LANDING_MEDIA`, else `~/Projects/Gitunia-Landing/media`, or pass `--out <dir>`. Commit and push the files there, not in this repository. The key script is `scripts/demo.tape`: edit its `Type` and `Sleep` lines when the UI or keys change, then rerun (keep the GIF at a few MB; re-record only when the UI visibly changed). The recording works in the fixed directory `/tmp/gitunia-demo` (removed on exit) so the paths shown in it are the same on every run; only one run at a time is supported. Separate from the screenshots flow; never runs in CI.

Design history (CI minutes, screenshot hosting, accepted timer waits): `docs/internal/history/testing.md`.
