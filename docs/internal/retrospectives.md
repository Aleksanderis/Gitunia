# Retrospectives

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Log of `/retrospective` runs: date, PR range, recurring groups, proposals applied or declined, instruction line counts.

## 2026-10-17 (PRs #324-#406, 40 PRs)

Recurring groups (distinct PRs): long lines 18, decisions entry missing/stale 15 (+3 too long), stale docs/hints 12, ordering 9, untested UI glue (skipped) 11, allocation nits (skipped) 9, wide-char nits (skipped) 3, minor nits (skipped) 7, destructive scope/concurrency/UI-glue/dead code 4/4/3/4. One owner comment ("Approve", #359).

Applied (owner approved all): P1 line-length wording, P2 decisions item first with trigger, P3-P6 reviewer demotions, P7 PR number on review comments, A1 `destructive` label removed, A2 stale #196, A3-A6 four memory files deleted (restated repo rules).

Line counts: CLAUDE.md 73, csharp-style 64, git-layer 19, ui-changes 32, reviewer 40, work-task 76, work-orchestrator 71, work-readiness 44, testing 83, performance 70, screenshots 15, retrospective 47 (total 634). Memory: 7 files down to 3.

## 2026-10-22 (PRs #518-#593, 40 PRs; #407-#517 not covered)

Recurring groups (distinct PRs): missing/weak tests 27, stale docs 14, decisions hygiene 11, races 11, scope-gap follow-ups 11, unmeasured perf 10, style nits 10, long lines 8, test hygiene 6, UI-thread IO 4, bare keys 2. No owner comments; 4 misposted review comments.

Applied: P1 and P2 (decisions and test hygiene checks), P6 and P7 (reviewer demotions), S1 (cleanup script rebases a local-ahead main), S2 (work-task: no rebase of pushed branches, clean screenshot dir), S4 (lean prompt: merge when review is clean), S5 (`.gitattributes` union merge for decisions.md). Declined/dropped: P3, P4, P8 (duplicates of existing rules or poor return). Held: P5.

Line counts: CLAUDE.md 74, csharp-style 64, git-layer 19, ui-changes 32, reviewer 40, work-task 78, work-orchestrator 71, work-readiness 44, testing 85, performance 70, screenshots 15, retrospective 47 (total 639).

Owner-approved docs-reduction: (1) decisions entries only for non-obvious choices or overrides, replacing 'log every behaviour change' and the reviewer's 'decisions entry missing' check; (2) user guide key tables replaced by a pointer to the in-app Key reference and the Keymap in `docs/ux.md`, replacing the keep-in-sync note; (3) reviewer checks docs only where the diff changes behaviour, replacing whole-doc stale sweeps.

## 2026-11-02 M7 impact report (effects-as-data, #990; issue #1031)

Baseline `468de95` (main before the design spec PR #998, no `Gitunia.App`). End `d151844` (main after #1054). Numbers from `scripts/m7-impact.sh` (raw `find`/`grep`/`wc`, no `tokei`/`cloc` available) and `git diff`. Code lines = non-blank, not starting with `//`. Tests = `[Fact]`/`[Theory]` attributes (a Theory counts once, not per data row). The performance, size, test-time and constructor numbers were first skipped and are in "Performance and constructor measurements" below (#1055).

### Totals (baseline..end)

| Area | Files changed | Insertions | Deletions | Net |
|---|---|---|---|---|
| `src/` | 798 | 37595 | 22817 | +14778 |
| `tests/` | 312 | 34953 | 14069 | +20884 |
| `docs/` | 9 | 301 | 38 | +263 |

`src/` + `tests/` files: 747 added, 76 deleted, 95 renamed.

### Code lines per project (code / raw / files)

| Project | Before | After |
|---|---|---|
| `src/Gitunia.Core` | 13315 / 19254 / 292 | 26142 / 38862 / 625 |
| `src/Gitunia.App` | none | 6339 / 8486 / 111 |
| `src/Gitunia.Git` | 9663 / 13252 / 244 | 9659 / 13262 / 250 |
| `src/Gitunia.Platform` | 535 / 828 / 19 | 536 / 832 / 19 |
| `src/Gitunia.Tui` | 35079 / 45939 / 337 | 23474 / 32597 / 424 |
| **src total** | 58592 | 66150 (+7558, +13%) |
| `tests/Gitunia.Core.Tests` | 12863 / 16040 / 121 | 27454 / 34539 / 194 |
| `tests/Gitunia.App.Tests` | none | 12390 / 15223 / 74 |
| `tests/Gitunia.Git.Tests` | 11400 / 14144 / 93 | 11412 / 14158 / 93 |
| `tests/Gitunia.Platform.Tests` | 330 / 412 / 8 | 330 / 412 / 8 |
| `tests/Gitunia.Tui.Tests` | 38597 / 47425 / 213 | 27866 / 34520 / 198 |
| **tests total** | 63190 | 79452 (+16262, +26%) |

### Size and shape (`src/`)

| Measure | Before | After |
|---|---|---|
| `TuiApplication.cs` lines | 2344 | 95 |
| `ActionRouter.cs` lines | 479 | gone |
| Largest `.cs` file | 2344 (`TuiApplication.cs`) | 504 (`Core/Keymap/ActionIds.cs`, a constant list) |
| Files over 500 lines | 26 | 1 (`ActionIds.cs`) |
| Files over 500 lines in Tui + App | 21 of the 26 are in `Gitunia.Tui` | 0 (largest: `Tui/App/Overlays/OverlayController.cs` 480, `App/EffectRunner.cs` 466) |
| `*Controller*.cs` files | 22 | 9 |
| `null!` occurrences | 44 | 1 |
| `Action<`/`Func<` tokens | 392 | 83 |
| `Action<`/`Func<` typed parameters (grep estimate) | 306 | 45 |
| 15th-largest file | 577 lines | 422 lines |

Top 5 before: `TuiApplication` 2344, `MainView` 1064, `ConflictEditorController` 1059, `DiffPaneController` 998, `BranchActions` 969. Top 5 after: `ActionIds` 504, `SettingsFrameBuilder` 494, `DiffListSource` 491, `ActionCatalog` 485, `OverlayController` 480.

### Tests

| Measure | Before | After |
|---|---|---|
| `[Fact]`/`[Theory]`: Core | 1099 | 2337 |
| App (new) | none | 651 |
| Git | 743 | 743 |
| Platform | 30 | 30 |
| Tui | 2360 | 1759 |
| **Total** | 4232 | 5520 (+1288) |
| Core + App tests (no terminal) | 1099 (26%) | 2988 (54%) |
| Tui tests | 2360 (56%) | 1759 (32%) |

Core + App tests are counted as "no terminal" tests; I did not classify each App test as sync vs async, so the 54% is an upper bound for purely synchronous reducer tests. `dotnet test` wall time: see the measurements section below.

### Verdict

| Question | Answer |
|---|---|
| Net code | `src/` grew 13% (+7.6k lines) and tests 26%: M7 added a layer (Core feature slices, `Gitunia.App` runner) rather than shrinking the program. |
| What moved | About 11.6k lines left `Gitunia.Tui` (35.1k to 23.5k); Core gained 12.8k and App 6.3k. Tui is now views, rendering and input. |
| Shape | God class (2344 to 95 lines), `ActionRouter` and all files over 500 lines in Tui/App are gone; late `null!` wiring 44 to 1; delegate parameters about 85% fewer. |
| Tests | 54% of tests need no terminal (was 26%); Tui tests fell by 600 as logic tests moved to Core and App. |
| Git and Platform layers | Unchanged (within 10 raw lines). |

### Against the "done when" line (`docs/internal/roadmap.md`)

| Criterion | Status |
|---|---|
| Every M7 issue closed | Not yet: open #1006 (overlay return mode; the RunActionCommand bridge half was removed by #1051), #1008 (`FilterPicker<T>`, `IPromptHandler`), #1042 (composition root follow-ups), #1045 (failing `OsTraitGuardTests`, flaky `CompareInputTests`), #1047 (last render-callback delegates in Tui views; in flight, another agent), plus this report and tracking issue #990. #1013 and #1020 are closed. |
| Feature logic covered by synchronous Core tests | Largely met (see tests table); not audited feature by feature. |
| No delegate dependencies | Not met: 83 `Action<`/`Func<` tokens remain, 45 typed parameters by grep estimate (some are legitimate, e.g. callbacks inside pure helpers); #1047 and #1008 cover part. |
| No late `null!` wiring | Met: the one left is an `out` default in `App/Loading/Commits/CommitDetailsCache.cs`, not late wiring. |
| No constructors over five parameters | Met for services: classes with more than five constructor parameters fell from 46 to 2 (`WorkingTreeActionHandler` and `DiffJumpRows`, 7 each). Not met for data: 82 records/structs (state, commands, effects, DTOs) have more than five, up from 52. See the measurements section. |
| No files over ~500 lines in `src/` | Met in practice: one file at 504 lines, a constant list. |
| Startup and key latency unchanged | Met: key latency and page scroll equal, cold start +8% (56.8 to 61.2 ms median, inside the run-to-run noise and far under the 100 ms target). See the measurements section. |

### Performance and constructor measurements (#1055, #1042)

Baseline `468de95` against `main` at `e062f2f` (after #1062). Both built as Native AOT linux-x64 Release (`dotnet publish src/Gitunia.Tui -c Release`), the baseline in a temporary worktree. Same machine (Linux, 14 threads), 5000-commit repository from `scripts/gen-big-repo.sh`, `scripts/measure.sh --runs 10 --keys 30`, 120x40, private `XDG_STATE_HOME`/`XDG_CONFIG_HOME`. Three rounds, baseline and main alternating; the table gives the median of the three round values (range in brackets). Tests: `dotnet build` then `dotnet test --no-build`, two runs each, alternating.

| Measure | Baseline `468de95` | `main` `e062f2f` | Change |
|---|---|---|---|
| Cold start median (ms) | 56.8 (56.3-60.0) | 61.2 (58.5-65.7) | +8% |
| Cold start p95 (ms) | 82.0 (67.0-83.1) | 77.1 (71.8-88.5) | inside noise |
| `j` key latency median (ms) | 16.7 (16.4-20.7) | 16.7 (15.0-16.7) | 0% |
| `j` key latency p95 (ms) | 35.4 (34.9-36.3) | 29.7 (26.5-33.3) | inside noise |
| Page scroll median (ms) | 17.4 (16.6-26.9) | 16.7 (15.7-17.2) | 0% |
| Commit details step median (ms) | 22.2 (21.4-28.1) | 28.0 (25.5-28.6) | +26%, flagged |
| 200 queued `j` settle (ms) | 24.9 (17.3-42.5) | 26.9 (25.4-33.3) | inside noise |
| Jump to end (ms) | 25.0 (16.4-25.9) | 27.8 (12.7-30.0) | inside noise |
| RSS after load (MiB) | 51.1 (50.5-51.2) | 55.1 (52.6-55.2) | +8% (+4.0 MiB) |
| AOT binary (`gitunia`, bytes) | 16,037,320 | 17,400,904 | +8.5% (+1.3 MiB) |
| `dotnet test` wall time, no build (s) | 10.7 / 9.7 | 12.1 / 10.3 | +10% on average, with 1288 more tests |
| Classes with more than 5 constructor parameters | 46 (max 17) | 2 (max 7) | -96% |
| Records/structs with more than 5 parameters (state, commands, effects, DTOs) | 52 | 82 | +30 |

Counts of constructors come from `scripts/ctor-params.py` (a regex scan of primary and explicit constructors, not a parser; "class" means a class, "data" a record or struct). The classes that remain: `WorkingTreeActionHandler` (7, the issue #1042 late-wiring item) and `DiffJumpRows` (7, was 7). The 15 largest `src/` files by lines, baseline: `TuiApplication` 2344, `MainView` 1064, `ConflictEditorController` 1059, `DiffPaneController` 998, `BranchActions` 969, `RepositoryWatcher` 917, `CommitActions` 865, `ConflictEditorState` 809, `OverlayController` 664, `PushActions` 631, `SettingsController` 625, `DiffRowRenderer` 597, `MessageEditor` 591, `ConflictEditorFrameBuilder` 589, `RebaseEditorController` 577. Main: `ActionIds` 504, `SettingsFrameBuilder` 494, `DiffListSource` 491, `ActionCatalog` 485, `OverlayController` 480, `DetailContentBuilder` 479, `SettingsReducer` 472, `EffectRunner` 466, `PaneHints` 464, `PaletteTable` 455, `MainView` 454, `WorkingDiffController` 449, `DiffListActions` 446, `DiffPaneInput` 432, `CommitReducer` 422.

Verdict: startup and key latency are unchanged in practice. Key latency and page scroll are equal, cold start is about 4-5 ms slower (+8% by median, +7% by mean, rounds overlap), far under the 100 ms target. Flagged above 10%: the commit details step (+26%, 22 to 28 ms; the ranges touch at 28 ms and the metric swings 21-28 ms between rounds of one binary, so it is suspect rather than proven; follow-up #1071 re-measures it and looks for the cause) and test wall time (+10% for 30% more tests). Memory and size grew moderately (RSS +4.0 MiB, binary +1.3 MiB), consistent with the added `Gitunia.App` layer and the effect/command types; there is no AOT trimming warning. The documented numbers in `docs/internal/performance.md` still hold, so it is unchanged. Single machine, three rounds: differences under ~10% are not distinguishable from harness noise.
