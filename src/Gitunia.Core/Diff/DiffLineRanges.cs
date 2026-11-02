using Gitunia.Git.History.Models;

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

        return leftOut == 1 ? "1 uncommitted line is not in the history" : $"{leftOut} uncommitted lines are not in the history";
    }
}
