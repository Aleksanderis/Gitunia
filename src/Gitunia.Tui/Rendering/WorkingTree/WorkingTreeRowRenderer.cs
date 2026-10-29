using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>Draws one file of the Unstaged or Staged list: its status letter and path (a rename as <c>old -&gt; new</c>).</summary>
public static class WorkingTreeRowRenderer
{
    /// <summary>Columns the status letter and the blank after it take before the path.</summary>
    public const int MarkColumns = 2;

    private const string ConflictMark = "!";
    private const string UntrackedMark = "?";
    private const string AddedMark = "A";
    private const string DeletedMark = "D";
    private const string RenamedMark = "R";
    private const string CopiedMark = "C";
    private const string TypeChangedMark = "T";
    private const string UnmergedMark = "U";
    private const string ModifiedMark = "M";
    private const string RenameArrow = " -> ";

    /// <summary>The line of an entry, exactly <paramref name="width"/> columns wide.</summary>
    /// <param name="entry">The status entry.</param>
    /// <param name="section">The list the row is in; it decides which side of the change the letter shows.</param>
    /// <param name="width">Columns for the row's text.</param>
    /// <param name="isSelected">The row is the cursor of its list.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="jump">The quick-jump label of the row, null for none.</param>
    /// <param name="pathStyle">How the path is shown: the full path, or the name first with the directory dimmed
    /// after it.</param>
    public static StyledLine Render(
        StatusEntry entry,
        WorkingTreeSection section,
        int width,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null,
        FilePathStyle pathStyle = FilePathStyle.FullPath)
    {
        var mark = MarkOf(entry, section);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        builder.Append(mark, RoleOf(mark)).Append(" ", SegmentRole.Plain);
        var conflict = ConflictSuffix(entry);
        if (jump is { } target)
        {
            var shown = PathLayoutText(
                entry,
                section,
                pathStyle,
                Math.Max(0, builder.Remaining - TextWidth.Of(conflict)),
                glyphs.Ellipsis);
            JumpOverlay.Append(builder, shown, SegmentRole.Plain, target);
        }
        else if (pathStyle == FilePathStyle.FullPath)
        {
            builder.Append(PathText(entry, section), SegmentRole.Plain);
        }
        else
        {
            // The conflict text keeps its columns: the directory is what gives way.
            var columns = Math.Max(0, builder.Remaining - TextWidth.Of(conflict));
            foreach (var segment in PathSegments(entry, section, pathStyle, columns, glyphs.Ellipsis))
            {
                builder.Append(segment.Text, segment.Role);
            }
        }

        builder.Append(conflict, SegmentRole.Faint);

        return builder.Build(isSelected: isSelected);
    }

    /// <summary>The line of a file in the tree style: indented under its folder, the status letter and the
    /// name only.</summary>
    /// <param name="entry">The status entry.</param>
    /// <param name="row">The row of the file in the tree.</param>
    /// <param name="section">The list the row is in.</param>
    /// <param name="width">Columns for the row's text.</param>
    /// <param name="isSelected">The row is the cursor of its list.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="jump">The quick-jump label of the row, null for none.</param>
    public static StyledLine RenderTreeFile(
        StatusEntry entry,
        WorkingRow row,
        WorkingTreeSection section,
        int width,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null)
    {
        var mark = MarkOf(entry, section);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        builder.Append(TreeRowText.Indent(row.Depth), SegmentRole.Plain);
        builder.Append(mark, RoleOf(mark)).Append(" ", SegmentRole.Plain);
        var conflict = ConflictSuffix(entry);
        var name = TreeName(entry, row, section);
        var columns = Math.Max(0, builder.Remaining - TextWidth.Of(conflict));
        var shown = TextWidth.Fit(name, columns, glyphs.Ellipsis);
        if (jump is { } target)
        {
            JumpOverlay.Append(builder, shown, SegmentRole.Plain, target);
        }
        else
        {
            builder.Append(shown, SegmentRole.Plain);
        }

        builder.Append(conflict, SegmentRole.Faint);

        return builder.Build(isSelected: isSelected);
    }

    /// <summary>The line of a folder in the tree style: its name with a separator, and the file count when it
    /// is closed.</summary>
    /// <param name="row">The folder row.</param>
    /// <param name="closedFileCount">Files under the folder when it is closed, null when it is open.</param>
    /// <param name="width">Columns for the row's text.</param>
    /// <param name="isSelected">The row is the cursor of its list.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="jump">The quick-jump label of the row, null for none.</param>
    public static StyledLine RenderFolder(
        WorkingRow row,
        int? closedFileCount,
        int width,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        builder.Append(TreeRowText.Indent(row.Depth), SegmentRole.Plain);
        var name = FolderText(row);
        if (jump is { } target)
        {
            var shown = TextWidth.Fit(name, builder.Remaining, glyphs.Ellipsis);
            JumpOverlay.Append(builder, shown, SegmentRole.SectionTitle, target);
        }
        else
        {
            builder.Append(name, SegmentRole.SectionTitle);
        }

        if (closedFileCount is { } count)
        {
            builder.Append($"  {(count == 1 ? "1 file" : $"{count} files")}", SegmentRole.Faint);
        }

        return builder.Build(isSelected: isSelected);
    }

