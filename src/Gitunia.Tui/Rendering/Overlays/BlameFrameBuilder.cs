using System.Globalization;
using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Text;
using Gitunia.Git.Blame.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the blame view over the whole screen but the status bar: a gutter with the short id, the author and the
/// age, the line number and the line. Only the visible lines are built, so a 50 000 line file costs the same as a
/// short one.
/// </summary>
public static class BlameFrameBuilder
{
    private const int ChromeRows = 7;
    private const int StatusRows = 1;
    private const int IdWidth = 9;
    private const int AuthorWidth = 14;
    private const int AgeWidth = 10;
    private const int NumberPadding = 5;
    private const int WideGutterMinimum = 72;
    private const int TabWidth = 4;
    private const char Tab = '\t';
    private const string Pending = "...";
    private const string Uncommitted = "uncommitted";
    private const string Separator = " ";
    private const string Divider = "|";
    private const int PercentScale = 100;

    private static readonly HintItem[] PlainHints = [new("/", "search"), new("Esc", "close")];

    private static readonly HintItem[] SteppedHints = [new("/", "search"), new("Esc", "back")];

    private static readonly HintChip[] ToggleChips =
    [
        new("-w", DefaultMouseMap.BlameWhitespaceButton),
        new("-M", DefaultMouseMap.BlameMovesButton),
        new("-C", DefaultMouseMap.BlameCopiesButton),
    ];

