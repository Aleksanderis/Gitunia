# UI consistency audit and plan

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Audit of 2026-11-17 (read-only, owner request after the same audit in the sister project Geeratya), approved by the owner the same day; the work is one issue per phase in the rolling milestone M11 (section f). Line numbers are as of commit `eb73a24` and drift; search for the named type or text.

Seven parallel read-only audits covered forms, pickers and overlays, lists and values, colours, keys, mouse and hints, and the drawn screens. The last one ran the real AOT binary on throwaway repositories and captured ANSI frames in four modes: truecolor, 16 colours, NO_COLOR and ASCII. The orchestrator re-checked every claim marked ✓ in the code or in the captured frames. Claims without ✓ come from one agent's reading: re-check them before acting on them.

## (a) What is shared today, and where it is bypassed

| Shared piece | Used by | Bypassed by |
|---|---|---|
| `OverlayBox` (box, title bar, footer, wrap) | every overlay builder | a title with a right-aligned count is hand-built twice (`PromptPopupFrameBuilder.cs:88-105`, `PickerComposer.cs:79-93`) |
| `PromptTextEditor` (one-line editing) | about 15 fields: prompts, picker, help, settings, searches, filters, jumps | ✓ the command palette only appends text and drops the last code point on Backspace (`PaletteState.cs:42-44`): no caret, no word motions, no Ctrl-w |
| `CaretField` (one-line caret cell) | prompts, picker, sidebar, help, settings, blame, reflog, find bar | ✓ the palette draws a literal `_` caret (`PaletteFrameBuilder.cs:35`). ✓ Callers choose the text role freely, so typed text appears in 4 roles (see c1) |
| `MessageEditor` (multi-line) | commit box, conflict result, reword | features depend on the host: undo only in the commit box, no copy or cut in reword, undo turned off in conflict Insert (`ConflictEditorState.Editing.cs:101-104`) |
| `ListMotion` (j/k, paging, gg/G) | most lists and reducers | conflict editor half page (`ConflictActions.cs:153-157`, ignores the count); own move code in palette, picker plan, rebase move, reflog ref picker and prompt picker |
| `HintBar` / `OverlayBox.Hints` / `KeyedChip` + `ChipKeys` | status bar, banner, overlay footers, title chips | ✓ the palette footer (`PaletteFrameBuilder.cs:74-81`). Key and label rows with literal keys: Blame (`:88-117`), Reflog (`:22-27`), File history (`:36-37`), Confirm (`:66-73`), Settings yes/no (`:22-23`), Mainline `[n]`. Hand-built brackets: `[+]`/`[x]` (`SettingsRowText.cs:28-33`) and Help `[Tab] n/m` |
| `ShortHash`, `CountText.Plural` | Core texts, blame, reflog, rebase | ✓ the hash length 7 is hard-coded 4 times (`GraphRowRenderer.cs:14`, `DetailContentBuilder.cs:20`, `FileHistoryFrameBuilder.cs:31`, `GraphColumnLayout.cs:33`). Hand-built plurals in about 10 places (`RebaseCounts.cs:7`, `ConflictReducer.cs:439`, `HelpBuilder.cs:266`, `RangeCopier.cs:76/100/129`, ...) |
| `Palette` / `PaletteTable` | everything (no hard-coded colours in views, which is good) | roles are picked for how they look, not what they mean (see c1). ✓ `SegmentRole.Button` is mapped but never used |
| `DefaultMouseMap` plus a test that the table matches it exactly | every mapped gesture | the gestures handled outside the map (`ux-mouse.md` prose, lines 169-208: resizing, focus clicks, overlay row clicks, click outside to cancel) are not tested |
| Glyph tables with ASCII sets | graph, picker, sidebar | ✓ `HelpBuilder.cs:24` `"✗ / (x)"` has no ASCII fallback |

## (b) Duplicated code

