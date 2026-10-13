# Testing

## Principles

- Test where the risk is: parsing git output, graph layout, destructive operations, worktree handling, key handling, and rendering regressions. Plain wiring gets no tests.
- Real `git` against real temporary repos for the Git layer. No mocking the process runner except for timeout/cancellation/error-path tests.
- State and keymap logic is pure and tested without a terminal.
- No test touches the user's repos, global git config or network. Isolation recipe and fixture shapes are in the `testing` skill.
- No new sleeps or polling loops to wait for background work. Wait on a signal: `UiPump` (queues posted UI callbacks and runs them on the test thread until a condition holds), a `TaskCompletionSource` completed from the store's `Changed` event or a file watcher, or an injected delay (e.g. the `delay` parameter of `CommitDetailsStore`, `scanningDelay` of `PickerController`). A guard timeout only bounds a hang. `RepositoryWatcher.BecameStale` is the signal for a background tab turning stale.
- Accepted intrinsic timer waits (the wait is the thing under test): in `GitRunnerTests`, the slow-consumer test (consumer sleeps 600 ms against a 200 ms runner timeout) and the stalled-stream test (the fake prints once, then sleeps; the idle timeout is 2 s, not 500 ms, because the timer also covers process start-up, which can exceed 500 ms under CPU load so the first chunk never arrives) and, on Windows only, `AssertNeverMarkedAsync` outwaiting 3 s because the cmd fake cannot record its pid. On Linux/macOS that helper waits for the process to exit. Measured under load (#182): 100 runs of the stalled-stream test with one busy loop per core (14 cores) all passed on that 14-core dev machine (`dotnet test --no-build --filter ThenTimeoutAfterFirstChunk` in a loop, one `while :; do :; done` per core). A 2-4 core CI runner is untested; if it flakes, widen the timeout first.
- A failing test blocks merge. No coverage percentage gate.

## Naming

Test methods are named `Given<Context>_When<Action>_Then<Outcome>` in PascalCase, the three parts separated by underscores (CA1707 is off under `tests/`).

## Layers

| Layer | Tools | Covers |
|---|---|---|
| Git parsers | xUnit, captured output fixtures | Every parser incl. odd cases (renames, unicode, detached, unborn, in-progress ops) |
| Git integration | xUnit, temp repos (see `testing` skill) | Commands end to end: branches, worktrees, stash, merge/rebase, partial staging |
| Graph layout | xUnit, property tests | Lane assignment invariants, determinism, incremental == full |
| State/keymap | xUnit | Command -> state transitions, modal behaviour, no binding conflicts (test reads the keymap table) |
| Rendering | snapshot tests of frames as text (`ScreenComposer`; `gitunia <repo> --print-frame WxH [--diff] [--zoom <pane>] [--tab details\|changes\|files]` prints the same frame) | Layout at several terminal sizes, wide chars, ASCII fallback |
| AOT smoke | CI script | AOT publish succeeds with no `IL*` warnings; binary starts and renders a frame against a fixture repo |
| Manual | checklist | Real terminals (kitty, alacritty, Windows Terminal, tmux), credential prompts, huge repos; mouse: the Terminal.Gui views (`PaneListView`, `PickerView`) have no headless test, so check click, double-click and wheel in tmux by sending SGR sequences (`tmux send-keys -l $'\e[<0;X;YM\e[<0;X;Ym'`, wheel is button 64/65; a drag is the press `0;X;Y` + `M`, one or more moves `32;X;Y` + `M`, then the release `0;X;Y` + `m`) or with a real mouse; the pure parts (`DragTracker`, `PaneResizer`, the button layouts, `VisualRangeController`) are unit-tested |

CI (`.github/workflows/ci.yml`) runs build, test, AOT publish and the smoke run on linux-x64 for every ready (non-draft) PR and push to main; the OS matrix comes in M4. To save Actions minutes (2,000 a month on the free plan) it skips draft PRs (it runs when one is marked ready), PRs whose base branch is `review/**` (owner review-only PRs), and docs-only changes (`docs/**`, `*.md`, `.claude/**`). Agents open PRs as drafts and mark them ready once the reviewer has no blockers.

## Performance checks

A benchmark (BenchmarkDotNet, not part of the normal test run) for graph load and layout on a generated 100k-commit repo. Record numbers in `docs/architecture.md` when targets are verified.

### Big repo

`scripts/gen-big-repo.sh [dir] [commits]` builds a deterministic repo with `git fast-import` (default 100000 commits, ~5-10 s, linear `main` with a merged 4-commit feature branch every 40 commits, 24 kept branches, a tag per 5000 commits, commit-graph written). Default location `${GITUNIA_BIG_REPO:-${XDG_CACHE_HOME:-~/.cache}/gitunia/big-<commits>}`; it is generated on demand, never committed, and re-running is a no-op. Subjects are `Commit #<n>: ...` / `Merge feature f<k>`, so the newest and oldest commit are recognizable on screen. Linux/macOS only (bash, awk).

### Measurement harness

`scripts/measure.sh <binary> <repo> [--runs N] [--keys N] [--marker T] [--oldest T] [--size WxH] [--only-start]` runs the binary as `<binary> <repo>` in a private tmux server and prints `key=value` lines. Requires bash >= 5 and tmux; Linux/macOS only. The binary runs with a private `XDG_STATE_HOME`/`XDG_CONFIG_HOME` (on Linux), so a restored session of the real user (extra tabs, saved scroll position) cannot change the frame or the RSS; macOS state under `~/Library` is not isolated. Timings are wall-clock ms from polling `capture-pane`, so they carry a 1-3 ms polling floor; compare binaries with the same harness rather than reading them as absolute.

`scripts/measure-tabs.sh <binary> [--tabs N] [--commits N] [--runs N] [--size WxH]` measures a session restore: it builds N throwaway repos and a `session.json` under a temp `XDG_STATE_HOME`/`HOME` (nothing real is touched), runs the binary without arguments and prints cold start, RSS (active tab only, all tabs visited) and tab-switch times. Same tmux requirements and polling floor.

Contract for a binary (spikes included):

- Invoked as `<binary> <repo-path>`; renders in the alternate screen, fills the terminal.
- First frame shows the commit list with the newest commit subject (HEAD's subject, from `git log -1 --format=%s`) as text. "Cold start" = tmux `new-session` until that text is on screen.
- Keys: `j`/`k` move the selection, `Ctrl-d` half page down, `G` last commit, `gg` first commit. Each must change the rendered frame (a moved highlight counts, since captures keep escape sequences). Jumping to the end must show the oldest commit's subject; `gg` the newest.
- It must re-render on terminal resize (the harness resizes and expects the marker again).
- Binaries that can't do all of this still work with `--only-start --marker <text>`.

Metrics: `binary_bytes`, `cold_start_ms_{median,p95,max}`, `key_latency_ms_{median,p95,max}` (`j` to changed frame), `page_scroll_ms_*` (`Ctrl-d`), `details_step_ms_{median,p95,max}` and `details_burst_10j_ms` (#215: `j` until the details pane header shows another commit and no "Loading..." is left, with 300 ms between steps / 10 `j` in one burst; needs the details pane in its default layout), `burst_200j_settle_ms` (200 queued `j` until the frame stops changing), `jump_to_end_ms` (`G` after the log finished streaming; the harness waits for the `+` in `n/total+` to vanish, because `G` goes to the last loaded commit), `jump_to_start_ms`, `resize_ms` (frame changes after a window shrink and still shows the newest subject), `rss_kib`. `nan` means the expected frame never appeared within `--timeout`. Each poll costs a tmux round trip, so expect a few ms of floor on every latency figure; run a trivial reference program to calibrate on your machine. `cold_start_samples` and `key_samples` report how many samples produced numbers.

## Screenshots

UI pull requests carry 2-3 real screenshots (colours, glyphs, layout) next to the `--print-frame` text. `scripts/screenshot.sh` (Linux/macOS; needs bash, tmux, rsvg-convert, dotnet, git, and `gh` for publish/clean; a missing tool prints which one and exits 3; Windows is unsupported and exits 2, there is no PowerShell twin because tmux has no Windows build) does everything locally; CI never runs it.

- `scripts/screenshot.sh render --binary <gitunia> [--out <dir>] [--scenarios default,diff,narrow,light] [--repo <dir>]`: runs the real binary in a private tmux server (truecolor, private `XDG_*`) on a deterministic throwaway repo (generated under a temp dir unless `--repo`; never point it at a real repo), captures `capture-pane -p -e`, converts it with `tools/AnsiToSvg` (built on first use into `~/.cache/gitunia/screenshot`) and rsvg-convert into `<dir>/<scenario>.png`. Scenarios: `default` 120x36, `diff` (`Enter Enter`: the file diff open), `narrow` 64x30, `light` (a light terminal background; the app has no theme setting yet, so this shows how the colours hold up on a light background).
- `scripts/screenshot.sh publish --pr <n>`: pushes the PNGs to `pr-<n>/<short sha>/` on the orphan `screenshots` branch (plain push, never forced, only that branch) and prints markdown for a PR comment. The repo is private, so links are `https://github.com/<owner>/<repo>/raw/<screenshots commit>/pr-<n>/<sha>/<scenario>.png` (they render for viewers with repo access; `raw.githubusercontent.com` would need a token) and are pinned to a commit, so later pushes never break them.
- `scripts/screenshot.sh clean`: removes `pr-<n>` directories whose PR is merged or closed and pushes the removal. Run it after merging. The branch is never force-pushed, so its history keeps every image: old pinned links keep working and `clean` only tidies the branch tip, it does not shrink the repository (accepted for now; screenshots are small, and an owner-run history rewrite is the only way to reclaim space). `publish`/`clean` commit as `Claude (implementer)` (override with `GITUNIA_AGENT_ROLE`) with an `Agent:` trailer and retry a rejected push once after `pull --rebase`. Needs tmux 3.2 or newer.
- Agents attach the images, they never read them back (token cost). Look at one yourself only when debugging the pipeline.
- Known limits: the font is whatever the machine has (JetBrains Mono if installed, else DejaVu/Liberation Mono), so glyph widths and Nerd Font icons differ from the owner's terminal: those stay a manual check (#196). A frame is captured once after it settles; animations and the mouse are not covered.
