using Gitunia.Git.Diff.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Diff;

/// <summary>
/// Turns rows of a shown diff into the range of file lines <c>git log -L</c> follows. The numbers are those of the file
/// as the history starts from: the new side for a commit's diff (the file after the commit), the old side for a diff of
/// the working tree (the file in <c>HEAD</c>, so lines that are not committed yet have no number). Pure.
/// </summary>
public static class DiffLineRanges
{
    /// <summary>The notice when the rows hold no line of the file at the starting commit.</summary>
    public const string NoLineNotice = "Select lines of the file: this selection holds none";

    /// <summary>The notice when every selected line is in the working tree only.</summary>
    public const string UncommittedNotice = "Uncommitted lines have no history yet";

    /// <summary>The notice when the selected lines are all removed ones, which the commit no longer has.</summary>
    public const string RemovedNotice = "Removed lines are not in the file any more: select lines that are";

    /// <summary>The notice when the selection reaches into a second file.</summary>
    public const string FirstFileNotice = "Only the lines of the first file in the selection are followed";

    /// <summary>The notice for a file that was never committed.</summary>
    public const string UntrackedNotice = "This file is not committed: it has no history yet";

    /// <summary>The notice for an unstaged diff of a file that also has staged changes.</summary>
    public const string StagedTooNotice = "This file has staged changes too: open the line history from its Staged diff";

    /// <summary>The notice for the diff of a folder.</summary>
    public const string FolderNotice = "This is the diff of a folder: pick one of its files for a line history";

    /// <summary>The notice for a conflicted file.</summary>
    public const string ConflictedNotice = "This file is conflicted: resolve it first";

    /// <summary>Why the Changes view diff of a file cannot give a line history, null when it can.</summary>
    /// <param name="entry">The status entry of the file.</param>
    /// <param name="side">The list the file is in.</param>
    /// <param name="isFolder">The diff shows a folder, not one file.</param>
    public static string? RefusalFor(StatusEntry entry, WorkingDiffSide side, bool isFolder)
    {
        if (isFolder)
        {
            return FolderNotice;
        }

        if (entry.Kind is StatusEntryKind.Untracked or StatusEntryKind.Ignored)
        {
            return UntrackedNotice;
        }

        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return ConflictedNotice;
        }

        // The old side of an unstaged diff is the index: with staged changes its numbers are not those of HEAD.
        return side == WorkingDiffSide.Unstaged && entry.IsStaged ? StagedTooNotice : null;
    }

    /// <summary>The path HEAD knows the file by: a staged rename is followed under its old name.</summary>
    /// <param name="entry">The status entry of the file.</param>
    public static string HeadPathOf(StatusEntry entry) => entry.OriginalPath ?? entry.Path;

    /// <summary>Picks the lines of rows <paramref name="firstRow"/> to <paramref name="lastRow"/>.</summary>
    /// <param name="document">The shown diff.</param>
    /// <param name="firstRow">The first row (inclusive).</param>
    /// <param name="lastRow">The last row (inclusive).</param>
    /// <param name="side">Whose line numbers to take.</param>
    public static DiffLineSelection Of(DiffDocument document, int firstRow, int lastRow, DiffNumbering side)
    {
        var isNew = side == DiffNumbering.NewSide;
        var changed = isNew ? DiffRowKind.Added : DiffRowKind.Removed;
        var otherSide = isNew ? DiffRowKind.Removed : DiffRowKind.Added;
        var fileIndex = -1;
        var start = int.MaxValue;
        var end = int.MinValue;
        var leftOut = 0;
        var otherFile = false;
        for (var row = Math.Max(firstRow, 0); row <= lastRow && row < document.Rows.Count; row++)
        {
            var current = document.Rows[row];
            leftOut += current.Kind == otherSide ? 1 : 0;
            var number = isNew ? current.NewLineNumber : current.OldLineNumber;
            if (number is not { } line || (current.Kind != DiffRowKind.Context && current.Kind != changed))
            {
                continue;
            }

            fileIndex = fileIndex < 0 ? current.FileIndex : fileIndex;
            if (current.FileIndex != fileIndex)
            {
                otherFile = true;

                continue;
            }

            start = Math.Min(start, line);
            end = Math.Max(end, line);
        }

        if (fileIndex < 0)
        {
            return DiffLineSelection.Refused(RefusalOf(leftOut > 0, isNew));
        }

        return new DiffLineSelection(fileIndex, new LineRange(start, end), NoticeOf(otherFile, leftOut, isNew));
    }

    private static string RefusalOf(bool hasOtherSide, bool isNew)
    {
        if (!hasOtherSide)
        {
            return NoLineNotice;
        }

        return isNew ? RemovedNotice : UncommittedNotice;
    }

    // Uncommitted lines inside a range are followed through (git follows the committed lines around them), but said.
    private static string NoticeOf(bool otherFile, int leftOut, bool isNew)
    {
        if (otherFile)
        {
            return FirstFileNotice;
        }

        if (isNew || leftOut == 0)
        {
            return string.Empty;
        }

        var noun = leftOut == 1 ? "1 uncommitted line is" : $"{leftOut} uncommitted lines are";

        return $"{noun} not in the history";
    }
}