| What | Copies | Where |
|---|---|---|
| ✓ "Keep the selected row visible" | 12 | centred: `PickerRows.cs:17`, `PromptPopupFrameBuilder.cs:79`, `GroupedRows.cs:62`, `ReflogFrameBuilder.cs:223` (identical to) `FileHistoryFrameBuilder.cs:205`, `RebaseEditorFrameBuilder.cs:251`, `SettingsFrameBuilder.cs:529`, `BlameFrameBuilder.cs:182`, `HelpFrameBuilder.cs:314`. Follows the edge: ✓ `MessageEditorRenderer.cs:25` (identical to) `ConflictEditorState.Scrolling.cs:58`, `WorkingTreePaneRenderer.cs:234`. Bottom-anchored: `PickerLayout.cs:94`. Also Terminal.Gui `EnsureSelectedItemVisible` on graph, diff and details, with hand-written viewport fixes (`GraphTabBinder.cs:195`, `DiffListActions.cs:278`). The prompt picker list is windowed twice (`PickerRows.Window`, then `FitRows`) |
| Window records | 3 | `PaletteWindow` and `MenuWindow` (same shape), `PickerWindow` |
| ✓ Fuzzy matcher | 2 classes | `Core/Search/FuzzyMatcher.cs` and `Core/Overlays/FuzzyMatcher.cs`, with different scoring |
| ✓ Contains filters | 6 | `HelpBuilder:172`, `ReflogState:57`, `ReflogRefPicker:25`, `SettingsState:286`, `FileTreeModel:147`, `SidebarBuilder:158` |
| ✓ List cursor | 3 that wrap, 5+ that stop at the ends | wrap: `PromptPicker.cs:49`, `ReflogRefPicker.cs:44`, `RefJumpState.cs:25`. Stop: `PaletteState.cs:50`, `PickerListPlan.cs:192`, `RebaseEditorState.cs:116`, `FileHistoryState.cs:40`, `SettingsState.cs:161` |
| Clamp-cursor helpers in state types | 6 | Sidebar, Reflog, FileHistory, Blame, Rebase, Settings |
| Editor key tables | 2 | `PromptTextEditor.cs:57-80` mirrors `MessageEditor.Keys.cs:132-193` |
| Caret drawing | 3 | `CaretField`, `MessageEditorRenderer`, the palette's `_` |
| "n/m" position text | 7+ formats | `ChromeBuilder.cs:120`, `PickerRows.cs:30`, palette footer, Reflog, File history, Blame, Help `a-b/total`, picker "N repositories", settings "N matches", ref jump `(p/t)`. Placed in the title in some overlays and in the footer in others |
| Status letter switch | 2 | `DetailContentBuilder.cs:462`, `WorkingTreeRowRenderer.cs:247` |
| Ahead/behind text | 2 | `SidebarRowRenderer.cs:123`, `TabBarLayout.cs:158` |
| Hint declarations per surface | 2 per surface | `PaneHints` (status bar) and the overlay's own footer, for Reflog, FileHistory, Blame, Palette, Confirm, Settings and Rebase |
| Overlay width limits | 7 constants | 96 / 100 / 110, plus Blame at full width with no margin |

## (c) Differences a user would notice

**1. Colour**
- ✓ **Typed text** in prompt popups is yellow bold. `PromptPopupFrameBuilder.cs:109` passes `OverlayKey`, which maps to `Warn`. This is the Geeratya bug again. Elsewhere typed text is plain (picker, sidebar, settings, editors), muted on the bar (find bar, reflog and blame search) or inside the title chip (help filter, palette). The captures confirm all four.
- ✓ **Key labels** come in three colours: blue bold (status bar, footers: `StatusKey` maps to Accent), yellow bold (palette, help, which-key, menu, Confirm: `OverlayKey` maps to Warn, documented in `ux-look.md:38`), and default bold (title chips). Yellow is also the colour of tags, modified files and warnings.
- **Empty or no-match text**: palette muted, help and picker default colour.
- **Group headers**: the palette's are plain, the help's are blue bold.
- ✓ **Settings warning marker** uses the `Error` role (`SettingsFrameBuilder.cs:338`), so it is red there and yellow on the status bar.
- **Resolve Confirm**: the reassuring undo hint is red bold (`working-resolve` row 18).
- **Conflict editor**: the title shows "ours" in green, but ours content is red and theirs content green, so one side has two colours.
- **Loading** uses 3 roles (Faint, Status, StatusProgress). **Dividers** use Faint or FocusedBorder (conflict editor, settings).
- **Directory names** are drawn as `SectionTitle`, so they look like headings. Accent carries about 9 meanings.
- **Colour tiers:**
  - ✓ Light 16-colour: Accent and Remote are both blue (`PaletteTable.cs:331,334`).
  - Classic console: Accent and Info are both bright cyan.
  - 16-colour: muted text, hints, borders and the focused selection bar are all bright black.
  - NO_COLOR:
    - Underline means three things: an unfocused selection, a dim pane title and a hint chip.
    - An over-long commit subject is bold only.
    - All four status severities look the same (bold).
    - The current conflict's extra rows are marked only by the gutter.

