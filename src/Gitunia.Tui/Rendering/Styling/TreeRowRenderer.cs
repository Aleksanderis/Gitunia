using Gitunia.Tui.Rendering.Sidebar;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// Draws one row of a tree (the sidebar and the Files tab) as a line of an exact width: a folder is a chevron, a name
/// and a count; a leaf is a prefix, a name and markers. A name that must be cut is cut in the middle, the markers keep
/// their columns and the last one gives way first.
/// </summary>
public static class TreeRowRenderer
{
    private const int MinimumLabelWidth = 8;

    /// <summary>A folder row: <paramref name="indent"/>, the chevron, the name and a faint count after it.</summary>
    /// <param name="indent">Blank columns before the chevron.</param>
    /// <param name="chevron">The open or closed marker.</param>
    /// <param name="name">The folder name as shown (with any trailing slash).</param>
    /// <param name="count">The count text including its leading blank, empty for none.</param>
    /// <param name="width">Columns of the row.</param>
    /// <param name="ellipsis">The cut marker of the glyph set.</param>
    /// <param name="isSelected">Whether the cursor is on the row.</param>
    public static StyledLine Folder(
        string indent,
        char chevron,
        string name,
        string count,
        int width,
        string ellipsis,
        bool isSelected)
    {
        var available = width - TextWidth.Of($"{indent}{chevron} ") - TextWidth.Of(count);

        return new LineBuilder(width, ellipsis)
            .Append($"{indent}{chevron} ", SegmentRole.Plain)
            .Append(TextWidth.FitMiddle(name, Math.Max(1, available), ellipsis), SegmentRole.SectionTitle)
            .Append(count, SegmentRole.Faint)
            .Build(isSelected: isSelected);
    }

    /// <summary>A leaf row: <paramref name="prefix"/>, the name and the markers after it.</summary>
    /// <param name="prefix">Everything before the name (indent and current-branch mark or alignment blanks).</param>
    /// <param name="label">The name.</param>
    /// <param name="labelRole">The colour of the name.</param>
    /// <param name="extras">The markers after the name, left to right.</param>
    /// <param name="width">Columns of the row.</param>
    /// <param name="ellipsis">The cut marker of the glyph set.</param>
    /// <param name="isSelected">Whether the cursor is on the row.</param>
    public static StyledLine Leaf(
        string prefix,
        string label,
        SegmentRole labelRole,
        IReadOnlyList<SidebarExtra> extras,
        int width,
        string ellipsis,
        bool isSelected)
    {
        var extrasWidth = extras.Sum(extra => 1 + TextWidth.Of(extra.Text));
        var available = width - TextWidth.Of(prefix);
        var labelBudget = Math.Max(
            Math.Min(TextWidth.Of(label), Math.Max(MinimumLabelWidth, available / 2)),
            available - extrasWidth);
        var builder = new LineBuilder(width, ellipsis)
            .Append(prefix, SegmentRole.Plain)
            .Append(TextWidth.FitMiddle(label, labelBudget, ellipsis), labelRole);

        // The last extra takes what is left, cut in the middle.
        var remaining = available - Math.Min(TextWidth.Of(label), labelBudget);
        for (var index = 0; index < extras.Count; index++)
        {
            var extra = extras[index];
            var text = index == extras.Count - 1
                ? TextWidth.FitMiddle(extra.Text, remaining - 1, ellipsis)
                : extra.Text;
            if (text.Length == 0 || text == ellipsis)
            {
                break;
            }

            builder.Append(" ", SegmentRole.Plain).Append(text, extra.Role);
            remaining -= 1 + TextWidth.Of(text);
        }

        return builder.Build(isSelected: isSelected);
    }
}
