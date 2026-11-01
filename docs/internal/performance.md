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

## Idle CPU (#909)

Cause: Terminal.Gui 2.5.0 runs its main loop unconditionally, `Application.MaximumIterationsPerSecond` times a second, each ending in `Task.Delay(..).Wait()` (a wakeup of the timer thread, a pool worker and the main thread, plus a size query and a draw check), and its input thread polls every 20 ms. The poller, the 2 s fingerprint and the UI timers are small next to this. Idle CPU is about proportional to the rate (Linux, AOT, 5000-commit repository, 8 refs, 45 s windows, whole process, threads summed from `/proc/<pid>/task/*/stat`): 100/s 2.56%, 50/s 1.44%, 25/s 1.04%, 10/s 0.91%; 230-390 voluntary context switches per second on each of five threads at 100/s. The rate stays 100/s while working for key latency (the default 25/s gave ~34 ms, `docs/history/decisions.md`).

Fix: `IterationRateGovernor` keeps 100/s while the user is working and for 2 s after the last key, mouse event or paste, and drops to 50/s on an untouched screen; the first input after an idle spell is handled by one slower iteration, then the rate is back at 100/s.

| Case (AOT, 5000-commit repository, 60 s idle after a 15 s settle, whole process, `git status` children excluded) | Before | After |
|---|---|---|
| Idle CPU, 3 runs before / 2 after | 2.25, 2.35, 2.58% (median 2.35%) | 1.45, 1.63% |
| RSS | ~46 MB | ~46-47 MB |
| `j` after a 3 s pause (20 samples per round, two rounds, tmux poll included) | median 17.6 / 23.1 ms | median 26.8 / 32.4 ms (about +9 ms) |
| `j` 0.3 s after the previous key (same harness) | median 22.3 / 24.1 ms | median 22.9 / 19.3 ms (unchanged) |

With the `git status` children (~1%) the whole app idles at about 2.5%, so the ~2% bar is met for the Gitunia process alone, not for the total. Not measured: the latency of an automatic refresh or reload that finishes while the screen is idle (its redraw waits at most one 20 ms iteration longer), and Windows/macOS. Going lower costs first-key latency in proportion (25/s idle: ~1.0% but about +30 ms); a loop without idle wakeups needs a change inside Terminal.Gui (blocking wait on input and timers). Reproduce: sum fields 14+15 of every `/proc/<pid>/task/*/stat` twice, 60 s apart, in a private tmux.

## Many tabs

Only the active tab loads (lazy restore); a visited small tab adds ~0.5-1 MB. Background tabs get their dirty/ahead/behind markers from `BackgroundStatusPoller`: `git status` only, one tab at a time, once at start and then every 30 s, with no graph, refs or watchers loaded. An idle preload would only add memory and processes, so none is done.

## Background work (#993)

