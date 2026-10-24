using Gitunia.Core.Diff;
using Gitunia.Core.Jump.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// Draws one line of the side-by-side diff: the old side, a vertical rule and the new side, each with its own line number, marker and
/// text (cut to its half, or wrapped over several screen lines); headers and notes span both sides like in the unified view.
/// </summary>
public static class DiffSplitRenderer
{
    /// <summary>The narrowest pane (text columns) that shows two sides; narrower panes keep the unified rows.</summary>
    public const int MinimumWidth = 80;

    private const int SeparatorWidth = 1;
    private const int SideMarkerAndSpaceWidth = 2;

    /// <summary>Columns of the old (left) side in a pane of <paramref name="width"/> columns.</summary>
    public static int LeftWidth(int width) => (width - SeparatorWidth) / 2;

    /// <summary>Columns of the new (right) side; one more than the left when the width is even.</summary>
    public static int RightWidth(int width) => width - SeparatorWidth - LeftWidth(width);

    /// <summary>Columns of the line number and marker in front of the text of one side.</summary>
    public static int SidePrefixWidth(int gutterDigits) => gutterDigits + SideMarkerAndSpaceWidth;

    /// <summary>How many screen lines <paramref name="line"/> takes when its text wraps in a pane of <paramref name="width"/> columns.</summary>
    public static int SegmentCount(DiffDocument document, DiffSplitLine line, int width, int gutterDigits)
    {
        if (line.IsFull)
        {
            return DiffRowRenderer.SegmentCount(document.Rows[line.LeftRow], width, gutterDigits);
        }

        return Math.Max(
            SideSegmentCount(document, line.LeftRow, LeftWidth(width), gutterDigits),
            SideSegmentCount(document, line.RightRow, RightWidth(width), gutterDigits));
    }

    /// <param name="document">The diff the line belongs to.</param>
    /// <param name="line">The line to draw.</param>
    /// <param name="segment">Which screen line of the line to draw (0 unless text wraps).</param>
    /// <param name="width">Columns of the pane.</param>
    /// <param name="gutterDigits">Digits of a line number column, from <see cref="DiffRowRenderer.GutterDigits"/>.</param>
    /// <param name="highlight">Text to highlight (the diff search), null for none.</param>
    /// <param name="isSelected">The line is under the cursor.</param>
    /// <param name="isWrapped">Text wraps instead of being cut.</param>
    /// <param name="glyphs">Supplies the ellipsis and the rule.</param>
    /// <param name="leftJump">The quick-jump label of the old side's row (the whole line's when it spans both).</param>
    /// <param name="rightJump">The quick-jump label of the new side's row.</param>
    /// <param name="showWhitespace">Draw tabs, spaces and carriage returns as marks.</param>
    public static StyledLine Render(
        DiffDocument document,
        DiffSplitLine line,
        int segment,
        int width,
        int gutterDigits,
        string? highlight,
        bool isSelected,
        bool isWrapped,
        GraphGlyphSet glyphs,
        JumpLabel? leftJump = null,
        JumpLabel? rightJump = null,
        bool showWhitespace = false)
    {
        if (line.IsFull)
        {
            var row = document.Rows[line.LeftRow];
            return isWrapped
                ? DiffRowRenderer.RenderSegment(
                    row, segment, width, gutterDigits, highlight, isSelected, glyphs, leftJump, showWhitespace)
                : DiffRowRenderer.Render(
                    row, width, gutterDigits, highlight, isSelected, glyphs, leftJump, showWhitespace);
        }

        var leftWidth = LeftWidth(width);
        var rightWidth = RightWidth(width);
        var left = RenderSide(
            document,
            line.LeftRow,
            true,
            segment,
            leftWidth,
            gutterDigits,
            highlight,
            isWrapped,
            glyphs,
            leftJump,
            showWhitespace);
        var right = RenderSide(
            document,
            line.RightRow,
            false,
            segment,
            rightWidth,
            gutterDigits,
            highlight,
            isWrapped,
            glyphs,
            rightJump,
            showWhitespace);
        var rule = glyphs.For(LineDirections.Up | LineDirections.Down).ToString();
        return new LineBuilder(width, glyphs.Ellipsis)
            .Append(left)
            .Append(rule, SegmentRole.Faint)
            .Append(right)
            .Build(SegmentRole.Plain, isSelected);
    }

    private static int SideSegmentCount(DiffDocument document, int row, int width, int gutterDigits) =>
        row < 0 ? 1 : DiffRowRenderer.TextSegmentCount(document.Rows[row].Text, width - SidePrefixWidth(gutterDigits));

    private static StyledLine RenderSide(
        DiffDocument document,
        int rowIndex,
        bool isOldSide,
        int segment,
        int width,
        int gutterDigits,
        string? highlight,
        bool isWrapped,
        GraphGlyphSet glyphs,
        JumpLabel? jump,
        bool showWhitespace)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (rowIndex < 0)
        {
            return builder.Build();
        }

        var row = document.Rows[rowIndex];
        var role = row.Kind switch
        {
            DiffRowKind.Added => SegmentRole.DiffAdded,
            DiffRowKind.Removed => SegmentRole.DiffRemoved,
            _ => SegmentRole.Plain,
        };
        var prefixWidth = SidePrefixWidth(gutterDigits);
        if (segment == 0)
        {
            var number = isOldSide ? row.OldLineNumber : row.NewLineNumber;
            var marker = row.Kind == DiffRowKind.Added ? "+" : row.Kind == DiffRowKind.Removed ? "-" : " ";
            builder.Append($"{DiffRowRenderer.Number(number, gutterDigits)} ", SegmentRole.Faint);
            builder.Append(marker, role == SegmentRole.Plain ? SegmentRole.Faint : role);
        }
        else
        {
            builder.Append(new string(' ', prefixWidth), SegmentRole.Plain);
        }

        var columns = width - prefixWidth;
        if (columns <= 0)
        {
            return builder.Build();
        }

        if (isWrapped)
        {
            var expanded = DiffRowRenderer.Expand(row.Text, showWhitespace ? glyphs : null, false);
            var chunks = DiffRowRenderer.Chunks(expanded.Text, columns);
            var chunk = segment < chunks.Count ? chunks[segment] : string.Empty;
            var start = DiffRowRenderer.StartOf(chunks, segment);
            AppendText(builder, chunk, role, highlight, segment == 0 ? jump : null, expanded.Marks, start);
        }
        else
        {
            var cut = DiffRowRenderer.CutToWidth(row.Text, columns);
            var shown = DiffRowRenderer.Expand(cut, showWhitespace ? glyphs : null, cut.Length != row.Text.Length);
            AppendText(builder, shown.Text, role, highlight, jump, shown.Marks, 0);
        }

        return builder.Build();
    }

    private static void AppendText(
        LineBuilder builder,
        string shown,
        SegmentRole role,
        string? highlight,
        JumpLabel? jump,
        bool[]? marks,
        int markOffset)
    {
        if (jump is { } target)
        {
            JumpOverlay.Append(builder, shown, role, target);
        }
        else
        {
            DiffRowRenderer.AppendHighlighted(builder, shown, role, highlight, marks, markOffset);
        }
    }
}