    /// <summary>The text of a folder row: its name and a separator.</summary>
    public static string FolderText(WorkingRow row) => $"{Printable(row.Name)}/";

    /// <summary>The name a file shows in the tree style (a rename as <c>old -&gt; new</c> in the Staged
    /// list).</summary>
    public static string TreeName(StatusEntry entry, WorkingRow row, WorkingTreeSection section) =>
        TreeRowText.FileName(
            Printable(entry.Path),
            Printable(row.Name),
            section == WorkingTreeSection.Staged && entry.OriginalPath is { } original ? Printable(original) : null);

    /// <summary>Columns the conflict text of an entry takes after its path; 0 when it is not conflicted.</summary>
    public static int ConflictColumns(StatusEntry entry) => TextWidth.Of(ConflictSuffix(entry));

    private static string ConflictSuffix(StatusEntry entry) =>
        entry.Kind == StatusEntryKind.Unmerged ? $"  {ConflictText(entry.Conflict)}" : string.Empty;

    /// <summary>The text of the path as <paramref name="style"/> lays it out in <paramref name="columns"/>, for the
    /// quick jump.</summary>
    public static string PathLayoutText(
        StatusEntry entry,
        WorkingTreeSection section,
        FilePathStyle style,
        int columns,
        string ellipsis)
    {
        if (style == FilePathStyle.FullPath)
        {
            return PathText(entry, section);
        }

        return string.Concat(PathSegments(entry, section, style, columns, ellipsis).Select(segment => segment.Text));
    }

    /// <summary>The status letter of an entry in a list: <c>!</c> for a conflict, <c>?</c> for an untracked path, else the change's letter.</summary>
    public static string MarkOf(StatusEntry entry, WorkingTreeSection section)
    {
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return ConflictMark;
        }

        if (entry.Kind == StatusEntryKind.Untracked)
        {
            return UntrackedMark;
        }

        return Letter(section == WorkingTreeSection.Staged ? entry.IndexChange : entry.WorktreeChange);
    }

    /// <summary>
    /// The text of the path: <c>old -&gt; new</c> for a rename or copy in the Staged list (the rename is a change of the index; an edit
    /// of the renamed file in the working tree is listed under its new path only). Lone surrogates (bytes that are not UTF-8) show as
    /// replacement characters.
    /// </summary>
    public static string PathText(StatusEntry entry, WorkingTreeSection section)
    {
        if (section == WorkingTreeSection.Staged && entry.OriginalPath is { } original)
        {
            return $"{Printable(original)}{RenameArrow}{Printable(entry.Path)}";
        }

        return Printable(entry.Path);
    }

    private static IReadOnlyList<StyledSegment> PathSegments(
        StatusEntry entry,
        WorkingTreeSection section,
        FilePathStyle style,
        int columns,
        string ellipsis)
    {
        var original = section == WorkingTreeSection.Staged && entry.OriginalPath is { } old ? Printable(old) : null;

        return PathRowLayout.Segments(style, Printable(entry.Path), original, columns, ellipsis);
    }

    /// <summary>The text with every lone surrogate replaced by the replacement character, so a terminal never gets a broken string.</summary>
    public static string Printable(string text)
    {
        if (!text.Any(char.IsSurrogate))
        {
            return text;
        }

        var characters = text.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (char.IsHighSurrogate(characters[index]) && index + 1 < characters.Length && char.IsLowSurrogate(characters[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(characters[index]))
            {
                characters[index] = '�';
            }
        }

        return new string(characters);
    }

    private static SegmentRole RoleOf(string mark) => mark switch
    {
        ConflictMark => SegmentRole.Error,
        UntrackedMark => SegmentRole.FileAdded,
        AddedMark => SegmentRole.FileAdded,
        DeletedMark => SegmentRole.FileDeleted,
        _ => SegmentRole.FileChanged,
    };

    private static string Letter(FileChangeKind change) => change switch
    {
        FileChangeKind.Added => AddedMark,
        FileChangeKind.Deleted => DeletedMark,
        FileChangeKind.Renamed => RenamedMark,
        FileChangeKind.Copied => CopiedMark,
        FileChangeKind.TypeChanged => TypeChangedMark,
        FileChangeKind.UpdatedButUnmerged => UnmergedMark,
        _ => ModifiedMark,
    };

    private static string ConflictText(ConflictKind conflict) => conflict switch
    {
        ConflictKind.BothDeleted => "both deleted",
        ConflictKind.AddedByUs => "added by us",
        ConflictKind.DeletedByThem => "deleted by them",
        ConflictKind.AddedByThem => "added by them",
        ConflictKind.DeletedByUs => "deleted by us",
        ConflictKind.BothAdded => "both added",
        ConflictKind.BothModified => "both modified",
        _ => "conflict",
    };
}
