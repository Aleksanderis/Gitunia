using Gitunia.App;
using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Selection;

/// <summary>
/// The file whose history an action opens: the file under the cursor of the detail pane (the Changes list or the
/// Files tree of the shown commit, so the history starts there) or of a Changes view list (it starts at HEAD).
/// </summary>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="sessions">The shown tab's session, which holds the detail rows and the shown commit.</param>
/// <param name="panes">The detail list.</param>
public sealed class FileHistorySelection(Dispatcher host, IActiveSessions sessions, PaneViews panes)
    : IFileHistoryTargetSource
{
    /// <summary>The revision a file of the Changes view starts from.</summary>
    public const string HeadRevision = "HEAD";

    /// <inheritdoc />
    public FileHistoryTarget? FileUnderCursor()
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return null;
        }

        var pane = tab.View.FocusedPane;
        if (WorkingPanes.IsList(pane))
        {
            return OfWorkingTree(tab, pane);
        }

        return pane == PaneKind.Detail ? OfDetail() : null;
    }

    /// <summary>The selected file of the Changes view list that <paramref name="pane"/> is, null for a folder.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="pane">The focused list.</param>
    public static FileHistoryTarget? OfWorkingTree(RepositoryState tab, PaneKind pane)
    {
        if (tab.WorkingTree?.Selection is not { IsFolder: false } selection
            || selection.Section != WorkingPanes.SectionOf(pane))
        {
            return null;
        }

        return new FileHistoryTarget(selection.Path, HeadRevision);
    }

    private FileHistoryTarget? OfDetail()
    {
        if (sessions.ActiveSession() is not { } session)
        {
            return null;
        }

        var path = PathOf(session.DetailSource.Content, panes.Detail.SelectedItem ?? -1);
        if (path is null)
        {
            return null;
        }

        var revision = session.Details.Current.Commit?.ToString() ?? HeadRevision;

        return new FileHistoryTarget(path, revision);
    }

    /// <summary>
    /// The path of the file on a detail row: the Files tab lists a tree (a directory row is no file), the Changes tab
    /// the changed files. Null on any other row.
    /// </summary>
    /// <param name="content">What the detail pane lists.</param>
    /// <param name="row">The selected row.</param>
    public static string? PathOf(DetailContent content, int row)
    {
        if (content.Tree.Count > 0)
        {
            var isFile = row >= 0 && row < content.Tree.Count && content.Tree[row].Kind != FileTreeRowKind.Directory;

            return isFile ? content.Tree[row].Path : null;
        }

        var index = content.FileIndexAt(row);

        return index >= 0 && index < content.Files.Count ? content.Files[index].Path : null;
    }
}
