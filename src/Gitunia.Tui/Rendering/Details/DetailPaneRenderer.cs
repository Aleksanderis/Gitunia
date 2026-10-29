using System.Globalization;
using Gitunia.Core.FileTree;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>Draws the detail pane's title and rows, cut to the width.</summary>
public static class DetailPaneRenderer
{
    /// <summary>The title row followed by <paramref name="contentHeight"/> rows starting at the pane's top row; the rows are padded (<see cref="PanePadding"/>).</summary>
    public static IReadOnlyList<StyledLine> RenderPane(DetailPaneModel pane, int width, int contentHeight, GraphGlyphSet glyphs, int paddingColumns = PanePadding.Columns)
    {
        var title = RenderTitle(pane.Content, pane.IsFocused, width, glyphs, isRepeating: pane.IsRepeating);
        var lines = new List<StyledLine>(contentHeight + 1) { title };
        for (var offset = 0; offset < contentHeight; offset++)
        {
            var row = pane.TopRow + offset;
            var textWidth = PanePadding.TextWidth(width, paddingColumns);
            lines.Add(PanePadding.Wrap(RenderLine(pane, row, textWidth, glyphs), paddingColumns));
        }

        return lines;
    }

    /// <summary>
    /// The title row, which is the top edge of the pane's box: a stretch of line, the tabs, the commit and its file count, and line
    /// again to the end (the line is <see cref="SegmentRole.Rule"/>, drawn by the frame in the colour of the focus).
    /// </summary>
    /// <param name="content">The rows and the selected tab.</param>
    /// <param name="isFocused">Whether the pane has focus.</param>
    /// <param name="width">Columns of the title row.</param>
    /// <param name="glyphs">The glyph set.</param>
    /// <param name="keys">Where the key text of the view chip comes from; null uses the default keymap.</param>
    public static StyledLine RenderTitle(
        DetailContent content,
        bool isFocused,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null,
        bool isRepeating = false)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        AppendTitleText(builder, content, isFocused, width, glyphs, isRepeating);
        if (ViewButton(content, width, glyphs, keys) is not { } chip)
        {
            return builder.BuildRule(glyphs.Horizontal);
        }

        var used = Math.Min(width - builder.Remaining, chip.Start);
        builder.Append(new string(glyphs.Horizontal, chip.Start - used), SegmentRole.Rule);
        builder.Append(ViewChip.Line(chip, glyphs));

