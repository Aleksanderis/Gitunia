using System.Globalization;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Draws the rows of the conflict editor's panes (ux.md, "Conflict editor"): a two-column margin (a bar on the
/// current conflict and a mark that says resolved or not), the line number, then the text with its changed words.
/// The cursor row is tinted in every pane (strongly in the active one), a Visual selection has the range tint.
/// </summary>
internal static class ConflictPaneRows
{
    private const string TabText = "    ";
    private const string EmptySide = "(nothing)";
    private const string EmptyBase = "(empty base)";
    private const string NoBase = "(no base: set diff3, then R)";
    private const char UnresolvedMark = '?';
    private const char MixedMark = '~';
    private const string NotTakenMark = "·";
    private const string NotTakenMarkAscii = ".";
    private const string ResolvedMark = "✓";
    private const string ResolvedMarkAscii = "v";
    private const int MinimumGutter = 2;
    private const string Space = " ";
    private const int MarginColumns = 2;

    /// <summary>Digits of the widest line number of a version pane, at least two.</summary>
    public static int GutterOf(ConflictEditorState state, ConflictPane pane) =>
        Math.Max(MinimumGutter, Digits(state.Alignment.LastNumberOf(pane)));

    /// <summary>Digits of the widest result line number, at least two.</summary>
    public static int ResultGutter(ConflictEditorState state) =>
        Math.Max(MinimumGutter, Digits(state.ResultLineCount));

    /// <summary>One row of a version pane, exactly <paramref name="width"/> columns.</summary>
    public static StyledLine Version(
        ConflictEditorState state,
        ConflictPane pane,
        int row,
        int width,
        GraphGlyphSet glyphs)
    {
        var alignment = state.Alignment;
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        var hunk = alignment.HunkAt(row);
        if (row >= alignment.RowCount)
        {
            return builder.Build();
        }

        AppendMargin(builder, state, hunk, glyphs, pane);
        var number = alignment.NumberAt(row, pane);
        var gutter = GutterOf(state, pane);
        builder.Append(Gutter(number, gutter), SegmentRole.Faint);
        AppendText(builder, state, pane, row, hunk);
        var line = builder.Build();

        return Styled(line, SelectionOfVersionRow(state, pane, row), hunk == state.Index && hunk >= 0);
    }

    /// <summary>One row of the result pane: <paramref name="rendered"/> is the editor's own line for it.</summary>
    public static StyledLine Result(
        ConflictEditorState state,
        int line,
        StyledLine rendered,
        GraphGlyphSet glyphs)
    {
        var lineCount = state.ResultLineCount;
        var margin = new LineBuilder(MarginColumns, glyphs.Ellipsis);
        var hunk = line < lineCount ? state.Result.RegionAt(line) : -1;
        AppendMargin(margin, state, hunk, glyphs);
        var gutterWidth = ResultGutter(state);
        var number = line < lineCount ? line + 1 : (int?)null;
        var role = RoleOfRegion(state, hunk);
        var segments = new List<StyledSegment>(margin.Build().Segments)
        {
            new(Gutter(number, gutterWidth), SegmentRole.Faint),
        };
        segments.AddRange(rendered.Segments.Select(segment =>
            segment.Role == SegmentRole.Plain ? segment with { Role = role } : segment));

        return Styled(new StyledLine(segments), SelectionOfResultRow(state, line), hunk == state.Index && hunk >= 0);
    }

    /// <summary>The columns taken in front of the text of a result row.</summary>
    public static int ResultPrefixWidth(ConflictEditorState state) => MarginColumns + ResultGutter(state) + 1;

    /// <summary>The mark of a settled conflict, in the glyph set's range.</summary>
    public static string ResolvedMarkFor(GraphGlyphSet glyphs) => glyphs.IsAscii ? ResolvedMarkAscii : ResolvedMark;

    /// <summary>The text with tabs shown as four spaces, as the version panes draw it.</summary>
    public static string Printable(string line) => line.Replace("\t", TabText, StringComparison.Ordinal);

