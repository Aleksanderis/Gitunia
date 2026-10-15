using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>Draws one file of the Unstaged or Staged list: its status letter and path (a rename as <c>old -&gt; new</c>).</summary>
public static class WorkingTreeRowRenderer
{
    private const string ConflictMark = "!";
    private const string UntrackedMark = "?";
    private const string AddedMark = "A";
    private const string DeletedMark = "D";
    private const string RenameArrow = " -> ";

    /// <summary>The line of an entry, exactly <paramref name="width"/> columns wide.</summary>
    /// <param name="entry">The status entry.</param>
    /// <param name="section">The list the row is in; it decides which side of the change the letter shows.</param>
    /// <param name="width">Columns for the row's text.</param>
    /// <param name="isSelected">The row is the cursor of its list.</param>
    /// <param name="glyphs">The character set.</param>
    public static StyledLine Render(StatusEntry entry, WorkingTreeSection section, int width, bool isSelected, GraphGlyphSet glyphs)
    {
        var mark = MarkOf(entry, section);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        builder.Append(mark, RoleOf(mark), 0).Append(" ", SegmentRole.Plain);
        builder.Append(PathText(entry), SegmentRole.Plain);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            builder.Append($"  {ConflictText(entry.Conflict)}", SegmentRole.Faint);
        }

        return builder.Build(isSelected: isSelected);
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

    /// <summary>The text of the path: <c>old -&gt; new</c> for a rename or copy. Lone surrogates (bytes that are not UTF-8) show as replacement characters.</summary>
    public static string PathText(StatusEntry entry) =>
        entry.OriginalPath is { } original ? $"{Printable(original)}{RenameArrow}{Printable(entry.Path)}" : Printable(entry.Path);

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
        FileChangeKind.Renamed => "R",
        FileChangeKind.Copied => "C",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.UpdatedButUnmerged => "U",
        _ => "M",
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