        return builder.BuildRule(glyphs.Horizontal);
    }

    /// <summary>
    /// The <c>view ▾</c> chip at the right end of the title row, or null when the Details tab is selected or the row
    /// has no room.
    /// </summary>
    /// <param name="content">The rows and the selected tab.</param>
    /// <param name="width">Columns of the title row.</param>
    /// <param name="glyphs">The glyph set.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static TitleButton? ViewButton(
        DetailContent content,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        if (content.Tab == BottomTab.Details)
        {
            return null;
        }

        var probe = new LineBuilder(width, glyphs.Ellipsis);
        AppendTitleText(probe, content, false, width, glyphs);

        // One column of line follows the chip, then the corner of the box.
        return ViewChip.Layout(
            DefaultMouseMap.DetailViewButton,
            content.Tab == BottomTab.Files ? DefaultKeymap.DetailFilesView : DefaultKeymap.DetailChangesView,
            width - probe.Remaining,
            width - 2,
            keys);
    }

    private static void AppendTitleText(
        LineBuilder builder,
        DetailContent content,
        bool isFocused,
        int width,
        GraphGlyphSet glyphs,
        bool isRepeating = false)
    {
        var strip = DetailTabStrip.Layout(width, content.Tab);
        var column = 0;
        foreach (var button in strip)
        {
            builder.Append(new string(glyphs.Horizontal, button.Start - column), SegmentRole.Rule);
            var isSelected = DetailTabStrip.TabOf(button) == content.Tab;
            builder.Append(ButtonLabel.For(button.Label, glyphs.IsAscii), TabRole(isSelected, isFocused));
            column = button.Start + button.Width;
        }

        // Right after the tabs, so a narrow row cuts the commit and its file count first.
        if (isRepeating)
        {
            builder.Append(new string(glyphs.Horizontal, 1), SegmentRole.Rule);
            builder.Append($" {RepeatMode.TitleMarker} ", SegmentRole.Faint);
        }

        if (content.ShortHash.Length > 0)
        {
            builder.Append(new string(glyphs.Horizontal, 1), SegmentRole.Rule);
            builder.Append($" {content.ShortHash} ", SegmentRole.Hash);
            builder.Append(new string(glyphs.Horizontal, 1), SegmentRole.Rule);
            builder.Append($" {FileCountText(content.FileCount)} ", SegmentRole.Faint);
        }
    }

    /// <summary>A row of the pane; a file row of the Changes tab is laid out in <paramref name="pathStyle"/> for the
    /// width it gets.</summary>
    /// <param name="row">The row.</param>
    /// <param name="isSelected">Whether the cursor is on the row.</param>
    /// <param name="width">Columns of the text area.</param>
    /// <param name="glyphs">The glyph set.</param>
    /// <param name="pathStyle">How a file row shows its path.</param>
    public static StyledLine RenderRow(
        DetailRow row,
        bool isSelected,
        int width,
        GraphGlyphSet glyphs,
        FilePathStyle pathStyle = FilePathStyle.FullPath)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (row.File is { } file && pathStyle == FilePathStyle.NameFirst)
        {
            var mark = row.Segments[0];
            builder.Append(mark.Text + " ", mark.Role, mark.ColorIndex);
            var columns = builder.Remaining;
            var segments = PathRowLayout.Segments(pathStyle, file.Path, file.OldPath, columns, glyphs.Ellipsis);
            foreach (var segment in segments)
            {
                builder.Append(segment.Text, segment.Role);
            }

            return builder.Build(SegmentRole.Plain, isSelected);
        }

        if (row.Tree is { } tree)
        {
            return RenderTreeRow(tree, isSelected, width, glyphs.Ellipsis);
        }

        foreach (var segment in row.Segments)
        {
            builder.Append(segment.Text, segment.Role, segment.ColorIndex);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    // A Files tab row goes through the renderer the sidebar uses, so long names are cut in the middle.
    internal static StyledLine RenderTreeRow(FileTreeLine tree, bool isSelected, int width, string ellipsis)
    {
        var row = tree.Row;
        var indent = new string(' ', row.Depth * DetailContentBuilder.TreeIndent);
        if (row.Kind == FileTreeRowKind.Directory)
        {
            var count = row.IsExpanded ? string.Empty : $"  {DetailContentBuilder.FileCountText(row.FileCount)}";

            return TreeRowRenderer.Folder(indent, tree.Chevron, row.Label, count, width, ellipsis, isSelected);
        }

        // Files line up with the names of directories, which start after their chevron.
        return TreeRowRenderer.Leaf(
            $"{indent}  ",
            row.Label,
            SegmentRole.Plain,
            [new SidebarExtra(tree.Suffix, SegmentRole.Faint)],
            width,
            ellipsis,
            isSelected);
    }

    /// <summary>A row with the quick-jump label drawn over it: the text after the change mark is flattened to one colour so the match and the letter show.</summary>
    /// <param name="content">The rows of the pane.</param>
    /// <param name="index">The row.</param>
    /// <param name="isSelected">Whether the cursor is on the row.</param>
    /// <param name="width">Columns of the text area.</param>
    /// <param name="glyphs">The glyph set.</param>
    /// <param name="jump">The label of the row.</param>
    public static StyledLine RenderRow(DetailContent content, int index, bool isSelected, int width, GraphGlyphSet glyphs, JumpLabel jump)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (DetailJumpRows.MarkColumns(content, index) > 0)
        {
            var mark = content.Rows[index].Segments[0];
            builder.Append(mark.Text + " ", mark.Role, mark.ColorIndex);
        }

        var text = DetailJumpRows.TextOf(content, index, width, glyphs.Ellipsis);
        JumpOverlay.Append(builder, text, SegmentRole.Plain, jump);

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>A row past the last one: blank.</summary>
    public static StyledLine RenderEmpty(int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis).Build();

    private static StyledLine RenderLine(DetailPaneModel pane, int row, int textWidth, GraphGlyphSet glyphs)
    {
        if (row < 0 || row >= pane.Content.Rows.Count)
        {
            return RenderEmpty(textWidth, glyphs);
        }

        return RenderRow(
            pane.Content.Rows[row],
            pane.IsFocused && row == pane.SelectedRow,
            textWidth,
            glyphs,
            pane.Content.PathStyle);
    }

    private static SegmentRole TabRole(bool isSelected, bool isFocused)
    {
        if (!isSelected)
        {
            return SegmentRole.RepositoryTab;
        }

        return isFocused ? SegmentRole.ActiveTab : SegmentRole.SelectedTab;
    }

    // ".. files" until the count is known, so the title does not show 0 and then jump to the real number.
    private static string FileCountText(int? count) => count switch
    {
        null => ".. files",
        1 => "1 file",
        { } number => $"{number.ToString(CultureInfo.InvariantCulture)} files",
    };
}
