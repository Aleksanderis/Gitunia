# Performance

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Targets and verified numbers. The checklist for writing hot-path code is the `performance` skill; the measurement harness is in `docs/testing.md`. Record new numbers here when a target is verified.

## Targets

- Cold start to first frame: < 100 ms with a warm git cache.
- Commit graph: open a 100k-commit repo and scroll without dropping frames. Load incrementally (`git log` streamed, paged); never materialize the whole graph eagerly in the UI.
- Many tabs: background tabs hold only compact state and are refreshed lazily or on a throttled timer; only the active tab does continuous work. Tab switch to a recent tab is instant (no reload); tabs beyond the 5 most recently used drop their graph and reload on activation.
- Memory: compact commit model (struct-of-arrays or interned hashes), virtualized lists.
- Render: diff against the previous frame, write one buffered flush per frame.


## Verified numbers

Native AOT linux-x64, 120x40 under tmux, medians (they include 1-3 ms of polling). Per-issue logs, harness faults and machine details: `docs/history/performance.md`.

| Case | Result |
|---|---|
| Many tabs (`scripts/measure-tabs.sh`): 12 restored tabs, cold start to the active tab's first frame | ~50 ms, RSS ~36 MB (only the active tab loads) |
| Many tabs: first visit of a background tab / back to a loaded tab | ~37 ms / ~10-30 ms; all 12 visited ~46 MB |
| Graph, 100k commits (`scripts/gen-big-repo.sh`, `scripts/measure.sh`): cold start to first rows | ~72-89 ms (p95 ~106), 80x24 to 200x60 |
| Graph: `j` / `Ctrl-d` latency, 200 queued `j` settle | ~7-9 ms / ~7-12 ms / ~15 ms |
| Graph: RSS with all 100k loaded | ~69-73 MB (~340 bytes per commit) |
| Graph: whole log streamed and laid out | ~0.8 s, input never blocked |
| Search over 100k loaded commits (no match) | ~70 ms in 5000-row slices |
| Incremental reload (HEAD or refs moved) | ~105 ms vs ~840 ms full read; a branch ref moving onto a loaded commit 90-125 ms |
| Commit details / diff while moving | ~20 ms per step (LRU caches with neighbour prefetch: 64 commits, 12 diffs) |
| `body:` search (`log --grep`, 100k commits) | ~0.4 s no match, ~0.8 s matching nearly all; one process per tab, off the UI thread |
| Terminal background query (OSC 11, `theme = auto`) | first frame ~26 ms answered, ~75-80 ms when the terminal never answers |
| File content, worst case 1 MiB of newlines (1M rows) | ~50 ms build, ~56 MB; no row cap |
| `DiffDocument.ForFile` for a 100,000-row file | 2-9 ms per cursor move; no cache |
| Keymap defaults (#857): read the embedded `keymap.default.json` (438 rows, 551 bindings) with `Utf8JsonReader` and build the bindings | JIT debug first call ~8 ms including JIT of the code path (an upper bound only; the AOT startup was not timed separately, the keymap is a small part of the ~50 ms cold start), ~0.24 ms to rebuild from parsed rows; measured once by a throwaway test; AOT publish has no `IL*` warnings |
| Staging lines (#878): a Visual range runs 4 process calls one after the other: the file's diff, `apply --cached`, the status, then the diff again for the shown file (an untracked file adds `add -N` and, on failure, a cleanup call) | Linux, 2000-line file with 400 changed lines, single calls timed by hand: diff ~3 ms, apply ~1 ms, status ~1-5 ms (about 10 ms summed). Key press to redrawn diff was not measured end to end; nothing was optimised and the pending key was left out. Windows spawns slower; the owner checks its feel by hand |
| `WordDiff.Compare` per changed row | 0.002 ms typical, 0.31 ms worst case; visible rows only, bounded |

Known limit: at 80x24 the newest row is cut inside the harness marker; pass `--marker "Commit #97500"` there.

## Blame and file history (#1125)

Measured on Linux (Release build of the git readers and view code, JIT, one machine) with two throwaway repos: `scripts/gen-big-file-repo.sh` (`big.txt`, 50,000 lines, 501 commits) and `scripts/gen-big-repo.sh` (100,000 commits). No blame or file-history budget existed; the bar used was the graph's: the lines visible at once, the first blamed rows within a frame or two, a frame build far under 16 ms, everything off the UI thread. These numbers are the baseline for later changes.

Repeat: `scripts/gen-big-file-repo.sh` and `scripts/gen-big-repo.sh`, then by hand `time git -C <bigfile repo> blame --incremental big.txt >/dev/null` (once with `GIT_CONFIG_GLOBAL=/dev/null`) and `time git -C <big-100000> log --follow -n 1000 --name-status -- src/file5.txt >/dev/null`. The in-process numbers come from a throwaway xunit test (not committed): 4 runs of `BlameReader.ReadContentAsync` plus `BlameAsync` (timing the first chunk and the end), 3 runs of `FileHistoryReader.ReadAsync(.., "HEAD", "src/file5.txt", 1000)`, 3 x 1000 `BlameFrameBuilder.Build` calls (120x40, cursor moving) and 3 `BlameSearch.Find` calls over a synthetic 50k-line `BlameData` with 501 commits; values are single runs or ranges over those runs, not medians. File history of 903 entries is the whole history of that file (below the 1000 limit).

| Case | Result |
|---|---|
| Blame, 50k-line file: read the lines (working tree) | 1-6 ms, so the lines show at once |
| Blame: first blamed chunk | ~10 ms (20 ms cold) |
| Blame: all 50,000 lines blamed (git plus streaming parser, 6.5 MB of output) | ~190 ms; git alone ~200 ms, so the parser adds nothing visible; 0-27 MB allocated per run (noisy, one thread's counter) |
| Blame view: one frame build while scrolling (120x40, 50k lines) | ~0.1 ms (visible rows only) |
| Blame view: data held (50k lines of text, commit per line, original line numbers) | ~7 MB |
| Blame search `/` over 50k lines (text, author, subject; whole file scanned, off the UI thread) | 9-12 ms |
| File history, 100k-commit repository, file with 903 commits (`log --follow`, limit 1000) | ~0.5 s, off the UI thread |
| File history, 100k commits, path without history | ~0.17 s |

No hot spot was found, so no code changed. Caveat: with `diff.algorithm = histogram` in the owner's global git config, `git blame --incremental` of the same 50k-line file took ~7.9 s against ~0.2 s with the config ignored. Gitunia respects the user's config and the view stays usable meanwhile (lines first, commits as git finds them). Decision (#1136, owner): keep the default, no `diff.algorithm` override; the footer shows `blaming N%` while git runs (tested) and the user guide names the effect. Registered: the end-to-end AOT measurement as #1137. Not measured there: Windows, macOS, the AOT binary end to end. The AOT run is below.

### Blame view, AOT binary under tmux (#1137)

Linux, Native AOT Release build (17.7 MB), tmux 120x40, `scripts/gen-big-file-repo.sh` repo (`big.txt`, 50,000 lines, 501 commits), the blame arguments of #1125 (before #1136 changes them; this is the baseline). Repeat: `scripts/measure.sh <binary> <bigfile repo> --blame` (focuses the details pane, opens the blame of `big.txt` with `<space>gb`, then times `j` x20, `Ctrl-d` x20 and `G` until the frame changes; RSS of the process at the end). Three runs (one with a 5-run cold start, two with 3); same harness caveats as the other cases (tmux poll adds ~1-3 ms per sample).

| Case | Result |
|---|---|
| `<space>gb` until the first blamed rows (hash column filled) | 228-304 ms |
| `j` in the blame view | median 39-40 ms, p95 55-62 ms in two runs; 15-22 ms median in the first run (same binary and repo; repeat after #1136) |
| `Ctrl-d` (page) | median 29-44 ms, p95 40-56 ms |
| `G` (jump to line 50,000) | 24-71 ms |
| RSS with the view open | 70-73 MB (50 MB for the same repo in the graph view) |
| Cold start of the same repo (for scale) | median 76-98 ms |

Reading: the view is usable at once (first rows ~0.25-0.3 s, of which git's blame of the whole file is ~0.2 s, see above) and keys land within about two frames. Key latency is higher than the graph's `j` (~20 ms) (the poll itself adds a full-frame capture per sample, the cause in the view was not investigated); the numbers vary between runs by 2x, so treat them as order of magnitude. The view holds ~20 MB more RSS than the graph (not broken down; the 50 MB figure is from an earlier run of this same case on the same repo with the blame view not yet open). Not measured: Windows and macOS (an optional owner run of `scripts/measure.ps1` / `scripts/measure.sh --blame`; `--blame` is not in `measure.ps1`).

## Idle CPU (#909)

Cause: Terminal.Gui 2.5.0 runs its main loop unconditionally, `Application.MaximumIterationsPerSecond` times a second, each ending in `Task.Delay(..).Wait()` (a wakeup of the timer thread, a pool worker and the main thread, plus a size query and a draw check), and its input thread polls every 20 ms. The poller, the 2 s fingerprint and the UI timers are small next to this. Idle CPU is about proportional to the rate (measured at 100/s to 10/s; Linux, AOT, 5000-commit repository, 8 refs, 45 s windows, whole process, threads summed from `/proc/<pid>/task/*/stat`): 100/s 2.56%, 50/s 1.44%, 25/s 1.04%, 10/s 0.91%; 230-390 voluntary context switches per second on each of five threads at 100/s. The rate stays 100/s while working for key latency (the default 25/s gave ~34 ms, `docs/history/decisions.md`).

Fix: `IterationRateGovernor` keeps 100/s while the user is working and for 2 s after the last key, mouse event or paste, and drops to 5/s on an untouched screen (50/s until #1209, see "Idle output"); the first input after an idle spell is handled by one slower iteration, then the rate is back at 100/s.

| Case (AOT, 5000-commit repository, 60 s idle after a 15 s settle, whole process, `git status` children excluded) | Before | After |
|---|---|---|
| Idle CPU, 3 runs before / 2 after | 2.25, 2.35, 2.58% (median 2.35%) | 1.45, 1.63% |
| RSS | ~46 MB | ~46-47 MB |
| `j` after a 3 s pause (20 samples per round, two rounds, tmux poll included) | median 17.6 / 23.1 ms | median 26.8 / 32.4 ms (about +9 ms) |
| `j` 0.3 s after the previous key (same harness) | median 22.3 / 24.1 ms | median 22.9 / 19.3 ms (unchanged) |

With the `git status` children (~1%) the whole app idles at about 2.5%, so the ~2% bar is met for the Gitunia process alone, not for the total. Not measured: the latency of an automatic refresh or reload that finishes while the screen is idle (its redraw waits for the next iteration: at most ~20 ms at 50/s, up to ~200 ms at the 5/s idle rate since #1209), and Windows/macOS. Going lower costs first-key latency in proportion (25/s idle: ~1.0% but about +30 ms); a loop without idle wakeups needs a change inside Terminal.Gui (blocking wait on input and timers). Reproduce: sum fields 14+15 of every `/proc/<pid>/task/*/stat` twice, 60 s apart, in a private tmux.

## Idle output (#1207)

Check for the sibling project's pattern (Geeratya issue #744) (full-frame repaints at idle). Method: AOT build, 120x36 pty via `script`, private `XDG_STATE_HOME`/`XDG_CONFIG_HOME`, throwaway 3-commit repo, 14 s run, the 4 s startup capture subtracted. Counts of `ESC[1;1H` (cursor home) and `ESC[?25l` (cursor hide).

| Idle 10 s | Result |
|---|---|
| Frames with cell content | 0 |
| `ESC[1;1H ESC[?25l` pairs | 473 (~47/s, one per main-loop iteration at the 50/s idle rate) |
| Bytes | 5676 (28352 minus 22676 of the 4 s run; ~570 B/s, exactly 473 pairs of 12 bytes) |
| Mouse modes | `?1003h` (any-event) and `?1006h` once at start; no `?1002h` |

So it only partly reproduces: no full-frame repaint, but the 1 frame/s bar is not met. Cause: Terminal.Gui 2.5.0 writes cursor-home plus cursor-hide on every iteration even when nothing is dirty. `Console.SetOut` with a filter that drops repeated pairs had no effect (the pairs kept arriving: the driver does not write through `Console.Out`, see the spy-writer result below), so the fix needs a hook Terminal.Gui does not currently offer; see the next paragraph. `?1003h` is set by Terminal.Gui for mouse support; Gitunia has no hover feature, but the mode cannot be changed without the same hook. Not measured: the terminal CPU comparison (Ghostty vs Foot, needs a graphical session) and whether mouse moves cause repaints. The `docs/architecture.md` pitfall note and the `?1002h` switch stay with #1209, which remains open (needs-owner) because the cause cannot be fixed from Gitunia.

#1209 tried the hooks Terminal.Gui 2.5.0 offers and found none that reaches the output (AOT build, same capture method; a plain `Window` with no Gitunia code shows the same: 94 cursor-homes in 4 s at the default 25 iterations/s). The writes come from the per-iteration cursor update (`ApplicationNavigation.UpdateCursor`, documented as "called once per main loop iteration"), which ends in the driver's `IOutput`.

- `Console.SetOut` before `Init` is bypassed (a spy writer got no calls while the pty received the sequences), so the driver writes to its own stream.
- `IComponentFactory`/`IOutput` are public, but `Application.Init(name)` does not consult `DriverRegistry`: `Init("custom")` throws "Unknown driver name", and re-registering `ansi`/`dotnet` with a subclassed `AnsiComponentFactory`/`NetComponentFactory` whose `CreateOutput` wraps the outputis never called.
- `ApplicationImpl`, the only type that takes an `IComponentFactory`, is internal; `Application.Create` accepts only an `ITimeProvider`; `IApplication.Driver` has no setter.

So a decorating `IOutput` cannot be installed without reflection (not AOT-safe) or replacing the process's stdout at file-descriptor level (platform-specific, and the driver queries the terminal size on it). Options left: a Terminal.Gui change (idle cursor update skipped when the cursor is unchanged, or a public factory hook), or a lower idle iteration rate, which costs first-key latency (see "Idle CPU").

#1209 (owner decision: lower the idle rate as a stopgap). `IdleIterationsPerSecond` (`TerminalHost`) went from 50 to 5. Same method as above (AOT, 120x36 `script`, 10 s window, 3-commit throwaway repo), plus first key after idle: `j`/`k` alternating every 3.3 s of idle (past the 2 s `IdleAfter`), 25 samples, private tmux, time until the captured pane changes (tmux poll included):

| Idle rate | Cursor-home+hide pairs / 10 s | Bytes / 10 s | First key after idle (median / p95 / max) |
|---|---|---|---|
| 50/s (before) | 513 | 6156 | 31 / 47 / 51 ms |
| 10/s | 100 | 1200 | 94 / 106 / 119 ms |
| 5/s (chosen) | 51 | 612 | 102 / 127 / 138 ms |
| 2/s | 51 | 678 | 204 / 219 / 233 ms |

Idle writes drop about 10x (513 to 51 pairs, ~61 B/s). Below 5/s the writes do not fall (51 pairs at 2/s too; possibly the input thread or a Terminal.Gui timer still drives the loop, not investigated) while latency doubles, so 5/s is the floor. Cost: the first key after 2 s of idle waits up to ~200 ms for the slow iteration, median +71 ms, worst sample +87 ms, inside the +100 ms budget; later keys are back at 100/s. The 31 ms at 50/s is not comparable with the +9 ms in the #909 table (different pause, repository and harness). Idle CPU at 5/s was not measured. Background results (refresh, watcher, async operation completion) shown on an idle screen can wait up to ~200 ms for the next iteration; not measured. The bar of 1 frame/s is still not met (the fix is skipping the cursor update upstream, see the follow-up). `?1003h` against `?1002h`: at idle there is no mouse input, so the mode changes no output; it only adds reports (and wakeups) while the mouse moves over the window, which this measurement does not cover, and it still cannot be changed (same blocked hook).

## Many tabs

Only the active tab loads (lazy restore); a visited small tab adds ~0.5-1 MB. Background tabs get their dirty/ahead/behind markers from `BackgroundStatusPoller`: `git status` only, one tab at a time, once at start and then every 30 s, with no graph, refs or watchers loaded. An idle preload would only add memory and processes, so none is done.

## Background work (#993)

Polls of hidden tabs, the worktree overview, commit and diff prefetch and issue-link reads run through `BackgroundWork`: at most 8 at once (`DefaultLimit`, `Gitunia.App/Background/`), the rest wait. Each job runs its reads one after the other, so this also caps the processes background work can start at 8. User effects and the load of the shown tab bypass it. Measured on the 5000-commit repository (`scripts/measure.sh`, AOT, 120x40, two alternating rounds of main and the #993 branch): cold start 59-62 ms vs 60-68 ms, `j` key latency median 15-24 ms vs 17 ms, page scroll 17-23 ms vs 17 ms, RSS 52-53 MB both; the differences are inside the run-to-run noise of the harness. The queue only waits when more than 8 jobs overlap, which a single user's navigation does not reach.

## Shell state (#970)

Key mode, status messages, the confirm dialog and the output box are `AppState.Shell`, so a mode switch or a status message is now a dispatched command and raises `Dispatcher.Changed` (before, a mode switch only redrew the bar). Measured on the 5000-commit repository (`scripts/measure.sh`, AOT, 120x40, two alternating rounds of main and the #970 branch): cold start 71 and 58 ms vs 56 and 58 ms, `j` key latency median 16.5 and 15.7 ms vs 16.5 and 16.6 ms, page scroll 16.8 and 16.6 ms vs 16.4 and 16.7 ms, RSS 51-53 MB both; inside the run-to-run noise of the harness.

## Bounded memory

`EffectRunner` keeps the 5 most recently used loaded tabs (`DefaultWarmTabLimit`, the active one included). A tab beyond that, not running an operation, drops its session and goes back to pending; cursor, scroll and selected commit live in the workspace state and are restored by the background reload (graph search and diff position are lost). With 12 tabs of 20,000 commits, RSS after visiting all fell from ~125 MB to ~98 MB; going back to an evicted tab costs a reload (~100 ms), a recent tab stays instant. Memory is about 5 tabs x size, not the sum.

## Polling and watching

Cadence and the opt-in watching budget are specified in `docs/git-layer.md`, "Refresh". Cost: one `git status` read (~54 ms on a 1276-file checkout) per 5 s, about 1% of a core while the tab is active, nothing for a hidden tab; a repository whose status takes 2 s polls every 10 s (~20%, the back-off caps growth). Measured on Linux (#894, AOT build, 60 s idle after a 15 s settle, private tmux, CPU ticks from `/proc/<pid>/stat`, the Gitunia process only; the `git status` children add ~1% on top; reproduce with a clone of `scripts/gen-big-repo.sh 5000` plus `git update-ref --stdin` creating the refs, a published binary in a private tmux, and two reads of fields 14+15 of `/proc/<pid>/stat` 60 s apart): the fingerprint on a throwaway clone of the 5000-commit repository with 8,000 loose refs (5,000 branches, 3,000 tags) takes ~13 ms median (8-50 ms) against 0.1 ms with 8 refs (warm file cache; packed refs are one file and cheaper). Idle CPU of the whole app with the poller running is ~2.8% of a core with 8 refs and ~3.4% with 8,000 loose refs, so the fingerprint adds ~0.6% (the 50 ms maximum is a single spike in 30 timed runs after one warm-up, not a cold start); RSS 49 vs 64 MB. That ~2.4-2.8% baseline was above the ~2% bar set in #894; #909 found the cause and fixed it, see "Idle CPU" below. Per-group hashing (#1308) enumerates the same files per tick, minus other worktrees' index and logs, so the tick costs no more; Gitunia's own refreshes (stage, commit, manual) add one such read each, not measured separately. Ignore-aware watching starts in ~2 ms against ~48 ms recursive and registers ~900 of ~9,900 directories off-thread in ~333 ms; the inotify instance limit binds before the 2048-directory cap, then the watcher falls back to polling.

## Soft row limit (#515)

A tab loads its newest 100,000 commits (`GraphLoader.DefaultSoftRowLimit`); the rest loads on demand (`docs/git-layer.md`, "Refresh"). On a 1,000,000-commit log: newest rows on screen ~620 ms either way, RSS ~76 MB instead of ~390 MB, ~394 MB after `G` loads the rest (~10 s). `git log --topo-order` of the whole log alone takes ~1.9 s. The "continues below" row holds no commit; `GraphListSource.CommitIndexOf` gives `graph.Count` for it and consumers bounds-check.

## Files tab (commit tree)

Measured on Linux with the AOT binary (`--print-frame 120x36 --tab files`, one commit with 100 000 files in 20 x 50 x 100 directories): `ls-tree -r -z --long` alone 0.21 s, the whole frame (process start, read, parse, model, rows, render) 0.60 s. The tree is shown as "Loading..." meanwhile and the UI never waits. A filter walks every file once per keystroke: the worst case (a one-letter filter that matches all 100 000 files and their directories, 201 020 rows) took 72 ms in `Rows` plus 16 ms to build the pane rows in a Debug build (JIT), so a keystroke stays near 0.1 s; rows are not built lazily. Lazy per-directory loading was not needed at this size; revisit it if a 1M-file monorepo shows up.

## Conflict editor (#742)

The full-screen conflict editor (#742) works on the whole file. Measured with a throwaway xunit test (not committed; Release build of the Tui tests, JIT, this machine) on a 4,600-line file with 100 conflicts: opening it (alignment rows, result buffer, formats) 4 ms once; a frame of a 160 x 50 terminal 0.7 ms (only the visible rows are built and compared; mean of 500); a key typed into the result 0.05 ms (the line diff that keeps the conflict regions in step is linear in the file, 4,600 string comparisons plus a copy of the line list); a pick (with its undo snapshot, a copy of the line list too) 0.04 ms. The undo stack keeps at most 100 snapshots of the line list (references to shared strings), so about 4 MB at that size.