Polls of hidden tabs, the worktree overview, commit and diff prefetch and issue-link reads run through `BackgroundWork`: at most 8 at once (`DefaultLimit`, `Gitunia.App/Background/`), the rest wait. Each job runs its reads one after the other, so this also caps the processes background work can start at 8. User effects and the load of the shown tab bypass it. Measured on the 5000-commit repository (`scripts/measure.sh`, AOT, 120x40, two alternating rounds of main and the #993 branch): cold start 59-62 ms vs 60-68 ms, `j` key latency median 15-24 ms vs 17 ms, page scroll 17-23 ms vs 17 ms, RSS 52-53 MB both; the differences are inside the run-to-run noise of the harness. The queue only waits when more than 8 jobs overlap, which a single user's navigation does not reach.

## Bounded memory

`EffectRunner` keeps the 5 most recently used loaded tabs (`DefaultWarmTabLimit`, the active one included). A tab beyond that, not running an operation, drops its session and goes back to pending; cursor, scroll and selected commit live in the workspace state and are restored by the background reload (graph search and diff position are lost). With 12 tabs of 20,000 commits, RSS after visiting all fell from ~125 MB to ~98 MB; going back to an evicted tab costs a reload (~100 ms), a recent tab stays instant. Memory is about 5 tabs x size, not the sum.

## Polling and watching

Cadence and the opt-in watching budget are specified in `docs/git-layer.md`, "Refresh". Cost: one `git status` read (~54 ms on a 1276-file checkout) per 5 s, about 1% of a core while the tab is active, nothing for a hidden tab; a repository whose status takes 2 s polls every 10 s (~20%, the back-off caps growth). Measured on Linux (#894, AOT build, 60 s idle after a 15 s settle, private tmux, CPU ticks from `/proc/<pid>/stat`, the Gitunia process only; the `git status` children add ~1% on top; reproduce with a clone of `scripts/gen-big-repo.sh 5000` plus `git update-ref --stdin` creating the refs, a published binary in a private tmux, and two reads of fields 14+15 of `/proc/<pid>/stat` 60 s apart): the fingerprint on a throwaway clone of the 5000-commit repository with 8,000 loose refs (5,000 branches, 3,000 tags) takes ~13 ms median (8-50 ms) against 0.1 ms with 8 refs (warm file cache; packed refs are one file and cheaper). Idle CPU of the whole app with the poller running is ~2.8% of a core with 8 refs and ~3.4% with 8,000 loose refs, so the fingerprint adds ~0.6% (the 50 ms maximum is a single spike in 30 timed runs after one warm-up, not a cold start); RSS 49 vs 64 MB. That ~2.4-2.8% baseline was above the ~2% bar set in #894; #909 found the cause and fixed it, see "Idle CPU" below. Ignore-aware watching starts in ~2 ms against ~48 ms recursive and registers ~900 of ~9,900 directories off-thread in ~333 ms; the inotify instance limit binds before the 2048-directory cap, then the watcher falls back to polling.

## Soft row limit (#515)

A tab loads its newest 100,000 commits (`GraphLoader.DefaultSoftRowLimit`); the rest loads on demand (`docs/git-layer.md`, "Refresh"). On a 1,000,000-commit log: newest rows on screen ~620 ms either way, RSS ~76 MB instead of ~390 MB, ~394 MB after `G` loads the rest (~10 s). `git log --topo-order` of the whole log alone takes ~1.9 s. The "continues below" row holds no commit; `GraphListSource.CommitIndexOf` gives `graph.Count` for it and consumers bounds-check.

## Files tab (commit tree)

Measured on Linux with the AOT binary (`--print-frame 120x36 --tab files`, one commit with 100 000 files in 20 x 50 x 100 directories): `ls-tree -r -z --long` alone 0.21 s, the whole frame (process start, read, parse, model, rows, render) 0.60 s. The tree is shown as "Loading..." meanwhile and the UI never waits. A filter walks every file once per keystroke: the worst case (a one-letter filter that matches all 100 000 files and their directories, 201 020 rows) took 72 ms in `Rows` plus 16 ms to build the pane rows in a Debug build (JIT), so a keystroke stays near 0.1 s; rows are not built lazily. Lazy per-directory loading was not needed at this size; revisit it if a 1M-file monorepo shows up.

## Conflict editor (#742)

The full-screen conflict editor (#742) works on the whole file. Measured with a throwaway xunit test (not committed; Release build of the Tui tests, JIT, this machine) on a 4,600-line file with 100 conflicts: opening it (alignment rows, result buffer, formats) 4 ms once; a frame of a 160 x 50 terminal 0.7 ms (only the visible rows are built and compared; mean of 500); a key typed into the result 0.05 ms (the line diff that keeps the conflict regions in step is linear in the file, 4,600 string comparisons plus a copy of the line list); a pick (with its undo snapshot, a copy of the line list too) 0.04 ms. The undo stack keeps at most 100 snapshots of the line list (references to shared strings), so about 4 MB at that size.
