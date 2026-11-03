using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Details;

/// <summary>
/// The keys of the bottom detail pane as pure transitions: its tab, the path style of the file lists, the filter and
/// the opened directories of the Files tab and the closed folders of the Changes tab's tree style. The state is the
/// tab's <see cref="DetailTreeState"/> and the key mode (Insert while the filter box is open).
/// </summary>
public static class DetailReducer
{
    /// <summary>The notice for the filter key outside the Files tab.</summary>
    public const string WrongTabNotice = "The filter works on the Files tab of the detail pane";

    private static readonly int TabCount = Enum.GetValues<BottomTab>().Length;

    /// <summary>Applies a command; a command for a tab that is not open any more changes nothing.</summary>
    public static AppUpdate Reduce(AppState state, DetailCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        return command switch
        {
            DetailTabMoved moved => MoveTab(state, tab, moved.Move),
            PathStyleChosen chosen => ChooseStyle(state, tab, chosen.Style),
            FileFilterStarted => StartFilter(state, tab),
            FileFilterKeyTyped typed => TypeFilter(state, tab, typed.Key),
            FileFilterAccepted => EndFilter(state, tab, false),
            FileFilterCancelled => EndFilter(state, tab, true),
            FileFilterCleared => ClearFilter(state, tab),
            FileTreeOpened opened => OpenRow(state, tab, opened.Cursor),
            FileTreeExpanded expanded => ExpandRow(state, tab, expanded.Cursor),
            FileTreeCollapsed collapsed => CollapseRow(state, tab, collapsed.Cursor),
            ChangesFolderToggled toggled => ToggleFolder(state, tab, toggled.Cursor),
            ChangesFolderExpanded expanded => ExpandFolder(state, tab, expanded.Cursor),
            ChangesFolderCollapsed collapsed => CollapseFolder(state, tab, collapsed.Cursor),
            _ => throw new UnhandledCommandException(command),
        };
    }

    /// <summary>
    /// A filter box left behind by a tab switch or a closed tab must not keep owning Insert mode: it closes and the
    /// mode returns to Normal.
    /// </summary>
    /// <param name="before">The workspace before the command.</param>
    /// <param name="after">The state after it.</param>
    public static AppState CloseFilterOfLeftTab(Workspace before, AppState after)
    {
        if (before.ActiveTab is not { } left
            || !left.View.DetailTree.IsEditingFilter
            || left.Id == after.Workspace.ActiveTabId)
        {
            return after;
        }

        var shell = after.Shell.Mode == KeyMode.Insert ? after.Shell.WithMode(KeyMode.Normal) : after.Shell;
        if (after.Workspace.FindTab(left.Id) is null)
        {
            return after with { Shell = shell };
        }

        var update = WorkspaceReducer.Reduce(
            after.Workspace,
            new TabCommand(left.Id, new SetDetailTreeCommand(left.View.DetailTree with { IsEditingFilter = false })));

        return new AppState(update.Workspace, shell);
    }

    private static AppUpdate MoveTab(AppState state, RepositoryState tab, DetailTabMove move)
    {
        var current = tab.View.BottomTab;
        var wanted = move switch
        {
            DetailTabMove.Next => (BottomTab)(((int)current + 1) % TabCount),
            DetailTabMove.Previous => (BottomTab)(((int)current + TabCount - 1) % TabCount),
            DetailTabMove.Details => BottomTab.Details,
            DetailTabMove.Changes => BottomTab.Changes,
            _ => BottomTab.Files,
        };

        return wanted == current ? new AppUpdate(state, []) : Apply(state, tab, new SetBottomTabCommand(wanted));
    }

    private static AppUpdate ChooseStyle(AppState state, RepositoryState tab, FilePathStyle? style)
    {
        RepositoryCommand command = style is { } chosen ? new SetPathStyleCommand(chosen) : new TogglePathStyleCommand();
        var update = Apply(state, tab, command);
        var shown = update.State.Workspace.FindTab(tab.Id)?.View.PathStyle ?? FilePathStyle.NameFirst;

        return new AppUpdate(update.State, [.. update.Effects, Notice(StyleNotice(shown))]);
    }

    private static string StyleNotice(FilePathStyle style) => style switch
    {
        FilePathStyle.FullPath => "File paths: full path",
        FilePathStyle.Tree => "File paths: tree",
        _ => "File paths: name first",
    };

    private static AppUpdate StartFilter(AppState state, RepositoryState tab)
    {
        if (!DetailTreeRules.IsFilesTab(tab.View))
        {
            return new AppUpdate(state, [Notice(WrongTabNotice)]);
        }

        var update = Apply(state, tab, new SetDetailTreeCommand(tab.View.DetailTree with { IsEditingFilter = true }));

        return new AppUpdate(WithMode(update.State, KeyMode.Insert), []);
    }

    private static AppUpdate TypeFilter(AppState state, RepositoryState tab, KeyEvent key)
    {
        var tree = tab.View.DetailTree;
        if (!DetailTreeRules.IsEditingFilter(tab.View) || PromptTextEditor.Apply(tree.Filter, tree.Caret, key) is not { } edit)
        {
            return new AppUpdate(state, []);
        }

        if (edit.Text == tree.Filter)
        {
            return Apply(state, tab, new SetDetailTreeCommand(tree with { Caret = edit.Caret }));
        }

        return ChangeFilter(state, tab, edit.Text, tree.IsEditingFilter, edit.Caret);
    }

