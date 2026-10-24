using System.Globalization;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Mouse;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the conflict editor (ux.md, workflow 4, "Conflict editor"): the hunk under the cursor with our side and
/// their side next to each other, the result below it, a strip that marks every hunk as resolved or not, clickable
/// chips and a footer. A frame narrower than <see cref="StackedBelowWidth"/> stacks the two sides above the result
/// instead.
/// </summary>
public static class ConflictEditorFrameBuilder
{
    /// <summary>
    /// The frame width (the screen minus 6 columns of margin) below which the two sides are stacked instead of drawn
    /// side by side.
    /// </summary>
    public const int StackedBelowWidth = 90;

    private const int MaximumWidth = 180;
    private const int ScreenMargin = 2;
    private const int HorizontalMargin = 6;
    private const int ChipStartColumn = 2;
    private const int ChipGap = 2;
    private const int ContextLines = 2;
    private const int TitleAndStripRows = 2;
    private const int FooterAndBottomRows = 2;
    private const int ResultHeaderRows = 1;
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
    private const string CompleteHint = "every hunk is resolved: Enter saves the file";
    private const string EmptySide = "(nothing)";
    private const string Unresolved = "-";
    private const string EditingHint = "Esc or Alt-Enter keeps the text, Enter adds a line; undo takes it back";
    private const string NoBaseText = "(no base for this hunk: set merge.conflictStyle=diff3, then R)";

    private static readonly ConflictEditorChip[] Chips =
    [
        new("[o ours]", DefaultMouseMap.ConflictOursButton),
        new("[t theirs]", DefaultMouseMap.ConflictTheirsButton),
        new("[b both]", DefaultMouseMap.ConflictBothButton),
        new("[e edit]", DefaultMouseMap.ConflictEditButton),
        new("[r reset]", DefaultMouseMap.ConflictResetButton),
        new("[u undo]", DefaultMouseMap.ConflictUndoButton),
        new("[Ctrl-r redo]", DefaultMouseMap.ConflictRedoButton),
        new("[B base]", DefaultMouseMap.ConflictBaseButton),
        new("[n next]", DefaultMouseMap.ConflictNextButton),
        new("[N previous]", DefaultMouseMap.ConflictPreviousButton),
        new("[R re-create]", DefaultMouseMap.ConflictRecreateButton),
        new("[Enter save]", DefaultMouseMap.ConflictSaveButton),
        new("[Esc close]", DefaultMouseMap.ConflictCancelButton),
    ];

    private static readonly ConflictEditorChip[] EditingChips =
    [
        new("[Alt-Enter keep]", DefaultMouseMap.ConflictEditDoneButton),
    ];

    /// <summary>The frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        ConflictEditorState state,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs)
    {
        if (state.Selected is not { } hunk || PlanFor(state, screenWidth, screenHeight) is not { } plan)
        {
            return null;
        }

        var title = $"Conflict: {state.Path}  hunk {state.Index + 1}/{state.Hunks.Count}";
        var lines = new List<StyledLine>
        {
            OverlayBox.Title(title, plan.Width, glyphs),
            StripLine(state, plan.Width, glyphs),
        };
        if (plan.IsStacked)
        {
            AddStackedSides(lines, hunk, plan, glyphs);
        }
        else
        {
            AddSideBySide(lines, hunk, plan, glyphs);
        }

        if (plan.BaseRows > 0)
        {
            AddBase(lines, hunk, plan, glyphs);
        }

        lines.Add(OverlayBox.Text(ResultHeader(state), plan.Width, glyphs, SegmentRole.SectionTitle));
        AddResult(lines, state, plan, glyphs);
        foreach (var chips in plan.ChipRows)
        {
            lines.Add(ChipLine(chips, plan.Width, glyphs));
        }

        lines.Add(FooterLine(state, plan.Width, glyphs));
        lines.Add(OverlayBox.Bottom(plan.Width, glyphs));

        return new OverlayFrame((screenWidth - plan.Width) / 2, 1, lines);
    }

    /// <summary>The mouse view of the chip drawn at a frame line and column; null when none.</summary>
    public static string? ChipAt(
        ConflictEditorState state,
        int frameLine,
        int frameColumn,
        int screenWidth,
        int screenHeight)
    {
        if (PlanFor(state, screenWidth, screenHeight) is not { } plan)
        {
            return null;
        }

        var chipRow = frameLine - plan.FirstChipLine;
        if (chipRow < 0 || chipRow >= plan.ChipRows.Count)
        {
            return null;
        }

        var start = ChipStartColumn;
        foreach (var chip in plan.ChipRows[chipRow])
        {
            var end = start + TextWidth.Of(chip.Text);
            if (frameColumn >= start && frameColumn < end)
            {
                return chip.View;
            }

            start = end + ChipGap;
        }

        return null;
    }

