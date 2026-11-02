using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Discard;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Tui.App.WorkingTree;

// Stage and discard from the lists and the diff.
public sealed partial class WorkingTreeActionHandler
{
    private bool TryStage(RepositoryState tab, ActionId action)
    {
        if (!StagingActions.Handles(action))
        {
            return false;
        }

        if (WorkingPanes.IsDiff(tab.View.FocusedPane))
        {
            Message = StageFromDiff(tab, action);
            return true;
        }

        // A Visual range of files: stage and unstage act on all of them and the range ends once the operation started.
        var range = ListRange(tab);
        var isRangeAction = action == ActionIds.WorkingTreeStage || action == ActionIds.WorkingTreeUnstage;
        Message = context.Staging.Execute(tab, action, isRangeAction ? range : null);

        // The range ends once the operation started; a refusal (posted by the reducer) keeps it.
        if (Message.Length == 0 && isRangeAction && context.Staging.IsRunning(tab.Id))
        {
            context.Visual?.End();
        }

        return true;
    }

    // The Visual range of the focused file list, null when there is none.
    private RowRange? ListRange(RepositoryState tab) =>
        context.Visual is { IsActive: true, Range: { } range } && WorkingPanes.IsList(tab.View.FocusedPane) ? range : null;

    // In the diff the same keys act on the hunk under the cursor, or on the Visual lines.
    private string StageFromDiff(RepositoryState tab, ActionId action)
    {
        var isStage = action == ActionIds.WorkingTreeStage;
        var isUnstage = action == ActionIds.WorkingTreeUnstage;
        if (!isStage && !isUnstage)
        {
            return "Focus a file list first";
        }

        var isUnstagedSide = tab.View.FocusedPane == PaneKind.WorkingUnstagedDiff;
        if (isStage != isUnstagedSide)
        {
            return isStage ? "Stage works in the Unstaged diff" : "Unstage works in the Staged diff";
        }

        if (context.Diff is null)
        {
            return "The diff is not available";
        }

        var capture = context.Diff.CaptureStaging(context.Visual?.Range);
        if (capture.Request is not { } request)
        {
            return capture.Notice;
        }

        var notice = context.Staging.ExecuteSelection(tab, request, new DiffCursorMove(capture.HunkIndex, capture.ChangesBefore));
        if (notice.Length == 0 && context.Staging.IsRunning(tab.Id))
        {
            context.Visual?.End();
        }

        return notice;
    }

    // In the list the key acts on the file or folder under the cursor, or on the files of the Visual range; in the diff on
    // the hunk under the cursor or the Visual lines. The reducer asks first and the range ends once the user confirmed.
    private bool TryDiscard(RepositoryState tab, ActionId action)
    {
        if (action != ActionIds.WorkingTreeDiscard)
        {
            return false;
        }

        if (WorkingPanes.IsDiff(tab.View.FocusedPane))
        {
            Message = DiscardFromDiff(tab);
        }
        else
        {
            host.Dispatch(new DiscardRequested(tab.Id, ListRange(tab)));
        }

        return true;
    }

    private string DiscardFromDiff(RepositoryState tab)
    {
        if (tab.View.FocusedPane != PaneKind.WorkingUnstagedDiff)
        {
            return "Discard works in the Unstaged diff: unstage the changes first";
        }

        if (context.Diff is null)
        {
            return "The diff is not available";
        }

        var capture = context.Diff.CaptureStaging(context.Visual?.Range);
        if (capture.Request is not { } request)
        {
            return capture.Notice;
        }

        var move = new DiffCursorMove(capture.HunkIndex, capture.ChangesBefore);
        host.Dispatch(new DiscardSelectionRequested(tab.Id, request.ToSelection(), move));

        return string.Empty;
    }
}