**2. Wording**
- **Esc** is called close, back, cancel, clear, clear filter, restore, keep, end compare, stop writing, done.
- **Enter for "select the commit in the graph"** is called select, graph, commit, "select the commit in the graph", "show the line's commit in the graph".
- **Ctrl-g** is called keep, keeps the message, Commit.
- ✓ The **conflict insert hint** uses "keep" for both Esc and Ctrl-g, and repeats itself ("Esc or Ctrl-g keeps the text  Esc keep  Ctrl-g keep").
- ✓ **No match**: "No match", "No match: x", "No match for x", "No match in view", "No matching branch", "no matching branch", "no matching action", "Nothing matches the filter." (with a full stop).
- **Empty or unchanged prompt answers**: about 8 different phrasings, such as "No name: nothing was created", "Same or empty name: nothing was renamed", "Type something first", "No branch given".
- **"No search"**: 3 phrasings.
- **Filter hints**: "type to filter" versus "Type filters".
- **Hint gap**: two spaces versus three in the prompt popup.
- ✓ The **conflict title** says "4 unresolved  unresolved, still holds its markers".

**3. Behaviour (keys)**
- ✓ **Esc in multi-line editors**: the commit box and conflict result keep the text, but reword silently discards it (`RebaseEditorReducer.cs:254`). Settings input also drops silently.
- **Rejected prompt answers**: the prompt closes before the answer is checked, so a rejected answer loses the typed text. Only clone and init re-ask.
- **List ends**: k at the top wrapped in the branch, tag and remote pickers, the reflog ref picker and ref jump; decision 9 is applied (#1445): every list stops at its ends.
- **Scrolling**: panes scroll only as far as needed, overlays keep the selection centred.
- **Tab in pickers** moves down (hard-coded, `PickerKeys.cs:21`). In the repository opener it completes a path. Shift-Tab does nothing.
- **j/k** type text in pickers, but move in ListOnly prompts, which also take h/l.
- **Save** is Ctrl-s in the conflict editor, Ctrl-g in the commit box and reword, and Ctrl-g also means "done" in conflict Insert.
- ✓ **q in conflict Visual** closes the editor (`ConflictActions.cs:126`). `ux.md:75` says q ends Visual first.
- **q versus Esc depth**: in Blame and Settings, q closes the whole screen while Esc steps back. The help's q skips the filter step. Confirm ignores q.
- **Rebase editor Enter** starts the whole rebase, while Enter drills in everywhere else.
- **Paging and jumps**: the palette, picker and menus have no Ctrl-d/u or gg/G. The commit box has Ctrl-d/u but no gg/G.
- **Keys outside the keymap** (not remappable): `PickerKeys`, the ListOnly letters, `ReflogReducer.cs:315`, `RefJumpController.cs:84`.

**4. Mouse**
- **Prompt popup**: no click targets at all, which breaks the "nothing keyboard-only" rule. Also keyboard-only: settings `/`, `J/K` and `+/-`; blame and reflog search; the status-bar `?` and `q` hints.
- **Wheel**: 3 rows in lists and most overlays, 1 row in the palette, menus and picker. The commit box has its own `WheelLines` constant.
- **Clicks on the banner or a title row** do not close an open overlay, although `ux-mouse.md:175` says a click on a pane or the tab row does.

**5. Drawing glitches (all ✓ in the captures)**
- At 64 columns, settings, picker and prompt draw double borders `││`, junctions `├│ … │┤` and `╰╰──╯╯`. Popup title rows have no left border.
- The MERGING banner replaced the Changes pane's bottom border (`working`, `working-narrow`). Fixed (#1482): the banner has a row of its own below the frame.
- In ASCII mode, adjacent chips lose a bracket: `[view v]A stage all]`.
- In the reflog, the message column moves one cell right at `HEAD@{10}`. The selector width is not padded.
- The which-key title chip leaves the rest of its row blank, and the right border is missing (about 75 % sure; may be a capture artefact).

**6. Docs and tooling**
- ✓ `architecture.md:292` and `ui-changes.md` both say the architecture doc lists the hint rows not converted yet. There is no such list.
- ✓ `docs/history/ux.md` is a stale path (`ux.md:8`, `ux-mouse.md:215`); the file is `docs/internal/history/ux.md`.
- `StatusChannel` (`git-layer.md:100`), `CommitScriptTests` and `BranchCreateSucceeded/Failed` no longer exist in code.
- `ux-mouse.md:8-9` are near-duplicate paragraphs.
- Nine `screenshot.sh` scenarios no longer reach their screen: rebase-editor, rebase-reword, rebase-confirm, blame-lines, line-history, opener, settings-keys, theme-latte and working-empty-staged. Also, `light` is the dark palette on a light PNG background, and `screenshot.sh` deletes its `.ans` files.

## (d) Proposed reusable components

Each component is split like the rest of the app: pure state and behaviour in Core, a renderer in Tui.

1. **`ListViewport` (Core, `Lists/`).**
   - State: `(Selected, Top, Count, Rows)`.
   - `Follow(policy)`, where the policy is `Edge` or `Centre`, and `Move(action, count, wrap)` built on `ListMotion`.
   - Renderer: none (data). One `ListWindow` record replaces `PaletteWindow`, `MenuWindow` and `PickerWindow`.
   - Replaces the 12 scroll copies, the 8 cursor copies and the 6 clamp helpers.
2. **`TextField` (Core, `Editing/`).**
   - State: `PromptLine` plus a placeholder plus an optional validator (format as you type).
   - Behaviour: `PromptTextEditor`, with one key table shared with `MessageEditor` (generated from a common table).
   - Renderer: `CaretField` with a fixed `FieldRoles` (TypedText, Prefix, Placeholder, Caret); the caller can no longer pass any role.
   - Replaces the palette's hand-rolled input and `_` caret, the 4 typed-text roles and the scattered placeholders.
3. **`FilterList<T>` (Core, `Picker/` or `Lists/`).**
   - State: a `TextField` query, a `Matcher` (Fuzzy or Contains, one implementation each), a `ListViewport`, and an `Empty`/`Loading`/`Error` state with text.
   - Renderer: `FilterListFrame` (rows, selection bar, empty text in the EmptyText role, position via `PositionText`).
   - Replaces `PromptPicker`, `PaletteState`, `ReflogRefPicker`, `RefJumpState`, the settings and help filters, the 2 fuzzy classes and the 6 Contains filters.
4. **`HintSet` (Core) and `ActionRow` (Tui).**
   - One declaration per surface feeds both the status bar (`HintSet`) and the in-frame footer. Keys come from the keymap via `ChipKeys`, and labels come from a closed `HintVerb` vocabulary (Close, Back, Cancel, Keep, Open, Select, Save, ...).
   - `ActionRow` draws the "key: description" rows.
   - Replaces the double declarations, the literal-key rows in Blame, Reflog, FileHistory, Confirm, Settings and Mainline, and the palette footer.
   - The same declaration gives click targets, which fixes the keyboard-only prompt popup.
5. **`OverlayBox.TitleWithCount` and `PositionText` (Tui and Core).** Replace the 2 hand-built titles and the 7+ position formats, and fix where the position goes (decide title or footer once).
6. **`UiTexts` (Core, `Text/`).**
   - `NoMatch(subject)`, `Nothing(verb)`, `Unchanged(subject, verb)`, `Empty(subject)`, `Loading`, `NoSearch`.
   - One casing and punctuation rule, always drawn in the EmptyText role.
   - Replaces the no-match, nothing and unchanged constants spread over about 20 reducers.
7. **`ValueFormat` (Core, `Text/`).**
   - Hash (`ShortHash` only), Date (one absolute and one relative format), `StatusLetter`, `RenameText`, `AheadBehind`.
   - Replaces the 4 hash constants, the 5 date formats, the 2 letter switches, the 2 arrow directions and the 2 ahead/behind builders.
8. **`EditorHost` policy (Core, `Editing/`).** One declaration per host of undo, clipboard and discard behaviour (`DraftPolicy.Keep` or `Ask`), so the commit box, conflict result and reword stop drifting.
9. **Semantic roles (Tui `SegmentRole`).**
   - Add or rename roles by meaning: `TypedText`, `KeyLabel` (merges `OverlayKey` and `StatusKey`), `Placeholder`, `EmptyText`, `Divider`, `Heading`, `Loading`.
   - Drop `Button`.
   - Retire the role-by-look call sites, and fix the 16-colour collisions and the monochrome styles for Error and severities.

## (e) Guardrails

- **Architecture doc.** Add a `docs/architecture.md` section, "UI components":
  - one row per component above, with its state type, renderer, the surfaces that use it and the rule "extend it, don't copy it";
  - the list of surfaces not migrated yet (replacing the missing "not converted" list);
  - the semantic role table (meaning to role).
- **`.claude/rules/ui-changes.md` bullets:**
  - A list, field, filter, footer or empty text uses its component.
  - A role is chosen by meaning from the role table, never because it looks right.
  - No literal glyphs outside the glyph tables.
  - Hint words come from `HintVerb`.
  - A new screen adds its row to the contract tests below.
- **Contract tests that run the same scenarios against every surface using a component** (xUnit theories over a registry of surfaces):
  - `ListViewport`: k at the top, G, Ctrl-d at the end, resize smaller, empty list.
  - `TextField`: type, Left, Ctrl-w, Backspace on a grapheme, paste, a frame check that typed text is the TypedText role and the caret cell is the Caret role.
  - `FilterList`: no match shows `UiTexts.NoMatch` in EmptyText; loading; wheel.
  - `HintSet`: every key is bound in the keymap, every verb is in the vocabulary, and the footer and status bar agree.
  - Overlay frames at 64 and 40 columns: no doubled border characters, no blank title edge.
  - ASCII mode: every chip keeps its brackets.
- **Lint-style tests (Core and Tui):**
  - no `[..7]` or numeric hash length outside `ShortHash`;
  - no `== 1 ?` pluralisation;
  - no literal box or arrow glyphs outside the glyph tables;
  - no `SegmentRole.KeyLabel` on non-key segments (check by builder).
- **Screenshot scenarios** assert a marker text after their keys, so a stale scenario fails instead of capturing the wrong screen. Keep the `.ans` files.

## (f) Phased order (smallest visible fixes first)

Issues: phase 1 #1443, 2 #1444, 3 #1445, 4 #1446, 5 #1447 (after 3 and 4), 6 #1448 (after 2), 7 #1449 (after 4 and 6).

1. **Visible fixes (small and independent):**
   - prompt typed-text role;
   - palette caret onto `CaretField`;
   - conflict title duplicate and the "keep" hint wording;
   - ASCII chip bracket;
   - reflog selector padding;
   - double borders in narrow overlays;
   - MERGING banner over the pane border (fixed, #1482);
   - red reassuring Confirm text;
   - settings warning marker role;
   - ASCII `✗`;
   - stale doc paths and names, plus the missing "not converted" list (as the first version of the architecture section);
   - stale screenshot scenarios, marker assertions and keeping the `.ans` files.
2. **Wording:**
   - `UiTexts` and `HintVerb`;
   - one no-match, nothing and unchanged phrasing;
   - Esc and Enter verbs;
   - plurals through `CountText`;
   - hash via `ShortHash`;
   - `ValueFormat` for dates, letters and rename (after decision g6).
3. **`ListViewport` and cursor:** replace the 12 scroll copies and the wrap and stop cursors, with contract tests.
4. **`TextField`:**
   - palette onto `PromptTextEditor`;
   - shared key table;
   - fixed `FieldRoles` and placeholders;
   - editor host parity (reword undo, copy and Esc policy);
   - prompts that re-ask with the typed text kept.
5. **`FilterList`:** one fuzzy and one Contains matcher; picker, palette, reflog ref picker, ref jump, help and settings filters; empty, loading and error states.
6. **`HintSet` and `ActionRow`:**
   - a single declaration per surface;
   - mouse targets for the prompt popup and the remaining keyboard-only hints;
   - one wheel step rule.
7. **Semantic colour roles:** the role table, `KeyLabel` merge, Divider, Loading and Heading roles, 16-colour collisions, monochrome Error and severity styles, and the role lint test.

The guardrails land in the phase that brings each component, so the doc section and rule grow as components arrive.

## (g) Decisions

The owner approved the plan without settling these, so each one is the recommended default (taken 2026-11-17, logged here per CLAUDE.md "Don't block on questions"). The owner may override any of them; an override is written here and in the affected phase issue. Starting points were the sister project's decisions; what Gitunia did before is in brackets.

1. **Empty values:** sentence case with no full stop, always in the empty-text (muted) role: "None" for an absent value, "No matching <thing>" for a filter, "Nothing to <verb>" for an action, "<Thing> unchanged: nothing was <verb>ed" for an unchanged answer. [About 10 forms, mixed case and punctuation.]
2. **Esc:** two steps everywhere (leave editing, then close or go back). Esc keeps typed text in every editor, like the commit box; throwing text away is an explicit action that asks. [Reword Esc discarded the text, settings input dropped it silently.]
3. **q:** closes one level and quits only from the main panes (already so). In conflict Visual, q ends Visual first, as `ux.md` says [it closed the editor]. Blame and Settings keep q as "close the whole screen" while Esc steps back. Confirm keeps ignoring q (y, n and Esc only).
4. **Wheel:** moves the highlight where there is one (lists, pickers, menus) and scrolls where there isn't (help page, message history, output). One step size: 3 rows in panes and long lists, 1 row in popups of at most a screen of entries (palette, menus, pickers). It is data in `DefaultMouseMap` only; the commit box's own constant goes.
5. **Validation:** on Enter, as now, but a rejected prompt answer reopens the prompt with the typed text and the reason, like clone and init already do. No format checking as you type for now (would be a new feature; ask first).
6. **Dates:** absolute `yyyy-MM-dd HH:mm` in lists (graph when wide, file history), `yyyy-MM-dd HH:mm zzz` in the detail header, and one relative format of our own (`3h ago`, the blame one) for blame and reflog; the reflog stops showing git's `--date=relative` text.
7. **Key label colour:** one `KeyLabel` role, accent bold, everywhere (status bar, footers, palette, help, which-key, menus, Confirm, title chips). Yellow stays for tags, modified files and warnings. `ux-look.md` is updated in the same change. [Yellow in overlays, as documented, blue on the bar.]
8. **Scroll policy:** two policies in one component: panes follow the edge (vim-like), overlays and popups keep the selection centred.
9. **List ends:** stop at the ends everywhere, like `ListMotion` and vim. [Some pickers wrapped.]
10. **Tab in pickers:** Tab and Shift-Tab move to the next and previous row in every picker; the repository opener keeps Tab as path completion (its own key, `picker.complete-path`). The picker keys move into the keymap so they can be remapped.
11. **Save key:** no binding change. Ctrl-g accepts in every multi-line editor (commit box, reword, conflict result "done"); Ctrl-s stays the conflict editor's save to file. Only the hint words are aligned.
12. **Conflict editor colours:** the pane title colours follow the content colours of the same side.
13. **Position text:** `n/m` goes at the right end of the title bar in every list overlay and popup, through one helper.

## (h) What this audit did not cover

- Windows and macOS rendering, the classic Windows console 16-colour scheme, and fonts other than the capture font (Nerd Font glyph widths).
- A real light terminal. Without an OSC 11 answer the app assumes dark, so `light` and `theme-latte` were not truly seen.
- Screens the captures did not reach: the rebase editor, reword and its confirm, blame, line history, settings key entry and filter typing, hook output, file-history diff colours, and themes other than the default and Tokyo Night.
- Terminals whose background is bright black (Solarized), in 16 colours.
- Animations, hover, live mouse drags (only SGR-scripted ones), and resize while an overlay is open.
- The content and accuracy of git error messages, performance, accessibility (screen readers), and right-to-left or combining-heavy text.
- Tab-bar drag, the which-key title glitch (about 75 % sure), and the whole-pane `#222232` tint in conflicted diffs, which may be intended.
- Sidebar scroll-into-view and the view ▾ dropdown key handling were not traced in code.
