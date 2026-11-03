using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>Draws sidebar rows and the filter line as styled lines of an exact width.</summary>
public static class SidebarRowRenderer
{
    private const int DepthIndent = 1;
    private const char FolderSuffix = '/';
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
            var header = $"{(row.IsExpanded ? sidebarGlyphs.Expanded : sidebarGlyphs.Collapsed)} ";
            builder
                .Append(header, SegmentRole.SectionTitle)
                .Append(row.Label, SegmentRole.SectionTitle)
                .Append(count, SegmentRole.Faint);

            return builder.Build(isSelected: isSelected);
        }

        var indent = new string(' ', (row.Depth - SidebarRow.SectionDepth) * DepthIndent);
        if (row.IsFolder)
        {
            return RenderFolder(row, indent, width, isSelected, glyphs, sidebarGlyphs);
        }

        return TreeRowRenderer.Leaf(
            $"{indent}{(row.IsCurrent ? "*" : " ")} ",
            row.ShortName ?? row.Label,
            LabelRole(row),
            ExtrasOf(row, sidebarGlyphs),
            width,
            glyphs.Ellipsis,
            isSelected);
    }

    // A prefix folder: marker, name with a trailing slash (reads without colour) and its entry count.
    private static StyledLine RenderFolder(
        SidebarRow row,
        string indent,
        int width,
        bool isSelected,
        GraphGlyphSet glyphs,
        SidebarGlyphs sidebarGlyphs) => TreeRowRenderer.Folder(
            indent,
            row.IsExpanded ? sidebarGlyphs.Expanded : sidebarGlyphs.Collapsed,
            $"{row.Label}{FolderSuffix}",
            $" {row.Count.ToString(CultureInfo.InvariantCulture)}",
            width,
            glyphs.Ellipsis,
            isSelected);

    /// <summary>The last row of the sidebar: the filter being typed or the active one, blank otherwise.</summary>
    public static StyledLine FilterLine(string filterText, bool isEditing, int width, GraphGlyphSet glyphs, int caret = PromptCaret.End)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (isEditing || filterText.Length > 0)
        {
            // The caret is part of the text while editing, so the row keeps it in view when the filter is wider than the pane.
            var shown = isEditing ? PromptCaret.Mark(filterText, caret, Math.Max(1, width - 1)) : filterText;
            builder.Append("/", SegmentRole.SectionTitle).Append(shown, SegmentRole.Plain);
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

        if (row.IsUpstreamGone)
        {
            extras.Add(new SidebarExtra(sidebarGlyphs.UpstreamGone, SegmentRole.Faint));
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
