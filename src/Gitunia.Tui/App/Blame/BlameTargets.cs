using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Blame;

/// <summary>
/// Finds the file the blame action stands for: the selected file of the Changes view (blamed in the working tree), or
/// the selected file of the details pane (blamed at the commit the details show).
/// </summary>
/// <param name="lists">The lists of the panes, for the cursor of the details pane.</param>
/// <param name="sessions">The shown tab's session, which holds the details pane content.</param>
/// <param name="graph">The commit selected in the graph.</param>
public sealed class BlameTargets(IPaneLists lists, IActiveSessions sessions, IGraphSelectionSource graph)
    : IBlameTargets
{
    /// <summary>The notice when no file is selected.</summary>
    public const string NoFileNotice = "Select a file to blame it";

    /// <summary>The notice when the selected file was deleted by the shown commit.</summary>
    public const string DeletedNotice = "The commit deletes this file: there is nothing to blame";

    /// <summary>The notice when no commit is selected.</summary>
    public const string NoCommitNotice = "Select a commit to blame its files";

    /// <inheritdoc />
    public BlameTarget Resolve(RepositoryState tab)
    {
        if (tab.View.Mode == TabViewMode.WorkingTree)
        {
            return InWorkingTree(tab);
        }

        return InDetails();
    }

    private static BlameTarget InWorkingTree(RepositoryState tab)
    {
        if (tab.WorkingTree?.Selection is not { IsFolder: false } selection)
        {
            return BlameTarget.Refused(NoFileNotice);
        }

        var isStagedList = tab.View.FocusedPane == PaneKind.WorkingStaged;
        if (isStagedList != (selection.Section == WorkingTreeSection.Staged))
        {
            return BlameTarget.Refused(NoFileNotice);
        }

        return BlameTarget.Of(selection.Path, null);
    }

    private BlameTarget InDetails()
    {
        var content = sessions.ActiveSession()?.DetailSource.Content;
        var row = lists.ListOf(PaneKind.Detail).SelectedItem;
        if (content is null || row is not { } selected)
        {
            return BlameTarget.Refused(NoFileNotice);
        }

        if (graph.SelectedCommit() is not { } commit)
        {
            return BlameTarget.Refused(NoCommitNotice);
        }

        if (content.Tree.Count > 0)
        {
            return FromTree(content, selected, commit);
        }

        var index = content.FileIndexAt(selected);
        if (index < 0 || index >= content.Files.Count)
        {
            return BlameTarget.Refused(NoFileNotice);
        }

        var file = content.Files[index];

        return file.Kind == FileChangeKind.Deleted ? BlameTarget.Refused(DeletedNotice) : BlameTarget.Of(file.Path, commit);
    }

    private static BlameTarget FromTree(DetailContent content, int row, string commit)
    {
        if (row < 0 || row >= content.Tree.Count)
        {
            return BlameTarget.Refused(NoFileNotice);
        }

        var entry = content.Tree[row];
        var isFile = entry.Kind is FileTreeRowKind.File or FileTreeRowKind.Executable or FileTreeRowKind.Symlink;

        return isFile ? BlameTarget.Of(entry.Path, commit) : BlameTarget.Refused(NoFileNotice);
    }
}
