using Gitunia.Core.Keymap;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App.WorkingTree;

// Staging the hunk or lines under the cursor and putting the cursor back after the diff reloads.
public sealed partial class WorkingDiffController
{
    /// <summary>
    /// Reads what to stage or unstage from the shown diff: the Visual range when there is one, else the hunk under the cursor (a
    /// file without hunks, such as a binary one, goes whole). Refuses while the diff is loading or cut short, for a conflicted
    /// file and for a diff that holds no file.
    /// </summary>
    /// <param name="range">The Visual range in list lines, null for the hunk under the cursor.</param>
    public DiffStagingCapture CaptureStaging(RowRange? range)
    {
        if (_sessions.ActiveSession()?.WorkingDiff is not { } diff || diff.Store.Request is not { } target)
        {
            return DiffStagingCapture.Refused("There is no diff to stage from");
        }

        var document = diff.Source.Document;
        if (diff.Store.Status != DiffStatus.Loaded)
        {
            return DiffStagingCapture.Refused("The diff is still loading");
        }

        if (diff.Store.IsCut)
        {
            return DiffStagingCapture.Refused(
                $"The diff is cut short: press {KeyTokens.Of(ActionIds.DiffLoadAnyway, "load all of it")} first");
        }

        if (target.Folder is not null)
        {
            return DiffStagingCapture.Refused(
                "This is the diff of a folder: pick one of its files to stage a hunk or lines");
        }

        if (target.Entry.Kind == StatusEntryKind.Unmerged)
        {
            return DiffStagingCapture.Refused($"{WorkingTreeRowRenderer.Printable(target.Entry.Path)} is conflicted: resolve it first");
        }

        if (document.FileRows.Count == 0)
        {
            return DiffStagingCapture.Refused("There is nothing to stage here");
        }

        var row = _actions.CursorRow(diff);
        var hunkIndex = DiffSelection.HunkIndexAt(document, row);
        if (range is { } selected)
        {
            var lines = DiffSelection.OfRows(document, diff.Source.RowOf(selected.First), diff.Source.LastRowOf(selected.Last));
            if (lines.Count == 0)
            {
                return DiffStagingCapture.Refused("No added or removed line is selected");
            }

            var before = DiffSelection.ChangedRowsBefore(document, diff.Source.RowOf(selected.First));

            return new DiffStagingCapture(new DiffStagingRequest(target, lines, true), hunkIndex, string.Empty, before);
        }

        var hunkLines = DiffSelection.OfHunk(document, row);
        if (hunkIndex >= 0 && hunkLines.Count == 0)
        {
            return DiffStagingCapture.Refused("The change has no added or removed line");
        }

        return new DiffStagingCapture(new DiffStagingRequest(target, hunkLines, false), hunkIndex, string.Empty);
    }

    /// <summary>After the next reload of the same file the cursor goes to the hunk at <paramref name="hunkIndex"/> (the next one when that hunk was staged).</summary>
    public void MoveToHunkAfterReload(int hunkIndex)
    {
        _hunkAfterReload = hunkIndex;
        _changeAfterReload = null;
    }

    /// <summary>
    /// After the next reload of the same file the cursor goes to the changed row with number <paramref name="changeNumber"/>
    /// (the first change after a staged range, the last one when none follows), scrolling only when it is off screen.
    /// </summary>
    public void MoveToChangeAfterReload(int changeNumber)
    {
        _changeAfterReload = changeNumber;
        _hunkAfterReload = null;
    }

    /// <summary>After the next reload of the same file the cursor goes where <paramref name="move"/> says (a staged or discarded hunk or range).</summary>
    public void MoveAfterReload(DiffCursorMove move)
    {
        if (move.ChangesBefore >= 0)
        {
            MoveToChangeAfterReload(move.ChangesBefore);
        }
        else
        {
            MoveToHunkAfterReload(move.HunkIndex);
        }
    }
}
