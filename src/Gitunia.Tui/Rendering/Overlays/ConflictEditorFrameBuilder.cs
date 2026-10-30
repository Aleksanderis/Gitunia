using System.Globalization;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the conflict editor (ux-workflows.md, workflow 4, "Conflict editor"): the hunk under the cursor with our side and
/// their side next to each other, the result below it, a strip that marks every hunk as resolved or not, clickable
/// chips and a footer. A frame of <see cref="ThreePaneFromWidth"/> columns or more shows the base, ours and theirs
/// side by side; a narrower one draws the two sides and the base below them on demand (<c>B</c>), and a frame narrower
/// than <see cref="StackedBelowWidth"/> stacks the two sides above the result.
/// </summary>
public static class ConflictEditorFrameBuilder
{
    /// <summary>
    /// The frame width (the screen minus 6 columns of margin) below which the two sides are stacked instead of drawn
    /// side by side.
    /// </summary>
    public const int StackedBelowWidth = 90;

    /// <summary>
    /// The frame width from which ours, the base and theirs are drawn as three panes side by side; between
    /// <see cref="StackedBelowWidth"/> and this width the base sits below the two sides and <c>B</c> toggles it.
    /// </summary>
    public const int ThreePaneFromWidth = 108;

    private const int MaximumWidth = 180;
    private const int ScreenMargin = 2;
    private const int HorizontalMargin = 6;
    private const int ContextLines = 2;
    private const int TitleAndStripRows = 3;
    private const int FooterAndBottomRows = 3;
    private const int ResultHeaderRows = 2;
    private const int MinimumGutterWidth = 2;
    private const char OursMark = '-';
    private const char TheirsMark = '+';
    private const int RuleRows = 1;
    private const int StackedSides = 2;
    private const int SideBySideHeaders = 1;
    private const int StackedShareDivisor = 4;
    private const int SideBySideShare = 2;
    private const int SideBySideShareOf = 5;
    private const int MinimumSideRows = 1;
    private const int MinimumResultRows = 1;
    private const int BaseHeaderRows = 1;
    private const int BaseShareDivisor = 2;
    private const string ChipGapText = "  ";
    private const string TabText = "    ";
    private const string DiscardHint = "Esc again discards your picks";
    private const string EmptySide = "(nothing)";
    private const string Unresolved = "-";
    private const string NoBasePaneText = "(no base: set diff3, then R)";
    private const string NoBaseText = "(no base for this hunk: set merge.conflictStyle=diff3, then R)";

    private static readonly HintItem[] EditingHints =
    [
        new("Esc/Ctrl-g", "keeps the text"),
        new("Enter", "adds a line"),
        new(string.Empty, "undo takes it back"),
    ];

    private static readonly HintItem[] CompleteHints =
    [
        new(string.Empty, "every hunk is resolved"),
        new("Enter", "saves the file"),
    ];

    private static readonly HintChip[] ChipLabels =
    [
        new("ours", DefaultMouseMap.ConflictOursButton),
        new("theirs", DefaultMouseMap.ConflictTheirsButton),
        new("both", DefaultMouseMap.ConflictBothButton),
        new("all ours", DefaultMouseMap.ConflictAllOursButton),
        new("all theirs", DefaultMouseMap.ConflictAllTheirsButton),
        new("edit", DefaultMouseMap.ConflictEditButton),
        new("reset", DefaultMouseMap.ConflictResetButton),
        new("undo", DefaultMouseMap.ConflictUndoButton),
        new("redo", DefaultMouseMap.ConflictRedoButton),
        new("base", DefaultMouseMap.ConflictBaseButton),
        new("next conflict", DefaultMouseMap.ConflictNextButton),
        new("previous conflict", DefaultMouseMap.ConflictPreviousButton),
        new("next diff", DefaultMouseMap.ConflictNextDiffButton),
        new("previous diff", DefaultMouseMap.ConflictPreviousDiffButton),
        new("scroll down", DefaultMouseMap.ConflictScrollDownButton),
        new("scroll up", DefaultMouseMap.ConflictScrollUpButton),
        new("re-create", DefaultMouseMap.ConflictRecreateButton),
        new("save", DefaultMouseMap.ConflictSaveButton),
        new("close", DefaultMouseMap.ConflictCancelButton),
    ];

