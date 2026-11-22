using Gitunia.Core.Operations;
using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Hints;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Overlays;

namespace Gitunia.Tui.App.Screen;

/// <summary>The key hints of the status bar: those of the overlay or editor that has the keys, else of the focused pane.</summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="store">The confirm dialog.</param>
/// <param name="editors">The settings screen and the two editors.</param>
/// <param name="screens">The reflog list, the blame view and the file history.</param>
/// <param name="context">The repeat mode, the diff pane and the hint options.</param>
public sealed class StatusHints(
    Dispatcher host,
    OverlayStore store,
    EditorOverlays editors,
    ListScreens screens,
    HintContext context)
{
    /// <summary>The hints to show now.</summary>
    public IReadOnlyList<KeyHint> Current()
    {
        if (store.State.Kind == OverlayKind.Confirm)
        {
            return HintSet.ForConfirm();
        }

        if (editors.Settings.State is { } settings)
        {
            return HintSet.ForSettings(settings);
        }

        if (editors.Conflict.IsOpen)
        {
            return ConflictEditorHints();
        }

        if (editors.Rebase.IsOpen)
        {
            return editors.Rebase.IsEditingMessage ? HintSet.ForRebaseMessage() : HintSet.ForRebaseEditor();
        }

        if (screens.FileHistory.IsOpen)
        {
            return screens.FileHistory.Range is { } range
                ? HintSet.ForLineHistory(range)
                : HintSet.ForFileHistory();
        }

        if (screens.Blame.State is { } blame)
        {
            return HintSet.ForBlame(blame);
        }

        if (screens.Reflog.State is { } reflog)
        {
            return HintSet.ForReflog(reflog);
        }

        if (CommitRules.IsEditing(host.State))
        {
            return HintSet.ForMessageEditing();
        }

        if (context.Repeat.Active is { } repeating)
        {
            return HintSet.ForRepeat(repeating.Kind);
        }

        // Dialogs, editors and the repeat mode above keep their hints: they say which keys act right now.
        return HintSet.Limit(
            PaneAndConflictHints(),
            context.Config.Options.StatusBarHints,
            context.Config.Options.CommandLevel);
    }

    private IReadOnlyList<KeyHint> ConflictEditorHints()
    {
        if (editors.Conflict.IsEditing)
        {
            return HintSet.ForConflictEdit();
        }

        if (editors.Conflict.IsResultActive)
        {
            return HintSet.ForConflictResult();
        }

        return editors.Conflict.IsVisual ? HintSet.ForConflictVisual() : HintSet.ForConflictVersions();
    }

    private IReadOnlyList<KeyHint> PaneAndConflictHints()
    {
        var hints = HintSetOfFocus();
        if (!ConflictPending())
        {
            return hints;
        }

        if (ConflictEditorSupport.AnyCanOpen(host.Workspace.ActiveTab?.WorkingTree))
        {
            return HintSet.WithConflictEditor(hints);
        }

        return HintSet.WithConflictMergetool(hints);
    }

    private IReadOnlyList<KeyHint> HintSetOfFocus()
    {
        var view = host.Workspace.ActiveTab?.View;
        if (view is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Changes })
        {
            return HintSet.ForDetailChanges();
        }

        if (view is { FocusedPane: PaneKind.Graph, CompareMark: not null })
        {
            return HintSet.ForGraphComparing();
        }

        var focused = view?.FocusedPane ?? PaneKind.Graph;

        return HintSet.For(
            focused,
            context.DiffView.IsShowingContent,
            OnConflictedFile(),
            OnRefusedConflictedFile());
    }

    // The working tree has conflicted files (a stopped operation or a stash pop):
    // <space>gr opens the editor from every pane.
    private bool ConflictPending() =>
        OperationBannerBuilder.HasConflicts(host.Workspace.ActiveTab?.WorkingTree);

    // Only the Unstaged list and its diff lead with the conflict editor, so no other pane pays for the lookup.
    private bool OnConflictedFile() =>
        host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && ConflictEditorSupport.CanOpen(entry.Conflict);

    // The same panes, on a conflicted file the editor refuses: the hints name the mergetool.
    private bool OnRefusedConflictedFile() =>
        host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && !ConflictEditorSupport.CanOpen(entry.Conflict);
}
