using System.Globalization;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>Draws sidebar rows and the filter line as styled lines of an exact width.</summary>
public static class SidebarRowRenderer
{
    private const int MinimumLabelWidth = 8;
    private const string WorktreeMarker = "wt:";
    private const char StagedLetter = 'S';
    private const char ChangedLetter = 'M';
    private const char ConflictedLetter = 'U';
    private const int ChangeLetterCount = 3;

    public static StyledLine Render(SidebarRow row, int width, bool isSelected, GraphGlyphSet glyphs)
    {
        var sidebarGlyphs = SidebarGlyphs.For(glyphs);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (row.IsSection)
        {
            var count = $" {row.Count.ToString(CultureInfo.InvariantCulture)}";
            var header = $" {(row.IsExpanded ? sidebarGlyphs.Expanded : sidebarGlyphs.Collapsed)} ";
            var buttons = ButtonsOf(row, width, glyphs);
            builder
                .Append(header, SegmentRole.SectionTitle)
                .Append(row.Label, SegmentRole.SectionTitle)
                .Append(count, SegmentRole.Faint);
            if (buttons.Count > 0)
            {
                var gap = Math.Max(0, buttons[0].Start - (width - builder.Remaining));
                builder.Append(new string(' ', gap), SegmentRole.Plain);
                foreach (var button in buttons)
                {
                    builder.Append(button.Label, SegmentRole.Button);
                    builder.Append(" ", SegmentRole.Plain);
                }
            }

            return builder.Build(isSelected: isSelected);
        }

        const string indent = "  ";
        var prefix = $"{indent}{(row.IsCurrent ? "*" : " ")} ";
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

    /// <summary>The buttons drawn on a header row of <paramref name="width"/> columns (none for other rows).</summary>
    public static IReadOnlyList<TitleButton> ButtonsOf(SidebarRow row, int width, GraphGlyphSet glyphs)
    {
        var sidebarGlyphs = SidebarGlyphs.For(glyphs);
        var marker = row.IsExpanded ? sidebarGlyphs.Expanded : sidebarGlyphs.Collapsed;
        var headerWidth = TextWidth.Of($" {marker} {row.Label} {row.Count.ToString(CultureInfo.InvariantCulture)}");
        return SidebarHeaderButtons.Layout(row, headerWidth, width);
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

    // Staged, changed (unstaged and untracked) and conflicted path counts, each shown only when above zero.
    private static string ChangesText(WorktreeStatus changes)
    {
        var parts = new List<string>(ChangeLetterCount);
        if (changes.Staged > 0)
        {
            parts.Add($"{StagedLetter}{changes.Staged.ToString(CultureInfo.InvariantCulture)}");
        }

        if (changes.Changed > 0)
        {
            parts.Add($"{ChangedLetter}{changes.Changed.ToString(CultureInfo.InvariantCulture)}");
        }

        if (changes.Conflicted > 0)
        {
            parts.Add($"{ConflictedLetter}{changes.Conflicted.ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join(' ', parts);
    }

    private static List<SidebarExtra> ExtrasOf(SidebarRow row, SidebarGlyphs sidebarGlyphs)
    {
        var extras = new List<SidebarExtra>();
        var tracking = (row.Ahead is > 0 ? sidebarGlyphs.Ahead + row.Ahead.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)
            + (row.Behind is > 0 ? sidebarGlyphs.Behind + row.Behind.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        if (tracking.Length > 0)
        {
            extras.Add(new SidebarExtra(tracking, SegmentRole.Punctuation));
        }

        if (row.Changes is { IsDirty: true } changes)
        {
            extras.Add(new SidebarExtra(ChangesText(changes), SegmentRole.FileChanged));
        }

        if (row.Annotation.Length > 0)
        {
            var isBranch = row.Kind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch;
            extras.Add(new SidebarExtra($"{(isBranch ? WorktreeMarker : string.Empty)}{row.Annotation}", SegmentRole.Faint));
        }

        return extras;
    }
}