    // The base is always on screen in three panes, so the chip that toggles it is left out.
    private static readonly HintChip[] ThreePaneChipLabels =
        [.. ChipLabels.Where(chip => chip.View != DefaultMouseMap.ConflictBaseButton)];

    private static readonly HintChip[] EditingChipLabels =
    [
        new("keep", DefaultMouseMap.ConflictEditDoneButton),
    ];

    /// <summary>True when a screen is wide enough for ours, the base and theirs side by side.</summary>
    public static bool IsThreePaneWidth(int screenWidth)
    {
        var width = Math.Min(MaximumWidth, screenWidth - HorizontalMargin);

        return width >= ThreePaneFromWidth;
    }

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        ConflictEditorState state,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        if (state.Selected is not { } hunk || PlanFor(state, screenWidth, screenHeight, keys) is not { } plan)
        {
            return null;
        }

        var title = $"Conflict: {state.Path}  hunk {state.Index + 1}/{state.Hunks.Count}";
        var lines = new List<StyledLine>
        {
            TopBorder(title, plan.Width, glyphs),
            StripLine(state, plan.Width, glyphs),
            Rule(plan, glyphs, Junction.OpensDown),
        };
        if (plan.IsStacked)
        {
            AddStackedSides(lines, hunk, plan, glyphs);
        }
        else if (plan.IsThreePane)
        {
            AddThreePanes(lines, hunk, plan, glyphs);
        }
        else
        {
            AddSideBySide(lines, hunk, plan, glyphs);
        }

        if (plan.BaseRows > 0)
        {
            lines.Add(Rule(plan, glyphs, plan.IsStacked ? Junction.None : Junction.ClosesUp));
            AddBase(lines, hunk, plan, glyphs);
        }

        lines.Add(Rule(plan, glyphs, !plan.IsStacked && plan.BaseRows == 0 ? Junction.ClosesUp : Junction.None));
        lines.Add(OverlayBox.Text(ResultHeader(state), plan.Width, glyphs, SegmentRole.SectionTitle));
        AddResult(lines, state, plan, glyphs);
        lines.Add(Rule(plan, glyphs, Junction.None));
        foreach (var chips in plan.ChipRows)
        {
            lines.Add(HintChipRows.Line(chips, plan.Width, glyphs));
        }

        lines.Add(FooterLine(state, plan.Width, glyphs));
        lines.Add(OverlayBox.Bottom(plan.Width, glyphs));

