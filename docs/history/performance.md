# Performance: measurement history

Notes that backed the numbers in `docs/performance.md`: how they were measured, comparisons that were inconclusive, harness faults. Not needed for routine work.

- Graph view numbers: the original paragraph, verbatim (2026-10-16 split):

  The first 150 commits are on screen at once; the whole log streams in and is laid out in ~0.8 s without blocking input (the status bar shows `n/total+` while loading). A search over all 100k loaded commits (`/` then a query with no match) takes ~70 ms in 5000-row slices and does not change key latency (`j` ~7.4 ms, cold start ~82 ms, RSS ~73 MB). Idle CPU after loading is ~1%. Memory per loaded commit is ~340 bytes (ad hoc measurement: managed heap growth for 100k synthetic `GraphEntry` values with short subjects, not a repeatable script; a compact `GraphEntry`, not the full `CommitInfo`); an automatic refresh that moves HEAD or refs reloads incrementally (~105 ms for one new commit instead of ~840 ms, #126; the manual `r` always reads everything); frame cost depends on the visible rows only, not on the number of loaded commits. A narrow window cuts the newest subject, so the harness's resize check cannot find its marker there (`resize_ms=nan` at 80x24 is that, not a failure to redraw). RSS re-check (#191): a measurement reported ~107 MB, most likely taken with a restored session of the user (extra tabs, saved scroll position; it could not be reproduced). With state isolated, the build before the graph columns (#168) and the current one both give ~75 MB, so columns and author date cost nothing measurable (a hypothesis for the 107 MB, not a proven cause; per row they add a 16-byte date and a reference to an already interned name, ad hoc estimate ~2.4 MB per 100k rows); `scripts/measure.sh` now isolates state. `jump_to_end_ms=nan` (#192) was a harness fault, not an app bug: default markers are now 20-character prefixes (long subjects are ellipsized, the newest also carries ref decorations) and the jump waits for the log to finish streaming, since `G` goes to the last loaded commit (~25-30 ms afterwards). At 80x24 the newest row is cut inside the marker, so pass an explicit `--marker "Commit #97500"` there. (The wide-character table limit noted here was lifted in #99.)

  Commit details while moving (#215): stepping to the next commit shows its details in ~20 ms (harness floor ~16 ms; was ~118 ms) because `CommitDetailsStore` reads the next 3 and previous 1 commits ahead into an LRU cache (64 entries, 20,000 changed files in total) and starts a selection that follows a pause without the 80 ms debounce. Holding a key still waits for the debounce (~110-130 ms to the last commit's details) and cancels stale reads. Details: `docs/decisions.md`.

  Diff while moving (#225): with the diff pane beside the Changes file list, `j`/`k` to the filled diff took ~37 ms (details plus one diff run). `DiffStore` now keeps a bounded LRU `DiffCache` (12 diffs, 16,000 rows in total, a few MB; a diff over the budget alone is not cached) and, once the shown diff has loaded, reads the same neighbours as the details prefetch (3 ahead, 1 behind, one at a time, after an 80 ms pause, cancelled when the selection moves) with at most 4,000 rows each; bigger neighbours load on demand as before. Result ~20 ms (harness floor ~16 ms) on the 100k repo and on a repo of 3,000-row commits; cold start and RSS unchanged on the 100k repo (+~6 MB on the 3,000-row repo). A tab that is switched away from drops its cache. Details: `docs/decisions.md`.

  Lane 0 for the current branch (#301): the layout reserves lane 0 for HEAD's first-parent chain (a comparison per row) and each graph load adds one `git log --no-walk --all` run concurrently with the log. 100k repo, AOT binary, `scripts/measure.sh --runs 7`, base and new alternating twice on a noisy machine: cold start median 88.7 and 104.8 ms before (the baseline itself varied that much) against 106.1 and 108.6 ms after, details step ~30-34 ms before against 33-38 ms after; no difference outside the noise.

  Branches ahead of HEAD (#308): each graph load runs one more `git rev-list --parents --topo-order --reverse --ancestry-path --exclude=refs/stash --all ^HEAD`, started together with the tip read, and the first row waits for the slower of the two (the layout needs the set before the first commit). The walk must follow all parents (a `--first-parent` walk was ~20 ms but made the set depend on which refs reach a commit, which breaks incremental reload). 100k repo, HEAD at the main tip: it prints nothing and takes ~40 ms (the tip read ~3 ms), so the first row appears ~35-40 ms later than before; with HEAD 60k commits behind main it prints 60k lines, takes ~70-100 ms, and the span-based parser needs ~50 ms for them in a Debug build (ad hoc timing of 60k synthetic lines; a rare worst case, a stale branch checked out). End-to-end `scripts/measure.sh --runs 7` of the AOT binary, base and new alternating twice, was inconclusive on a noisy machine (cold start median 81.5 and 121.5 ms before against 114.8 and 110.4 ms after), so the git timings above are the evidence. Incremental reload fingerprints are unchanged: set membership depends only on a commit's first-parent chain, not on the refs.

## Per-issue measurement logs moved from performance.md (2026-10-30)

Verbatim sections from `docs/performance.md` before the compaction; the headline numbers stay there.


`scripts/measure-tabs.sh <binary> --tabs 12 --runs 7` builds throwaway repos (300 commits each) and a session file in a temp state directory, then restores them under tmux (120x40, Native AOT linux-x64, medians, 1-3 ms polling floor included):

| Metric | Result |
|---|---|
| Cold start to the first frame of the active tab, 12 tabs restored | ~50 ms (max of 7 runs 57; run `--runs 20` for a real p95) |
| RSS after start (only the active tab loaded) | ~36 MB (1 tab restored: ~52 ms, ~36.7 MB) |
| First visit of a background tab (`gt`, includes its load) | ~37 ms (max 48) |
| Switch back to an already loaded tab | ~10-30 ms (single sample, poll-bound) |
| RSS after visiting all 12 tabs | ~46 MB (20 tabs: ~50 MB) |

Restoring many tabs costs no start-up time or memory beyond the active tab (lazy loading, #91); each visited small tab adds roughly 0.5-1 MB (bounded to 5 loaded tabs, see below). Decision: background tabs do not load when idle; the first-visit cost is well inside the budget and an idle preload would only add memory and git processes. Background tabs get their dirty/ahead/behind markers from `BackgroundStatusPoller` (#129): `git status` only, one tab at a time, once at start and then every 30 s, with no graph, refs or watchers loaded. Larger repos cost more per visited tab (the 100k-commit figure below, ~73 MB, applies per tab of that size).

### Polling the active tab (#543)

Watching is opt-in, so by default the active tab polls (`docs/git-layer.md`, "Polling"): a fingerprint every 2 s and a `git status --porcelain=v2 -z` (read-only, `GIT_OPTIONAL_LOCKS=0`) every 5 s, backed off to at least 5 times the last refresh up to 60 s. Measured on Linux with this repository's checkout (1276 tracked files), mean of 10 runs: one status read takes ~54 ms, so the steady state is one such process per 5 s, about 1% of a core while the tab is active, and nothing for a hidden tab. A repository whose status takes 2 s polls every 10 s (about 20%, the back-off keeps it from growing). The fingerprint only lists the git directory root, `refs/` and `worktrees/`; it was not timed separately (tens of entries here, a repository with thousands of refs enumerates them every 2 s). The timer wakes the process once per refs interval (2 s) while a tab is visible. Idle CPU of the whole app was not re-measured with the poller and the large `testing`-skill repository was not used; the numbers above are estimates from the status run (follow-up measurement welcome).

### Working tree watching (#504, opt-in since #543)

Measured with a throwaway repo (git init, no remote), Linux, Debug build of the watcher tests project, `RepositoryWatcher` with `WorkingTreeDirectoryLister` against the old recursive mode. Tree: 450 tracked source directories (2 levels, one file each) plus an ignored `node_modules` of 1500 packages x 4 nested directories (9922 directories on disk in all).

| Metric | Ignore-aware | Recursive (Windows/macOS mode) |
|---|---|---|
| Directories on disk / watched | 9922 / 901 | 9922 / all (one inotify watch each) |
| `ls-files` listing | 22 ms | not used |
| `Start()` returns | ~2 ms | ~48 ms |
| Until every directory is watched | ~333 ms (off-thread) | ~48 ms |
| Longest wait of a gate-taking property read while watchers are created | 0.03 ms | n/a |

A tree with 1801 listed directories did not fit: the watcher fell back to polling, because each `FileSystemWatcher` holds one inotify instance and this machine allows 1024 per user (below the 2048-directory cap). On a system with the old default of 128 instances the limit is reached after ~100 directories. Registered as #543. Per-directory watchers were created while holding the gate; the lock now covers only their bookkeeping (`Start`, the recursive root fallback and the `worktrees` watcher still create under it, one to six watchers). One-off run, not scripted: the tree is built by a throwaway test, the gate wait is a single worst-case sample.

### Bounded memory with many tabs (#199)

Lazy restore already existed (#91: only the active tab loads). New: `EffectRunner` keeps the 5 most recently used loaded tabs (`DefaultWarmTabLimit`, the active one included). A tab beyond that, not loading, drops its session (rows, caches, watcher) and goes back to the pending state; cursor, scroll and selected commit live in the workspace state, so activating it reloads in the background and restores them. Measured with `scripts/measure-tabs.sh <binary> --tabs 12 --commits 20000 --runs 3`, Native AOT linux-x64, 120x40:

| Metric | Before | After |
|---|---|---|
| RSS after start (active tab only) | ~49.6 MB | ~47.4 MB |
| RSS after visiting all 12 tabs | ~125 MB | ~98 MB |
| First visit of a background tab | ~85 ms | ~88 ms |
| Switch back to a tab (script's last step: tab 2, evicted after) | ~26 ms (kept) | ~101 ms (reload of 20k commits) |

A tab running an operation (push, fetch, merge, rebase, ...) is never evicted, since closing its session cancels the work. An evicted tab loses its graph search and diff position (cursor, scroll and selected commit are kept). Switching to a recent tab is unchanged (instant, ~26 ms measured). Memory is now about (5 tabs x size), not the sum over all open tabs. Step 4, the soft row limit, followed in #515 (next section).

### Soft row limit for huge logs (#515)

A tab loads its newest 100,000 commits eagerly (`GraphLoader.DefaultSoftRowLimit`); a longer log is cut there. The status bar then reads `12/100000+ of 1204331` (selected, loaded, whole log; the total is one `rev-list --count` after the cut). The rest loads on demand and appends behind the shown rows: `G` (a plain one), a graph search that scans to the loaded end, a jump to a branch, tag or commit that is not loaded, a sidebar jump, and a restored selection that is not in the loaded rows. The rest is read by a second full `log` stream (the lane layout needs every row before), skipping the rows already shown after checking that the last shown commit sits at the same place; a log that changed meanwhile ends with `history changed while loading, press r to refresh`. A refresh of a cut tab reads the newest 100,000 rows again (no incremental splice, since the rows below the cut are not loaded) and its baseline pass skips the ref-decoration check (refs below the cut can never show). Once the rest is loaded the tab behaves as before.

Measured on `scripts/gen-big-repo.sh <dir> 1000000` (1,000,000 commits, linear main with merged branches, `commit-graph` file present), Native AOT linux-x64, 120x40 under tmux, one run each (before/after the change, same machine; `git log --topo-order` of the whole log alone takes ~1.9 s, `rev-list --count --all` ~0.36 s):

| Metric | Before (whole log) | After (cut at 100k) |
|---|---|---|
| Newest rows on screen | ~630 ms | ~620 ms |
| RSS once loading settled | ~383-396 MB | ~76 MB |
| After `G` (rest loaded, ~10 s) | n/a (all loaded) | ~394 MB (as before, only when asked for) |

A kernel-sized tab therefore costs what a 100k tab costs until the user asks for its end (~350 bytes per commit measured on the 1M log). The "continues below" marker (#555) is one extra list item after the last loaded row, `⋮ more commits below, G loads the rest` (`:` instead of `⋮` in ASCII), drawn while the log is cut and nothing loads. It holds no commit: `GraphListSource.CommitIndexOf` gives `graph.Count` for it and every consumer (copy hash, selection, range text, pick range, jumps) already bounds-checks that.

### Verified numbers (graph view)

Native AOT linux-x64 binary (9.9 MB plus the 564 KB native library), 100k-commit repo from `scripts/gen-big-repo.sh`, `scripts/measure.sh` medians (they include 1-3 ms of tmux polling), Terminal.Gui at 100 iterations per second:

| Terminal | Cold start to first rows | `j` latency | `Ctrl-d` | 200 queued `j` settle | RSS with all 100k loaded |
|---|---|---|---|---|---|
| 80x24 | ~72 ms (p95 106) | ~7.5 ms | ~7 ms | ~15 ms | ~69 MB |
| 120x40 | ~80 ms (p95 101) | ~7.3 ms | ~7.6 ms | ~14 ms | ~73 MB |
| 200x60 | ~89 ms (p95 106) | ~8.8 ms | ~11.7 ms | ~16 ms | ~73 MB |

- The first 150 commits are on screen at once; the whole log streams in and is laid out in ~0.8 s without blocking input (the status bar shows `n/total+` while loading).
- Search over all 100k loaded commits (a query with no match): ~70 ms in 5000-row slices; key latency unchanged. Idle CPU after loading ~1%. Memory per loaded commit ~340 bytes (a compact `GraphEntry`, not the full `CommitInfo`; ad hoc estimate). Frame cost depends on the visible rows only.
- Incremental reload after HEAD or refs move: ~105 ms for one new commit, against ~840 ms for a full read (the manual `r` always reads everything).
- Incremental reload when a branch ref moves onto a loaded commit (#795; `IncrementalGraphLoader` against `scripts/gen-big-repo.sh` 100k, Debug build, so a full read is ~850 ms like the Native AOT figure above, one run each): before, a branch created, moved or deleted on an old commit (a `GraphRow` that chose no lane colour) fell back to the full read, 940-1040 ms in total; now 90-125 ms (a new commit on top 140 ms, a checkout 130 ms), the result equal to a full read in every measured case. Still a full read (~1 s) when the branch name of a branch tip or of the commit that started a lane changes (a branch added on another branch's tip).
- Branch colours (#761): one more `git log --no-walk` per graph load, started with the log, plus one dictionary lookup per new lane. Cold start to the first rows on the 100k repo (`scripts/gen-big-repo.sh`, Native AOT, 120x40, 11 runs, two alternating rounds): median 69.5 / 67.2 ms before, 74.3 / 68.3 ms after; p95 77.5 / 76.3 before, 81.3 / 79.0 after (within the harness noise, at most a few ms). The incremental reload now waits for the same read before its first row; not re-measured (the git calls are the same as before).
- One lane per branch (#759): lanes now live to the fork commit, so a row loops over more open lanes. Cold start to the first rows, 100k-commit repo (Native AOT, 120x40, measure.sh --runs 11 --only-start), two alternating rounds against main: median 75.1 / 75.7 ms before, 74.7 / 72.0 ms after; p95 100.4 / 119.6 before, 103.5 / 97.6 after (no difference outside noise). With 50 stale branches forked 20,000 to 70,000 commits back (50 lanes open over tens of thousands of rows; same 100k repo plus 50 branches): cold start median 78.0 / 78.7 ms on `main`, 73.7 / 73.0 ms here; RSS after load 173 / 177 MB on `main`, 181 / 182 MB here (+4%); page scroll 16 ms on both. A lane already stayed open down to its fork before; only the last hop (a branch whose parent is the commit another lane waits for) used to bend early. The ~340 bytes per commit above hold for the repositories measured; every open lane adds one 16-byte edge per row.
- Commit details while moving: ~20 ms per step (harness floor ~16 ms; was ~118 ms) thanks to a prefetch of the next 3 and previous 1 commits into an LRU cache (64 entries, 20,000 changed files in total). Holding a key still waits for the 80 ms debounce (~110-130 ms to the last commit's details) and cancels stale reads.
- Diff while moving (diff pane beside the Changes file list): ~20 ms per step (was ~37 ms) thanks to a bounded LRU `DiffCache` (12 diffs, 16,000 rows in total) and a neighbour prefetch of the same shape with at most 4,000 rows each. Cold start and RSS unchanged on the 100k repo; a tab that is switched away from drops its cache.
- Known limit: at 80x24 the newest row is cut inside the harness marker, so pass `--marker "Commit #97500"` there (`resize_ms=nan` at that size is the marker, not a redraw failure).

How these were measured, harness faults, noisy-machine comparisons and the RSS re-check: `docs/history/performance.md`.

### Body search: `log --grep` (#509)

`body:` queries run `log -z --format=%H --regexp-ignore-case --fixed-strings --grep=<text> --exclude=refs/stash --all` (`CommitMessageSearcher`), off the UI thread after a 250 ms typing debounce. Measured on the 100k-commit repo from `scripts/gen-big-repo.sh` (warm cache, three runs each, the CLI alone, linux-x64):

| Query | Hits | Time |
|---|---|---|
| no match | 0 | ~380-430 ms |
| one feature branch's merges | 555 | ~380 ms |
| matches nearly every commit | 97,501 | ~750-785 ms |

The cost is the walk over every commit message, so it grows with the log and only slightly with the hit count. A refresh that replaces the graph restarts the search, but the restart cancels the earlier run first (at most one process per tab) and refreshes are already coalesced by the watcher debounce, so no extra throttle is added: the worst case is one ~0.4-0.8 s process per refresh while a body query is open, off the UI thread. Revisit if a much larger log shows otherwise.

### Startup: terminal background query (OSC 11, #527)

With `theme = auto` startup writes one OSC 11 query followed by a DA1 query (`ESC [ c`) and reads until the DA1 reply, which every terminal sends after the OSC 11 answer (or instead of it, when OSC 11 is unknown). The wait therefore ends with the terminal's own answer and a late OSC reply cannot reach the UI as keys; the 50 ms timeout only bounds a terminal that answers neither.

Measured with a throwaway pty harness (a Python script that forks the Native AOT linux-x64 binary on a 120x40 pty and replies like a terminal; not committed; single runs, a few ms of floor) as time to the first frame: a terminal answering OSC 11 and DA1, or DA1 only, ~26 ms; a terminal that never answers ~75-80 ms (the full timeout), still inside the 100 ms budget. Skipped for `theme` light or dark, no tty, `NO_COLOR`, Windows and `TERM` unset, dumb or linux.

### File content view, worst case (#580)

Since #635 the content follows the tree cursor: the worst case below applies per rest of the cursor on a big file, not per `Enter`. Reads are cancelled when the cursor moves on and start only after 40 ms of rest (not measured separately; the cost per rest is the one below, a held `j` reads only where it stops).

A 1 MiB blob of `"\n"` lines (the 1 MiB blob limit gives 1,048,576 rows, no row cap). Measured with a throwaway xUnit test (not committed; JIT, not the AOT binary), one run, this machine; the rows then share one empty string, and a file of `a` lines has half the rows with separate strings (not measured): building the document 50 ms and about 56 MB of rows; the wrap map at 100 columns 23 ms; searching for a string that never matches 11 ms; copying the whole file 16 ms. Scrolling was not timed: a frame renders the visible rows only, and the wrap map is the one per-document pass (rebuilt on a width change). The committed test only checks the row count and the copied text. No row cap was added: the numbers are well inside the budget and the memory is held only while the file is shown. Revisit if the blob limit is raised.

### One-file slice of the diff (#566)

`DiffDocument.ForFile` copies the rows of one file on every file-cursor move (on the UI thread). Measured with a throwaway xUnit test (not committed; JIT Debug build, not the AOT binary), a diff of two files (the first one 100,000 rows, the default per-file cap, no hunk rows), 5 runs after a warm-up, this machine: 2 to 9 ms per call (mostly 3 ms), one list and one `with` copy per row. Well inside the frame budget, so no cache or offset index was added. After "Load anyway" (`<space>vl`) the cost grows linearly with the file (a 400,000-row file is extrapolated to roughly 12 to 36 ms, not measured). Not measured: a diff with thousands of hunks (the call also scans all hunk rows once) and the folder view (`ForFiles`, same path). Revisit if either shows in a real use.

### Changed words in diff lines (#740)

`WordDiff.Compare` runs for the visible changed rows only, on every draw (no cache; wrapped rows compare once per screen line). Measured with a throwaway xunit test (not committed; Release build of the Core tests, JIT, not the AOT binary), this machine, mean of 200 runs after a warm-up: a typical edited code line (22 characters, one changed word) 0.002 ms; the worst case the bounds allow, two 389-character lines of 199 tokens with 50 changed words each (a 199 x 199 table, just under the 40,000-cell cap) 0.31 ms. A full screen of 40 such rows is therefore about 12 ms per frame (a pathological diff; extrapolated from the single-line figure, not timed as a frame); ordinary diffs cost well under 1 ms. Lines over 2000 characters, runs over 2000 changed lines and larger tables fall back to cheap paths (no marks, or the changed middle marked whole), so a minified file costs no more than a short one. Revisit with a per-row cache if a real diff shows it. The conflict editor (#764) compares per visible row ours with the base, theirs with the base and, for the base pane, both again: at most four `Compare` calls per row, same bounds, so a full screen of worst-case rows is a few times the figure above (not timed as a frame).
