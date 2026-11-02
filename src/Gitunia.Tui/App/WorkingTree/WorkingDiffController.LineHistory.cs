using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Tui.App.FileHistory;

namespace Gitunia.Tui.App.WorkingTree;

// The lines of the shown diff whose history to follow.
public sealed partial class WorkingDiffController
{
    /// <summary>The revision the old side of a working tree diff belongs to.</summary>
    public const string HeadRevision = "HEAD";

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

        if (DiffLineRanges.RefusalFor(target.Entry, target.Side, target.Folder is not null) is { } refusal)
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
        return new LineHistoryTarget(DiffLineRanges.HeadPathOf(target.Entry), HeadRevision, picked.Range, picked.Notice);
    }
}
