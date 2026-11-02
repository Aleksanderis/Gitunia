using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Selection;

/// <summary>
/// The lines whose history an action opens: the Visual range of the focused diff, else its cursor line. The commit diff
/// numbers them as the commit has the file; the diff of the Changes view as <c>HEAD</c> has it.
/// </summary>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="sessions">The shown tab's session, which holds the commit diff.</param>
/// <param name="commitDiff">The cursor and viewport handling of the commit diff pane.</param>
/// <param name="workingDiff">The diff box of the Changes view.</param>
/// <param name="visual">The Visual range.</param>
public sealed class LineHistorySelection(
    Dispatcher host,
    IActiveSessions sessions,
    DiffListActions commitDiff,
    WorkingDiffController workingDiff,
    VisualRangeInput visual) : ILineHistorySource
{
    /// <summary>The notice when the focus is not on a diff.</summary>
    public const string NoDiffNotice = "Focus a diff and select lines to see their history";

    /// <summary>The notice when the pane shows a file's content, which has no change to follow.</summary>
    public const string ContentNotice = "Open the diff of the file to follow lines";

    /// <inheritdoc />
    public LineHistoryTarget Resolve()
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return LineHistoryTarget.Refused(NoDiffNotice);
        }

        var range = visual.IsActive ? visual.Range : null;
        var pane = tab.View.FocusedPane;
        if (WorkingPanes.IsDiff(pane))
        {
            return workingDiff.CaptureLineHistory(range);
        }

        return pane == PaneKind.Diff ? OfCommitDiff(range) : LineHistoryTarget.Refused(NoDiffNotice);
    }

    private LineHistoryTarget OfCommitDiff(RowRange? range)
    {
        if (sessions.ActiveSession()?.Diff is not { } diff)
        {
            return LineHistoryTarget.Refused(NoDiffNotice);
        }

        var document = diff.Source.Document;
        if (document.IsFileContent)
        {
            return LineHistoryTarget.Refused(ContentNotice);
        }

        if (diff.Store.Status != DiffStatus.Loaded || diff.Store.Commit is not { } commit)
        {
            return LineHistoryTarget.Refused(WorkingDiffController.NotLoadedNotice);
        }

        var cursor = commitDiff.CursorRow(diff);
        var first = range is { } selected ? diff.Source.RowOf(selected.First) : cursor;
        var last = range is { } selectedLast ? diff.Source.LastRowOf(selectedLast.Last) : cursor;
        var picked = DiffLineRanges.Of(document, first, last, DiffNumbering.NewSide);
        if (!picked.IsAccepted)
        {
            return LineHistoryTarget.Refused(picked.Notice);
        }

        var path = document.FilePaths[picked.FileIndex][0];

        return new LineHistoryTarget(path, commit.ToString(), picked.Range, picked.Notice);
    }
}