    /// <summary>The blame frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(
        BlameState state,
        BlameData? data,
        DateTimeOffset now,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs)
    {
        var rows = VisibleRows(screenHeight);
        if (screenWidth < OverlayBox.MinimumWidth || rows < 1)
        {
            return null;
        }

        var heading = $"Blame: {state.Title}";
        var lines = new List<StyledLine> { OverlayBox.Title(heading, screenWidth, glyphs) };
        var top = TopOf(state.Cursor, state.LineCount, rows);
        if (data is null)
        {
            lines.Add(OverlayBox.Text($"Reading {state.Path}...", screenWidth, glyphs, SegmentRole.Faint));
        }
        else if (state.LineCount == 0)
        {
            lines.Add(OverlayBox.Text("the file is empty", screenWidth, glyphs, SegmentRole.Faint));
        }

        for (var line = top; data is not null && line < Math.Min(state.LineCount, top + rows); line++)
        {
            lines.Add(Row(state, data, line, now, screenWidth, glyphs));
        }

        while (lines.Count < rows + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, screenWidth, glyphs));
        }

        lines.Add(OverlayBox.TwoColumns(
            "Enter",
            "show the line's commit in the graph",
            screenWidth,
            glyphs,
            SegmentRole.OverlayKey,
            SegmentRole.Faint));
        lines.Add(OverlayBox.TwoColumns(
            "Alt-p",
            "blame at the parent of the line's commit",
            screenWidth,
            glyphs,
            SegmentRole.OverlayKey,
            SegmentRole.Faint));
        lines.Add(OverlayBox.TwoColumns(
            "<space>H",
            "history of the file at this revision",
            screenWidth,
            glyphs,
            SegmentRole.OverlayKey,
            SegmentRole.Faint));
        lines.Add(HintChipRows.Line(ToggleChips.Select(WithKey).ToArray(), screenWidth, glyphs));
        lines.Add(OverlayBox.Hints(FooterHints(state), screenWidth, glyphs, FooterLead(state)));
        lines.Add(OverlayBox.Bottom(screenWidth, glyphs));

        return new OverlayFrame(0, 0, lines);
    }

    /// <summary>Lines the view shows at a screen height (the page size of the half-page motions).</summary>
    public static int VisibleRows(int screenHeight) => screenHeight - StatusRows - ChromeRows;

    /// <summary>The file line shown on a frame line, or null for none.</summary>
    public static int? LineIndexAt(BlameState state, int frameLine, int screenHeight)
    {
        var rows = VisibleRows(screenHeight);
        var offset = frameLine - 1;
        var line = TopOf(state.Cursor, state.LineCount, rows) + offset;
        if (offset >= 0 && offset < rows && line < state.LineCount)
        {
            return line;
        }

        return null;
    }

    /// <summary>The mouse view of the action row on a frame line; null when the line is none.</summary>
    public static string? ActionAt(int frameLine, int frameColumn, int screenHeight) =>
        (frameLine - VisibleRows(screenHeight)) switch
        {
            1 => DefaultMouseMap.BlameShowButton,
            2 => DefaultMouseMap.BlameParentButton,
            3 => DefaultMouseMap.BlameHistoryButton,
            4 => HintChipRows.ViewAt(ToggleChips.Select(WithKey).ToArray(), frameColumn),
            _ => null,
        };

    private static HintChip WithKey(HintChip chip) =>
        chip with { Keys = ChipKeys.Default.For(chip.View, DefaultKeymap.BlameView, KeyMode.Normal) ?? string.Empty };

    private static int TopOf(int cursor, int count, int rows)
    {
        var selected = count == 0 ? 0 : Math.Clamp(cursor, 0, count - 1);

        return Math.Clamp(selected - (rows / 2), 0, Math.Max(0, count - rows));
    }

    private static StyledLine Row(
        BlameState state,
        BlameData data,
        int line,
        DateTimeOffset now,
        int width,
        GraphGlyphSet glyphs)
    {
        var inner = OverlayBox.InnerWidth(width);
        var builder = new LineBuilder(inner, glyphs.Ellipsis).Append(Separator, SegmentRole.Plain);
        var owner = data.OwnerOf(line);
        var isWide = width >= WideGutterMinimum;
        builder.Append(Pad(IdText(owner), IdWidth, glyphs), owner is null ? SegmentRole.Faint : SegmentRole.Hash);
        if (isWide)
        {
            builder
                .Append(Pad(AuthorText(owner), AuthorWidth, glyphs), SegmentRole.Plain)
                .Append(Pad(AgeText(owner, now), AgeWidth, glyphs), SegmentRole.Faint);
        }

        var number = (line + 1).ToString(CultureInfo.InvariantCulture).PadLeft(NumberPadding);
        builder
            .Append($"{number}{Separator}{Divider}{Separator}", SegmentRole.Faint)
            .Append(Printable(data.Lines[line]), SegmentRole.Plain);

        return OverlayBox.Wrap(builder.Build(), glyphs, line == state.Cursor);
    }

    private static string IdText(BlameCommit? owner)
    {
        if (owner is null)
        {
            return Pending;
        }

        var hash = ShortHash.Of(owner.Id);

        return owner.IsBoundary ? $"^{hash}" : hash;
    }

    private static string AuthorText(BlameCommit? owner)
    {
        if (owner is null)
        {
            return string.Empty;
        }

        return owner.IsUncommitted ? Uncommitted : owner.Author;
    }

    private static string AgeText(BlameCommit? owner, DateTimeOffset now)
    {
        if (owner is null || owner.IsUncommitted)
        {
            return string.Empty;
        }

        return BlameDates.Relative(owner.AuthorTime, now);
    }

    private static string Pad(string text, int columns, GraphGlyphSet glyphs)
    {
        var fitted = TextWidth.Fit(text, columns - 1, glyphs.Ellipsis);

        return fitted + new string(' ', Math.Max(1, columns - TextWidth.Of(fitted)));
    }

    // Tabs and other control characters would break the column count.
    private static string Printable(string text)
    {
        if (!text.Any(character => character == Tab || char.IsControl(character)))
        {
            return text;
        }

        var expanded = text.Replace(Tab.ToString(), new string(' ', TabWidth));

        return string.Concat(expanded.Select(character => char.IsControl(character) ? ' ' : character));
    }

    private static HintItem[] FooterHints(BlameState state) =>
        state.HasSearch ? [new("Esc", state.IsEditingSearch ? "cancel" : "clear")] : Plain(state);

    private static HintItem[] Plain(BlameState state) => state.Steps.IsEmpty ? PlainHints : SteppedHints;

    // The switches in force and the ignore-revs file, so the user sees what shaped the attribution.
    private static string Flags(BlameState state)
    {
        var options = string.Join(' ', state.ActiveOptions.Arguments());
        var ignore = state.IgnoreRevsFile is null ? string.Empty : " ignore-revs";
        var switches = options.Length == 0 ? string.Empty : $"  {options}";

        return $"{switches}{ignore}";
    }

    private static string FooterLead(BlameState state)
    {
        if (state.HasSearch)
        {
            var cursor = state.IsEditingSearch ? "_" : string.Empty;

            return $"/{state.SearchText}{cursor}";
        }

        var position = state.LineCount == 0 ? 0 : state.Cursor + 1;
        var text = string.Create(CultureInfo.InvariantCulture, $"{position}/{state.LineCount}{Flags(state)}");
        if (state.IsComplete || state.LineCount == 0)
        {
            return text;
        }

        var percent = state.BlamedLines * PercentScale / state.LineCount;

        return string.Create(CultureInfo.InvariantCulture, $"{text}  blaming {percent}%");
    }
}
