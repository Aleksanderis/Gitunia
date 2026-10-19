# Performance

Targets and verified numbers. The checklist for writing hot-path code is the `performance` skill; the measurement harness is in `docs/testing.md`. Record new numbers here when a target is verified.

## Targets

- Cold start to first frame: < 100 ms with a warm git cache.
- Commit graph: open a 100k-commit repo and scroll without dropping frames. Load incrementally (`git log` streamed, paged); never materialize the whole graph eagerly in the UI.
- Many tabs: background tabs hold only compact state and are refreshed lazily or on a throttled timer; only the active tab does continuous work. Tab switch to a recent tab is instant (no reload); tabs beyond the 5 most recently used drop their graph and reload on activation.
- Memory: compact commit model (struct-of-arrays or interned hashes), virtualized lists.
- Render: diff against the previous frame, write one buffered flush per frame.

## Verified numbers (many restored tabs, #111)

`scripts/measure-tabs.sh <binary> --tabs 12 --runs 7` builds throwaway repos (300 commits each) and a session file in a temp state directory, then restores them under tmux (120x40, Native AOT linux-x64, medians, 1-3 ms polling floor included):

| Metric | Result |
|---|---|
| Cold start to the first frame of the active tab, 12 tabs restored | ~50 ms (max of 7 runs 57; run `--runs 20` for a real p95) |
| RSS after start (only the active tab loaded) | ~36 MB (1 tab restored: ~52 ms, ~36.7 MB) |
| First visit of a background tab (`gt`, includes its load) | ~37 ms (max 48) |
| Switch back to an already loaded tab | ~10-30 ms (single sample, poll-bound) |
| RSS after visiting all 12 tabs | ~46 MB (20 tabs: ~50 MB) |