    private static AppUpdate EndFilter(AppState state, RepositoryState tab, bool clear)
    {
        var tree = tab.View.DetailTree;
        if (!DetailTreeRules.IsEditingFilter(tab.View))
        {
            return new AppUpdate(state, []);
        }

        var ended = WithMode(state, KeyMode.Normal);
        if (clear)
        {
            return ChangeFilter(ended, tab, string.Empty, false);
        }

        return Apply(ended, tab, new SetDetailTreeCommand(tree with { IsEditingFilter = false }));
    }

    private static AppUpdate ClearFilter(AppState state, RepositoryState tab) =>
        tab.View.DetailTree.Filter.Length == 0
            ? new AppUpdate(state, [])
            : ChangeFilter(state, tab, string.Empty, tab.View.DetailTree.IsEditingFilter);

    // The cursor goes to the top after every change of the filter, so the first match shows.
    private static AppUpdate ChangeFilter(
        AppState state,
        RepositoryState tab,
        string text,
        bool editing,
        int caret = PromptCaret.End)
    {
        var filtered = tab.View.DetailTree with { Filter = text, IsEditingFilter = editing, Caret = caret };
        var update = Apply(state, tab, new SetDetailTreeCommand(filtered));

        return new AppUpdate(update.State, [new MoveDetailCursorEffect(0)]);
    }

    private static AppUpdate OpenRow(AppState state, RepositoryState tab, FileTreeCursor cursor)
    {
        if (cursor.Row is not { } row)
        {
            return new AppUpdate(state, []);
        }

        if (row.Kind != FileTreeRowKind.Directory)
        {
            return new AppUpdate(state, [new OpenFileContentEffect(row)]);
        }

        var tree = tab.View.DetailTree;
        if (tree.Filter.Length > 0)
        {
            return new AppUpdate(state, []);
        }

        var open = row.IsExpanded ? tree.OpenDirectories.Remove(row.Path) : tree.OpenDirectories.Add(row.Path);

        return Apply(state, tab, new SetDetailTreeCommand(tree with { OpenDirectories = open }));
    }

    // Bare l opens a closed directory and does nothing else: a file opens with Enter.
    private static AppUpdate ExpandRow(AppState state, RepositoryState tab, FileTreeCursor cursor)
    {
        var tree = tab.View.DetailTree;
        if (cursor.Row is not { Kind: FileTreeRowKind.Directory, IsExpanded: false } row || tree.Filter.Length > 0)
        {
            return new AppUpdate(state, []);
        }

        return Apply(state, tab, new SetDetailTreeCommand(tree with { OpenDirectories = tree.OpenDirectories.Add(row.Path) }));
    }

    private static AppUpdate CollapseRow(AppState state, RepositoryState tab, FileTreeCursor cursor)
    {
        var tree = tab.View.DetailTree;
        var step = DetailTreeRules.FilesCollapseStep(tree, cursor);
        if (step == TreeStep.Close && cursor.Row is { } row)
        {
            return Apply(state, tab, new SetDetailTreeCommand(tree with { OpenDirectories = tree.OpenDirectories.Remove(row.Path) }));
        }

        return new AppUpdate(state, step == TreeStep.Parent ? [new MoveDetailCursorEffect(cursor.ParentRow)] : []);
    }

    private static AppUpdate ToggleFolder(AppState state, RepositoryState tab, ChangesCursor cursor)
    {
        var tree = tab.View.DetailTree;
        if (cursor.Folder is not { } folder)
        {
            return new AppUpdate(state, []);
        }

        var opened = tree.ClosedFolders.Remove(folder);
        var closed = ReferenceEquals(opened, tree.ClosedFolders) ? tree.ClosedFolders.Add(folder) : opened;

        return Apply(state, tab, new SetDetailTreeCommand(tree with { ClosedFolders = closed }));
    }

    private static AppUpdate ExpandFolder(AppState state, RepositoryState tab, ChangesCursor cursor)
    {
        var tree = tab.View.DetailTree;
        if (cursor.Folder is not { } folder || !tree.ClosedFolders.Contains(folder))
        {
            return new AppUpdate(state, []);
        }

        return Apply(state, tab, new SetDetailTreeCommand(tree with { ClosedFolders = tree.ClosedFolders.Remove(folder) }));
    }

    private static AppUpdate CollapseFolder(AppState state, RepositoryState tab, ChangesCursor cursor)
    {
        var tree = tab.View.DetailTree;
        var step = DetailTreeRules.ChangesCollapseStep(tree, cursor);
        if (step == TreeStep.Close && cursor.Folder is { } folder)
        {
            return Apply(state, tab, new SetDetailTreeCommand(tree with { ClosedFolders = tree.ClosedFolders.Add(folder) }));
        }

        return new AppUpdate(state, step == TreeStep.Parent ? [new MoveDetailCursorEffect(cursor.ParentRow)] : []);
    }

    private static AppUpdate Apply(AppState state, RepositoryState tab, RepositoryCommand command)
    {
        var update = WorkspaceReducer.Reduce(state.Workspace, new TabCommand(tab.Id, command));

        return new AppUpdate(state with { Workspace = update.Workspace }, [.. update.Effects]);
    }

    private static AppState WithMode(AppState state, KeyMode mode) =>
        state with { Shell = state.Shell.WithMode(mode) };

    private static PostStatusEffect Notice(string text) => new(text, StatusSeverity.Info);
}