    // The second margin column is the state of the conflict: `?` unresolved, in a version pane a check mark on the
    // version(s) the result took, a dot on the others and `~` on all of them for picked lines or typed text, in the
    // result a check mark for any settled conflict (the one place that says what a conflict is, no marker on the
    // pane headers).
    private static void AppendMargin(
        LineBuilder builder,
        ConflictEditorState state,
        int hunk,
        GraphGlyphSet glyphs,
        ConflictPane pane = ConflictPane.Result)
    {
        if (hunk < 0)
        {
            builder.Append("  ", SegmentRole.Plain);

            return;
        }

        var bar = hunk == state.Index ? glyphs.For(LineDirections.Up | LineDirections.Down).ToString() : Space;
        builder.Append(bar, SegmentRole.DiffGroupBar);
        if (state.Result.IsUnresolved(hunk))
        {
            builder.Append(UnresolvedMark.ToString(), SegmentRole.Error);

            return;
        }

        var pick = state.Result.Regions[hunk].Pick;
        if (pane == ConflictPane.Result || IsTaken(pane, pick))
        {
            builder.Append(ResolvedMarkFor(glyphs), SegmentRole.DiffAdded);
        }
        else if (pick is ConflictPick.Lines or ConflictPick.Edited)
        {
            builder.Append(MixedMark.ToString(), SegmentRole.DiffAdded);
        }
        else
        {
            builder.Append(glyphs.IsAscii ? NotTakenMarkAscii : NotTakenMark, SegmentRole.Faint);
        }
    }

    private static bool IsTaken(ConflictPane pane, ConflictPick pick) => pane switch
    {
        ConflictPane.Ours => pick is ConflictPick.Ours or ConflictPick.Both,
        ConflictPane.Theirs => pick is ConflictPick.Theirs or ConflictPick.Both,
        ConflictPane.Base => pick == ConflictPick.Base,
        _ => false,
    };

    private static void AppendText(LineBuilder builder, ConflictEditorState state, ConflictPane pane, int row, int hunk)
    {
        var alignment = state.Alignment;
        var text = alignment.TextAt(row, pane);
        if (text is null)
        {
            AppendPlaceholder(builder, state, pane, row, hunk);

            return;
        }

        var full = Printable(text);
        var skip = TextWidth.SkipColumns(full, state.HorizontalScroll);
        var printable = full[skip.Index..];
        builder.Append(new string(' ', skip.Overshoot), SegmentRole.Plain);
        if (hunk < 0)
        {
            builder.Append(printable, SegmentRole.Plain);

            return;
        }

        var ours = alignment.TextAt(row, ConflictPane.Ours);
        var theirs = alignment.TextAt(row, ConflictPane.Theirs);
        var differs = !string.Equals(ours, theirs, StringComparison.Ordinal);
        var role = RoleOfVersion(pane, differs);
        var changes = differs ? Shifted(WordChanges(alignment, pane, row), skip.Index) : [];
        if (pane == ConflictPane.Base)
        {
            ChangedText.Append(builder, printable, role, changes, SegmentRole.DiffRemovedWord);
        }
        else
        {
            ChangedText.Append(builder, printable, role, changes);
        }
    }

    // The word marks of a line whose first characters were scrolled out of view: spans move left, and the part of a
    // span that lies in the hidden start goes with it.
    private static IReadOnlyList<ChangedSpan> Shifted(IReadOnlyList<ChangedSpan> changes, int hidden)
    {
        if (hidden == 0 || changes.Count == 0)
        {
            return changes;
        }

        var shifted = new List<ChangedSpan>(changes.Count);
        foreach (var span in changes.Where(span => span.End > hidden))
        {
            var start = Math.Max(span.Start, hidden);
            shifted.Add(new ChangedSpan(start - hidden, span.End - start));
        }

        return shifted;
    }

