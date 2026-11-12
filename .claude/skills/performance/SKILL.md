---
name: performance
description: Performance and memory checklist for Gitunia hot paths (git output parsers, graph layout, rendering, caches, startup). Use when you write or review code that parses git output, lays out or scrolls the graph, renders per frame, caches data, or runs at startup, or when a change touches the numbers in docs/internal/performance.md.
---

# Performance and memory

Targets and verified numbers are in `docs/internal/performance.md`; read it first, do not repeat it here. Budgets: cold start to first frame under 100 ms, scrolling a 100k-commit graph without dropped frames, background tabs cheap, input never blocked by a git process.

## Measure before optimising

- Write the straightforward version first. Optimise only a path you measured. End-to-end: `scripts/measure.sh`, `scripts/measure-tabs.sh`, `scripts/gen-big-repo.sh`, `scripts/gen-big-file-repo.sh` (a 50k-line file for blame; tmux based; on Windows `scripts/measure.ps1` (`-Blame` too) and `scripts/measure-tabs.ps1` via psmux, numbers not comparable with tmux runs; `gen-big-repo.sh` is Linux/macOS only; contract in `docs/testing.md`). For a pure parser or layout function: an ad hoc timed loop over the output of the 100k-commit repo, with the command in the PR.
- Record before and after numbers (median plus p95, binary type, repo size) in the PR. An optimisation without a number is not accepted, except a simple, readable change on a path whose cost is clearly proportional to repository size (per commit, per file, per line, per frame); the PR must then say it was not measured. A change that makes code harder to read for an unmeasured gain is never accepted.
- Measure the Native AOT binary, not `dotnet run`: JIT and AOT numbers differ.

## What counts as a hot path

Parsers of `git log`, `status`, `diff` and `for-each-ref` output; graph layout; anything run per visible row or per frame; the key-to-frame path; startup before the first frame; caches and prefetch. Everything else (dialogs, one-off commands) favours clarity.

## CPU

- Stream, do not buffer: read git output incrementally and parse as it arrives; never materialise a whole 100k-commit log or a huge diff as one string.
- Do work once: no repeated scans of the same list per frame or per key; compute layout once per loaded slice and reuse it.
- Work proportional to what is visible: virtualize lists, lay out and render only visible rows.
- Chunk long loops (for example search over loaded commits in slices) so input stays responsive, and honour cancellation inside them.
- Avoid LINQ, capturing closures and `foreach` over interfaces in hot loops; use indexed loops over arrays or lists.
- Never block the UI thread; background work is async, cancellable and has a timeout (CLAUDE.md).

## Allocations

- Parse over `ReadOnlySpan<char>` or `ReadOnlySpan<byte>`; slice instead of `Substring` or `Split`. Allocate a string only for the part that is kept.
- Spans cannot cross `await` or be stored in classes: parse synchronously per record, then store the result.
- Reuse buffers (`ArrayPool<T>`, a reused `StringBuilder`, a per-frame buffer that is cleared, not recreated) where the lifetime is clear and the size is large or the path runs per frame. Always return pooled buffers and never keep a reference after returning.
- Struct versus class: small immutable data that lives in big arrays (per-commit layout, cell, row) is a `readonly record struct` (no tuples, see `.claude/rules/csharp-style.md`), kept small (a guideline, not a measured limit). Anything long-lived, shared, nullable or mutated is a class or record. Do not box (interfaces, `object`, non-generic collections).
- Hoist invariants: static readonly argument lists and option objects (also a rule in `.claude/rules/csharp-style.md`), precomputed delimiters and lookup tables, `static` lambdas instead of capturing ones in hot code.
- String interpolation stays the house style. Replace it with buffer writes or a cached string only in a per-frame or per-row loop where measurement shows it matters, and say so in the PR. The same applies to avoiding LINQ.
- Intern or share long-lived repeated strings (ref names, author names); store hashes compactly, not as a 40-char string per use.

## Caching and laziness

- Lazy by default: load a tab, a diff or a commit's details only when shown. Eager only for a small, bounded prefetch of neighbours, started after a pause and cancelled when the selection moves.
- Every cache has a bound (entry count and a size budget such as rows or bytes), an eviction rule (LRU) and an owner that drops it (tab switched away, tab closed). No unbounded dictionaries keyed by commit, path or file.
- State what invalidates it (ref change, refresh, worktree change) and test that stale data is never shown.
- A cache must save measured time; record its memory cost (RSS delta) next to the gain.

## Collections and strings

- Pre-size when the count is known (`new List<T>(capacity)`, dictionary capacity). Arrays or `List<T>` for dense, index-addressed data; `Dictionary` or `HashSet` only for real lookups.
- Flat arrays of structs (or struct-of-arrays) for per-commit data; avoid one object per commit plus child lists.
- Do not copy collections to guard against mutation in hot paths; expose `IReadOnlyList<T>` (index into it with a `for` loop) or a span over the same storage.
- Many concatenations: `StringBuilder` or span writes, never `+` in a loop. `string.Create`, `Span<char>` and `stackalloc` (small, bounded sizes only) for fixed shapes.
- Ordinal comparisons (`StringComparison.Ordinal`, ordinal dictionaries) for git data; culture-sensitive comparison is slow and wrong for refs and paths.
- Do not recompute display width per frame for unchanged text; cache it per row or string.

## Rendering

- Frame diffing and the single flush are specified in `docs/architecture.md`; add no per-cell allocations and no work for unchanged rows.
- Invalidate narrowly (the changed pane or row) on selection or data changes, not the whole screen.

## AOT and startup

- No reflection, runtime codegen or `dynamic`; use source generators (`System.Text.Json` source gen). Reflection-based serializers, `Activator` and expression compilation break or slow AOT.
- Startup path: nothing that is not needed for the first frame (no git calls for background tabs, no config scans, no regex construction, no large static initialisers, no assembly scanning). Defer the rest until after the first frame.
- Prefer source-generated regex or none; avoid generic instantiations over many value types (binary size, startup).
- After adding a dependency or touching anything reflection-adjacent, run the AOT publish (command in CLAUDE.md) and re-measure cold start against `docs/internal/performance.md`.
- Spawning `git` dominates cost: batch (one call yielding many NUL-separated records) instead of one process per item, and never poll on the UI thread.

## Reporting

In the PR body: path touched, how measured (command, repo, binary), before and after, memory (RSS) delta. If the budget is at risk and unmeasured, register a follow-up issue rather than guessing.