    private static Plan? PlanFor(ConflictEditorState state, int screenWidth, int screenHeight)
    {
        var width = Math.Min(MaximumWidth, screenWidth - HorizontalMargin);
        if (width < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var chipRows = ChipRowsFor(state.IsEditing ? EditingChips : Chips, width);
        var isStacked = width < StackedBelowWidth;
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
        var baseRows = state.IsBaseShown ? Math.Max(MinimumSideRows, sideRows / BaseShareDivisor) : 0;
        var baseTotal = baseRows > 0 ? BaseHeaderRows + baseRows : 0;
        var resultRows = available - (sideRows * sideCount) - baseTotal;
        if (resultRows < MinimumResultRows)
        {
            // No room for the base: the result matters more.
            baseRows = 0;
            baseTotal = 0;
            resultRows = available - (sideRows * sideCount);
        }

        var firstChipLine = TitleAndStripRows + sideHeaders + (sideRows * sideCount) + baseTotal + ResultHeaderRows
            + resultRows;

        return new Plan(width, isStacked, sideRows, baseRows, resultRows, chipRows, firstChipLine);
    }

    private static void AddSideBySide(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(plan.Width);
        var leftWidth = (inner - 1) / 2;
        var rightWidth = inner - 1 - leftWidth;
        var divider = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        var ours = SideLines(hunk.Ours, plan.SideRows);
        var theirs = SideLines(hunk.Theirs, plan.SideRows);
        lines.Add(Joined(
            Cell($"ours  {hunk.OursLabel}", leftWidth, SegmentRole.LocalBranch, glyphs),
            divider,
            Cell($"theirs  {hunk.TheirsLabel}", rightWidth, SegmentRole.RemoteBranch, glyphs),
            inner,
            glyphs));
        for (var row = 0; row < plan.SideRows; row++)
        {
            lines.Add(Joined(
                Cell(RowText(ours, row), leftWidth, RoleOf(ours, row), glyphs),
                divider,
                Cell(RowText(theirs, row), rightWidth, RoleOf(theirs, row), glyphs),
                inner,
                glyphs));
        }
    }

    private static void AddBase(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        lines.Add(OverlayBox.Text("base  common ancestor", plan.Width, glyphs, SegmentRole.SectionTitle));
        var rows = hunk.Base is null ? [NoBaseText] : SideLines(hunk.Base, plan.BaseRows);
        var isFaint = hunk.Base is null || (rows.Count == 1 && rows[0] == EmptySide);
        var role = isFaint ? SegmentRole.Faint : SegmentRole.Plain;
        for (var row = 0; row < plan.BaseRows; row++)
        {
            lines.Add(OverlayBox.Text(RowText(rows, row), plan.Width, glyphs, role));
        }
    }

    private static void AddStackedSides(List<StyledLine> lines, ConflictHunk hunk, Plan plan, GraphGlyphSet glyphs)
    {
        AddSide(lines, $"ours  {hunk.OursLabel}", SegmentRole.LocalBranch, hunk.Ours, plan, glyphs);
        AddSide(lines, $"theirs  {hunk.TheirsLabel}", SegmentRole.RemoteBranch, hunk.Theirs, plan, glyphs);
    }

    private static void AddSide(
        List<StyledLine> lines,
        string header,
        SegmentRole headerRole,
        string text,
        Plan plan,
        GraphGlyphSet glyphs)
    {
        var side = SideLines(text, plan.SideRows);
        lines.Add(OverlayBox.Text(header, plan.Width, glyphs, headerRole));
        for (var row = 0; row < plan.SideRows; row++)
        {
            lines.Add(OverlayBox.Text(RowText(side, row), plan.Width, glyphs, RoleOf(side, row)));
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

        var rows = ResultRows(state, plan.ResultRowCount);
        for (var row = 0; row < plan.ResultRowCount; row++)
        {
            var shown = row < rows.Count ? rows[row] : new ResultRow(string.Empty, SegmentRole.Plain);
            lines.Add(OverlayBox.Text(shown.Text, plan.Width, glyphs, shown.Role));
        }
    }

    // The hunk's result with up to two unconflicted lines on each side, as many as the pane has room for.
    private static List<ResultRow> ResultRows(ConflictEditorState state, int rowCount)
    {
        var role = state.SelectedPick == ConflictPick.None ? SegmentRole.Error : SegmentRole.Plain;
        var hunkLines = ConflictEditorState.Lines(state.ResultOf(state.Index)).Select(Printable).ToList();
        if (hunkLines.Count == 0)
        {
            hunkLines.Add(EmptySide);
            role = SegmentRole.Faint;
        }

        var spare = Math.Max(0, rowCount - hunkLines.Count);
        var before = state.ContextAround(state.Index, before: true, Math.Min(ContextLines, spare / 2));
        var after = state.ContextAround(state.Index, before: false, Math.Min(ContextLines, spare - before.Count));
        var rows = new List<ResultRow>();
        rows.AddRange(before.Select(line => new ResultRow(Printable(line), SegmentRole.Faint)));
        var shown = Truncate(hunkLines, Math.Max(1, rowCount - before.Count));
        rows.AddRange(shown.Select(line => new ResultRow(line, role)));
        rows.AddRange(after.Select(line => new ResultRow(Printable(line), SegmentRole.Faint)));

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

    private static List<string> SideLines(string text, int rowCount)
    {
        var lines = ConflictEditorState.Lines(text).Select(Printable).ToList();

        return lines.Count == 0 ? [EmptySide] : Truncate(lines, rowCount);
    }

    private static string RowText(List<string> lines, int row) => row < lines.Count ? lines[row] : string.Empty;

    private static SegmentRole RoleOf(List<string> lines, int row) =>
        row == 0 && lines.Count == 1 && lines[0] == EmptySide ? SegmentRole.Faint : SegmentRole.Plain;

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
            .Append(divider, SegmentRole.Border)
            .Append(right)
            .Build();

        return OverlayBox.Wrap(inner, glyphs);
    }

    private static string ResultHeader(ConflictEditorState state) => state.IsEditing
        ? "result  editing"
        : state.SelectedPick switch
        {
            ConflictPick.Edited => "result  edited by you",
            ConflictPick.Ours => "result  our side",
            ConflictPick.Theirs => "result  their side",
            ConflictPick.Both => "result  both sides, ours first",
            _ => "result  unresolved: the conflict markers stay in the file",
        };

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

    private static StyledLine ChipLine(List<ConflictEditorChip> chips, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis).Append(" ", SegmentRole.Plain);
        for (var index = 0; index < chips.Count; index++)
        {
            builder.Append(index == 0 ? string.Empty : ChipGapText, SegmentRole.Plain);
            builder.Append(chips[index].Text, SegmentRole.Button);
        }

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }

    private static StyledLine FooterLine(ConflictEditorState state, int width, GraphGlyphSet glyphs)
    {
        if (state.IsEditing)
        {
            return OverlayBox.Text(EditingHint, width, glyphs, SegmentRole.Faint);
        }

        if (state.IsDiscardPending)
        {
            return OverlayBox.Text(DiscardHint, width, glyphs, SegmentRole.Error);
        }

        if (state.IsComplete)
        {
            return OverlayBox.Text(CompleteHint, width, glyphs, SegmentRole.Faint);
        }

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{state.ResolvedCount} resolved  {state.UnresolvedCount} unresolved");

        return OverlayBox.Text(text, width, glyphs, SegmentRole.Faint);
    }

    // The chips flow left to right and start a new row where the next one would not fit, so none is cut off.
    private static List<List<ConflictEditorChip>> ChipRowsFor(ConflictEditorChip[] chipSet, int frameWidth)
    {
        var available = OverlayBox.InnerWidth(frameWidth) - 1;
        var rows = new List<List<ConflictEditorChip>> { new() };
        var used = 0;
        foreach (var chip in chipSet)
        {
            var width = TextWidth.Of(chip.Text);
            var needed = rows[^1].Count == 0 ? width : used + ChipGap + width;
            if (rows[^1].Count > 0 && needed > available)
            {
                rows.Add([]);
                needed = width;
            }

            rows[^1].Add(chip);
            used = needed;
        }

        return rows;
    }

    private sealed record Plan(
        int Width,
        bool IsStacked,
        int SideRows,
        int BaseRows,
        int ResultRowCount,
        List<List<ConflictEditorChip>> ChipRows,
        int FirstChipLine);

    private readonly record struct ResultRow(string Text, SegmentRole Role);
}