        return new OverlayFrame((screenWidth - plan.Width) / 2, 1, lines);
    }

    /// <summary>
    /// The most lines our side and their side scroll down at this size (0 when everything fits or nothing is drawn).
    /// </summary>
    public static int MaxSideScroll(
        ConflictEditorState state,
        int screenWidth,
        int screenHeight,
        ChipKeys? keys = null) =>
        PlanFor(state, screenWidth, screenHeight, keys)?.MaxScroll ?? 0;

    /// <summary>The rows each side pane shows at this size (0 when nothing is drawn).</summary>
    public static int SideRowCount(
        ConflictEditorState state,
        int screenWidth,
        int screenHeight,
        ChipKeys? keys = null) =>
        PlanFor(state, screenWidth, screenHeight, keys)?.SideRows ?? 0;

    /// <summary>The mouse view of the chip drawn at a frame line and column; null when none.</summary>
    public static string? ChipAt(
        ConflictEditorState state,
        int frameLine,
        int frameColumn,
        int screenWidth,
        int screenHeight,
        ChipKeys? keys = null)
    {
        if (PlanFor(state, screenWidth, screenHeight, keys) is not { } plan)
        {
            return null;
        }

        var chipRow = frameLine - plan.FirstChipLine;
        if (chipRow < 0 || chipRow >= plan.ChipRows.Count)
        {
            return null;
        }

        return HintChipRows.ViewAt(plan.ChipRows[chipRow], frameColumn);
    }

    private static Plan? PlanFor(ConflictEditorState state, int screenWidth, int screenHeight, ChipKeys? keys)
    {
        var width = Math.Min(MaximumWidth, screenWidth - HorizontalMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var isStacked = width < StackedBelowWidth;
        var isThreePane = !isStacked && width >= ThreePaneFromWidth;
        var chipSet = ChipSetFor(state, isThreePane, keys);
        var chipRows = HintChipRows.Flow(chipSet, width);
        var sideHeaders = isStacked ? StackedSides : SideBySideHeaders;
        var sideCount = isStacked ? StackedSides : 1;
        var available = screenHeight - ScreenMargin - TitleAndStripRows - FooterAndBottomRows - chipRows.Count
            - sideHeaders - ResultHeaderRows;
        if (available < (sideCount * MinimumSideRows) + MinimumResultRows)
        {
            return null;
        }

        var share = isStacked ? available / StackedShareDivisor : available * SideBySideShare / SideBySideShareOf;
        var sideRows = Math.Max(MinimumSideRows, share);
        var baseRows = state.IsBaseShown && !isThreePane ? Math.Max(MinimumSideRows, sideRows / BaseShareDivisor) : 0;
        var baseTotal = baseRows > 0 ? RuleRows + BaseHeaderRows + baseRows : 0;
        var resultRows = available - (sideRows * sideCount) - baseTotal;
        if (resultRows < MinimumResultRows)
        {
            // No room for the base: the result matters more.
            baseRows = 0;
            baseTotal = 0;
            resultRows = available - (sideRows * sideCount);
        }

        var firstChipLine = TitleAndStripRows + sideHeaders + (sideRows * sideCount) + baseTotal + ResultHeaderRows
            + resultRows + RuleRows;

        var sideLines = Math.Max(
            ConflictEditorState.Lines(state.Selected!.Ours).Count,
            ConflictEditorState.Lines(state.Selected.Theirs).Count);
        var baseLines = 0;
        if (baseRows > 0 || isThreePane)
        {
            baseLines = ConflictEditorState.Lines(state.Selected.Base ?? string.Empty).Count;
        }

        var maxScroll = Math.Max(
            Math.Max(0, sideLines - sideRows),
            Math.Max(0, baseLines - (isThreePane ? sideRows : baseRows)));
        var resultStart = state.ResultLineOf(state.Index);
        var lastNumber = resultStart + ContextLines
            + Math.Max(sideLines, ConflictEditorState.Lines(state.ResultOf(state.Index)).Count);
        var gutter = Math.Max(MinimumGutterWidth, lastNumber.ToString(CultureInfo.InvariantCulture).Length);

        return new Plan(
            width,
            isStacked,
            isThreePane,
            sideRows,
            baseRows,
            resultRows,
            chipRows,
            firstChipLine,
            Math.Min(state.SideScroll, maxScroll),
            maxScroll,
            resultStart,
            gutter,
            state.DifferingRows(),
            state.Selected.Base is not null && (isThreePane || baseRows > 0));
    }

    private static SideText? BaseOf(ConflictHunk hunk, Plan plan) =>
        plan.ShowsBase && hunk.Base is not null ? SideText.Of(hunk.Base) : null;

    private static void AddSideBySide(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(plan.Width);
        var leftWidth = SideBySideLeftWidth(plan.Width);
        var rightWidth = inner - 1 - leftWidth;
        var divider = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        var ours = SideText.Of(hunk.Ours, OursMark, SegmentRole.DiffRemoved) with { Base = BaseOf(hunk, plan) };
        var theirs = SideText.Of(hunk.Theirs, TheirsMark, SegmentRole.DiffAdded) with { Base = BaseOf(hunk, plan) };
        lines.Add(Joined(
            Cell(SideTitle("ours", hunk.OursLabel, ours, plan), leftWidth, SegmentRole.LocalBranch, glyphs),
            divider,
            Cell(SideTitle("theirs", hunk.TheirsLabel, theirs, plan), rightWidth, SegmentRole.RemoteBranch, glyphs),
            inner,
            glyphs));
        for (var row = 0; row < plan.SideRows; row++)
        {
            var line = plan.Scroll + row;
            lines.Add(Joined(
                NumberedCell(ours, theirs, true, line, leftWidth, plan, glyphs),
                divider,
                NumberedCell(theirs, ours, false, line, rightWidth, plan, glyphs),
                inner,
                glyphs));
        }
    }

    private static int SideBySideLeftWidth(int frameWidth) => (OverlayBox.InnerWidth(frameWidth) - 1) / 2;

    private static int ThreePaneWidth(int frameWidth) => (OverlayBox.InnerWidth(frameWidth) - 2) / 3;

    // The columns (inside the frame) of the vertical dividers between the panes.
    private static List<int> DividerColumns(Plan plan)
    {
        if (!plan.IsThreePane)
        {
            return [SideBySideLeftWidth(plan.Width)];
        }

        var pane = ThreePaneWidth(plan.Width);

        return [pane, pane + 1 + pane];
    }

    private static void AddThreePanes(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(plan.Width);
        var oursWidth = ThreePaneWidth(plan.Width);
        var baseWidth = oursWidth;
        var theirsWidth = inner - 2 - oursWidth - baseWidth;
        var divider = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        var ours = SideText.Of(hunk.Ours, OursMark, SegmentRole.DiffRemoved) with { Base = BaseOf(hunk, plan) };
        var theirs = SideText.Of(hunk.Theirs, TheirsMark, SegmentRole.DiffAdded) with { Base = BaseOf(hunk, plan) };
        var common = hunk.Base is null ? new SideText([NoBasePaneText], true) : SideText.Of(hunk.Base);
        lines.Add(JoinedThree(
            Cell(SideTitle("base", "common ancestor", common, plan), baseWidth, SegmentRole.SectionTitle, glyphs),
            Cell(SideTitle("ours", hunk.OursLabel, ours, plan), oursWidth, SegmentRole.LocalBranch, glyphs),
            Cell(SideTitle("theirs", hunk.TheirsLabel, theirs, plan), theirsWidth, SegmentRole.RemoteBranch, glyphs),
            divider,
            inner,
            glyphs));
        for (var row = 0; row < plan.SideRows; row++)
        {
            var line = plan.Scroll + row;
            var baseCell = new LineBuilder(baseWidth, glyphs.Ellipsis)
                .Append(" ", SegmentRole.Plain)
                .Append(GutterText(null, plan.Gutter), SegmentRole.Faint);
            ChangedText.Append(
                baseCell,
                common.TextAt(line),
                common.RoleAt(line, plan),
                common.BaseChangesAt(line, ours, theirs, plan),
                SegmentRole.DiffRemovedWord);
            lines.Add(JoinedThree(
                baseCell.Build(),
                NumberedCell(ours, theirs, true, line, oursWidth, plan, glyphs),
                NumberedCell(theirs, ours, false, line, theirsWidth, plan, glyphs),
                divider,
                inner,
                glyphs));
        }
    }

    private static StyledLine JoinedThree(
        StyledLine first,
        StyledLine second,
        StyledLine third,
        string divider,
        int innerWidth,
        GraphGlyphSet glyphs)
    {
        var inner = new LineBuilder(innerWidth, glyphs.Ellipsis)
            .Append(first)
            .Append(divider, SegmentRole.FocusedBorder)
            .Append(second)
            .Append(divider, SegmentRole.FocusedBorder)
            .Append(third)
            .Build();

        return OverlayBox.Wrap(inner, glyphs);
    }

    // A rule across the frame, joined to both side borders. Side by side it also joins the divider of the two sides:
    // opening it downwards under the strip, closing it upwards above the result.
    private static StyledLine Rule(Plan plan, GraphGlyphSet glyphs, Junction junction)
    {
        var inner = OverlayBox.InnerWidth(plan.Width);
        var horizontal = glyphs.Horizontal;
        var builder = new LineBuilder(inner, glyphs.Ellipsis);
        if (junction != Junction.None && !plan.IsStacked)
        {
            var down = junction == Junction.OpensDown;
            var vertical = down ? LineDirections.Down : LineDirections.Up;
            var joint = Joint(glyphs, LineDirections.Left | LineDirections.Right | vertical);
            var chars = new string(horizontal, inner).ToCharArray();
            foreach (var column in DividerColumns(plan))
            {
                chars[column] = joint;
            }

            builder.Append(new string(chars), SegmentRole.FocusedBorder);
        }
        else
        {
            builder.Append(new string(horizontal, inner), SegmentRole.FocusedBorder);
        }

        var opening = Joint(glyphs, LineDirections.Up | LineDirections.Down | LineDirections.Right);
        var closing = Joint(glyphs, LineDirections.Up | LineDirections.Down | LineDirections.Left);

        return new StyledLine(
        [
            new StyledSegment(opening.ToString(), SegmentRole.FocusedBorder),
            .. builder.Build().Segments,
            new StyledSegment(closing.ToString(), SegmentRole.FocusedBorder),
        ]);
    }

    // The top border with the title set into it: `╭─ Conflict: file  hunk 1/2 ────╮`.
    private static StyledLine TopBorder(string title, int width, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var horizontal = glyphs.Horizontal.ToString();
        var label = TextWidth.Fit($" {title} ", Math.Max(0, inner - 1), glyphs.Ellipsis);
        var fill = Math.Max(0, inner - 1 - TextWidth.Of(label));

        var opening = Joint(glyphs, LineDirections.Down | LineDirections.Right);
        var closing = Joint(glyphs, LineDirections.Down | LineDirections.Left);

        return new StyledLine(
        [
            new StyledSegment(opening.ToString(), SegmentRole.FocusedBorder),
            new StyledSegment(horizontal, SegmentRole.FocusedBorder),
            new StyledSegment(label, SegmentRole.SectionTitle),
            new StyledSegment(string.Concat(Enumerable.Repeat(horizontal, fill)), SegmentRole.FocusedBorder),
            new StyledSegment(closing.ToString(), SegmentRole.FocusedBorder),
        ]);
    }

    // Corners and junctions are a plus sign in the ASCII set, as in the other overlays.
    private static char Joint(GraphGlyphSet glyphs, LineDirections directions) =>
        glyphs.IsAscii ? '+' : glyphs.For(directions);

    private static string SideTitle(string name, string label, SideText side, Plan plan)
    {
        if (side.IsEmpty || side.Lines.Count <= plan.SideRows)
        {
            return $"{name}  {label}";
        }

        var last = Math.Min(side.Lines.Count, plan.Scroll + plan.SideRows);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name}  (lines {plan.Scroll + 1}-{last} of {side.Lines.Count})  {label}");
    }

    private static StyledLine NumberedCell(
        SideText side,
        SideText other,
        bool isOurs,
        int line,
        int width,
        Plan plan,
        GraphGlyphSet glyphs)
    {
        var gutter = GutterText(side.NumberAt(line, plan.ResultStart), plan.Gutter, side.MarkAt(line, plan));
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Plain)
            .Append(gutter, SegmentRole.Faint);
        ChangedText.Append(
            builder,
            side.TextAt(line),
            side.RoleAt(line, plan),
            side.ChangesAt(line, other, isOurs, plan));

        return builder.Build();
    }

    // The line number, then a mark for a line the other side does not have in the same row (so colour is not alone).
    private static string GutterText(int? number, int width, char mark = ' ')
    {
        if (number is { } value)
        {
            return $"{value.ToString(CultureInfo.InvariantCulture).PadLeft(width)}{mark}";
        }

        return new string(' ', width + 1);
    }

    private static StyledLine NumberedText(
        int? number,
        string text,
        SegmentRole role,
        Plan plan,
        GraphGlyphSet glyphs) => NumberedText(GutterText(number, plan.Gutter), text, role, plan, glyphs);

    private static StyledLine NumberedText(
        string gutter,
        string text,
        SegmentRole role,
        Plan plan,
        GraphGlyphSet glyphs,
        IReadOnlyList<ChangedSpan>? changes = null)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(plan.Width), glyphs.Ellipsis)
            .Append(" ", SegmentRole.Plain)
            .Append(gutter, SegmentRole.Faint);
        ChangedText.Append(builder, text, role, changes);

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }

    private static void AddBase(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        lines.Add(OverlayBox.Text("base  common ancestor", plan.Width, glyphs, SegmentRole.SectionTitle));
        var side = hunk.Base is null ? new SideText([NoBaseText], true) : SideText.Of(hunk.Base);
        var ours = SideText.Of(hunk.Ours);
        var theirs = SideText.Of(hunk.Theirs);
        for (var row = 0; row < plan.BaseRows; row++)
        {
            var line = plan.Scroll + row;
            var builder = new LineBuilder(OverlayBox.InnerWidth(plan.Width), glyphs.Ellipsis)
                .Append(" ", SegmentRole.Plain);
            ChangedText.Append(
                builder,
                side.TextAt(line),
                side.RoleAt(line, plan),
                side.BaseChangesAt(line, ours, theirs, plan),
                SegmentRole.DiffRemovedWord);
            lines.Add(OverlayBox.Wrap(builder.Build(), glyphs));
        }
    }

    private static void AddStackedSides(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        var ours = SideText.Of(hunk.Ours, OursMark, SegmentRole.DiffRemoved) with { Base = BaseOf(hunk, plan) };
        var theirs = SideText.Of(hunk.Theirs, TheirsMark, SegmentRole.DiffAdded) with { Base = BaseOf(hunk, plan) };
        AddSide(lines, "ours", hunk.OursLabel, SegmentRole.LocalBranch, ours, theirs, true, plan, glyphs);
        AddSide(lines, "theirs", hunk.TheirsLabel, SegmentRole.RemoteBranch, theirs, ours, false, plan, glyphs);
    }

    private static void AddSide(
        List<StyledLine> lines,
        string name,
        string label,
        SegmentRole headerRole,
        SideText side,
        SideText other,
        bool isOurs,
        Plan plan,
        GraphGlyphSet glyphs)
    {
        lines.Add(OverlayBox.Text(SideTitle(name, label, side, plan), plan.Width, glyphs, headerRole));
        for (var row = 0; row < plan.SideRows; row++)
        {
            var line = plan.Scroll + row;
            var number = side.NumberAt(line, plan.ResultStart);
            var gutter = GutterText(number, plan.Gutter, side.MarkAt(line, plan));
            var changes = side.ChangesAt(line, other, isOurs, plan);
            lines.Add(NumberedText(gutter, side.TextAt(line), side.RoleAt(line, plan), plan, glyphs, changes));
        }
    }

    private static void AddResult(List<StyledLine> lines, ConflictEditorState state, Plan plan, GraphGlyphSet glyphs)
    {
        if (state.Edit is { } edit)
        {
            var textWidth = OverlayBox.InnerWidth(plan.Width) - 1;
            var editorLines = MessageEditorRenderer.Render(
                edit.Editor,
                textWidth,
                plan.ResultRowCount,
                showCursor: true,
                marksSubject: false);
            foreach (var line in editorLines)
            {
                var padded = new StyledLine([new StyledSegment(" ", SegmentRole.Plain), .. line.Segments]);
                lines.Add(OverlayBox.Wrap(padded, glyphs));
            }

            return;
        }

        var rows = ResultRows(state, plan);
        for (var row = 0; row < plan.ResultRowCount; row++)
        {
            var shown = row < rows.Count ? rows[row] : new ResultRow(string.Empty, SegmentRole.Plain);
            lines.Add(NumberedText(shown.Number, shown.Text, shown.Role, plan, glyphs));
        }
    }

    // The hunk's result with up to two unconflicted lines on each side, as many as the pane has room for.
    private static List<ResultRow> ResultRows(ConflictEditorState state, Plan plan)
    {
        var rowCount = plan.ResultRowCount;
        var role = state.SelectedPick == ConflictPick.None ? SegmentRole.Error : SegmentRole.Plain;
        var hunkLines = ConflictEditorState.Lines(state.ResultOf(state.Index)).Select(Printable).ToList();
        var realCount = hunkLines.Count;
        if (hunkLines.Count == 0)
        {
            hunkLines.Add(EmptySide);
            role = SegmentRole.Faint;
        }

        var spare = Math.Max(0, rowCount - hunkLines.Count);
        var before = state.ContextAround(state.Index, before: true, Math.Min(ContextLines, spare / 2));
        var after = state.ContextAround(state.Index, before: false, Math.Min(ContextLines, spare - before.Count));
        var shown = Truncate(hunkLines, Math.Max(1, rowCount - before.Count));
        var keptReal = shown.Count == hunkLines.Count ? realCount : shown.Count - 1;
        var rows = new List<ResultRow>();
        rows.AddRange(before.Select((line, index) =>
            new ResultRow(Printable(line), SegmentRole.Faint, plan.ResultStart - before.Count + index)));
        rows.AddRange(shown.Select((line, index) =>
            new ResultRow(line, role, index < keptReal ? plan.ResultStart + index : null)));
        rows.AddRange(after.Select((line, index) =>
            new ResultRow(Printable(line), SegmentRole.Faint, plan.ResultStart + realCount + index)));

        return rows;
    }

    private static List<string> Truncate(List<string> lines, int rowCount)
    {
        if (lines.Count <= rowCount)
        {
            return lines;
        }

        var kept = lines.Take(rowCount - 1).ToList();
        kept.Add(string.Create(CultureInfo.InvariantCulture, $"... {lines.Count - kept.Count} more lines"));

        return kept;
    }

    private static string Printable(string line) => line.Replace("\t", TabText, StringComparison.Ordinal);

    private static StyledLine Cell(string text, int width, SegmentRole role, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis).Append($" {text}", role).Build();

    private static StyledLine Joined(
        StyledLine left,
        string divider,
        StyledLine right,
        int innerWidth,
        GraphGlyphSet glyphs)
    {
        var inner = new LineBuilder(innerWidth, glyphs.Ellipsis)
            .Append(left)
            .Append(divider, SegmentRole.FocusedBorder)
            .Append(right)
            .Build();

        return OverlayBox.Wrap(inner, glyphs);
    }

    private static string ResultHeader(ConflictEditorState state)
    {
        if (state.IsEditing)
        {
            return "result  editing";
        }

        return state.SelectedPick switch
        {
            ConflictPick.Edited => "result  edited by you",
            ConflictPick.Ours => "result  our side",
            ConflictPick.Theirs => "result  their side",
            ConflictPick.Both => "result  both sides, ours first",
            _ => "result  unresolved: the conflict markers stay in the file",
        };
    }

    private static HintChip[] ChipSetFor(ConflictEditorState state, bool isThreePane, ChipKeys? keys)
    {
        if (state.IsEditing)
        {
            return HintChipRows.WithKeys(EditingChipLabels, keys, DefaultKeymap.ConflictEditorView, KeyMode.Insert);
        }

        var labels = isThreePane ? ThreePaneChipLabels : ChipLabels;
        return HintChipRows.WithKeys(labels, keys, DefaultKeymap.ConflictEditorView);
    }

    // One token per hunk, the one under the cursor in brackets: `1 ours  [2 -]  3 both`.
    private static StyledLine StripLine(ConflictEditorState state, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis)
            .Append(" hunks  ", SegmentRole.Faint);
        for (var index = 0; index < state.Hunks.Count; index++)
        {
            var token = string.Create(CultureInfo.InvariantCulture, $"{index + 1} {PickWord(state.Picks[index])}");
            var isCurrent = index == state.Index;
            var role = isCurrent ? SegmentRole.Cursor : RoleOfPick(state.Picks[index]);
            builder.Append(isCurrent ? $"[{token}]" : token, role);
            builder.Append(ChipGapText, SegmentRole.Plain);
        }

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }

    private static string PickWord(ConflictPick pick) => pick switch
    {
        ConflictPick.Ours => "ours",
        ConflictPick.Theirs => "theirs",
        ConflictPick.Both => "both",
        ConflictPick.Edited => "edited",
        _ => Unresolved,
    };

    private static SegmentRole RoleOfPick(ConflictPick pick) =>
        pick == ConflictPick.None ? SegmentRole.Error : SegmentRole.Plain;

    private static StyledLine FooterLine(ConflictEditorState state, int width, GraphGlyphSet glyphs)
    {
        if (state.IsEditing)
        {
            return OverlayBox.Hints(EditingHints, width, glyphs);
        }

        if (state.IsDiscardPending)
        {
            return OverlayBox.Text(DiscardHint, width, glyphs, SegmentRole.Error);
        }

        if (state.IsComplete)
        {
            return OverlayBox.Hints(CompleteHints, width, glyphs);
        }

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{state.ResolvedCount} resolved  {state.UnresolvedCount} unresolved");

        return OverlayBox.Text(text, width, glyphs, SegmentRole.Faint);
    }

    private enum Junction
    {
        None,
        OpensDown,
        ClosesUp,
    }

    private sealed record Plan(
        int Width,
        bool IsStacked,
        bool IsThreePane,
        int SideRows,
        int BaseRows,
        int ResultRowCount,
        List<List<HintChip>> ChipRows,
        int FirstChipLine,
        int Scroll,
        int MaxScroll,
        int ResultStart,
        int Gutter,
        IReadOnlyList<bool> Different,
        bool ShowsBase);

    private readonly record struct ResultRow(string Text, SegmentRole Role, int? Number = null);

    // The lines of one side; an empty side is one faint placeholder line without a number.
    // A line that differs from the other side's line in the same row carries the side's mark and role.
    private sealed record SideText(
        List<string> Lines,
        bool IsEmpty,
        char Mark = ' ',
        SegmentRole DifferingRole = SegmentRole.Plain)
    {
        // The base lines when the layout shows them: words are compared with them, not with the other side.
        public SideText? Base { get; init; }

        public static SideText Of(string text, char mark = ' ', SegmentRole differingRole = SegmentRole.Plain)
        {
            var lines = ConflictEditorState.Lines(text).Select(Printable).ToList();

            if (lines.Count == 0)
            {
                return new SideText([EmptySide], true);
            }

            return new SideText(lines, false, mark, differingRole);
        }

        public string TextAt(int line) => line < Lines.Count ? Lines[line] : string.Empty;

        public char MarkAt(int line, Plan plan) => IsDifferent(line, plan) ? Mark : ' ';

        public SegmentRole RoleAt(int line, Plan plan)
        {
            if (IsEmpty && line == 0)
            {
                return SegmentRole.Faint;
            }

            return IsDifferent(line, plan) ? DifferingRole : SegmentRole.Plain;
        }

        // The words of this side's line that differ from the other side's line in the same row (ours is the old side).
        public IReadOnlyList<ChangedSpan> ChangesAt(int line, SideText other, bool isOurs, Plan plan)
        {
            if (!IsDifferent(line, plan))
            {
                return [];
            }

            if (Base is { IsEmpty: false } reference)
            {
                return line < reference.Lines.Count ? WordDiff.Compare(reference.Lines[line], Lines[line]).New : [];
            }

            if (other.IsEmpty || line >= other.Lines.Count)
            {
                return [];
            }

            if (isOurs)
            {
                return WordDiff.Compare(Lines[line], other.Lines[line]).Old;
            }

            return WordDiff.Compare(other.Lines[line], Lines[line]).New;
        }

        // On the base side: the words of this line that ours or theirs changed.
        public IReadOnlyList<ChangedSpan> BaseChangesAt(int line, SideText ours, SideText theirs, Plan plan)
        {
            if (!IsDifferent(line, plan))
            {
                return [];
            }

            return ChangedSpan.Union(ChangesAgainst(line, ours), ChangesAgainst(line, theirs));
        }

        // The words of this line that the other side changed; none when the other side has no such line.
        private IReadOnlyList<ChangedSpan> ChangesAgainst(int line, SideText side)
        {
            if (line < side.Lines.Count && !side.IsEmpty)
            {
                return WordDiff.Compare(Lines[line], side.Lines[line]).Old;
            }

            return [];
        }

        private bool IsDifferent(int line, Plan plan) =>
            !IsEmpty && line < Lines.Count && line < plan.Different.Count && plan.Different[line];

        public int? NumberAt(int line, int start) => IsEmpty || line >= Lines.Count ? null : start + line;
    }
}