Restoring many tabs costs no start-up time or memory beyond the active tab (lazy loading, #91); each visited small tab adds roughly 0.5-1 MB (bounded to 5 loaded tabs, see below). Decision: background tabs do not load when idle; the first-visit cost is well inside the budget and an idle preload would only add memory and git processes. Background tabs get their dirty/ahead/behind markers from `BackgroundStatusPoller` (#129): `git status` only, one tab at a time, once at start and then every 30 s, with no graph, refs or watchers loaded. Larger repos cost more per visited tab (the 100k-commit figure below, ~73 MB, applies per tab of that size).

## Working tree watching (#504)

Measured with a throwaway repo (git init, no remote), Linux, Debug build of the watcher tests project, `RepositoryWatcher` with `WorkingTreeDirectoryLister` against the old recursive mode. Tree: 450 tracked source directories (2 levels, one file each) plus an ignored `node_modules` of 1500 packages x 4 nested directories (9922 directories on disk in all).

| Metric | Ignore-aware | Recursive (Windows/macOS mode) |
|---|---|---|
| Directories on disk / watched | 9922 / 901 | 9922 / all (one inotify watch each) |
| `ls-files` listing | 22 ms | not used |
| `Start()` returns | ~2 ms | ~48 ms |
| Until every directory is watched | ~333 ms (off-thread) | ~48 ms |
| Longest wait of a gate-taking property read while watchers are created | 0.03 ms | n/a |

A tree with 1801 listed directories did not fit: the watcher fell back to polling, because each `FileSystemWatcher` holds one inotify instance and this machine allows 1024 per user (below the 2048-directory cap). On a system with the old default of 128 instances the limit is reached after ~100 directories. Registered as #543. Per-directory watchers were created while holding the gate; the lock now covers only their bookkeeping (`Start`, the recursive root fallback and the `worktrees` watcher still create under it, one to six watchers). One-off run, not scripted: the tree is built by a throwaway test, the gate wait is a single worst-case sample.

## Bounded memory with many tabs (#199)

Lazy restore already existed (#91: only the active tab loads). New: `EffectRunner` keeps the 5 most recently used loaded tabs (`DefaultWarmTabLimit`, the active one included). A tab beyond that, not loading, drops its session (rows, caches, watcher) and goes back to the pending state; cursor, scroll and selected commit live in the workspace state, so activating it reloads in the background and restores them. Measured with `scripts/measure-tabs.sh <binary> --tabs 12 --commits 20000 --runs 3`, Native AOT linux-x64, 120x40:

| Metric | Before | After |
|---|---|---|
| RSS after start (active tab only) | ~49.6 MB | ~47.4 MB |
| RSS after visiting all 12 tabs | ~125 MB | ~98 MB |
| First visit of a background tab | ~85 ms | ~88 ms |
| Switch back to a tab (script's last step: tab 2, evicted after) | ~26 ms (kept) | ~101 ms (reload of 20k commits) |

A tab running an operation (push, fetch, merge, rebase, ...) is never evicted, since closing its session cancels the work. An evicted tab loses its graph search and diff position (cursor, scroll and selected commit are kept). Switching to a recent tab is unchanged (instant, ~26 ms measured). Memory is now about (5 tabs x size), not the sum over all open tabs. Soft row limit (step 4) is deferred (#515): ~350 bytes per commit means ~400 MB for one 1.2M-commit tab (extrapolated from the 20k-commit measurement, not measured on a kernel clone; the deferral rests on that extrapolation and #515 asks to measure first).

## Verified numbers (graph view)

Native AOT linux-x64 binary (9.9 MB plus the 564 KB native library), 100k-commit repo from `scripts/gen-big-repo.sh`, `scripts/measure.sh` medians (they include 1-3 ms of tmux polling), Terminal.Gui at 100 iterations per second:

| Terminal | Cold start to first rows | `j` latency | `Ctrl-d` | 200 queued `j` settle | RSS with all 100k loaded |
|---|---|---|---|---|---|
| 80x24 | ~72 ms (p95 106) | ~7.5 ms | ~7 ms | ~15 ms | ~69 MB |
| 120x40 | ~80 ms (p95 101) | ~7.3 ms | ~7.6 ms | ~14 ms | ~73 MB |
| 200x60 | ~89 ms (p95 106) | ~8.8 ms | ~11.7 ms | ~16 ms | ~73 MB |

- The first 150 commits are on screen at once; the whole log streams in and is laid out in ~0.8 s without blocking input (the status bar shows `n/total+` while loading).
- Search over all 100k loaded commits (a query with no match): ~70 ms in 5000-row slices; key latency unchanged. Idle CPU after loading ~1%. Memory per loaded commit ~340 bytes (a compact `GraphEntry`, not the full `CommitInfo`; ad hoc estimate). Frame cost depends on the visible rows only.
- Incremental reload after HEAD or refs move: ~105 ms for one new commit, against ~840 ms for a full read (the manual `r` always reads everything).
- Commit details while moving: ~20 ms per step (harness floor ~16 ms; was ~118 ms) thanks to a prefetch of the next 3 and previous 1 commits into an LRU cache (64 entries, 20,000 changed files in total). Holding a key still waits for the 80 ms debounce (~110-130 ms to the last commit's details) and cancels stale reads.
- Diff while moving (diff pane beside the Changes file list): ~20 ms per step (was ~37 ms) thanks to a bounded LRU `DiffCache` (12 diffs, 16,000 rows in total) and a neighbour prefetch of the same shape with at most 4,000 rows each. Cold start and RSS unchanged on the 100k repo; a tab that is switched away from drops its cache.
- Lane 0 for the current branch: one comparison per row plus one `git log --no-walk --all` run concurrently with the log; no difference outside noise.
- Branches ahead of HEAD: one more `git rev-list --parents --topo-order --reverse --ancestry-path --exclude=refs/stash --all ^HEAD` per graph load, started with the tip read. With HEAD at the main tip it takes ~40 ms, so the first row appears ~35-40 ms later; with HEAD 60k commits behind main it prints 60k lines and takes ~70-100 ms (a rare worst case). The walk must follow all parents: a `--first-parent` walk was faster but made the set depend on which refs reach a commit, which breaks incremental reload.
- Known limit: at 80x24 the newest row is cut inside the harness marker, so pass `--marker "Commit #97500"` there (`resize_ms=nan` at that size is the marker, not a redraw failure).

How these were measured, harness faults, noisy-machine comparisons and the RSS re-check: `docs/history/performance.md`.

## Body search: `log --grep` (#509)

`body:` queries run `log -z --format=%H --regexp-ignore-case --fixed-strings --grep=<text> --exclude=refs/stash --all` (`CommitMessageSearcher`), off the UI thread after a 250 ms typing debounce. Measured on the 100k-commit repo from `scripts/gen-big-repo.sh` (warm cache, three runs each, the CLI alone, linux-x64):

| Query | Hits | Time |
|---|---|---|
| no match | 0 | ~380-430 ms |
| one feature branch's merges | 555 | ~380 ms |
| matches nearly every commit | 97,501 | ~750-785 ms |

The cost is the walk over every commit message, so it grows with the log and only slightly with the hit count. A refresh that replaces the graph restarts the search, but the restart cancels the earlier run first (at most one process per tab) and refreshes are already coalesced by the watcher debounce, so no extra throttle is added: the worst case is one ~0.4-0.8 s process per refresh while a body query is open, off the UI thread. Revisit if a much larger log shows otherwise.

## Startup: terminal background query (OSC 11, #527)

With `theme = auto` startup writes one OSC 11 query followed by a DA1 query (`ESC [ c`) and reads until the DA1 reply, which every terminal sends after the OSC 11 answer (or instead of it, when OSC 11 is unknown). The wait therefore ends with the terminal's own answer and a late OSC reply cannot reach the UI as keys; the 50 ms timeout only bounds a terminal that answers neither.

Measured with a throwaway pty harness (a Python script that forks the Native AOT linux-x64 binary on a 120x40 pty and replies like a terminal; not committed; single runs, a few ms of floor) as time to the first frame: a terminal answering OSC 11 and DA1, or DA1 only, ~26 ms; a terminal that never answers ~75-80 ms (the full timeout), still inside the 100 ms budget. Skipped for `theme` light or dark, no tty, `NO_COLOR`, Windows and `TERM` unset, dumb or linux.