    private static void AppendPlaceholder(
        LineBuilder builder,
        ConflictEditorState state,
        ConflictPane pane,
        int row,
        int hunk)
    {
        if (hunk < 0 || state.Alignment.RowAt(row).Offset != 0)
        {
            return;
        }

        var conflict = state.Hunks[hunk];
        var text = pane switch
        {
            ConflictPane.Base => conflict.Base is null ? NoBase : EmptyBase,
            _ => EmptySide,
        };
        builder.Append(text, SegmentRole.Faint);
    }

    private static SegmentRole RoleOfVersion(ConflictPane pane, bool differs)
    {
        if (!differs)
        {
            return SegmentRole.Plain;
        }

        return pane switch
        {
            ConflictPane.Ours => SegmentRole.DiffRemoved,
            ConflictPane.Theirs => SegmentRole.DiffAdded,
            _ => SegmentRole.Plain,
        };
    }

    // The words that differ: ours and theirs against the base when the row has one, else against each other; the base
    // shows the words either side changed.
    private static IReadOnlyList<ChangedSpan> WordChanges(ConflictAlignment alignment, ConflictPane pane, int row)
    {
        var ours = Printed(alignment.TextAt(row, ConflictPane.Ours));
        var theirs = Printed(alignment.TextAt(row, ConflictPane.Theirs));
        var baseText = Printed(alignment.TextAt(row, ConflictPane.Base));
        if (pane == ConflictPane.Base)
        {
            return ChangedSpan.Union(Against(baseText, ours), Against(baseText, theirs));
        }

        var own = pane == ConflictPane.Ours ? ours : theirs;
        if (own is null)
        {
            return [];
        }

        if (baseText is not null)
        {
            return WordDiff.Compare(baseText, own).New;
        }

        var other = pane == ConflictPane.Ours ? theirs : ours;
        if (other is null)
        {
            return [];
        }

        return pane == ConflictPane.Ours ? WordDiff.Compare(own, other).Old : WordDiff.Compare(other, own).New;
    }

    private static string? Printed(string? text) => text is null ? null : Printable(text);

    private static IReadOnlyList<ChangedSpan> Against(string? baseText, string? side) =>
        baseText is null || side is null ? [] : WordDiff.Compare(baseText, side).Old;

    private static SegmentRole RoleOfRegion(ConflictEditorState state, int hunk)
    {
        if (hunk < 0)
        {
            return SegmentRole.Plain;
        }

        return state.Result.IsUnresolved(hunk) ? SegmentRole.Error : SegmentRole.DiffAdded;
    }

    private static SegmentSelection SelectionOfVersionRow(ConflictEditorState state, ConflictPane pane, int row)
    {
        var isActive = state.ActivePane == pane;
        if (isActive && state.IsVisual && row >= state.SelectionStart && row <= state.SelectionEnd)
        {
            return SegmentSelection.Range;
        }

        if (row != state.Row)
        {
            return SegmentSelection.Inherit;
        }

        return isActive ? SegmentSelection.Focused : SegmentSelection.Dim;
    }

    private static SegmentSelection SelectionOfResultRow(ConflictEditorState state, int line)
    {
        if (state.ActivePane == ConflictPane.Result)
        {
            return line == state.Result.Editor.CursorRow ? SegmentSelection.Focused : SegmentSelection.Inherit;
        }

        return line == state.ResultLineOfRow(state.Row) ? SegmentSelection.Dim : SegmentSelection.Inherit;
    }

    private static StyledLine Styled(StyledLine line, SegmentSelection selection, bool isCurrent)
    {
        if (selection == SegmentSelection.Inherit && !isCurrent)
        {
            return line;
        }

        return new StyledLine(
        [
            .. line.Segments.Select(segment => segment with { Selection = selection, InGroup = isCurrent }),
        ]);
    }

    private static string Gutter(int? number, int width)
    {
        if (number is { } value)
        {
            return $"{value.ToString(CultureInfo.InvariantCulture).PadLeft(width)} ";
        }

        return new string(' ', width + 1);
    }

    private static int Digits(int number) => number.ToString(CultureInfo.InvariantCulture).Length;
}
