using System.Globalization;
using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws sidebar rows and the filter line as styled lines of an exact width.</summary>
public static class SidebarRowRenderer
{
    private const int MinimumLabelWidth = 8;
    private const string WorktreeMarker = "wt:";

    public static StyledLine Render(SidebarRow row, int width, bool isSelected, GraphGlyphSet glyphs)
    {
        var sidebarGlyphs = SidebarGlyphs.For(glyphs);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (row.IsSection)
        {
            builder
                .Append(" " + (row.IsExpanded ? sidebarGlyphs.Expanded : sidebarGlyphs.Collapsed) + " ", SegmentRole.SectionTitle)
                .Append(row.Label, SegmentRole.SectionTitle)
                .Append(" " + row.Count.ToString(CultureInfo.InvariantCulture), SegmentRole.Faint);
            return builder.Build(isSelected: isSelected);
        }

        const string indent = "  ";
        var prefix = indent + (row.IsCurrent ? "*" : " ") + " ";
        var extras = ExtrasOf(row, sidebarGlyphs);
        var extrasWidth = extras.Sum(extra => 1 + TextWidth.Of(extra.Text));
        var available = width - TextWidth.Of(prefix);
        var labelBudget = Math.Max(Math.Min(TextWidth.Of(row.Label), Math.Max(MinimumLabelWidth, available / 2)), available - extrasWidth);
        builder
            .Append(prefix, SegmentRole.Plain)
            .Append(TextWidth.Fit(row.Label, labelBudget, glyphs.Ellipsis), LabelRole(row));
        foreach (var extra in extras)
        {
            builder.Append(" ", SegmentRole.Plain).Append(extra.Text, extra.Role);
        }

        return builder.Build(isSelected: isSelected);
    }

    /// <summary>The last row of the sidebar: the filter being typed or the active one, blank otherwise.</summary>
    public static StyledLine FilterLine(string filterText, bool isEditing, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (isEditing || filterText.Length > 0)
        {
            builder.Append("/", SegmentRole.SectionTitle).Append(filterText, SegmentRole.Plain);
            if (isEditing)
            {
                builder.Append("_", SegmentRole.Faint);
            }
        }

        return builder.Build();
    }

    private static SegmentRole LabelRole(SidebarRow row) => row.Kind switch
    {
        SidebarRowKind.LocalBranch => row.IsCurrent ? SegmentRole.CurrentBranch : SegmentRole.LocalBranch,
        SidebarRowKind.RemoteBranch => SegmentRole.RemoteBranch,
        SidebarRowKind.Tag => SegmentRole.Tag,
        _ => SegmentRole.Plain,
    };

    private static List<(string Text, SegmentRole Role)> ExtrasOf(SidebarRow row, SidebarGlyphs sidebarGlyphs)
    {
        var extras = new List<(string, SegmentRole)>();
        var tracking = (row.Ahead is > 0 ? sidebarGlyphs.Ahead + row.Ahead.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)
            + (row.Behind is > 0 ? sidebarGlyphs.Behind + row.Behind.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        if (tracking.Length > 0)
        {
            extras.Add((tracking, SegmentRole.Punctuation));
        }

        if (row.Annotation.Length > 0)
        {
            var isBranch = row.Kind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch;
            extras.Add(((isBranch ? WorktreeMarker : string.Empty) + row.Annotation, SegmentRole.Faint));
        }

        return extras;
    }
}
