# Performance

Targets and verified numbers. The checklist for writing hot-path code is the `performance` skill; the measurement harness is in `docs/testing.md`. Record new numbers here when a target is verified.

## Targets

- Cold start to first frame: < 100 ms with a warm git cache.
- Commit graph: open a 100k-commit repo and scroll without dropping frames. Load incrementally (`git log` streamed, paged); never materialize the whole graph eagerly in the UI.
- Many tabs: background tabs hold only compact state and are refreshed lazily or on a throttled timer; only the active tab does continuous work. Tab switch is instant (no reload).
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

Restoring many tabs costs no start-up time or memory beyond the active tab (lazy loading, #91); each visited small tab adds roughly 0.5-1 MB. Decision: background tabs do not load when idle; the first-visit cost is well inside the budget and an idle preload would only add memory and git processes. Background tabs get their dirty/ahead/behind markers from `BackgroundStatusPoller` (#129): `git status` only, one tab at a time, once at start and then every 30 s, with no graph, refs or watchers loaded. Larger repos cost more per visited tab (the 100k-commit figure below, ~73 MB, applies per tab of that size).

## Verified numbers (graph view, #22)

Native AOT linux-x64 binary (9.9 MB plus the 564 KB native library), 100k-commit repo from `scripts/gen-big-repo.sh`, `scripts/measure.sh` medians (they include 1-3 ms of tmux polling), Terminal.Gui at 100 iterations per second:

| Terminal | Cold start to first rows | `j` latency | `Ctrl-d` | 200 queued `j` settle | RSS with all 100k loaded |
|---|---|---|---|---|---|
| 80x24 | ~72 ms (p95 106) | ~7.5 ms | ~7 ms | ~15 ms | ~69 MB |
| 120x40 | ~80 ms (p95 101) | ~7.3 ms | ~7.6 ms | ~14 ms | ~73 MB |
| 200x60 | ~89 ms (p95 106) | ~8.8 ms | ~11.7 ms | ~16 ms | ~73 MB |

The first 150 commits are on screen at once; the whole log streams in and is laid out in ~0.8 s without blocking input (the status bar shows `n/total+` while loading). A search over all 100k loaded commits (`/` then a query with no match) takes ~70 ms in 5000-row slices and does not change key latency (`j` ~7.4 ms, cold start ~82 ms, RSS ~73 MB). Idle CPU after loading is ~1%. Memory per loaded commit is ~340 bytes (ad hoc measurement: managed heap growth for 100k synthetic `GraphEntry` values with short subjects, not a repeatable script; a compact `GraphEntry`, not the full `CommitInfo`); an automatic refresh that moves HEAD or refs reloads incrementally (~105 ms for one new commit instead of ~840 ms, #126; the manual `r` always reads everything); frame cost depends on the visible rows only, not on the number of loaded commits. A narrow window cuts the newest subject, so the harness's resize check cannot find its marker there (`resize_ms=nan` at 80x24 is that, not a failure to redraw). RSS re-check (#191): a measurement reported ~107 MB, most likely taken with a restored session of the user (extra tabs, saved scroll position; it could not be reproduced). With state isolated, the build before the graph columns (#168) and the current one both give ~75 MB, so columns and author date cost nothing measurable (a hypothesis for the 107 MB, not a proven cause; per row they add a 16-byte date and a reference to an already interned name, ad hoc estimate ~2.4 MB per 100k rows); `scripts/measure.sh` now isolates state. `jump_to_end_ms=nan` (#192) was a harness fault, not an app bug: default markers are now 20-character prefixes (long subjects are ellipsized, the newest also carries ref decorations) and the jump waits for the log to finish streaming, since `G` goes to the last loaded commit (~25-30 ms afterwards). At 80x24 the newest row is cut inside the marker, so pass an explicit `--marker "Commit #97500"` there. Known limit: the wide-character table in `TextWidth` covers CJK and the main emoji blocks only, so some newer emoji count as one column and can make a row one cell too wide.

Commit details while moving (#215): stepping to the next commit shows its details in ~20 ms (harness floor ~16 ms; was ~118 ms) because `CommitDetailsStore` reads the next 3 and previous 1 commits ahead into an LRU cache (64 entries, 20,000 changed files in total) and starts a selection that follows a pause without the 80 ms debounce. Holding a key still waits for the debounce (~110-130 ms to the last commit's details) and cancels stale reads. Details: `docs/decisions.md`.

Diff while moving (#225): with the diff pane beside the Changes file list, `j`/`k` to the filled diff took ~37 ms (details plus one diff run). `DiffStore` now keeps a bounded LRU `DiffCache` (12 diffs, 16,000 rows in total, a few MB; a diff over the budget alone is not cached) and, once the shown diff has loaded, reads the same neighbours as the details prefetch (3 ahead, 1 behind, one at a time, after an 80 ms pause, cancelled when the selection moves) with at most 4,000 rows each; bigger neighbours load on demand as before. Result ~20 ms (harness floor ~16 ms) on the 100k repo and on a repo of 3,000-row commits; cold start and RSS unchanged on the 100k repo (+~6 MB on the 3,000-row repo). A tab that is switched away from drops its cache. Details: `docs/decisions.md`.

Lane 0 for the current branch (#301): the layout reserves lane 0 for HEAD's first-parent chain (a comparison per row) and each graph load adds one `git log --no-walk --all` run concurrently with the log. 100k repo, AOT binary, `scripts/measure.sh --runs 7`, base and new alternating twice on a noisy machine: cold start median 88.7 and 104.8 ms before (the baseline itself varied that much) against 106.1 and 108.6 ms after, details step ~30-34 ms before against 33-38 ms after; no difference outside the noise.

