using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.FileHistory;

namespace Gitunia.Tui.App.WorkingTree;

// The lines of the shown diff whose history to follow.
public sealed partial class WorkingDiffController
{
    /// <summary>The revision the old side of a working tree diff belongs to.</summary>
    public const string HeadRevision = "HEAD";

    /// <summary>The notice for a file that was never committed.</summary>
    public const string UntrackedNotice = "This file is not committed: it has no history yet";

    /// <summary>The notice for an unstaged diff of a file that also has staged changes.</summary>
    public const string StagedToo = "This file has staged changes too: open the line history from its Staged diff";

    /// <summary>The notice for the diff of a folder.</summary>
    public const string FolderNotice = "This is the diff of a folder: pick one of its files for a line history";

    /// <summary>The notice for a conflicted file.</summary>
    public const string ConflictedNotice = "This file is conflicted: resolve it first";

    /// <summary>The notice while the diff is not on screen yet.</summary>
    public const string NotLoadedNotice = "The diff is still loading";

    /// <summary>
    /// The lines of the shown file to follow, numbered as <c>HEAD</c> has them (the old side of the diff): the Visual
    /// range, else the cursor line. Added lines are not committed and have no history.
    /// </summary>
    /// <param name="range">The Visual range in list lines, null for the cursor line.</param>
    public LineHistoryTarget CaptureLineHistory(RowRange? range)
    {
        if (_sessions.ActiveSession()?.WorkingDiff is not { } diff || diff.Store.Request is not { } target)
        {
            return LineHistoryTarget.Refused("There is no diff to follow");
        }

        if (diff.Store.Status != DiffStatus.Loaded)
        {
            return LineHistoryTarget.Refused(NotLoadedNotice);
        }

        if (LineHistoryRefusal(target) is { } refusal)
        {
            return LineHistoryTarget.Refused(refusal);
        }

        var cursor = _actions.CursorRow(diff);
        var first = range is { } selected ? diff.Source.RowOf(selected.First) : cursor;
        var last = range is { } selectedLast ? diff.Source.LastRowOf(selectedLast.Last) : cursor;
        var picked = DiffLineRanges.Of(diff.Source.Document, first, last, DiffNumbering.OldSide);
        if (!picked.IsAccepted)
        {
            return LineHistoryTarget.Refused(picked.Notice);
        }

        // A staged rename is followed under the name HEAD has.
        var path = target.Entry.OriginalPath ?? target.Entry.Path;

        return new LineHistoryTarget(path, HeadRevision, picked.Range, picked.Notice);
    }

    private static string? LineHistoryRefusal(WorkingDiffRequest target)
    {
        if (target.Folder is not null)
        {
            return FolderNotice;
        }

        if (target.Entry.Kind is StatusEntryKind.Untracked or StatusEntryKind.Ignored)
        {
            return UntrackedNotice;
        }

        if (target.Entry.Kind == StatusEntryKind.Unmerged)
        {
            return ConflictedNotice;
        }

        return target.Side == WorkingDiffSide.Unstaged && target.Entry.IsStaged ? StagedToo : null;
    }
}
